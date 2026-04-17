using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SemanticKerne.AiProvider.Unified.Services.Bailian;

/// <summary>
/// 阿里云百炼API错误处理器
/// 负责解析异常并提取百炼错误信息
/// </summary>
public class BailianErrorHandler
{
    private readonly ILogger<BailianErrorHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public BailianErrorHandler(ILogger<BailianErrorHandler> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // 初始化补充映射器
        // _ = BailianErrorMapperSupplement.GetSupplementalErrorCodes(); // 暂时禁用，该类不存在
    }
    
    /// <summary>
    /// 处理异常并转换为友好的错误消息
    /// </summary>
    /// <param name="exception">捕获的异常</param>
    /// <returns>友好的错误消息</returns>
    public BailianErrorMessage HandleException(Exception exception)
    {
        try
        {
            // 根据异常类型进行不同的处理
            return exception switch
            {
                HttpRequestException httpEx => HandleHttpRequestException(httpEx),
                UnauthorizedAccessException => new BailianErrorMessage
                {
                    ErrorCode = BailianErrorCodes.InvalidApiKey,
                    HttpStatus = 401,
                    Title = "认证失败",
                    Reason = "未授权访问API服务",
                    Solution = "请检查API Key配置是否正确",
                    IsCritical = true,
                    Category = BailianErrorCategory.AuthenticationError
                },
                TimeoutException => new BailianErrorMessage
                {
                    ErrorCode = BailianErrorCodes.RequestTimeout,
                    HttpStatus = 408,
                    Title = "请求超时",
                    Reason = "请求处理超时",
                    Solution = "请稍后重试,或检查网络连接",
                    IsCritical = false,
                    Category = BailianErrorCategory.NetworkError
                },
                OperationCanceledException => new BailianErrorMessage
                {
                    ErrorCode = "OperationCanceled",
                    HttpStatus = 499,
                    Title = "请求已取消",
                    Reason = "请求被客户端或服务器取消",
                    Solution = "如果是客户端取消,请重试;如果是服务器取消,请检查请求参数",
                    IsCritical = false,
                    Category = BailianErrorCategory.Other
                },
                JsonException jsonEx => new BailianErrorMessage
                {
                    ErrorCode = "JsonParseError",
                    HttpStatus = 400,
                    Title = "JSON解析错误",
                    Reason = $"解析JSON响应失败: {jsonEx.Message}",
                    Solution = "请检查API响应格式是否正确,或联系技术支持",
                    IsCritical = false,
                    Category = BailianErrorCategory.ParameterError
                },
                _ => HandleGenericException(exception)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理异常时发生错误");
            return new BailianErrorMessage
            {
                ErrorCode = "ErrorHandlerException",
                HttpStatus = 500,
                Title = "错误处理异常",
                Reason = $"处理错误时发生异常: {ex.Message}",
                Solution = "请稍后重试,或联系技术支持",
                IsCritical = true,
                Category = BailianErrorCategory.ServerError
            };
        }
    }
    
    /// <summary>
    /// 处理HTTP请求异常
    /// </summary>
    private BailianErrorMessage HandleHttpRequestException(HttpRequestException exception)
    {
        var errorMessage = new BailianErrorMessage
        {
            HttpStatus = (int?)exception.StatusCode ?? 0,
            ErrorCode = "HttpRequestError",
            Title = "HTTP请求失败",
            Reason = exception.Message,
            Solution = "请检查网络连接,或稍后重试",
            Category = BailianErrorCategory.NetworkError,
            IsCritical = false
        };
        
        // 尝试从响应中提取错误信息
        if (!string.IsNullOrEmpty(exception.Message))
        {
            // 尝试提取HTTP状态码
            var statusMatch = System.Text.RegularExpressions.Regex.Match(exception.Message, @"(\d{3})");
            if (statusMatch.Success && int.TryParse(statusMatch.Groups[1].Value, out var statusCode))
            {
                errorMessage.HttpStatus = statusCode;
                
                // 根据状态码推断错误类型
                var inferredCode = BailianErrorMapper.InferErrorCode(statusCode, exception.Message);
                var mappedMessage = BailianErrorMapper.GetErrorMessage(inferredCode, exception.Message);
                
                if (mappedMessage != null)
                {
                    return mappedMessage;
                }
            }
        }
        
        return errorMessage;
    }
    
    /// <summary>
    /// 处理通用异常
    /// </summary>
    private BailianErrorMessage HandleGenericException(Exception exception)
    {
        // 检查是否包含百炼错误码信息
        var errorCode = ExtractErrorCode(exception);
        
        if (!string.IsNullOrEmpty(errorCode))
        {
            // 优先使用官方文档映射器
            var officialMessage = BailianErrorMapperOfficial.GetErrorMessage(errorCode, exception.Message);
            if (officialMessage != null)
            {
                return officialMessage;
            }
            
            // 如果官方映射器没有，尝试从主映射器获取
            var mappedMessage = BailianErrorMapper.GetErrorMessage(errorCode, exception.Message);
            if (mappedMessage != null)
            {
                return mappedMessage;
            }
        }
        
        // 检查异常消息中的关键词
        var inferredCode = InferErrorCodeFromMessage(exception.Message);
        
        // 优先使用官方文档映射器
        var officialInferred = BailianErrorMapperOfficial.GetErrorMessage(inferredCode, exception.Message);
        if (officialInferred != null)
        {
            return officialInferred;
        }
        
        // 如果官方映射器没有，尝试从主映射器获取
        var mappedInferred = BailianErrorMapper.GetErrorMessage(inferredCode, exception.Message);
        if (mappedInferred != null)
        {
            return mappedInferred;
        }
        
        // 默认错误消息
        return new BailianErrorMessage
        {
            ErrorCode = "UnknownError",
            HttpStatus = 500,
            Title = "未知错误",
            Reason = exception.Message,
            Solution = "请稍后重试,如果问题持续存在,请联系技术支持",
            IsCritical = false,
            Category = BailianErrorCategory.Other
        };
    }
    
    /// <summary>
    /// 从异常中提取错误码
    /// </summary>
    private string? ExtractErrorCode(Exception exception)
    {
        var message = exception.Message;
        
        // 检查常见的错误码模式
        if (string.IsNullOrEmpty(message))
            return null;
        
        // 尝试匹配 "code": "xxx" 模式
        var codeMatch = System.Text.RegularExpressions.Regex.Match(message, @"""code""\s*:\s*""([^""]+)""");
        if (codeMatch.Success)
        {
            return codeMatch.Groups[1].Value;
        }
        
        // 尝试匹配 code=xxx 模式
        var codeMatch2 = System.Text.RegularExpressions.Regex.Match(message, @"code\s*[:=]\s*([^\s,}]+)");
        if (codeMatch2.Success)
        {
            return codeMatch2.Groups[1].Value.Trim('"');
        }
        
        // 检查已知的错误码（基于官方文档）
        if (message.Contains("enable_thinking must be set to false", StringComparison.OrdinalIgnoreCase))
            return "parameter.enable_thinking must be set to false for non-streaming calls";
        if (message.Contains("thinking_budget parameter must be a positive integer", StringComparison.OrdinalIgnoreCase))
            return "The thinking_budget parameter must be a positive integer and not greater than xxx";
        if (message.Contains("model only support stream mode", StringComparison.OrdinalIgnoreCase))
            return "This model only support stream mode";
        if (message.Contains("model does not support enable_search", StringComparison.OrdinalIgnoreCase))
            return "This model does not support enable_search";
        if (message.Contains("Range of input length should be", StringComparison.OrdinalIgnoreCase))
            return "Range of input length should be [1, xxx]";
        if (message.Contains("Temperature should be in", StringComparison.OrdinalIgnoreCase))
            return "Temperature should be in [0.0, 2.0)";
        
        // 官方文档中的错误码
        if (message.Contains("InvalidParameter", StringComparison.OrdinalIgnoreCase))
            return "InvalidParameter";
        if (message.Contains("ModelNotFound", StringComparison.OrdinalIgnoreCase))
            return "ModelNotFound";
        if (message.Contains("AuthenticationError", StringComparison.OrdinalIgnoreCase) || 
            message.Contains("InvalidApiKey", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Invalid API-key", StringComparison.OrdinalIgnoreCase))
            return "InvalidApiKey";
        if (message.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
            return "AccessDenied";
        if (message.Contains("Throttling", StringComparison.OrdinalIgnoreCase))
            return "Throttling";
        if (message.Contains("InternalError", StringComparison.OrdinalIgnoreCase))
            return "InternalError";
        if (message.Contains("DataInspectionFailed", StringComparison.OrdinalIgnoreCase))
            return "DataInspectionFailed";
        if (message.Contains("Arrearage", StringComparison.OrdinalIgnoreCase))
            return "Arrearage";
        if (message.Contains("InvalidFile", StringComparison.OrdinalIgnoreCase))
            return "InvalidFile";
        if (message.Contains("ModelUnavailable", StringComparison.OrdinalIgnoreCase))
            return "ModelUnavailable";
        
        return null;
    }
    
    /// <summary>
    /// 从异常消息中推断错误码
    /// </summary>
    private string InferErrorCodeFromMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return BailianErrorCodes.InternalError;
        
        // 思考模式相关
        if (message.Contains("enable_thinking", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.EnableThinkingMustBeFalse;
        if (message.Contains("thinking_budget", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.ThinkingBudgetInvalid;
        
        // Token相关
        if (message.Contains("token", StringComparison.OrdinalIgnoreCase) && message.Contains("limit", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.InputLengthOutOfRange;
        if (message.Contains("max_tokens", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.MaxTokensOutOfRange;
        
        // 参数范围
        if (message.Contains("temperature", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.TemperatureOutOfRange;
        if (message.Contains("top_p", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.TopPOutOfRange;
        if (message.Contains("top_k", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.TopKInvalid;
        
        // 文件相关
        if (message.Contains("file", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Contains("format", StringComparison.OrdinalIgnoreCase))
                return BailianErrorCodes.FileFormatNotSupported;
            if (message.Contains("size", StringComparison.OrdinalIgnoreCase))
                return BailianErrorCodes.FileSizeExceed;
            return BailianErrorCodes.InvalidFileId;
        }
        
        // 认证相关
        if (message.Contains("api key", StringComparison.OrdinalIgnoreCase) || 
            message.Contains("ApiKey", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.InvalidApiKey;
        
        // 限流相关
        if (message.Contains("limit", StringComparison.OrdinalIgnoreCase) || 
            message.Contains("throttle", StringComparison.OrdinalIgnoreCase))
            return BailianErrorCodes.Throttling;
        
        return BailianErrorCodes.InternalError;
    }
    
    /// <summary>
    /// 从HTTP响应中解析错误信息
    /// </summary>
    /// <param name="statusCode">HTTP状态码</param>
    /// <param name="responseBody">响应体</param>
    /// <returns>友好的错误消息</returns>
    public BailianErrorMessage ParseHttpResponse(HttpStatusCode statusCode, string? responseBody)
    {
        var httpStatus = (int)statusCode;
        
        // 尝试解析响应体中的错误信息
        if (!string.IsNullOrEmpty(responseBody))
        {
            try
            {
                // 尝试解析百炼格式: { "code": "xxx", "message": "xxx" }
                var bailianResponse = JsonSerializer.Deserialize<BailianErrorResponse>(responseBody, _jsonOptions);
                if (!string.IsNullOrEmpty(bailianResponse?.Code))
                {
                    var mapped = BailianErrorMapper.GetErrorMessage(bailianResponse.Code, bailianResponse.Message);
                    if (mapped != null)
                    {
                        mapped.HttpStatus = httpStatus;
                        return mapped;
                    }
                }
                
                // 尝试解析OpenAI兼容格式: { "error": { "code": "xxx", "message": "xxx" } }
                var openAIResponse = JsonSerializer.Deserialize<OpenAIErrorResponse>(responseBody, _jsonOptions);
                if (!string.IsNullOrEmpty(openAIResponse?.Error?.Code))
                {
                    var mapped = BailianErrorMapper.GetErrorMessage(openAIResponse.Error.Code, openAIResponse.Error.Message);
                    if (mapped != null)
                    {
                        mapped.HttpStatus = httpStatus;
                        return mapped;
                    }
                    
                    // 如果没有映射,创建基本错误消息
                    return new BailianErrorMessage
                    {
                        ErrorCode = openAIResponse.Error.Code,
                        HttpStatus = httpStatus,
                        Title = "API错误",
                        Reason = openAIResponse.Error.Message ?? "未知错误",
                        Solution = "请检查请求参数,或参考API文档",
                        Category = BailianErrorCategory.ParameterError,
                        IsCritical = false
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析HTTP响应JSON失败: {Response}", responseBody);
            }
        }
        
        // 如果无法解析,根据状态码创建基本错误消息
        var inferredCode = BailianErrorMapper.InferErrorCode(httpStatus, responseBody ?? "");
        var mappedInferred = BailianErrorMapper.GetErrorMessage(inferredCode, responseBody);
        
        if (mappedInferred != null)
        {
            return mappedInferred;
        }
        
        // 最后兜底:创建通用错误消息
        return new BailianErrorMessage
        {
            ErrorCode = $"HTTP_{httpStatus}",
            HttpStatus = httpStatus,
            Title = $"HTTP错误 ({httpStatus})",
            Reason = $"请求失败,状态码: {httpStatus}",
            Solution = GetDefaultSolution(httpStatus),
            Category = DetermineCategory(httpStatus),
            IsCritical = httpStatus >= 500
        };
    }
    
    /// <summary>
    /// 获取默认解决方案
    /// </summary>
    private static string GetDefaultSolution(int httpStatus)
    {
        return httpStatus switch
        {
            400 => "请检查请求参数是否正确",
            401 => "请检查API Key配置",
            403 => "请确认您有权限访问该资源",
            404 => "请检查请求的资源是否存在",
            429 => "请降低调用频率,稍后重试",
            500 or 503 => "服务器暂时不可用,请稍后重试",
            _ => "请稍后重试,或联系技术支持"
        };
    }
    
    /// <summary>
    /// 根据HTTP状态码确定错误分类
    /// </summary>
    private static BailianErrorCategory DetermineCategory(int httpStatus)
    {
        return httpStatus switch
        {
            400 => BailianErrorCategory.ParameterError,
            401 => BailianErrorCategory.AuthenticationError,
            403 => BailianErrorCategory.PermissionError,
            404 => BailianErrorCategory.NotFoundError,
            429 => BailianErrorCategory.RateLimitError,
            500 or 503 => BailianErrorCategory.ServerError,
            _ => BailianErrorCategory.Other
        };
    }
    
    /// <summary>
    /// 将错误消息转换为用户友好的字符串
    /// </summary>
    public static string FormatErrorMessage(BailianErrorMessage error)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"【{error.Title}】({error.ErrorCode})");
        sb.AppendLine($"HTTP状态码: {error.HttpStatus}");
        sb.AppendLine($"原因: {error.Reason}");
        if (!string.IsNullOrEmpty(error.Solution))
        {
            sb.AppendLine($"解决方案: {error.Solution}");
        }
        if (!string.IsNullOrEmpty(error.OriginalMessage))
        {
            sb.AppendLine($"原始消息: {error.OriginalMessage}");
        }
        return sb.ToString();
    }
    
    /// <summary>
    /// 将错误消息转换为简短的字符串(用于日志)
    /// </summary>
    public static string FormatShortMessage(BailianErrorMessage error)
    {
        return $"[{error.ErrorCode}] {error.Title}: {error.Reason}";
    }
}
