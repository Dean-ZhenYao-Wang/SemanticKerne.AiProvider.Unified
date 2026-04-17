namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 会话信息DTO
/// </summary>
public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Title { get; set; }
    public int MessageCount { get; set; }
}
