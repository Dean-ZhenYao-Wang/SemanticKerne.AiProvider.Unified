namespace SemanticKerne.AiProvider.Unified.Services.Mcp;

/// <summary>
/// MCP客户端服务接口
/// </summary>
public interface IMcpClientService
{
    /// <summary>
    /// 获取所有已配置的MCP服务器状态
    /// </summary>
    Task<Dictionary<string, McpServerStatus>> GetServerStatusesAsync(Dictionary<string, string>? mcpSessionIds = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出指定服务器的所有工具
    /// </summary>
    Task<IEnumerable<McpToolInfo>> ListToolsAsync(Dictionary<string, string>? mcpSessionIds = null, string? serverName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 调用MCP工具
    /// </summary>
    Task<(McpToolResult Result, Dictionary<string, string> UpdatedSessionIds)> CallToolAsync(
        Dictionary<string, string>? mcpSessionIds, 
        string serverName, 
        string toolName, 
        Dictionary<string, object?> arguments, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出指定服务器的所有资源
    /// </summary>
    Task<IEnumerable<McpResourceInfo>> ListResourcesAsync(Dictionary<string, string>? mcpSessionIds = null, string? serverName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取MCP资源
    /// </summary>
    Task<McpResourceContent> ReadResourceAsync(Dictionary<string, string>? mcpSessionIds, string serverName, string resourceUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// 列出指定服务器的所有提示模板
    /// </summary>
    Task<IEnumerable<McpPromptInfo>> ListPromptsAsync(Dictionary<string, string>? mcpSessionIds = null, string? serverName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取MCP提示模板
    /// </summary>
    Task<McpPromptResult> GetPromptAsync(Dictionary<string, string>? mcpSessionIds, string serverName, string promptName, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP服务器状态
/// </summary>
public class McpServerStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string? Error { get; set; }
    public int ToolCount { get; set; }
    public int ResourceCount { get; set; }
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// MCP工具信息
/// </summary>
public class McpToolInfo
{
    public string ServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, McpPropertySchema>? InputSchema { get; set; }
}

/// <summary>
/// MCP属性模式
/// </summary>
public class McpPropertySchema
{
    public string? Type { get; set; }
    public string? Description { get; set; }
    public bool Required { get; set; }
    public object? Default { get; set; }
}

/// <summary>
/// MCP工具调用结果
/// </summary>
public class McpToolResult
{
    public bool IsError { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ErrorType { get; set; }
}

/// <summary>
/// MCP资源信息
/// </summary>
public class McpResourceInfo
{
    public string ServerName { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? MimeType { get; set; }
}

/// <summary>
/// MCP资源内容
/// </summary>
public class McpResourceContent
{
    public string Uri { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// MCP提示模板信息
/// </summary>
public class McpPromptInfo
{
    public string ServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<McpPromptArgument>? Arguments { get; set; }
}

/// <summary>
/// MCP提示模板参数
/// </summary>
public class McpPromptArgument
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Required { get; set; }
}

/// <summary>
/// MCP提示模板结果
/// </summary>
public class McpPromptResult
{
    public string? Description { get; set; }
    public List<McpPromptMessage> Messages { get; set; } = [];
}

/// <summary>
/// MCP提示模板消息
/// </summary>
public class McpPromptMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
