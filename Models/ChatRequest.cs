using System.ComponentModel.DataAnnotations;

namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 聊天请求DTO
/// </summary>
public class ChatRequest
{
    [Required]
    public string Message { get; set; } = string.Empty;
}
