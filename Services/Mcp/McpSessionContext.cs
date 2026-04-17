using SemanticKerne.AiProvider.Unified.Models;

namespace SemanticKerne.AiProvider.Unified.Services.Mcp;

/// <summary>
/// MCP会话上下文持有者（使用AsyncLocal实现线程安全）
/// </summary>
public static class McpSessionContext
{
    private static readonly AsyncLocal<ChatSession?> _currentSession = new();

    /// <summary>
    /// 获取或设置当前会话
    /// </summary>
    public static ChatSession? CurrentSession
    {
        get => _currentSession.Value;
        set => _currentSession.Value = value;
    }

    /// <summary>
    /// 获取当前会话的MCP Session IDs
    /// </summary>
    public static Dictionary<string, string> McpSessionIds => CurrentSession?.McpSessionIds ?? new();
}
