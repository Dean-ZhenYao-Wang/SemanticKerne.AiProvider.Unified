using SemanticKerne.AiProvider.Unified.Models;

namespace SemanticKerne.AiProvider.Unified.Services;

/// <summary>
/// 会话管理接口
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 为用户创建新的聊天会话
    /// </summary>
    ChatSession CreateSession(string userId);

    /// <summary>
    /// 获取用户的所有会话信息
    /// </summary>
    IEnumerable<SessionInfo> GetUserSessions(string userId);

    /// <summary>
    /// 获取指定的聊天会话
    /// </summary>
    ChatSession? GetSession(string userId, string sessionId);

    /// <summary>
    /// 删除指定的聊天会话
    /// </summary>
    bool DeleteSession(string userId, string sessionId);

    /// <summary>
    /// 停止会话当前正在进行的请求（不删除会话）
    /// </summary>
    /// <returns>是否成功停止（false表示会话不存在或没有正在进行的请求）</returns>
    bool StopSession(string userId, string sessionId);
}
