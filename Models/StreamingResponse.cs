namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 流式响应类型
/// </summary>
public enum StreamingResponseType
{
    /// <summary>
    /// 思考过程
    /// </summary>
    Thinking,
    
    /// <summary>
    /// 回答内容
    /// </summary>
    Content,
    
    /// <summary>
    /// 错误信息
    /// </summary>
    Error,
    /// <summary>
    /// 异常信息
    /// </summary>
    Exception,
    /// <summary>
    /// 工具调用结果
    /// </summary>
    ToolResult,
    /// <summary>
    /// 统计信息
    /// </summary>
    Usage,
}

/// <summary>
/// 流式响应项
/// </summary>
public class StreamingResponse
{
    /// <summary>
    /// 响应类型
    /// </summary>
    public StreamingResponseType Type { get; set; }
    
    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误码(仅当Type为Error时有值)
    /// </summary>
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// HTTP状态码(仅当Type为Error时有值)
    /// </summary>
    public int? HttpStatus { get; set; }
    
    /// <summary>
    /// 错误标题(仅当Type为Error时有值)
    /// </summary>
    public string? ErrorTitle { get; set; }
    
    /// <summary>
    /// 错误原因(仅当Type为Error时有值)
    /// </summary>
    public string? ErrorReason { get; set; }
    
    /// <summary>
    /// 解决方案(仅当Type为Error时有值)
    /// </summary>
    public string? ErrorSolution { get; set; }
    
    /// <summary>
    /// 是否为严重错误(仅当Type为Error时有值)
    /// </summary>
    public bool? IsCritical { get; set; }
}
