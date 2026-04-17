using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;
using OllamaSharp.Models;

namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 单个聊天会话模型
/// </summary>
public class ChatSession : IDisposable
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public ChatHistory History { get; set; } = new();
    public Kernel Kernel { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Title { get; set; }
    
    /// <summary>
    /// Ollama Chat 实例，用于维护对话历史
    /// </summary>
    public Chat? OllamaChat { get; set; }
    
    /// <summary>
    /// MCP服务器会话ID映射（服务器名称 -> Session ID）
    /// </summary>
    public Dictionary<string, string> McpSessionIds { get; set; } = new();

    /// <summary>
    /// 用于取消当前正在进行的请求的CancellationTokenSource
    /// </summary>
    private CancellationTokenSource? _currentRequestCts;
    private readonly object _ctsLock = new();

    /// <summary>
    /// 获取当前请求的CancellationToken
    /// </summary>
    public CancellationToken GetCurrentCancellationToken()
    {
        lock (_ctsLock)
        {
            _currentRequestCts ??= new CancellationTokenSource();
            return _currentRequestCts.Token;
        }
    }

    /// <summary>
    /// 停止当前正在进行的请求
    /// </summary>
    public void StopCurrentRequest()
    {
        lock (_ctsLock)
        {
            if (_currentRequestCts != null && !_currentRequestCts.IsCancellationRequested)
            {
                _currentRequestCts.Cancel();
            }
        }
    }

    /// <summary>
    /// 重置CancellationTokenSource（用于开始新请求时）
    /// </summary>
    public void ResetCancellationToken()
    {
        lock (_ctsLock)
        {
            // 直接创建新的 CTS，不要取消旧的
            // 旧的需要等待其关联的请求自然结束
            _currentRequestCts?.Dispose();
            _currentRequestCts = new CancellationTokenSource();
        }
    }

    /// <summary>
    /// 是否正在处理请求
    /// </summary>
    public bool IsProcessing { get; set; }

    public void Dispose()
    {
        lock (_ctsLock)
        {
            _currentRequestCts?.Dispose();
            _currentRequestCts = null;
        }
    }
}
