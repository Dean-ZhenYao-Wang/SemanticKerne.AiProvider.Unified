namespace SemanticKerne.AiProvider.Unified.Services.Bailian;

/// <summary>
/// 用户友好的错误消息模型
/// </summary>
public class BailianErrorMessage
{
    /// <summary>
    /// 错误码
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;
    
    /// <summary>
    /// HTTP状态码
    /// </summary>
    public int HttpStatus { get; set; }
    
    /// <summary>
    /// 错误标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 错误原因说明
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// 解决方案
    /// </summary>
    public string Solution { get; set; } = string.Empty;
    
    /// <summary>
    /// 原始错误消息
    /// </summary>
    public string? OriginalMessage { get; set; }
    
    /// <summary>
    /// 是否为严重错误(需要立即处理)
    /// </summary>
    public bool IsCritical { get; set; }
    
    /// <summary>
    /// 错误分类
    /// </summary>
    public BailianErrorCategory Category { get; set; }
}

/// <summary>
/// 错误分类
/// </summary>
public enum BailianErrorCategory
{
    /// <summary>
    /// 参数错误 - 用户可修正
    /// </summary>
    ParameterError,
    
    /// <summary>
    /// 认证错误 - 需要检查API Key
    /// </summary>
    AuthenticationError,
    
    /// <summary>
    /// 权限错误 - 需要开通服务或购买
    /// </summary>
    PermissionError,
    
    /// <summary>
    /// 资源不存在 - 模型或文件不存在
    /// </summary>
    NotFoundError,
    
    /// <summary>
    /// 限流错误 - 需要降低调用频率
    /// </summary>
    RateLimitError,
    
    /// <summary>
    /// 服务器错误 - 服务端问题,稍后重试
    /// </summary>
    ServerError,
    
    /// <summary>
    /// 文件错误 - 文件格式或大小问题
    /// </summary>
    FileError,
    
    /// <summary>
    /// 输入验证错误 - 输入内容不符合要求
    /// </summary>
    ValidationError,
    
    /// <summary>
    /// 配额错误 - 配额不足或已用完
    /// </summary>
    QuotaError,
    
    /// <summary>
    /// 网络错误 - 连接或超时问题
    /// </summary>
    NetworkError,
    
    /// <summary>
    /// 内容错误 - 内容安全检查失败
    /// </summary>
    ContentError,
    
    /// <summary>
    /// 其他错误
    /// </summary>
    Other
}
