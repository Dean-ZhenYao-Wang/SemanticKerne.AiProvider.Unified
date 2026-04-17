using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace SemanticKerne.AiProvider.Unified.Services
{
    /// <summary>
    /// Ollama 流式聊天服务
    /// - 支持流式输出
    /// - 支持工具调用（MCP 兼容）
    /// - 支持 CancellationToken 取消
    /// - 安全解析 usage 统计信息
    /// - 正确处理同级别的 thinking 和 tool_call 字段
    /// </summary>
    public class OllamaStreamingChatCompletion : IChatCompletionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelId;
        private readonly Uri _endpoint;
        private readonly Kernel? _kernel;
        private readonly ILogger<OllamaStreamingChatCompletion> _logger;
        public Kernel? Kernel => _kernel;

        public OllamaStreamingChatCompletion(string modelId, string endpoint, ILogger<OllamaStreamingChatCompletion> logger, HttpClient? httpClient = null, Kernel? kernel = null)
        {
            _modelId = modelId;
            _kernel = kernel;
            _logger = logger;

            // 自动补全 /api/chat 路径（如果缺失）
            var uri = new Uri(endpoint);
            var path = uri.PathAndQuery;
            if (!path.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
            {
                var basePath = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
                _endpoint = new Uri($"{basePath}/api/chat");
            }
            else
            {
                _endpoint = uri;
            }

            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SemanticKernel-Ollama/1.0");
        }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?> { ["model"] = _modelId };

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
    ChatHistory chatHistory,
    PromptExecutionSettings? executionSettings = null,
    Kernel? kernel = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 用于累积工具调用的临时变量
            var accumulatedToolCalls = new Dictionary<int, (string id, string name, string argumentsJsonBuffer)>();

            var payload = BuildPayload(chatHistory, executionSettings, kernel);
            // 打印 payload
            var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogDebug("[Ollama Request Body]:\n{Payload}", payloadJson);

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = requestContent };

            // 关键：ResponseHeadersRead 确保底层流不被缓冲，实现真正流式
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                _logger.LogDebug("[Ollama Response Body]:\n{Line}", line);
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(" ")) continue;
                var data = line.Trim();
                if (data == "[DONE]") break;

                // 修复 CS1626：先解析，成功后在外部 yield
                JsonDocument? doc = null;
                try
                {
                    doc = JsonDocument.Parse(data);
                }
                catch (JsonException)
                {
                    continue; // 跳过无效行，保障流不断裂
                }

                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("message", out var messageProp))
                    {
                        var role = messageProp.GetProperty("role").GetString();
                        var content = messageProp.GetProperty("content").GetString();

                        // 🔍 1. 提取 thinking 字段（同级别的 thinking）
                        if (messageProp.TryGetProperty("thinking", out var thinkingProp) && thinkingProp.ValueKind == JsonValueKind.String)
                        {
                            var thinkingContent = thinkingProp.GetString();
                            if (!string.IsNullOrEmpty(thinkingContent))
                            {
                                var metadata = new Dictionary<string, object?> { ["reasoning_content"] = thinkingContent };
                                yield return new StreamingChatMessageContent(
                                    role: AuthorRole.Assistant,
                                    content: null,
                                    modelId: _modelId,
                                    metadata: metadata
                                );
                            }
                        }

                        // 🔍 2. 提取 content
                        if (!string.IsNullOrEmpty(content))
                        {
                            // 检查是否是 tool_call 标记
                            if (content.Contains("[tool_call]"))
                            {
                                yield return new StreamingChatMessageContent(
                                    role: AuthorRole.Tool,
                                    content: content,
                                    modelId: _modelId
                                );
                            }
                            else
                            {
                                yield return new StreamingChatMessageContent(
                                    role: AuthorRole.Assistant,
                                    content: content,
                                    modelId: _modelId
                                );
                            }
                        }

                        // 🔍 3. 提取 tool_calls（同级别的 tool_call）
                        if (messageProp.TryGetProperty("tool_calls", out var toolCallsProp) && toolCallsProp.ValueKind == JsonValueKind.Array)
                        {
                            int index = 0;
                            foreach (var toolCall in toolCallsProp.EnumerateArray())
                            {
                                // 获取当前 delta 中的 id
                                var currentId = toolCall.TryGetProperty("id", out var idProp) ? idProp.GetString() : "";

                                // 获取当前 delta 中的 function 信息
                                string currentName = "";
                                string currentArgumentsBuffer = "";

                                if (toolCall.TryGetProperty("function", out var functionProp))
                                {
                                    // 更新 name
                                    if (functionProp.TryGetProperty("name", out var nameProp))
                                    {
                                        currentName = nameProp.GetString();
                                    }

                                    // 更新 arguments
                                    if (functionProp.TryGetProperty("arguments", out var argsProp))
                                    {
                                        try
                                        {
                                            // 检查arguments是对象还是字符串
                                            if (argsProp.ValueKind == JsonValueKind.Object)
                                            {
                                                // 如果是对象，序列化为JSON字符串
                                                currentArgumentsBuffer = JsonSerializer.Serialize(argsProp);
                                            }
                                            else if (argsProp.ValueKind == JsonValueKind.String)
                                            {
                                                // 如果是字符串，直接使用
                                                currentArgumentsBuffer = argsProp.GetString();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "解析工具调用参数失败，使用空参数");
                                            currentArgumentsBuffer = "{}";
                                        }
                                    }
                                }

                                // 更新累积字典
                                accumulatedToolCalls[index] = (currentId, currentName, currentArgumentsBuffer);
                                index++;
                            }
                        }
                    }
                }
            }

            // 循环结束后，处理所有累积的工具调用
            foreach (var kvp in accumulatedToolCalls)
            {
                var callData = kvp.Value;
                if (!string.IsNullOrEmpty(callData.name))
                {
                    bool isValidFinalJson = false;
                    string finalParsedArguments = callData.argumentsJsonBuffer;

                    // 如果 arguments 是空的，设置为 "{}"
                    if (string.IsNullOrEmpty(finalParsedArguments))
                    {
                        finalParsedArguments = "{}";
                        isValidFinalJson = true;
                    }
                    else
                    {
                        try
                        {
                            using var parsedArgsDoc = JsonDocument.Parse(callData.argumentsJsonBuffer);
                            var parsedArgsElement = parsedArgsDoc.RootElement;
                            if (parsedArgsElement.ValueKind == JsonValueKind.Object)
                            {
                                isValidFinalJson = true;
                            }
                        }
                        catch (JsonException)
                        {
                            // 解析失败，尝试将累积的转义字符串解码后再解析
                            try
                            {
                                // 尝试解码转义字符串
                                var decodedArgs = JsonSerializer.Deserialize<string>($"\"{callData.argumentsJsonBuffer}\"");
                                if (!string.IsNullOrEmpty(decodedArgs))
                                {
                                    // 再次尝试解析解码后的字符串
                                    using var parsedArgsDoc = JsonDocument.Parse(decodedArgs);
                                    var parsedArgsElement = parsedArgsDoc.RootElement;
                                    if (parsedArgsElement.ValueKind == JsonValueKind.Object)
                                    {
                                        isValidFinalJson = true;
                                        finalParsedArguments = decodedArgs;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 最终也无法解析，使用空对象
                                _logger.LogWarning("Final parse failed for buffer: {Buffer}, Error: {Error}", callData.argumentsJsonBuffer, ex.Message);
                                finalParsedArguments = "{}";
                                isValidFinalJson = true;
                            }
                        }
                    }

                    if (isValidFinalJson)
                    {
                        var metadata = new Dictionary<string, object?>
                        {
                            ["function_call"] = new { id = callData.id, name = callData.name, arguments = finalParsedArguments }
                        };

                        yield return new StreamingChatMessageContent(
                            role: AuthorRole.Tool,
                            content: null,
                            modelId: _modelId,
                            metadata: metadata
                        );
                    }
                }
            }
        }

        private object BuildPayload(ChatHistory chatHistory, PromptExecutionSettings? executionSettings, Kernel? kernel)
        {
            var ext = (executionSettings as OpenAIPromptExecutionSettings)?.ExtensionData ?? new Dictionary<string, object>();

            // 优先使用请求传入的 Kernel，否则使用构造函数传入的
            var kernelInstance = kernel ?? _kernel;

            // 从 Kernel.Plugins 构建 tools 数组 - 转换为 OpenAI 兼容格式
            var tools = kernelInstance?.Plugins
                .SelectMany(plugin => plugin.Select(function =>
                {
                    // 将 KernelParameterMetadata 转换为 OpenAI 兼容的 JSON Schema
                    var parameters = new
                    {
                        type = "object",
                        properties = function.Metadata.Parameters.ToDictionary(
                            p => p.Name,
                            p => new
                            {
                                type = GetJsonType(p.ParameterType),
                                description = p.Description
                            }),
                        required = function.Metadata.Parameters
                            .Where(p => p.IsRequired)
                            .Select(p => p.Name)
                            .ToArray()
                    };

                    return new
                    {
                        type = "function",
                        function = new
                        {
                            name = $"{plugin.Name}-{function.Name}",  // 保持命名格式一致：如 "Mcp-call_mcp_tool"
                            description = function.Description,
                            parameters = parameters
                        }
                    };
                }))
                .ToList();

            return new
            {
                model = _modelId,
                messages = chatHistory.Select(m => new
                {
                    role = m.Role.Label.ToLowerInvariant(),
                    content = m.Content
                }),
                stream = true,
                stream_options = new { include_usage = true },

                // 🔧 关键：只有当有工具时才传递 tools 和 tool_choice
                tools = tools.Count > 0 ? tools : null,
                tool_choice = tools.Count > 0 ? "auto" : null,

                //enable_thinking = ext.TryGetValue("enable_thinking", out var et) && (bool)et,
                //preserve_thinking = ext.TryGetValue("preserve_thinking", out var pt) && (bool)pt
            };
        }

        // 辅助方法：将 .NET 类型转换为 JSON Schema 类型
        private static string GetJsonType(Type type)
        {
            if (type == typeof(string) || type.Name.StartsWith("String"))
                return "string";
            if (type == typeof(int) || type == typeof(long) || type.Name.StartsWith("Int"))
                return "integer";
            if (type == typeof(double) || type == typeof(float) || type.Name.StartsWith("Double") || type.Name.StartsWith("Float"))
                return "number";
            if (type == typeof(bool) || type.Name.StartsWith("Boolean"))
                return "boolean";
            if (type == typeof(object) || type.IsClass && type != typeof(string))
                return "object";

            // 默认返回 string
            return "string";
        }

        // 非流式方法按需实现（当前业务仅使用流式）
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("请使用流式接口 GetStreamingChatMessageContentsAsync");
    }
}