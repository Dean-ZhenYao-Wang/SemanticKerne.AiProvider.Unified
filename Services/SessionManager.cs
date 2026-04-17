using System.Collections.Concurrent;
using SemanticKerne.AiProvider.Unified.Models;

namespace SemanticKerne.AiProvider.Unified.Services;

/// <summary>
/// 内存会话管理实现
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, UserSession> _userSessions = new();
    private readonly ISemanticKernelService _kernelService;

    public SessionManager(ISemanticKernelService kernelService)
    {
        _kernelService = kernelService;
    }

    public ChatSession CreateSession(string userId)
    {
        var userSession = _userSessions.GetOrAdd(userId, _ => new UserSession { UserId = userId });

        var chatSession = new ChatSession
        {
            UserId = userId,
            Kernel = _kernelService.CreateKernel(),
            CreatedAt = DateTime.UtcNow
        };

        // 系统提示内容
        var systemPrompt = @$"你是一个智能助手，当前系统日期为{DateTime.Today.ToString("yyyy - MM - dd")}，请始终使用简体中文回答用户的所有问题。
当用户需要查询数据库时，请严格按照以下步骤执行：
1.首先使用 sql-mcp-http 服务的 describe_entities 工具了解数据结构
2.然后使用 sql-mcp-http 服务的 read_records 工具查询数据
3.最后根据查询结果回答用户问题
请确保在了解数据结构后，必须调用 read_records 工具执行实际查询，而不是仅描述如何使用工具。";
        // 添加系统提示到 SemanticKernel 历史
        chatSession.History.AddSystemMessage(systemPrompt);

        userSession.ChatSessions[chatSession.SessionId] = chatSession;
        userSession.LastActiveAt = DateTime.UtcNow;

        return chatSession;
    }

    public IEnumerable<SessionInfo> GetUserSessions(string userId)
    {
        if (!_userSessions.TryGetValue(userId, out var userSession))
        {
            return Enumerable.Empty<SessionInfo>();
        }

        return userSession.ChatSessions.Values.Select(s => new SessionInfo
        {
            SessionId = s.SessionId,
            CreatedAt = s.CreatedAt,
            Title = s.Title,
            MessageCount = s.History.Count
        });
    }

    public ChatSession? GetSession(string userId, string sessionId)
    {
        if (!_userSessions.TryGetValue(userId, out var userSession))
        {
            return null;
        }

        userSession.LastActiveAt = DateTime.UtcNow;
        return userSession.ChatSessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public bool DeleteSession(string userId, string sessionId)
    {
        if (!_userSessions.TryGetValue(userId, out var userSession))
        {
            return false;
        }

        if (userSession.ChatSessions.TryRemove(sessionId, out var session))
        {
            session.Dispose();
            return true;
        }
        return false;
    }

    public bool StopSession(string userId, string sessionId)
    {
        var session = GetSession(userId, sessionId);
        if (session == null)
        {
            return false;
        }

        // 如果正在处理，则取消
        if (session.IsProcessing)
        {
            session.StopCurrentRequest();
            return true;
        }

        // 即使 AI 已完成响应，也返回成功（避免前端 404）
        return true;
    }
}
