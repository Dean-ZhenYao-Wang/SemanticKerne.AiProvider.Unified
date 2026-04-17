using System.ComponentModel.DataAnnotations;

namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 登录请求DTO
/// </summary>
public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
