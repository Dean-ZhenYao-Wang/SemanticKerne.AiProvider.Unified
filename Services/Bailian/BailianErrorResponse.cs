using System.Text.Json.Serialization;

namespace SemanticKerne.AiProvider.Unified.Services.Bailian;

/// <summary>
/// 阿里云百炼API错误响应模型
/// </summary>
public class BailianErrorResponse
{
    /// <summary>
    /// 错误码
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    /// <summary>
    /// HTTP状态码
    /// </summary>
    [JsonPropertyName("status")]
    public int? HttpStatus { get; set; }
    
    /// <summary>
    /// 请求ID
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}

/// <summary>
/// OpenAI兼容格式的错误响应
/// </summary>
public class OpenAIErrorResponse
{
    /// <summary>
    /// 错误对象
    /// </summary>
    [JsonPropertyName("error")]
    public OpenAIErrorDetail? Error { get; set; }
}

/// <summary>
/// OpenAI错误详情
/// </summary>
public class OpenAIErrorDetail
{
    /// <summary>
    /// 错误消息
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    /// <summary>
    /// 错误类型
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    /// <summary>
    /// 错误码
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    /// <summary>
    /// 参数
    /// </summary>
    [JsonPropertyName("param")]
    public string? Param { get; set; }
}
