namespace SemanticKerne.AiProvider.Unified.Models;

/// <summary>
/// 登录响应DTO
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
}
