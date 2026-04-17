namespace SemanticKerne.AiProvider.Unified.Services.Mcp;

/// <summary>
/// MCP服务配置选项
/// </summary>
public class McpOptions
{
    /// <summary>
    /// 是否启用MCP功能
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// MCP服务器列表
    /// </summary>
    public List<McpServerConfig> Servers { get; set; } = [];
}

/// <summary>
/// 单个MCP服务器配置
/// </summary>
public class McpServerConfig
{
    /// <summary>
    /// 服务器名称（用于标识）
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 是否启用此服务器
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 服务器描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 传输类型（stdio, http, sse）
    /// </summary>
    public string Transport { get; set; } = "http";

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    #region HTTP 传输配置

    /// <summary>
    /// MCP服务端点URL（HTTP传输使用）
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 自定义请求头（HTTP传输使用）
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    #endregion

    #region stdio 传输配置

    /// <summary>
    /// 可执行命令（stdio传输使用）
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 命令行参数（stdio传输使用）
    /// </summary>
    public List<string>? Args { get; set; }

    /// <summary>
    /// 环境变量（stdio传输使用）
    /// </summary>
    public Dictionary<string, string>? Env { get; set; }

    /// <summary>
    /// 工作目录（stdio传输使用）
    /// </summary>
    public string? WorkingDirectory { get; set; }

    #endregion
}
