using ClaudeAgentSdk.Internal;

namespace ClaudeAgentSdk;

/// <summary>
/// A factory for creating in-process MCP servers that can be used with Claude Agent SDK.
/// SDK MCP servers run within your application's process, providing better performance
/// and direct access to application state compared to external MCP servers.
/// </summary>
public static class SdkMcpServer
{
    /// <summary>
    /// Create an in-process MCP server configuration with the specified tools.
    /// </summary>
    /// <param name="name">Unique identifier for the server.</param>
    /// <param name="version">Server version string (default: "1.0.0").</param>
    /// <param name="tools">List of tool definitions created with SdkMcpTool.Create().</param>
    /// <returns>McpSdkServerConfig ready to use with ClaudeAgentOptions.McpServers.</returns>
    /// <example>
    /// <code>
    /// var addTool = SdkMcpTool.Create&lt;CalcArgs&gt;(
    ///     "add",
    ///     "Add two numbers",
    ///     async args => SdkMcpToolResult.FromText($"Result: {args.A + args.B}"));
    ///
    /// var server = SdkMcpServer.Create("calculator", "1.0.0", [addTool]);
    ///
    /// var options = new ClaudeAgentOptions
    /// {
    ///     McpServers = new Dictionary&lt;string, McpServerConfig&gt;
    ///     {
    ///         ["calc"] = server
    ///     }
    /// };
    /// </code>
    /// </example>
    public static McpSdkServerConfig Create(
        string name,
        string version = "1.0.0",
        IEnumerable<ISdkMcpToolDefinition>? tools = null)
    {
        var serverInstance = new SdkMcpServerInstance(name, version, tools?.ToList() ?? []);

        return new McpSdkServerConfig
        {
            Name = name,
            Instance = serverInstance
        };
    }

    /// <summary>
    /// Create an in-process MCP server configuration with the specified tools.
    /// </summary>
    /// <param name="name">Unique identifier for the server.</param>
    /// <param name="tools">List of tool definitions.</param>
    /// <returns>McpSdkServerConfig ready to use with ClaudeAgentOptions.McpServers.</returns>
    public static McpSdkServerConfig Create(string name, params ISdkMcpToolDefinition[] tools)
        => Create(name, "1.0.0", tools);
}

/// <summary>
/// Internal implementation of ISdkMcpServer that wraps public tool definitions.
/// </summary>
internal sealed class SdkMcpServerInstance : ISdkMcpServer
{
    private readonly string _name;
    private readonly string _version;
    private readonly Dictionary<string, ISdkMcpToolDefinition> _tools;

    public SdkMcpServerInstance(string name, string version, IReadOnlyList<ISdkMcpToolDefinition> tools)
    {
        _name = name;
        _version = version;
        _tools = tools.ToDictionary(t => t.Name);
    }

    public SdkMcpServerInfo ServerInfo => new()
    {
        Name = _name,
        Version = _version
    };

    public Task<IReadOnlyList<Internal.SdkMcpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = _tools.Values.Select(t => new Internal.SdkMcpTool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.InputSchema
        }).ToList();

        return Task.FromResult<IReadOnlyList<Internal.SdkMcpTool>>(tools);
    }

    public async Task<Internal.SdkMcpToolResult> CallToolAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken ct = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return new Internal.SdkMcpToolResult
            {
                Content = [new Internal.SdkMcpContent { Text = $"Tool '{toolName}' not found" }],
                IsError = true
            };
        }

        try
        {
            var result = await tool.CallAsync(arguments, ct);

            // Convert public SdkMcpToolResult to internal SdkMcpToolResult
            return new Internal.SdkMcpToolResult
            {
                Content = result.Content.Select(c => new Internal.SdkMcpContent
                {
                    Text = c.Text
                }).ToList(),
                IsError = result.IsError
            };
        }
        catch (Exception ex)
        {
            return new Internal.SdkMcpToolResult
            {
                Content = [new Internal.SdkMcpContent { Text = $"Error: {ex.Message}" }],
                IsError = true
            };
        }
    }
}
