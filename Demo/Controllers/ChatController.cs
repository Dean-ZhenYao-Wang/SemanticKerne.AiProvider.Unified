using Microsoft.AspNetCore.Mvc;
using SemanticKerne.AiProvider.Unified.Models;
using SemanticKerne.AiProvider.Unified.Services;
using SemanticKerne.AiProvider.Unified.Services.Bailian;
using System.Security.Claims;
using System.Text.Json;

namespace lOT.API.Controllers;

/// <summary>
/// 聊天控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly ISemanticKernelService _kernelService;
    private readonly ILogger<ChatController> _logger;
    private readonly BailianErrorHandler _errorHandler;

    public ChatController(
        ISessionManager sessionManager,
        ISemanticKernelService kernelService,
        ILogger<ChatController> logger,
        BailianErrorHandler errorHandler)
    {
        _sessionManager = sessionManager;
        _kernelService = kernelService;
        _logger = logger;
        _errorHandler = errorHandler;
    }

    private string UserId => User.FindFirst(ClaimTypes.Name)?.Value ?? "test-user";

    /// <summary>
    /// 获取当前用户的所有会话
    /// </summary>
    [HttpGet("sessions")]
    public IActionResult GetSessions()
    {
        _logger.LogInformation("GetSessions called, UserId: {UserId}, AllClaims: {Claims}", 
            UserId, 
            string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        var sessions = _sessionManager.GetUserSessions(UserId);
        _logger.LogInformation("GetSessions result, count: {Count}", sessions.Count());
        return Ok(sessions);
    }

    /// <summary>
    /// 创建新的聊天会话
    /// </summary>
    [HttpPost("sessions")]
    public IActionResult CreateSession()
    {
        _logger.LogInformation("CreateSession called, UserId: {UserId}", UserId);
        var session = _sessionManager.CreateSession(UserId);
        _logger.LogInformation("CreateSession created, SessionId: {SessionId}, UserId: {UserId}", session.SessionId, UserId);
        return Ok(new CreateSessionResponse
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt
        });
    }

    /// <summary>
    /// 删除指定的聊天会话
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public IActionResult DeleteSession(string sessionId)
    {
        _logger.LogInformation("DeleteSession called, UserId: {UserId}, SessionId: {SessionId}", UserId, sessionId);
        var result = _sessionManager.DeleteSession(UserId, sessionId);
        if (!result)
        {
            _logger.LogWarning("DeleteSession failed, session not found, UserId: {UserId}, SessionId: {SessionId}", UserId, sessionId);
            return NotFound(new { message = "会话不存在" });
        }
        return NoContent();
    }

    /// <summary>
    /// 停止会话当前正在进行的请求（不删除会话）
    /// </summary>
    [HttpPost("sessions/{sessionId}/stop")]
    public IActionResult StopSession(string sessionId)
    {
        var session = _sessionManager.GetSession(UserId, sessionId);
        _logger.LogInformation("StopSession called, UserId: {UserId}, SessionId: {SessionId}, SessionExists: {Exists}, IsProcessing: {IsProcessing}", 
            UserId, sessionId, session != null, session?.IsProcessing);
        var result = _sessionManager.StopSession(UserId, sessionId);
        _logger.LogInformation("StopSession result: {Result}", result);
        if (!result)
        {
            return NotFound(new { message = "会话不存在或没有正在进行的请求" });
        }
        return Ok(new { message = "已停止当前请求" });
    }

    /// <summary>
    /// 发送消息并获取流式响应（SSE）
    /// </summary>
    [HttpPost("sessions/{sessionId}/chat")]
    public async Task Chat(string sessionId, [FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var session = _sessionManager.GetSession(UserId, sessionId);
        if (session == null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsync("会话不存在");
            return;
        }

        // 设置 SSE 响应头
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            await foreach (var response in _kernelService.StreamChatAsync(session, request.Message, cancellationToken))
            {
                // 构建响应对象
                if (response.Type == StreamingResponseType.Error)
                {
                    // 错误响应,包含详细错误信息
                    var errorObj = new
                    {
                        type = response.Type.ToString().ToLower(),
                        content = response.Content,
                        errorCode = response.ErrorCode,
                        httpStatus = response.HttpStatus,
                        title = response.ErrorTitle,
                        reason = response.ErrorReason,
                        solution = response.ErrorSolution,
                        isCritical = response.IsCritical
                    };
                    
                    var json = JsonSerializer.Serialize(errorObj);
                    
                    // SSE 格式: data: {json}\n\n
                    await Response.WriteAsync($"data: {json}\n\n", cancellationToken: cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    break; // 遇到细错类型的响应后停止继续发送
                }
                else if(response.Type == StreamingResponseType.Exception)
                {
                    // 错误响应,包含详细错误信息
                    var errorObj = new
                    {
                        type = response.Type.ToString().ToLower(),
                        content = response.Content,
                        errorCode = response.ErrorCode,
                        httpStatus = response.HttpStatus,
                        title = response.ErrorTitle,
                        reason = response.ErrorReason,
                        solution = response.ErrorSolution,
                        isCritical = response.IsCritical
                    };

                    var json = JsonSerializer.Serialize(errorObj);

                    // SSE 格式: data: {json}\n\n
                    await Response.WriteAsync($"data: {json}\n\n", cancellationToken: cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                    break; // 遇到异常类型的响应后停止继续发送
                }
                else
                {
                    // 普通内容响应
                    var responseObj = new
                    {
                        type = response.Type.ToString().ToLower(),
                        content = response.Content
                    };
                    
                    var json = JsonSerializer.Serialize(responseObj);
                    
                    // SSE 格式: data: {json}\n\n
                    await Response.WriteAsync($"data: {json}\n\n", cancellationToken: cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }

            // 发送结束标记
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken: cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("客户端断开连接，SessionId: {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogError(ex, "聊天处理出错，SessionId: {SessionId}", sessionId);
                
                // 使用错误处理器转换异常
                var errorInfo = _errorHandler.HandleException(ex);
                _logger.LogWarning("错误信息: {ErrorCode} - {Title}: {Reason}", 
                    errorInfo.ErrorCode, errorInfo.Title, errorInfo.Reason);
                
                // 构建用户友好的错误消息
                var friendlyErrorMessage = BuildFriendlyErrorMessage(errorInfo);
                
                // 将错误信息作为系统消息添加到会话历史中（不影响AI对后续消息的理解）
                session?.History.AddSystemMessage(friendlyErrorMessage);
                
                // 返回错误信息 - 包含用户可读的内容
                var errorObj = new
                {
                    type = "error",
                    content = friendlyErrorMessage,  // 使用用户友好的错误消息
                    errorCode = errorInfo.ErrorCode,
                    httpStatus = errorInfo.HttpStatus,
                    title = errorInfo.Title,
                    reason = errorInfo.Reason,
                    solution = errorInfo.Solution,
                    isCritical = errorInfo.IsCritical
                };
                
                var json = JsonSerializer.Serialize(errorObj);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken: cancellationToken);
                await Response.WriteAsync($"data: [DONE]\n\n", cancellationToken: cancellationToken);
            }
            catch (Exception innerEx)
            {
                // 即使错误处理失败，也确保返回一些内容
                _logger.LogCritical(innerEx, "错误处理也失败了，SessionId: {SessionId}", sessionId);
                try
                {
                    var fallbackError = new
                    {
                        type = "error",
                        content = "系统内部错误，请稍后重试",
                        errorCode = "InternalError",
                        httpStatus = 500,
                        title = "系统错误",
                        reason = "处理请求时发生内部错误",
                        solution = "请稍后重试或联系管理员",
                        isCritical = true
                    };
                    
                    var fallbackJson = JsonSerializer.Serialize(fallbackError);
                    await Response.WriteAsync($"data: {fallbackJson}\n\n", cancellationToken: cancellationToken);
                    await Response.WriteAsync($"data: [DONE]\n\n", cancellationToken: cancellationToken);
                }
                catch
                {
                    // 如果连回退错误都无法发送，至少尝试发送一个简单的错误
                    await Response.WriteAsync("data: {\"type\":\"error\",\"content\":\"系统错误\"}\n\n", cancellationToken: cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// 发送消息并获取完整响应（非流式，便于 Swagger 测试）
    /// </summary>
    [HttpPost("sessions/{sessionId}/chat/complete")]
    public async Task<IActionResult> ChatComplete(string sessionId, [FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var session = _sessionManager.GetSession(UserId, sessionId);
        if (session == null)
        {
            return NotFound(new { message = "会话不存在" });
        }

        var thinkingContent = new List<string>();
        var responseContent = new List<string>();

        try
        {
            await foreach (var response in _kernelService.StreamChatAsync(session, request.Message, cancellationToken))
            {
                if (response.Type == StreamingResponseType.Thinking)
                {
                    thinkingContent.Add(response.Content);
                }
                else
                {
                    responseContent.Add(response.Content);
                }
            }

            return Ok(new
            {
                thinking = thinkingContent.Count > 0 ? string.Join("", thinkingContent) : null,
                content = string.Join("", responseContent)
            });
        }
        catch (Exception ex)
        {
            try
            {
                _logger.LogError(ex, "聊天处理出错，SessionId: {SessionId}", sessionId);
                
                // 使用错误处理器转换异常
                var errorInfo = _errorHandler.HandleException(ex);
                _logger.LogWarning("错误信息: {ErrorCode} - {Title}: {Reason}", 
                    errorInfo.ErrorCode, errorInfo.Title, errorInfo.Reason);
                
                // 构建用户友好的错误消息
                var friendlyErrorMessage = BuildFriendlyErrorMessage(errorInfo);
                
                // 将错误信息作为系统消息添加到会话历史中（不影响AI对后续消息的理解）
                session?.History.AddSystemMessage(friendlyErrorMessage);
                
                return StatusCode(errorInfo.HttpStatus, new 
                { 
                    type = "error",
                    content = friendlyErrorMessage,  // 使用用户友好的错误消息
                    errorCode = errorInfo.ErrorCode,
                    httpStatus = errorInfo.HttpStatus,
                    title = errorInfo.Title,
                    reason = errorInfo.Reason,
                    solution = errorInfo.Solution,
                    isCritical = errorInfo.IsCritical
                });
            }
            catch (Exception innerEx)
            {
                // 即使错误处理失败，也确保返回一些内容
                _logger.LogCritical(innerEx, "错误处理也失败了，SessionId: {SessionId}", sessionId);
                
                return StatusCode(500, new 
                { 
                    type = "error",
                    content = "系统内部错误，请稍后重试",
                    errorCode = "InternalError",
                    httpStatus = 500,
                    title = "系统错误",
                    reason = "处理请求时发生内部错误",
                    solution = "请稍后重试或联系管理员",
                    isCritical = true
                });
            }
        }
    }

    /// <summary>
    /// 获取会话的聊天历史
    /// </summary>
    [HttpGet("sessions/{sessionId}/history")]
    public IActionResult GetHistory(string sessionId)
    {
        var session = _sessionManager.GetSession(UserId, sessionId);
        if (session == null)
        {
            return NotFound(new { message = "会话不存在" });
        }

        var history = session.History.Select(msg => new
        {
            Role = msg.Role.ToString(),
            Content = msg.Content
        });

        return Ok(history);
    }

    /// <summary>
    /// 构建用户友好的错误消息
    /// </summary>
    private static string BuildFriendlyErrorMessage(BailianErrorMessage errorInfo)
    {
        // 根据错误类型构建不同的友好消息
        var errorType = errorInfo.Category switch
        {
            BailianErrorCategory.ParameterError => "参数配置问题",
            BailianErrorCategory.AuthenticationError => "认证失败",
            BailianErrorCategory.PermissionError => "权限不足",
            BailianErrorCategory.NotFoundError => "资源不存在",
            BailianErrorCategory.RateLimitError => "请求频率过高",
            BailianErrorCategory.ServerError => "服务器内部错误",
            BailianErrorCategory.FileError => "文件处理问题",
            BailianErrorCategory.ValidationError => "输入验证失败",
            BailianErrorCategory.QuotaError => "配额不足",
            BailianErrorCategory.NetworkError => "网络连接问题",
            BailianErrorCategory.ContentError => "内容安全检查失败",
            _ => "系统错误"
        };

        // 添加时间戳和上下文信息
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        
        // 构建更详细的错误消息
        var isRetryable = !errorInfo.IsCritical;
        var retryAdvice = isRetryable ? "您可以稍后重试此操作。" : "此错误无法通过重试解决，请按照解决方案进行操作。";
        
        return $""""
抱歉，处理您的请求时遇到了问题：

**错误类型**: {errorType}
**具体原因**: {errorInfo.Reason}
**解决方案**: {errorInfo.Solution}

**错误代码**: {errorInfo.ErrorCode}
**发生时间**: {timestamp}
**状态**: {(isRetryable ? "可重试" : "严重错误")}
**建议**: {retryAdvice}

如果您已按照解决方案操作但问题仍然存在，请联系系统管理员。
"""";
    }
}
