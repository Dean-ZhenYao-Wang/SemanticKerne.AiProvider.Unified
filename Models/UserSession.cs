using System.Collections.Concurrent;

namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 用户会话模型，包含该用户的所有聊天会话
/// </summary>
public class UserSession
{
    public string UserId { get; set; } = string.Empty;
    public ConcurrentDictionary<string, ChatSession> ChatSessions { get; set; } = new();
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
}
