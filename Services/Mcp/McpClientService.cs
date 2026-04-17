using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SemanticKerne.AiProvider.Unified.Services.Mcp;

/// <summary>
/// MCP客户端服务实现 - 支持多个MCP服务器（HTTP和stdio传输）
/// 多用户支持：
/// - HTTP模式：每个AI会话有独立的MCP Session ID
/// - stdio模式：每个AI会话启动独立的MCP进程
/// </summary>
public class McpClientService : IMcpClientService, IAsyncDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<McpClientService> _logger;
    private readonly McpOptions _options;
    private readonly Dictionary<string, McpServerConfig> _servers;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // stdio 进程管理：key = "{serverName}:{sessionId}"，每个会话独立进程
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, StdioMcpProcess> _stdioProcesses = new();
    private readonly SemaphoreSlim _stdioLock = new(1, 1);
    
    // HTTP 会话管理
    private readonly SemaphoreSlim _httpLock = new(1, 1);

    public McpClientService(IHttpClientFactory httpClientFactory, ILogger<McpClientService> logger, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        
        _options = configuration.GetSection("Mcp").Get<McpOptions>() ?? new McpOptions();
        _servers = _options.Servers
            .Where(s => s.Enabled)
            .ToDictionary(s => s.Name, s => s);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _logger.LogInformation("MCP客户端初始化完成，已加载 {Count} 个服务器", _servers.Count);
        foreach (var server in _servers.Values)
        {
            var transportInfo = server.Transport == "stdio" 
                ? $"stdio: {server.Command}" 
                : $"http: {server.Endpoint}";
            _logger.LogInformation("  - {Name}: {Transport}", server.Name, transportInfo);
        }
    }

    public async Task<Dictionary<string, McpServerStatus>> GetServerStatusesAsync(Dictionary<string, string>? mcpSessionIds = null, CancellationToken cancellationToken = default)
    {
        var statuses = new Dictionary<string, McpServerStatus>();
        mcpSessionIds ??= new Dictionary<string, string>();

        foreach (var (name, config) in _servers)
        {
            var status = await CheckServerStatusAsync(config, mcpSessionIds, cancellationToken);
            statuses[name] = status;
        }

        return statuses;
    }

    private async Task<McpServerStatus> CheckServerStatusAsync(McpServerConfig config, Dictionary<string, string> mcpSessionIds, CancellationToken cancellationToken)
    {
        var status = new McpServerStatus
        {
            Name = config.Name,
            LastChecked = DateTime.UtcNow
        };

        try
        {
            if (config.Transport == "stdio")
            {
                // stdio 模式：尝试启动一个测试进程
                var testProcessKey = $"{config.Name}:test";
                var process = await GetOrCreateStdioProcessAsync(config, testProcessKey, cancellationToken);
                status.IsConnected = process.IsRunning;
                
                if (status.IsConnected)
                {
                    var tools = await ListToolsForServerAsync(config, mcpSessionIds, testProcessKey, cancellationToken);
                    status.ToolCount = tools.Count;
                }
            }
            else
            {
                // HTTP 模式
                var (sessionId, _) = await EnsureHttpSessionAsync(config, mcpSessionIds, cancellationToken);
                status.IsConnected = !string.IsNullOrEmpty(sessionId);
                
                if (status.IsConnected)
                {
                    var tools = await ListToolsForServerAsync(config, mcpSessionIds, null, cancellationToken);
                    status.ToolCount = tools.Count;
                }
            }
            _logger.LogInformation("检查MCP服务器状态成功: {Name}, IsConnected: {IsConnected}, ToolCount: {ToolCount}", config.Name, status.IsConnected, status.ToolCount);
        }
        catch (Exception ex)
        {
            status.IsConnected = false;
            status.Error = ex.Message;
            _logger.LogWarning(ex, "检查MCP服务器状态失败: {Name}", config.Name);
        }

        return status;
    }

    public async Task<IEnumerable<McpToolInfo>> ListToolsAsync(Dictionary<string, string>? mcpSessionIds = null, string? serverName = null, CancellationToken cancellationToken = default)
    {
        var allTools = new List<McpToolInfo>();
        mcpSessionIds ??= new Dictionary<string, string>();
        var servers = string.IsNullOrEmpty(serverName) 
            ? _servers.Values 
            : _servers.Values.Where(s => s.Name == serverName);

        foreach (var config in servers)
        {
            try
            {
                var tools = await ListToolsForServerAsync(config, mcpSessionIds, null, cancellationToken);
                allTools.AddRange(tools);
                _logger.LogInformation("获取服务器 {Name} 的工具列表成功，共 {Count} 个工具", config.Name, tools.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务器 {Name} 的工具列表失败", config.Name);
            }
        }

        return allTools;
    }

    private async Task<List<McpToolInfo>> ListToolsForServerAsync(McpServerConfig config, Dictionary<string, string> mcpSessionIds, string? stdioProcessKey, CancellationToken cancellationToken)
    {
        var request = new McpJsonRpcRequest
        {
            Id = GenerateId(),
            Method = "tools/list"
        };

        var response = await SendRequestAsync<McpJsonRpcResponse<McpToolsListResponse>>(config, mcpSessionIds, stdioProcessKey, request, cancellationToken);
        
        return response?.Result?.Tools?.Select(t => new McpToolInfo
        {
            ServerName = config.Name,
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema?.Properties?.ToDictionary(
                p => p.Key,
                p => new McpPropertySchema
                {
                    Type = p.Value.Type,
                    Description = p.Value.Description,
                    Required = t.InputSchema?.Required?.Contains(p.Key) ?? false
                })
        }).ToList() ?? [];
    }

    public async Task<(McpToolResult Result, Dictionary<string, string> UpdatedSessionIds)> CallToolAsync(
        Dictionary<string, string>? mcpSessionIds, 
        string serverName, 
        string toolName, 
        Dictionary<string, object?> arguments, 
        CancellationToken cancellationToken = default)
    {
        mcpSessionIds ??= new Dictionary<string, string>();
        
        if (!_servers.TryGetValue(serverName, out var config))
        {
            return (new McpToolResult
            {
                IsError = true,
                Content = $"未找到MCP服务器: {serverName}",
                ErrorType = "ServerNotFound"
            }, mcpSessionIds);
        }

        try
        {
            var request = new McpJsonRpcRequest
            {
                Id = GenerateId(),
                Method = "tools/call",
                Params = new
                {
                    name = toolName,
                    arguments = arguments
                }
            };

            var response = await SendRequestAsync<McpJsonRpcResponse<McpToolCallResponse>>(config, mcpSessionIds, null, request, cancellationToken);

            // 检查JSON-RPC错误
            if (response?.Error != null)
            {
                return (new McpToolResult
                {
                    IsError = true,
                    Content = response.Error.Message ?? "调用工具失败",
                    ErrorType = response.Error.Code?.ToString()
                }, mcpSessionIds);
            }

            // 检查工具调用结果
            var result = response?.Result;
            if (result?.Error != null)
            {
                return (new McpToolResult
                {
                    IsError = true,
                    Content = result.Error.Message ?? "调用工具失败",
                    ErrorType = result.Error.Code?.ToString()
                }, mcpSessionIds);
            }

            var content = result?.Content?
                .Where(c => c.Type == "text")
                .Select(c => c.Text)
                .FirstOrDefault() ?? "";

            return (new McpToolResult
            {
                IsError = result?.IsError ?? false,
                Content = content
            }, mcpSessionIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用MCP工具失败: {Server}.{Tool}", serverName, toolName);
            return (new McpToolResult
            {
                IsError = true,
                Content = ex.Message,
                ErrorType = "Exception"
            }, mcpSessionIds);
        }
    }

    public async Task<IEnumerable<McpResourceInfo>> ListResourcesAsync(Dictionary<string, string>? mcpSessionIds = null, string? serverName = null, CancellationToken cancellationToken = default)
    {
        var allResources = new List<McpResourceInfo>();
        mcpSessionIds ??= new Dictionary<string, string>();
        var servers = string.IsNullOrEmpty(serverName) 
            ? _servers.Values 
            : _servers.Values.Where(s => s.Name == serverName);

        foreach (var config in servers)
        {
            try
            {
                var resources = await ListResourcesForServerAsync(config, mcpSessionIds, null, cancellationToken);
                allResources.AddRange(resources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务器 {Name} 的资源列表失败", config.Name);
            }
        }

        return allResources;
    }

    private async Task<List<McpResourceInfo>> ListResourcesForServerAsync(McpServerConfig config, Dictionary<string, string> mcpSessionIds, string? stdioProcessKey, CancellationToken cancellationToken)
    {
        var request = new McpJsonRpcRequest
        {
            Id = GenerateId(),
            Method = "resources/list"
        };

        var response = await SendRequestAsync<McpJsonRpcResponse<McpResourcesListResponse>>(config, mcpSessionIds, stdioProcessKey, request, cancellationToken);
        
        return response?.Result?.Resources?.Select(r => new McpResourceInfo
        {
            ServerName = config.Name,
            Uri = r.Uri,
            Name = r.Name,
            Description = r.Description,
            MimeType = r.MimeType
        }).ToList() ?? [];
    }

    public async Task<McpResourceContent> ReadResourceAsync(Dictionary<string, string>? mcpSessionIds, string serverName, string resourceUri, CancellationToken cancellationToken = default)
    {
        mcpSessionIds ??= new Dictionary<string, string>();
        
        if (!_servers.TryGetValue(serverName, out var config))
        {
            return new McpResourceContent
            {
                Uri = resourceUri,
                Content = $"未找到MCP服务器: {serverName}"
            };
        }

        var request = new McpJsonRpcRequest
        {
            Id = GenerateId(),
            Method = "resources/read",
            Params = new { uri = resourceUri }
        };

        var response = await SendRequestAsync<McpJsonRpcResponse<McpResourceReadResponse>>(config, mcpSessionIds, null, request, cancellationToken);
        
        var content = response?.Result?.Contents?
            .Where(c => c.Type == "text")
            .Select(c => c.Text)
            .FirstOrDefault() ?? "";

        return new McpResourceContent
        {
            Uri = resourceUri,
            MimeType = response?.Result?.Contents?.FirstOrDefault()?.MimeType,
            Content = content
        };
    }

    public async Task<IEnumerable<McpPromptInfo>> ListPromptsAsync(Dictionary<string, string>? mcpSessionIds = null, string? serverName = null, CancellationToken cancellationToken = default)
    {
        var allPrompts = new List<McpPromptInfo>();
        mcpSessionIds ??= new Dictionary<string, string>();
        var servers = string.IsNullOrEmpty(serverName) 
            ? _servers.Values 
            : _servers.Values.Where(s => s.Name == serverName);

        foreach (var config in servers)
        {
            try
            {
                var prompts = await ListPromptsForServerAsync(config, mcpSessionIds, null, cancellationToken);
                allPrompts.AddRange(prompts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取服务器 {Name} 的提示模板列表失败", config.Name);
            }
        }

        return allPrompts;
    }

    private async Task<List<McpPromptInfo>> ListPromptsForServerAsync(McpServerConfig config, Dictionary<string, string> mcpSessionIds, string? stdioProcessKey, CancellationToken cancellationToken)
    {
        var request = new McpJsonRpcRequest
        {
            Id = GenerateId(),
            Method = "prompts/list"
        };

        var response = await SendRequestAsync<McpJsonRpcResponse<McpPromptsListResponse>>(config, mcpSessionIds, stdioProcessKey, request, cancellationToken);
        
        return response?.Result?.Prompts?.Select(p => new McpPromptInfo
        {
            ServerName = config.Name,
            Name = p.Name,
            Description = p.Description,
            Arguments = p.Arguments?.Select(a => new McpPromptArgument
            {
                Name = a.Name,
                Description = a.Description,
                Required = a.Required
            }).ToList()
        }).ToList() ?? [];
    }

    public async Task<McpPromptResult> GetPromptAsync(Dictionary<string, string>? mcpSessionIds, string serverName, string promptName, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        mcpSessionIds ??= new Dictionary<string, string>();
        
        if (!_servers.TryGetValue(serverName, out var config))
        {
            return new McpPromptResult
            {
                Description = $"未找到MCP服务器: {serverName}"
            };
        }

        var request = new McpJsonRpcRequest
        {
            Id = GenerateId(),
            Method = "prompts/get",
            Params = new
            {
                name = promptName,
                arguments = arguments
            }
        };

        var response = await SendRequestAsync<McpJsonRpcResponse<McpPromptGetResponse>>(config, mcpSessionIds, null, request, cancellationToken);
        
        return new McpPromptResult
        {
            Description = response?.Result?.Description,
            Messages = response?.Result?.Messages?.Select(m => new McpPromptMessage
            {
                Role = m.Role,
                Content = m.Content?.Text ?? ""
            }).ToList() ?? new List<McpPromptMessage>()
        };
    }

    #region 传输层实现

    private async Task<T?> SendRequestAsync<T>(McpServerConfig config, Dictionary<string, string> mcpSessionIds, string? stdioProcessKey, McpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (config.Transport == "stdio")
        {
            return await SendStdioRequestAsync<T>(config, stdioProcessKey, request, cancellationToken);
        }
        else
        {
            return await SendHttpRequestAsync<T>(config, mcpSessionIds, request, cancellationToken);
        }
    }

    #region HTTP 会话管理

    private async Task<(string? SessionId, Dictionary<string, string> UpdatedSessionIds)> EnsureHttpSessionAsync(
        McpServerConfig config, 
        Dictionary<string, string> mcpSessionIds, 
        CancellationToken cancellationToken)
    {
        // 如果已有会话ID，直接返回
        if (mcpSessionIds.TryGetValue(config.Name, out var existingSessionId) && !string.IsNullOrEmpty(existingSessionId))
        {
            return (existingSessionId, mcpSessionIds);
        }

        await _httpLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查
            if (mcpSessionIds.TryGetValue(config.Name, out existingSessionId) && !string.IsNullOrEmpty(existingSessionId))
            {
                return (existingSessionId, mcpSessionIds);
            }

            // 发送初始化请求
            var initRequest = new McpJsonRpcRequest
            {
                Id = GenerateId(),
                Method = "initialize",
                Params = new
                {
                    protocolVersion = "2026-04-15",
                    capabilities = new { },
                    clientInfo = new { name = "FindeMee-McpClient", version = "1.0.0" }
                }
            };

            using var client = CreateHttpClient(config);
            var json = JsonSerializer.Serialize(initRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("发送HTTP初始化请求到 {Endpoint}", config.Endpoint);
            var response = await client.PostAsync(config.Endpoint, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("HTTP初始化失败: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return (null, mcpSessionIds);
            }

            // 获取 Session ID
            string? sessionId = null;
            if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds))
            {
                sessionId = sessionIds.FirstOrDefault();
                mcpSessionIds[config.Name] = sessionId ?? "";
                _logger.LogDebug("获取到MCP会话ID: {SessionId}", sessionId);
            }

            return (sessionId, mcpSessionIds);
        }
        finally
        {
            _httpLock.Release();
        }
    }

    private HttpClient CreateHttpClient(McpServerConfig config)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        
        if (config.Headers != null)
        {
            foreach (var header in config.Headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        
        return client;
    }

    private async Task<T?> SendHttpRequestAsync<T>(McpServerConfig config, Dictionary<string, string> mcpSessionIds, McpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        var (sessionId, _) = await EnsureHttpSessionAsync(config, mcpSessionIds, cancellationToken);
        
        using var client = CreateHttpClient(config);
        
        // 添加 Session ID header
        if (!string.IsNullOrEmpty(sessionId))
        {
            client.DefaultRequestHeaders.Add("Mcp-Session-Id", sessionId);
        }

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        _logger.LogDebug("发送MCP请求到 {Endpoint}: {Json}", config.Endpoint, json);
        
        var response = await client.PostAsync(config.Endpoint, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("MCP请求失败: {StatusCode} - {Error}", response.StatusCode, errorContent);
            
            // 如果会话失效，清除会话ID
            if ((int)response.StatusCode == 400 || (int)response.StatusCode == 406)
            {
                mcpSessionIds.Remove(config.Name);
            }
            
            response.EnsureSuccessStatusCode();
        }
        
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("收到MCP响应 (ContentType: {ContentType}): {Response}", response.Content.Headers.ContentType, responseText);
        
        // 解析 SSE 格式响应
        var jsonContent = ParseSseResponse(responseText);
        
        // 尝试解析 JSON
        try
        {
            return JsonSerializer.Deserialize<T>(jsonContent, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON解析失败，原始响应: {Response}", responseText);
            throw;
        }
    }

    /// <summary>
    /// 解析 SSE 格式响应，提取 JSON 内容
    /// </summary>
    private static string ParseSseResponse(string sseContent)
    {
        var lines = sseContent.Split('\n');
        var dataBuilder = new System.Text.StringBuilder();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // 跳过空行和 event 行
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            // 提取 data 行
            if (trimmedLine.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var dataContent = trimmedLine.Substring(5).Trim();
                dataBuilder.Append(dataContent);
            }
        }
        
        var result = dataBuilder.ToString();
        return string.IsNullOrEmpty(result) ? sseContent : result;
    }

    #endregion

    #region stdio 传输

    /// <summary>
    /// 获取或创建 stdio 进程（每个会话独立进程）
    /// </summary>
    private async Task<StdioMcpProcess> GetOrCreateStdioProcessAsync(McpServerConfig config, string processKey, CancellationToken cancellationToken)
    {
        if (_stdioProcesses.TryGetValue(processKey, out var existingProcess) && existingProcess.IsRunning)
        {
            return existingProcess;
        }

        await _stdioLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查
            if (_stdioProcesses.TryGetValue(processKey, out existingProcess) && existingProcess.IsRunning)
            {
                return existingProcess;
            }

            var process = new StdioMcpProcess(config, _logger, _jsonOptions);
            await process.InitializeAsync(cancellationToken);
            
            _stdioProcesses[processKey] = process;
            _logger.LogInformation("创建stdio进程: {ProcessKey}", processKey);
            
            return process;
        }
        finally
        {
            _stdioLock.Release();
        }
    }

    private async Task<T?> SendStdioRequestAsync<T>(McpServerConfig config, string? processKey, McpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        // 如果没有指定进程key，使用默认key（兼容旧代码）
        var actualProcessKey = processKey ?? $"{config.Name}:default";
        
        var process = await GetOrCreateStdioProcessAsync(config, actualProcessKey, cancellationToken);
        return await process.SendRequestAsync<T>(request, cancellationToken);
    }

    /// <summary>
    /// 清理指定会话的 stdio 进程
    /// </summary>
    public async Task CleanupSessionProcessesAsync(string sessionId)
    {
        await _stdioLock.WaitAsync();
        try
        {
            var keysToRemove = _stdioProcesses.Keys
                .Where(k => k.EndsWith($":{sessionId}"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_stdioProcesses.TryRemove(key, out var process))
                {
                    await process.DisposeAsync();
                    _logger.LogInformation("清理stdio进程: {ProcessKey}", key);
                }
            }
        }
        finally
        {
            _stdioLock.Release();
        }
    }

    #endregion

    #endregion

    private static int _idCounter = 0;
    private static int GenerateId() => Interlocked.Increment(ref _idCounter);

    public async ValueTask DisposeAsync()
    {
        foreach (var process in _stdioProcesses.Values)
        {
            await process.DisposeAsync();
        }
        _stdioProcesses.Clear();
    }
}

/// <summary>
/// stdio MCP进程管理
/// </summary>
internal class StdioMcpProcess : IAsyncDisposable
{
    private readonly McpServerConfig _config;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized;

    public bool IsRunning => _process?.HasExited == false;

    public StdioMcpProcess(McpServerConfig config, ILogger logger, JsonSerializerOptions jsonOptions)
    {
        _config = config;
        _logger = logger;
        _jsonOptions = jsonOptions;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized && IsRunning) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = _config.Command,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (_config.Args != null)
            {
                foreach (var arg in _config.Args)
                {
                    var resolvedArg = ExpandVariables(arg);
                    startInfo.ArgumentList.Add(resolvedArg);
                }
            }

            if (_config.Env != null)
            {
                foreach (var (key, value) in _config.Env)
                {
                    startInfo.Environment[key] = ExpandVariables(value);
                }
            }

            if (!string.IsNullOrEmpty(_config.WorkingDirectory))
            {
                startInfo.WorkingDirectory = ExpandVariables(_config.WorkingDirectory);
            }

            _logger.LogInformation("启动MCP进程: {Command} {Args}", _config.Command, string.Join(" ", startInfo.ArgumentList));
            
            _process = new Process { StartInfo = startInfo };
            _process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogDebug("MCP进程输出: {Output}", e.Data);
                }
            };

            _process.Start();
            _process.BeginErrorReadLine();

            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;

            // 等待进程启动
            await Task.Delay(500, cancellationToken);
            
            var initRequest = new McpJsonRpcRequest
            {
                Id = 1,
                Method = "initialize",
                Params = new
                {
                    protocolVersion = "2026-04-15",
                    capabilities = new { },
                    clientInfo = new { name = "FindeMee-McpClient", version = "1.0.0" }
                }
            };

            var response = await SendRequestRawAsync<McpJsonRpcResponse<object>>(initRequest, cancellationToken);
            if (response?.Error != null)
            {
                throw new Exception($"MCP初始化失败: {response.Error.Message}");
            }

            var initializedNotification = new McpJsonRpcRequest
            {
                Method = "notifications/initialized"
            };
            await SendNotificationAsync(initializedNotification);

            _initialized = true;
            _logger.LogInformation("MCP进程初始化成功");
        }
        finally
        {
            _lock.Release();
        }
    }

    private string ExpandVariables(string value)
    {
        var result = value;
        
        foreach (System.Collections.DictionaryEntry env in Environment.GetEnvironmentVariables())
        {
            result = result.Replace($"${{{env.Key}}}", env.Value?.ToString() ?? "");
            result = result.Replace($"%{env.Key}%", env.Value?.ToString() ?? "");
        }

        result = result.Replace("${workspaceFolder}", Directory.GetCurrentDirectory());
        
        return result;
    }

    public async Task<T?> SendRequestAsync<T>(McpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            await InitializeAsync(cancellationToken);
        }

        return await SendRequestRawAsync<T>(request, cancellationToken);
    }

    private async Task<T?> SendRequestRawAsync<T>(McpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            _logger.LogDebug("发送MCP请求: {Json}", json);

            await _stdin!.WriteLineAsync(json);
            await _stdin.FlushAsync(cancellationToken);

            // 读取响应，跳过非 JSON 行
            string? responseJson = null;
            var maxAttempts = 100;
            var attempts = 0;
            
            while (attempts < maxAttempts)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                
                var readTask = _stdout!.ReadLineAsync(cts.Token).AsTask();
                var completedTask = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
                
                if (completedTask != readTask)
                {
                    _logger.LogError("MCP进程响应超时");
                    throw new TimeoutException("MCP进程响应超时");
                }

                var line = await readTask;
                attempts++;
                
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                // 尝试解析 JSON
                if (line.TrimStart().StartsWith("{") || line.TrimStart().StartsWith("["))
                {
                    responseJson = line;
                    _logger.LogDebug("收到MCP响应: {Json}", responseJson);
                    break;
                }
                else
                {
                    _logger.LogDebug("跳过非JSON行: {Line}", line);
                }
            }

            if (string.IsNullOrEmpty(responseJson))
            {
                throw new Exception("MCP进程返回空响应或非JSON格式");
            }

            return JsonSerializer.Deserialize<T>(responseJson, _jsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SendNotificationAsync(McpJsonRpcRequest notification)
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(notification, _jsonOptions);
            await _stdin!.WriteLineAsync(json);
            await _stdin.FlushAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_stdin != null)
            {
                await _stdin.DisposeAsync();
            }
            _stdout?.Dispose();

            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(1000);
            }
            _process?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "关闭MCP进程时出错");
        }
    }
}

#region JSON-RPC 模型

internal class McpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;
    
    [JsonPropertyName("params")]
    public object? Params { get; set; }
}

internal class McpJsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    
    [JsonPropertyName("result")]
    public T? Result { get; set; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

internal class McpError
{
    [JsonPropertyName("code")]
    public int? Code { get; set; }
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

internal class McpToolsListResponse
{
    [JsonPropertyName("tools")]
    public List<McpTool>? Tools { get; set; }
}

internal class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("inputSchema")]
    public McpInputSchema? InputSchema { get; set; }
}

internal class McpInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";
    
    [JsonPropertyName("properties")]
    public Dictionary<string, McpProperty>? Properties { get; set; }
    
    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }
}

internal class McpProperty
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

internal class McpToolCallResponse
{
    [JsonPropertyName("content")]
    public List<McpContent>? Content { get; set; }
    
    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

internal class McpContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal class McpResourcesListResponse
{
    [JsonPropertyName("resources")]
    public List<McpResource>? Resources { get; set; }
}

internal class McpResource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

internal class McpResourceReadResponse
{
    [JsonPropertyName("contents")]
    public List<McpResourceContentItem>? Contents { get; set; }
}

internal class McpResourceContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}

internal class McpPromptsListResponse
{
    [JsonPropertyName("prompts")]
    public List<McpPrompt>? Prompts { get; set; }
}

internal class McpPrompt
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("arguments")]
    public List<McpPromptArg>? Arguments { get; set; }
}

internal class McpPromptArg
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

internal class McpPromptGetResponse
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("messages")]
    public List<McpPromptMsg>? Messages { get; set; }
}

internal class McpPromptMsg
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public McpPromptMsgContent? Content { get; set; }
}

internal class McpPromptMsgContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
    
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

#endregion
