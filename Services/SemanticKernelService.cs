using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKerne.AiProvider.Unified.Models;
using SemanticKerne.AiProvider.Unified.Services.Mcp;
using System.Text.Json;

namespace SemanticKerne.AiProvider.Unified.Services
{
    /// <summary>
    /// Semantic Kernel 服务实现
    /// </summary>
    public class SemanticKernelService : ISemanticKernelService
    {
        private readonly SemanticKernelOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SemanticKernelService> _logger;
        private readonly IMcpClientService _mcpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public SemanticKernelService(IConfiguration configuration, ILoggerFactory loggerFactory, IMcpClientService mcpClient, IOptions<SemanticKernelOptions> options, IHttpClientFactory httpClientFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SemanticKernelService>();
            _mcpClient = mcpClient;
            _options = options.Value;

            // 验证配置
            if (!_options.Validate(out var errors))
            {
                throw new InvalidOperationException(
                    $"Semantic Kernel 配置无效：{string.Join("; ", errors)}");
            }

            _httpClientFactory = httpClientFactory;
        }

        public Kernel CreateKernel()
        {
            var builder = Kernel.CreateBuilder();

            builder.Services.AddLogging(services => services.SetMinimumLevel(LogLevel.Information));
            // 使用 IHttpClientFactory 创建 HttpClient
            var httpClient = _httpClientFactory.CreateClient("AIService");
            // 根据配置选择 AI 服务
            switch (_options.AiServiceType.ToLower())
            {
                case "ollama":
                    _logger.LogInformation("使用 Ollama AI 服务");
                    var ollamaLogger = _loggerFactory.CreateLogger<OllamaStreamingChatCompletion>();
                    var ollamaService = new OllamaStreamingChatCompletion(
                        modelId: _options.ModelId,
                        endpoint: _options.Endpoint,
                        logger: ollamaLogger,
                        httpClient: httpClient);
                    builder.Services.AddSingleton<IChatCompletionService>(ollamaService);
                    // 添加 MCP 插件
                    var mcpLogger3 = _loggerFactory.CreateLogger<McpPlugin>();
                    var mcpPlugin3 = new McpPlugin(_mcpClient, mcpLogger3);
                    builder.Plugins.AddFromObject(mcpPlugin3, "Mcp");
                    var kernel3 = builder.Build();
                    // 注入 Kernel 到 Ollama 服务
                    if (ollamaService.Kernel == null)
                    {
                        var field = typeof(OllamaStreamingChatCompletion)
                            .GetField("_kernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field?.SetValue(ollamaService, kernel3);
                    }
                    return kernel3;
                case "dashscope":
                    _logger.LogInformation("使用 Dashscope AI 服务");
                    var dashScopeLogger = _loggerFactory.CreateLogger<DashScopeStreamingChatCompletion>();
                    var dashScopeService = new DashScopeStreamingChatCompletion(
                           modelId: _options.ModelId,
                           apiKey: _options.ApiKey,
                           endpoint: _options.Endpoint,
                           logger: dashScopeLogger,
                           httpClient: httpClient); // 可传入原生 HttpClient
                    builder.Services.AddSingleton<IChatCompletionService>(dashScopeService);
                    // 🔧 添加 MCP 插件等其他插件
                    var mcpLogger2 = _loggerFactory.CreateLogger<McpPlugin>();
                    var mcpPlugin2 = new McpPlugin(_mcpClient, mcpLogger2);
                    builder.Plugins.AddFromObject(mcpPlugin2, "Mcp");

                    // 🔧 构建 Kernel
                    var kernel2 = builder.Build();

                    // 🔧 【关键】将构建好的 kernel 注入到 DashScope 服务
                    // 通过反射或属性设置（推荐用属性）
                    if (dashScopeService.Kernel == null)
                    {
                        // 如果构造函数没传，可以通过属性设置
                        var field = typeof(DashScopeStreamingChatCompletion)
                            .GetField("_kernel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field?.SetValue(dashScopeService, kernel2);
                    }

                    return kernel2;
                case "openai":
                    _logger.LogInformation("使用 OpenAI AI 服务");
                    builder.AddOpenAIChatCompletion(
                        endpoint: new Uri(_options.Endpoint),
                        modelId: _options.ModelId,
                        apiKey: _options.ApiKey,
                        httpClient: httpClient);
                    break;
                default:
                    break;
            }

            var kernel = builder.Build();
            //kernel.Plugins.AddFromType<LightsPlugin>("Lights");

            //// 添加 MCP 插件
            var mcpLogger = _loggerFactory.CreateLogger<McpPlugin>();
            var mcpPlugin = new McpPlugin(_mcpClient, mcpLogger);
            kernel.Plugins.AddFromObject(mcpPlugin, "Mcp");

            _logger.LogInformation("MCP 插件已加载到 SemanticKernel");

            return kernel;
        }

        public IAsyncEnumerable<StreamingResponse> StreamChatAsync(
            ChatSession session,
            string userInput,
            CancellationToken cancellationToken = default)
        {
            // 创建链接的 CancellationTokenSource，同时监听外部令牌和会话内部的令牌
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                session.GetCurrentCancellationToken());

            // 统一使用 IChatClient 方式（支持 MCP 工具）
            return new StreamingResponseEnumerable(
                this,
                session,
                userInput,
                linkedCts.Token);
        }

        private class StreamingResponseEnumerable : IAsyncEnumerable<StreamingResponse>
        {
            private readonly SemanticKernelService _service;
            private readonly ChatSession _session;
            private readonly string _userInput;
            private readonly CancellationToken _cancellationToken;

            public StreamingResponseEnumerable(
                SemanticKernelService service,
                ChatSession session,
                string userInput,
                CancellationToken cancellationToken)
            {
                _service = service;
                _session = session;
                _userInput = userInput;
                _cancellationToken = cancellationToken;
            }

            public IAsyncEnumerator<StreamingResponse> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new StreamingResponseEnumerator(
                    _service,
                    _session,
                    _userInput,
                    CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken).Token);
            }
        }

        private class StreamingResponseEnumerator : IAsyncEnumerator<StreamingResponse>
        {
            private readonly SemanticKernelService _service;
            private readonly ChatSession _session;
            private readonly string _userInput;
            private readonly CancellationToken _cancellationToken;
            private IChatCompletionService? _chatCompletionService;
            private OpenAIPromptExecutionSettings? _executionSettings;
            private IAsyncEnumerator<StreamingChatMessageContent>? _chatUpdateEnumerator;
            private StreamingResponse? _current;
            private bool _started = false;
            private bool _completed = false;
            private bool _error = false;
            private bool _hasContent = false;
            private int _updateCount = 0;
            private int _emptyUpdateCount = 0;
            private System.Text.StringBuilder? _aiResponseContent;
            private bool _inThinkingMode = false;
            private string? _pendingNormalContent = null;
            private const int MaxEmptyUpdatesBeforeEnd = 10;

            public StreamingResponseEnumerator(
                SemanticKernelService service,
                ChatSession session,
                string userInput,
                CancellationToken cancellationToken)
            {
                _service = service;
                _session = session;
                _userInput = userInput;
                _cancellationToken = cancellationToken;
            }

            public StreamingResponse Current => _current!;

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    // 🔥【新增】优先处理缓存的 reasoning_content（一次性返回，兼容旧逻辑）
                    if (!_hasContent && !_inThinkingMode && !string.IsNullOrEmpty(ReasoningContextHolder.Current))
                    {
                        var fullReasoning = ReasoningContextHolder.Current;
                        ReasoningContextHolder.Set(null); // 消费后清空，避免串扰

                        _service._logger.LogInformation("🧠 返回 reasoning_content (长度: {Length})", fullReasoning.Length);

                        _current = new StreamingResponse
                        {
                            Type = StreamingResponseType.Thinking,
                            Content = fullReasoning  // 一次性返回完整思考过程
                        };

                        _inThinkingMode = true;  // 标记已进入思考模式，下次返回正常内容
                        return true;
                    }

                    // 检查是否有待处理的正常内容
                    if (!string.IsNullOrEmpty(_pendingNormalContent))
                    {
                        string content = _pendingNormalContent;
                        _pendingNormalContent = null;

                        _current = new StreamingResponse
                        {
                            Type = StreamingResponseType.Content,
                            Content = content
                        };
                        _aiResponseContent?.Append(content);
                        return true;
                    }

                    if (!_started)
                    {
                        _session.ResetCancellationToken();
                        _session.IsProcessing = true;
                        McpSessionContext.CurrentSession = _session;

                        _chatCompletionService = _session.Kernel.GetRequiredService<IChatCompletionService>();

                        // 默认 ExtensionData
                        var defaultExtensionData = new Dictionary<string, object>
                        {
                            ["enable_thinking"] = true,
                            ["preserve_thinking"] = true
                        };

                        // 合并用户配置（用户配置优先）
                        var mergedExtensionData = new Dictionary<string, object>(defaultExtensionData);
                        if (_service._options.ExtensionData != null)
                        {
                            foreach (var kvp in _service._options.ExtensionData)
                            {
                                mergedExtensionData[kvp.Key] = kvp.Value; // 用户配置覆盖默认值
                            }
                        }

                        _executionSettings = new OpenAIPromptExecutionSettings
                        {
                            ExtensionData = mergedExtensionData,
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                            User = _session.UserId
                        };

                        _session.History.AddUserMessage(_userInput);

                        _service._logger.LogInformation("开始流式调用，Model: {ModelId}, SessionId: {SessionId}", _service._options.ModelId, _session.SessionId);
                        _service._logger.LogInformation("会话历史消息数: {Count}", _session.History.Count);
                        _service._logger.LogDebug("MCP Session IDs: {McpSessionIds}", string.Join(", ", _session.McpSessionIds.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                        _aiResponseContent = new System.Text.StringBuilder();

                        _chatUpdateEnumerator = _chatCompletionService.GetStreamingChatMessageContentsAsync(
                            _session.History,
                            _executionSettings,
                            _session.Kernel,
                            cancellationToken: _cancellationToken).GetAsyncEnumerator();

                        _started = true;
                    }

                    if (_cancellationToken.IsCancellationRequested)
                    {
                        _service._logger.LogInformation("检测到取消请求，停止生成");
                        _session.IsProcessing = false;
                        return false;
                    }

                    if (_chatUpdateEnumerator != null && await _chatUpdateEnumerator.MoveNextAsync())
                    {
                        var chatUpdate = _chatUpdateEnumerator.Current;
                        _updateCount++;

                        // 🔥 优先检查 Metadata 中的 reasoning_content（数据驱动识别，真正流式）
                        if (chatUpdate.Metadata != null && chatUpdate.Metadata.TryGetValue("reasoning_content", out var reasoningObj) && reasoningObj is string reasoningChunk && !string.IsNullOrEmpty(reasoningChunk))
                        {
                            _service._logger.LogDebug("🧠 [流式] 收到 reasoning_content: {Chunk}", reasoningChunk);
                            _current = new StreamingResponse
                            {
                                Type = StreamingResponseType.Thinking,
                                Content = reasoningChunk
                            };
                            return true;
                        }
                        // 🔥 处理错误响应（来自 AI 模型的错误）
                        if (chatUpdate.Metadata != null && chatUpdate.Metadata.TryGetValue("error", out var errorObj))
                        {
                            _service._logger.LogError("收到 AI 错误响应: {Error}", errorObj);
                            var errorMessage = chatUpdate.Metadata.TryGetValue("error_message", out var errorMessageObj)
                                ? errorMessageObj.ToString()
                                : "AI 模型返回错误";
                            var errorType = chatUpdate.Metadata.TryGetValue("error_type", out var errorTypeObj)
                                ? errorTypeObj.ToString()
                                : "UnknownError";
                            var errorCode = chatUpdate.Metadata.TryGetValue("error_code", out var errorCodeObj)
                                ? errorCodeObj.ToString()
                                : "400";

                            _current = new StreamingResponse
                            {
                                Type = StreamingResponseType.Error,
                                Content = errorMessage,
                                ErrorCode = errorType,
                                HttpStatus = int.TryParse(errorCode, out var status) ? status : 400,
                                ErrorTitle = "AI 模型错误",
                                ErrorReason = errorMessage,
                                ErrorSolution = "请检查输入内容是否过长或格式是否正确",
                                IsCritical = true
                            };
                            _hasContent = true;
                            return true;
                        }

                        // 🔥 处理 tool_calls（保持 MCP 兼容）
                        if (chatUpdate.Metadata != null && chatUpdate.Metadata.TryGetValue("function_call", out var fcObj) && fcObj is object fc)
                        {
                            _service._logger.LogInformation("收到工具调用请求: {FunctionCall}", fc);
                            _hasContent = true;

                            // 尝试解析工具调用信息
                            try
                            {
                                // 直接使用反射获取匿名类型的属性
                                var fcType = fc.GetType();
                                var nameProperty = fcType.GetProperty("name");
                                var argumentsProperty = fcType.GetProperty("arguments");

                                if (nameProperty != null)
                                {
                                    var fullName = nameProperty.GetValue(fc) as string;
                                    if (!string.IsNullOrEmpty(fullName) && fullName.StartsWith("Mcp-"))
                                    {
                                        // 提取方法名
                                        var methodName = fullName.Substring(4); // 移除 "Mcp-" 前缀

                                        // 解析参数
                                        var arguments = new KernelArguments();
                                        if (argumentsProperty != null)
                                        {
                                            var argsValue = argumentsProperty.GetValue(fc);
                                            if (argsValue is string argsJson)
                                            {
                                                try
                                                {
                                                    var argsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson);
                                                    if (argsDict != null)
                                                    {
                                                        foreach (var kvp in argsDict)
                                                        {
                                                            try
                                                            {
                                                                arguments[kvp.Key] = kvp.Value;
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                _service._logger.LogWarning(ex, "添加参数失败: {Key}", kvp.Key);
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    _service._logger.LogWarning(ex, "解析参数JSON失败，使用空参数");
                                                }
                                            }
                                        }

                                        // 使用 Kernel 调用 McpPlugin 中的方法
                                        _service._logger.LogInformation("使用 Kernel 调用 MCP 插件方法: {Method}", methodName);
                                        var result = await _session.Kernel.InvokeAsync<string>(
                                            functionName: methodName,
                                            pluginName: "Mcp",
                                            arguments: arguments,
                                            cancellationToken: _cancellationToken);

                                        // 将工具调用结果添加到对话历史
                                        _session.History.Add(new ChatMessageContent(AuthorRole.Tool, result));

                                        // 返回工具调用结果
                                        _current = new StreamingResponse
                                        {
                                            Type = StreamingResponseType.ToolResult,
                                            Content = result
                                        };

                                        // 工具调用完成后，再次调用AI模型，让AI基于工具结果生成响应
                                        _chatUpdateEnumerator = null;
                                        _started = false;
                                        _completed = false;
                                        _hasContent = false;
                                        _updateCount = 0;
                                        _aiResponseContent = new System.Text.StringBuilder();
                                        _inThinkingMode = false;
                                        _pendingNormalContent = null;

                                        // 继续执行，重新开始流式调用
                                        return await MoveNextAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _service._logger.LogWarning(ex, "解析工具调用失败");
                                // 返回错误信息给前端
                                _current = new StreamingResponse
                                {
                                    Type = StreamingResponseType.Error,
                                    Content = "工具调用失败：" + ex.Message,
                                    ErrorCode = "ToolCallFailed",
                                    ErrorTitle = "工具调用失败",
                                    ErrorReason = ex.Message,
                                    ErrorSolution = "请检查工具调用参数是否正确，或者尝试使用更大的模型"
                                };
                                return true;
                            }
                        }

                        // 🔥 处理 usage 元数据（安全提取，避免格式异常）
                        if (chatUpdate.Metadata?.TryGetValue("usage_total_tokens", out var usageObj) == true
                            && usageObj is int usageValue)
                        {
                            _service._logger.LogDebug("📊 收到 usage: {TotalTokens} tokens", usageValue);
                            // 纯 usage 分块不返回给前端，继续等待下一个
                            return await MoveNextAsync();
                        }

                        // 🔥 处理 reasoning_content 元数据（用于输出 thinking 部分）
                        if (chatUpdate.Metadata?.TryGetValue("reasoning_content", out var reasoningValue) == true
                            && reasoningValue is string reasoningContent)
                        {
                            _service._logger.LogInformation("🤔 [流式] 收到 Reasoning: {Reasoning}", reasoningContent);
                            _hasContent = true;
                            _emptyUpdateCount = 0; // 重置空内容计数器
                            _current = new StreamingResponse { Type = StreamingResponseType.Thinking, Content = reasoningContent };
                            return true;
                        }

                        // 🔥 处理 content（保留您原有的 <think> 标签解析逻辑）
                        if (!string.IsNullOrEmpty(chatUpdate?.Content))
                        {
                            var content = chatUpdate.Content;

                            // 过滤掉 [tool_result] 标记
                            if (content == "[tool_result]")
                            {
                                return await MoveNextAsync();
                            }

                            var toolCodeMatch = System.Text.RegularExpressions.Regex.Match(
                                content,
                                @"<tool_code>\s*(\{.*?\})\s*</tool_code>",
                                System.Text.RegularExpressions.RegexOptions.Singleline);

                            if (toolCodeMatch.Success)
                            {
                                try
                                {
                                    var toolJson = toolCodeMatch.Groups[1].Value;
                                    var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(toolJson);

                                    if (toolCall.TryGetValue("name", out var nameObj) && nameObj is string fullName)
                                    {
                                        // 解析 "server.tool" 格式
                                        var parts = fullName.Split('.', 2);
                                        if (parts.Length == 2)
                                        {
                                            var serverName = parts[0];      // "sql-mcp-http"
                                            var toolName = parts[1];         // "describe_entities"
                                            var arguments = toolCall.TryGetValue("arguments", out var argsObj)
                                                ? argsObj as Dictionary<string, object> ?? new Dictionary<string, object>() { }
                                                : new Dictionary<string, object>();

                                            // 🔧 正确调用：通过 _service 访问实例字段，并传入必需参数
                                            var result = await _service._mcpClient.CallToolAsync(
                                                mcpSessionIds: _session.McpSessionIds,  // 👈 必需参数
                                                serverName: serverName,
                                                toolName: toolName,
                                                arguments: arguments,
                                                cancellationToken: _cancellationToken);

                                            _current = new StreamingResponse
                                            {
                                                Type = StreamingResponseType.ToolResult,
                                                Content = JsonSerializer.Serialize(result)
                                            };
                                            return true;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _service._logger.LogWarning("解析 tool_code 失败: {Message}", ex.Message);
                                }
                            }
                            _hasContent = true;
                            _service._logger.LogInformation("\U0001f9e0  [流式] 收到 Content: {Chunk}", content);
                            StreamingResponseType responseType = StreamingResponseType.Content;

                            // ✅ 完全保留您原有的 <think> 逻辑（兼容 MiniMax2.7 等模型）
                            if (content.StartsWith("<think>"))
                            {
                                content = content["</think>".Length..];
                                _inThinkingMode = true;
                                responseType = StreamingResponseType.Thinking;
                                if (content.Contains("</think>"))
                                {
                                    var endIdx = content.IndexOf("</think>");
                                    _current = new StreamingResponse { Type = StreamingResponseType.Thinking, Content = content[..endIdx] };
                                    _inThinkingMode = false;
                                    var rest = content[(endIdx + "</think>".Length)..];
                                    if (!string.IsNullOrEmpty(rest)) _pendingNormalContent = rest;
                                    return true;
                                }
                            }
                            else if (content.StartsWith("</think>"))
                            {
                                _inThinkingMode = false;
                                content = content["</think>".Length..];
                                if (string.IsNullOrEmpty(content)) return await MoveNextAsync();
                            }
                            else if (_inThinkingMode)
                            {
                                responseType = StreamingResponseType.Thinking;
                                if (content.Contains("</think>"))
                                {
                                    var endIdx = content.IndexOf("</think>");
                                    _current = new StreamingResponse { Type = StreamingResponseType.Thinking, Content = content[..endIdx] };
                                    _inThinkingMode = false;
                                    var rest = content[(endIdx + "</think>".Length)..];
                                    if (!string.IsNullOrEmpty(rest)) _pendingNormalContent = rest;
                                    return true;
                                }
                            }

                            _current = new StreamingResponse { Type = responseType, Content = content };
                            if (responseType != StreamingResponseType.Thinking)
                            {
                                _aiResponseContent?.Append(content);
                                _hasContent = true; // 标记有有效内容
                                _emptyUpdateCount = 0; // 重置空内容计数器
                            }
                            return true;
                        }

                        // 工具结果/空内容等待（保持原有逻辑）
                        if (chatUpdate?.Role?.ToString() == "tool")
                        {
                            // 保留工具结果标记，但是不返回给前端
                            _hasContent = true;
                            _emptyUpdateCount = 0;
                            return await MoveNextAsync();
                        }

                        _emptyUpdateCount++;
                        _service._logger.LogDebug("空内容更新计数: {EmptyCount}", _emptyUpdateCount);

                        return await MoveNextAsync();
                    }

                    // 处理完成逻辑
                    if (!_completed)
                    {
                        _service._logger.LogInformation("流式调用结束，总共 {UpdateCount} 个更新，有内容: {HasContent}, 连续空更新: {EmptyCount}", _updateCount, _hasContent, _emptyUpdateCount);

                        if (_hasContent && _aiResponseContent != null)
                        {
                            var aiResponse = _aiResponseContent.ToString();
                            _session.History.AddAssistantMessage(aiResponse);
                            _service._logger.LogInformation("AI 回复已添加到会话历史，SessionId: {SessionId}", _session.SessionId);
                            _completed = true;
                        }
                        else if (_updateCount == 0)
                        {
                            _service._logger.LogWarning("AI 模型调用没有返回任何内容，SessionId: {SessionId}", _session.SessionId);
                            _current = new StreamingResponse
                            {
                                Type = StreamingResponseType.Error,
                                Content = "AI 模型没有返回任何内容"
                            };
                            return true;
                        }
                        else if (_emptyUpdateCount >= MaxEmptyUpdatesBeforeEnd)
                        {
                            _service._logger.LogWarning("连续 {EmptyCount} 次空更新，结束流式调用，SessionId: {SessionId}", _emptyUpdateCount, _session.SessionId);
                            _current = new StreamingResponse
                            {
                                Type = StreamingResponseType.Error,
                                Content = "AI 模型响应超时（连续无内容）"
                            };
                            return true;
                        }
                        else
                        {
                            _service._logger.LogWarning("AI 模型返回了数据但没有有效内容，连续第 {EmptyCount} 次，SessionId: {SessionId}", _emptyUpdateCount, _session.SessionId);
                            _current = new StreamingResponse
                            {
                                Type = StreamingResponseType.Error,
                                Content = $"AI 模型响应缓慢（第 {_emptyUpdateCount} 次尝试）"
                            };
                            return true;
                        }

                        _completed = true;
                    }

                    return false;
                }
                catch (FormatException ex)
                {
                    // 🔧 捕获数字格式异常（如 usage 嵌套对象解析失败），避免中断流
                    _service._logger.LogWarning("⚠️ 格式解析异常 (已忽略): {Message}", ex.Message);
                    _current = new StreamingResponse
                    {
                        Type = StreamingResponseType.Error,
                        Content = ex.Message
                    };
                    return true;
                    //return await MoveNextAsync();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // 其他非取消异常也记录但不中断
                    _service._logger.LogWarning("⚠️ 枚举器解析异常 (已忽略): {Message}", ex.Message);
                    _current = new StreamingResponse
                    {
                        Type = StreamingResponseType.Error,
                        Content = ex.Message
                    };
                    return true;
                    //return await MoveNextAsync();
                }
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    if (_chatUpdateEnumerator != null)
                    {
                        await _chatUpdateEnumerator.DisposeAsync();
                    }
                }
                finally
                {
                    // 清除会话上下文
                    McpSessionContext.CurrentSession = null;
                    _session.IsProcessing = false;
                }
            }
        }
    }
}