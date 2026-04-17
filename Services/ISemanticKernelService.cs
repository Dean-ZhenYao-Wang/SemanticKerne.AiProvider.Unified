using Microsoft.SemanticKernel;
using SemanticKerne.AiProvider.Unified.Models;

namespace SemanticKerne.AiProvider.Unified.Services;

/// <summary>
/// Semantic Kernel 服务接口
/// </summary>
public interface ISemanticKernelService
{
    /// <summary>
    /// 创建新的 Kernel 实例
    /// </summary>
    Kernel CreateKernel();

    /// <summary>
    /// 流式聊天响应（包含思考过程）
    /// </summary>
    IAsyncEnumerable<StreamingResponse> StreamChatAsync(ChatSession session, string userInput, CancellationToken cancellationToken = default);
}
