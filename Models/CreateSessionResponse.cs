namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 创建会话响应DTO
/// </summary>
public class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
