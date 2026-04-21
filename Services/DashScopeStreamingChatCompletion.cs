using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using SemanticKerne.AiProvider.Unified.Services.Bailian;

namespace SemanticKerne.AiProvider.Unified.Services
{
    /// <summary>
    /// DashScope 真正流式聊天服务
    /// - 支持 reasoning_content 逐字输出
    /// - 支持 tool_calls 工具调用（MCP 兼容）
    /// - 支持 CancellationToken 取消
    /// - 安全解析 usage 统计信息（避免嵌套对象解析失败）
    /// - 数据驱动识别，不依赖模型名称
    /// - 支持阿里异常处理
    /// </summary>
    public class DashScopeStreamingChatCompletion : IChatCompletionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _modelId;
        private readonly Uri _endpoint;
        private readonly Kernel? _kernel;
        private readonly ILogger<DashScopeStreamingChatCompletion> _logger;
        private readonly BailianErrorHandler _errorHandler;
        public Kernel? Kernel => _kernel;

        public DashScopeStreamingChatCompletion(string modelId, string apiKey, string endpoint, ILogger<DashScopeStreamingChatCompletion> logger, HttpClient? httpClient = null, Kernel? kernel = null, ILoggerFactory? loggerFactory = null)
        {
            _modelId = modelId;
            _kernel = kernel;
            _logger = logger;
            
            // 创建 BailianErrorHandler 实例
            var bailianLogger = loggerFactory?.CreateLogger<BailianErrorHandler>() ?? new NullLogger<BailianErrorHandler>();
            _errorHandler = new BailianErrorHandler(bailianLogger);

            // 🔧 自动补全 /chat/completions 路径（如果缺失）
            var uri = new Uri(endpoint);
            var path = uri.PathAndQuery;
            if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                var basePath = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
                _endpoint = new Uri($"{basePath}/chat/completions");
            }
            else
            {
                _endpoint = uri;
            }

            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SemanticKernel-DashScope/1.0");
        }

        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?> { ["model"] = _modelId };

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
    ChatHistory chatHistory,
    PromptExecutionSettings? executionSettings = null,
    Kernel? kernel = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 用于累积工具调用的临时变量
            // Key: tool call index, Value: 临时存储的 ID, Name, Arguments (累积的字符串)
            var accumulatedToolCalls = new Dictionary<int, (string id, string name, string argumentsJsonBuffer)>(); // argumentsJsonBuffer 用于累积 JSON 字符串

            var payload = BuildPayload(chatHistory, executionSettings, kernel);
            // 🔧 直接在这里打印 payload (因为 payload 就是你构建的匿名对象)
            var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogDebug("[DashScope Request Body]:\n{Payload}", payloadJson);

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = requestContent };

            // 🌟 关键：ResponseHeadersRead 确保底层流不被缓冲，实现真正流式
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // 处理 HTTP 错误响应
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[DashScope Error Response]:\n{ResponseBody}", responseBody);

                // 解析错误信息
                var errorMessage = _errorHandler.ParseHttpResponse(response.StatusCode, responseBody);
                var formattedError = BailianErrorHandler.FormatErrorMessage(errorMessage);

                // 生成错误消息并输出给前端
                yield return new StreamingChatMessageContent(
                    role: AuthorRole.Assistant,
                    content: formattedError,
                    modelId: _modelId,
                    metadata: new Dictionary<string, object?>
                    {
                        ["error"] = true,
                        ["error_code"] = errorMessage.ErrorCode,
                        ["http_status"] = errorMessage.HttpStatus
                    }
                );

                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                _logger.LogDebug("[DashScope Response Body]:\n{Line}", line);
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(" ")) continue;
                var data = line[6..].Trim();
                if (data == "[DONE]") break;

                // 🔧 修复 CS1626：先解析，成功后在外部 yield
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
                    // 检查是否是错误响应
                    if (root.TryGetProperty("error", out var errorProp))
                    {
                        var errorMessage = errorProp.TryGetProperty("message", out var errorMessageProp)
                            ? errorMessageProp.GetString()
                            : "AI 模型返回错误";
                        var errorType = errorProp.TryGetProperty("type", out var errorTypeProp)
                            ? errorTypeProp.GetString()
                            : "UnknownError";
                        var errorCode = errorProp.TryGetProperty("code", out var errorCodeProp)
                            ? errorCodeProp.GetInt32()
                            : 400;

                        _logger.LogError("DashScope 错误响应: {Message} (Type: {Type}, Code: {Code})", errorMessage, errorType, errorCode);

                        yield return new StreamingChatMessageContent(
                            role: AuthorRole.Assistant,
                            content: errorMessage,
                            modelId: _modelId,
                            metadata: new Dictionary<string, object?>
                            {
                                ["error"] = errorProp.ToString(),
                                ["error_message"] = errorMessage,
                                ["error_type"] = errorType,
                                ["error_code"] = errorCode
                            }
                        );
                        break;
                    }

                    // 🔍 处理 usage 统计信息（安全提取顶层数字字段，忽略嵌套对象）
                    if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
                    {
                        var usageMeta = new Dictionary<string, object?>();

                        if (usage.TryGetProperty("total_tokens", out var total) && total.TryGetInt32(out var totalVal))
                            usageMeta["usage_total_tokens"] = totalVal;
                        if (usage.TryGetProperty("prompt_tokens", out var prompt) && prompt.TryGetInt32(out var promptVal))
                            usageMeta["usage_prompt_tokens"] = promptVal;
                        if (usage.TryGetProperty("completion_tokens", out var completion) && completion.TryGetInt32(out var compVal))
                            usageMeta["usage_completion_tokens"] = compVal;

                        if (usageMeta.Count > 0)
                        {
                            yield return new StreamingChatMessageContent(
                                role: AuthorRole.Assistant,
                                content: null,
                                modelId: _modelId,
                                metadata: usageMeta); // 修正：使用 metadata
                        }
                        // 注意：纯 usage 的 chunk 没有 choices，直接继续下一个
                        if (!root.TryGetProperty("choices", out var choices2) || choices2.GetArrayLength() == 0)
                            continue;
                    }

                    if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0) continue;

                    var choice = choices[0];
                    if (!choice.TryGetProperty("delta", out var delta)) continue;

                    // 🔍 1. 提取 reasoning_content（数据驱动识别，真正流式）
                    if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
                    {
                        var rcText = rc.GetString();
                        if (!string.IsNullOrEmpty(rcText))
                        {
                            var metadata = new Dictionary<string, object?> { ["reasoning_content"] = rcText };
                            yield return new StreamingChatMessageContent(
                                role: AuthorRole.Assistant,
                                content: null,
                                modelId: _modelId,
                                metadata: metadata); // 修正：使用 metadata
                        }
                    }

                    // 🔍 2. 提取 tool_calls（MCP/插件兼容）- 修复：处理转义的 arguments 字符串片段
                    if (delta.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tc in toolCalls.EnumerateArray())
                        {
                            // 获取 tool call 的索引
                            if (!tc.TryGetProperty("index", out var indexElement) || !indexElement.TryGetInt32(out var index))
                            {
                                // 如果没有索引，默认为 0，适用于单个工具调用的情况
                                index = 0;
                            }

                            // 获取或创建当前索引的累积对象
                            var currentAccumulated = accumulatedToolCalls.ContainsKey(index)
                                ? accumulatedToolCalls[index]
                                : (id: "", name: "", argumentsJsonBuffer: "");

                            // 获取当前 delta 中的 id (如果存在)
                            var currentId = tc.TryGetProperty("id", out var idProp) ? idProp.GetString() : currentAccumulated.id;

                            // 获取当前 delta 中的 function 信息 (如果存在)
                            string currentName = currentAccumulated.name;
                            string currentArgumentsBuffer = currentAccumulated.argumentsJsonBuffer; // 累积的 JSON 字符串

                            if (tc.TryGetProperty("function", out var fn))
                            {
                                // 更新 name (如果存在)
                                if (fn.TryGetProperty("name", out var nameProp))
                                {
                                    currentName = nameProp.GetString(); // Name 通常是一次性给出的
                                }

                                // 更新 arguments (如果存在) - 关键：处理转义的 JSON 字符串片段
                                if (fn.TryGetProperty("arguments", out var argElement))
                                {
                                    // 🔧 如果 arguments 是字符串，则它是一个转义的 JSON 片段
                                    if (argElement.ValueKind == JsonValueKind.String)
                                    {
                                        var escapedArgFragmentString = argElement.GetString(); // 获取转义字符串
                                        if (!string.IsNullOrEmpty(escapedArgFragmentString))
                                        {
                                            // 累积原始的转义字符串片段
                                    // 当所有分块都累积完成后，再一次性解码
                                    currentArgumentsBuffer += escapedArgFragmentString; // 累积片段，处理分块发送的情况
                                        }
                                    }
                                    // 🔧 如果 arguments 不是字符串，而是对象或数组，则按之前的方式处理（虽然这种情况在此模型中似乎不会出现）
                                    else
                                    {
                                        var argFragmentString = argElement.GetRawText(); // 获取原始 JSON 文本
                                        if (!string.IsNullOrEmpty(argFragmentString))
                                        {
                                            currentArgumentsBuffer += argFragmentString; // 累积片段，处理分块发送的情况
                                        }
                                    }
                                }
                            }

                            // 更新累积字典
                            accumulatedToolCalls[index] = (currentId, currentName, currentArgumentsBuffer);
                        }
                    }

                    // 🔍 3. 暂时不处理工具调用，等待所有分块都收到
                    // 工具调用将在收到 finish_reason 时处理


                    // 🔍 4. 提取 content（新增：处理非标准的 type-based 格式）
                    if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        var contentText = c.GetString();
                        if (!string.IsNullOrEmpty(contentText))
                        {
                            // 检查是否是 tool_call 标记
                            if (contentText.Contains("[tool_call]"))
                            {
                                // 如果有之前积累的函数调用信息，尝试构建工具调用
                                // 这里需要额外的逻辑来处理 DashScope 的非标准格式
                                yield return new StreamingChatMessageContent(
                                    role: AuthorRole.Tool,
                                    content: contentText,
                                    modelId: _modelId);
                            }
                            else
                            {
                                yield return new StreamingChatMessageContent(
                                    role: AuthorRole.Assistant,
                                    content: contentText,
                                    modelId: _modelId);
                            }
                        }
                    }

                    // 🔍 5. 新增：处理 DashScope 特有的 type-based 格式
                    if (delta.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
                    {
                        var typeValue = typeElement.GetString();

                        if (typeValue == "thinking")
                        {
                            // 处理思考过程
                            if (delta.TryGetProperty("content", out var thinkingContent) && thinkingContent.ValueKind == JsonValueKind.String)
                            {
                                var thinkingText = thinkingContent.GetString();
                                if (!string.IsNullOrEmpty(thinkingText))
                                {
                                    var metadata = new Dictionary<string, object?> { ["reasoning_content"] = thinkingText };
                                    yield return new StreamingChatMessageContent(
                                        role: AuthorRole.Assistant,
                                        content: null,
                                        modelId: _modelId,
                                        metadata: metadata); // 修正：使用 metadata
                                }
                            }
                        }
                        else if (typeValue == "content")
                        {
                            // 处理内容，可能是工具调用标记
                            if (delta.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
                            {
                                var contentText = contentElement.GetString();
                                if (!string.IsNullOrEmpty(contentText))
                                {
                                    // 检查是否包含工具调用相关信息
                                    if (contentText.Contains("[tool_call]"))
                                    {
                                        // 这里可能需要更复杂的逻辑来解析实际的工具调用信息
                                        // 当前简单地作为工具调用处理
                                        yield return new StreamingChatMessageContent(
                                            role: AuthorRole.Tool,
                                            content: contentText,
                                            modelId: _modelId);
                                    }
                                    else
                                    {
                                        yield return new StreamingChatMessageContent(
                                            role: AuthorRole.Assistant,
                                            content: contentText,
                                            modelId: _modelId);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 🔧 循环结束后，处理所有累积的工具调用
            foreach (var kvp in accumulatedToolCalls)
            {
                var callData = kvp.Value;
                if (!string.IsNullOrEmpty(callData.name))
                {
                    bool isValidFinalJson = false;
                    string finalParsedArguments = callData.argumentsJsonBuffer; // 默认为累积的字符串

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
                            metadata: metadata); // 修正：使用 metadata
                    }
                }
            }
        }

        // 非流式方法按需实现（当前业务仅使用流式）
        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("请使用流式接口 GetStreamingChatMessageContentsAsync");

        private object BuildPayload(ChatHistory history, PromptExecutionSettings? settings, Kernel? requestKernel = null)
        {
            var ext = (settings as OpenAIPromptExecutionSettings)?.ExtensionData ?? new Dictionary<string, object>();

            // 🔧 优先使用请求传入的 Kernel，否则使用构造函数传入的
            var kernel = requestKernel ?? _kernel;

            // 🔧 从 Kernel.Plugins 构建 tools 数组 - 转换为 OpenAI 兼容格式
            var tools = kernel?.Plugins
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
                            parameters = parameters,
                            strict = false  // DashScope 兼容
                        }
                    };
                }))
                .ToList();

            // 🔧 动态构建 payload，将 ExtensionData 中的所有配置项展开到 payload 中
            var payloadDict = new Dictionary<string, object?>
            {
                ["model"] = _modelId,
                ["messages"] = history.Select(m => new
                {
                    role = m.Role.Label.ToLowerInvariant(),
                    content = m.Content
                }),
                ["stream"] = true,
                ["stream_options"] = new { include_usage = true },
                ["tools"] = tools.Count > 0 ? tools : null,
                ["tool_choice"] = tools.Count > 0 ? "auto" : null
            };

            // 🔧 将 ExtensionData 中的所有其他配置项添加到 payload 中
            // 这样支持任意自定义配置项，如 enable_thinking, preserve_thinking, temperature, max_tokens 等
            foreach (var kvp in ext)
            {
                // 跳过已处理的配置项
                if (kvp.Key == "FunctionChoiceBehavior" || kvp.Key == "User" || kvp.Key == "ToolCallBehavior")
                    continue;
                
                payloadDict[kvp.Key] = kvp.Value;
            }

            return payloadDict;
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

        // 空 logger 实现，用于当没有提供 loggerFactory 时
        private class NullLogger<T> : ILogger<T>
        {
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
        }
    }
}