using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace SemanticKerne.AiProvider.Unified.Services.Mcp;

/// <summary>
/// Semantic Kernel MCP插件 - 支持动态调用多个MCP服务器的工具
/// 使用 McpSessionContext 获取当前会话的 MCP Session IDs
/// </summary>
public class McpPlugin
{
    private readonly IMcpClientService _mcpClient;
    private readonly ILogger<McpPlugin> _logger;

    public McpPlugin(IMcpClientService mcpClient, ILogger<McpPlugin> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    [KernelFunction("list_mcp_servers")]
    [Description("列出所有已配置的MCP服务器及其状态")]
    public async Task<string> ListServersAsync()
    {
        _logger.LogInformation("列出MCP服务器状态");
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        var statuses = await _mcpClient.GetServerStatusesAsync(mcpSessionIds);
        
        if (statuses.Count == 0)
        {
            return "没有配置任何MCP服务器";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("MCP服务器状态:");
        result.AppendLine();

        foreach (var (name, status) in statuses)
        {
            var statusIcon = status.IsConnected ? "✅" : "❌";
            result.AppendLine($"{statusIcon} **{name}**");
            result.AppendLine($"   - 状态: {(status.IsConnected ? "已连接" : "未连接")}");
            
            if (!string.IsNullOrEmpty(status.Error))
            {
                result.AppendLine($"   - 错误: {status.Error}");
            }
            
            if (status.IsConnected)
            {
                result.AppendLine($"   - 工具数: {status.ToolCount}");
                result.AppendLine($"   - 资源数: {status.ResourceCount}");
            }
            
            result.AppendLine();
        }

        return result.ToString();
    }

    [KernelFunction("list_mcp_tools")]
    [Description("列出MCP服务器提供的所有工具。可选参数serverName指定服务器名称")]
    public async Task<string> ListToolsAsync(
        [Description("服务器名称（可选，不指定则列出所有服务器的工具）")] string? serverName = null)
    {
        _logger.LogInformation("列出MCP工具, 服务器: {Server}", serverName ?? "全部");
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        var tools = await _mcpClient.ListToolsAsync(mcpSessionIds, serverName);

        var toolList = tools.ToList();
        if (toolList.Count == 0)
        {
            return serverName == null 
                ? "没有可用的MCP工具" 
                : $"服务器 '{serverName}' 没有可用的工具";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("可用的MCP工具:");
        result.AppendLine();

        var groupedTools = toolList.GroupBy(t => t.ServerName);
        foreach (var group in groupedTools)
        {
            result.AppendLine($"### 服务器: {group.Key}");
            foreach (var tool in group)
            {
                result.AppendLine($"- **{tool.Name}**");
                if (!string.IsNullOrEmpty(tool.Description))
                {
                    result.AppendLine($"  {tool.Description}");
                }
                if (tool.InputSchema != null && tool.InputSchema.Count > 0)
                {
                    result.AppendLine("  参数:");
                    foreach (var prop in tool.InputSchema)
                    {
                        var required = prop.Value.Required ? " (必填)" : "";
                        var type = prop.Value.Type ?? "any";
                        var desc = !string.IsNullOrEmpty(prop.Value.Description) 
                            ? $" - {prop.Value.Description}" 
                            : "";
                        result.AppendLine($"    - `{prop.Key}` ({type}){required}{desc}");
                    }
                }
                result.AppendLine();
            }
        }

        return result.ToString();
    }

    [KernelFunction("call_mcp_tool")]
    [Description("调用指定MCP服务器的工具")]
    public async Task<string> CallToolAsync(
        [Description("服务器名称")] string serverName,
        [Description("工具名称")] string toolName,
        [Description("工具参数，JSON格式")] string? argumentsJson = null)
    {
        _logger.LogInformation("调用MCP工具: {Server}.{Tool}", serverName, toolName);
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        
        Dictionary<string, object?> arguments = new();
        
        if (!string.IsNullOrEmpty(argumentsJson))
        {
            try
            {
                var parsedArgs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
                if (parsedArgs != null)
                {
                    arguments = parsedArgs;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析参数JSON失败，将使用空参数");
            }
        }

        var (result, updatedSessionIds) = await _mcpClient.CallToolAsync(mcpSessionIds, serverName, toolName, arguments);

        // 更新会话中的 MCP Session IDs
        if (McpSessionContext.CurrentSession != null)
        {
            foreach (var kvp in updatedSessionIds)
            {
                McpSessionContext.CurrentSession.McpSessionIds[kvp.Key] = kvp.Value;
            }
        }

        if (result.IsError)
        {
            return $"调用工具失败: {result.Content}";
        }

        return result.Content;
    }

    [KernelFunction("list_mcp_resources")]
    [Description("列出MCP服务器提供的所有资源。可选参数serverName指定服务器名称")]
    public async Task<string> ListResourcesAsync(
        [Description("服务器名称（可选，不指定则列出所有服务器的资源）")] string? serverName = null)
    {
        _logger.LogInformation("列出MCP资源, 服务器: {Server}", serverName ?? "全部");
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        var resources = await _mcpClient.ListResourcesAsync(mcpSessionIds, serverName);

        var resourceList = resources.ToList();
        if (resourceList.Count == 0)
        {
            return serverName == null 
                ? "没有可用的MCP资源" 
                : $"服务器 '{serverName}' 没有可用的资源";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("可用的MCP资源:");
        result.AppendLine();

        var groupedResources = resourceList.GroupBy(r => r.ServerName);
        foreach (var group in groupedResources)
        {
            result.AppendLine($"### 服务器: {group.Key}");
            foreach (var resource in group)
            {
                result.AppendLine($"- **{resource.Name ?? resource.Uri}**");
                result.AppendLine($"  URI: `{resource.Uri}`");
                if (!string.IsNullOrEmpty(resource.Description))
                {
                    result.AppendLine($"  描述: {resource.Description}");
                }
                if (!string.IsNullOrEmpty(resource.MimeType))
                {
                    result.AppendLine($"  类型: {resource.MimeType}");
                }
                result.AppendLine();
            }
        }

        return result.ToString();
    }

    [KernelFunction("read_mcp_resource")]
    [Description("读取指定MCP服务器的资源内容")]
    public async Task<string> ReadResourceAsync(
        [Description("服务器名称")] string serverName,
        [Description("资源URI")] string resourceUri)
    {
        _logger.LogInformation("读取MCP资源: {Server}/{Uri}", serverName, resourceUri);
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        var result = await _mcpClient.ReadResourceAsync(mcpSessionIds, serverName, resourceUri);
        
        return result.Content;
    }

    [KernelFunction("list_mcp_prompts")]
    [Description("列出MCP服务器提供的所有提示模板。可选参数serverName指定服务器名称")]
    public async Task<string> ListPromptsAsync(
        [Description("服务器名称（可选，不指定则列出所有服务器的提示模板）")] string? serverName = null)
    {
        _logger.LogInformation("列出MCP提示模板, 服务器: {Server}", serverName ?? "全部");
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        var prompts = await _mcpClient.ListPromptsAsync(mcpSessionIds, serverName);

        var promptList = prompts.ToList();
        if (promptList.Count == 0)
        {
            return serverName == null 
                ? "没有可用的MCP提示模板" 
                : $"服务器 '{serverName}' 没有可用的提示模板";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("可用的MCP提示模板:");
        result.AppendLine();

        var groupedPrompts = promptList.GroupBy(p => p.ServerName);
        foreach (var group in groupedPrompts)
        {
            result.AppendLine($"### 服务器: {group.Key}");
            foreach (var prompt in group)
            {
                result.AppendLine($"- **{prompt.Name}**");
                if (!string.IsNullOrEmpty(prompt.Description))
                {
                    result.AppendLine($"  描述: {prompt.Description}");
                }
                if (prompt.Arguments != null && prompt.Arguments.Count > 0)
                {
                    result.AppendLine("  参数:");
                    foreach (var arg in prompt.Arguments)
                    {
                        var required = arg.Required ? " (必填)" : "";
                        result.AppendLine($"    - `{arg.Name}`{required}");
                        if (!string.IsNullOrEmpty(arg.Description))
                        {
                            result.AppendLine($"      {arg.Description}");
                        }
                    }
                }
                result.AppendLine();
            }
        }

        return result.ToString();
    }

    [KernelFunction("get_mcp_prompt")]
    [Description("获取指定MCP服务器的提示模板内容")]
    public async Task<string> GetPromptAsync(
        [Description("服务器名称")] string serverName,
        [Description("提示模板名称")] string promptName,
        [Description("模板参数，JSON格式")] string? argumentsJson = null)
    {
        _logger.LogInformation("获取MCP提示模板: {Server}.{Prompt}", serverName, promptName);
        
        var mcpSessionIds = McpSessionContext.McpSessionIds;
        
        Dictionary<string, object?> arguments = new();
        
        if (!string.IsNullOrEmpty(argumentsJson))
        {
            try
            {
                var parsedArgs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
                if (parsedArgs != null)
                {
                    arguments = parsedArgs;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "解析参数JSON失败，将使用空参数");
            }
        }

        var result = await _mcpClient.GetPromptAsync(mcpSessionIds, serverName, promptName, arguments);

        if (result.Messages.Count == 0)
        {
            return "提示模板为空";
        }

        var output = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(result.Description))
        {
            output.AppendLine($"描述: {result.Description}");
            output.AppendLine();
        }

        output.AppendLine("消息:");
        foreach (var message in result.Messages)
        {
            output.AppendLine($"[{message.Role}]: {message.Content}");
        }

        return output.ToString();
    }
}
