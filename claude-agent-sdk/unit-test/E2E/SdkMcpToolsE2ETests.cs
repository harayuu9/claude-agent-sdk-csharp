using System.Text.Json.Serialization;
using ClaudeAgentSdk;

namespace UnitTest.E2E;

/// <summary>
/// End-to-end tests for SDK MCP (inline) tools with real Claude API calls.
/// These tests verify that SDK-created MCP tools work correctly through the full stack.
/// Equivalent to Python e2e-tests/test_sdk_mcp_tools.py
/// </summary>
[Trait("Category", "E2E")]
public class SdkMcpToolsE2ETests : E2ETestBase
{
    #region Argument Types

    private record EchoArgs
    {
        [JsonPropertyName("text")]
        public required string Text { get; init; }
    }

    private record GreetArgs
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }

    #endregion

    /// <summary>
    /// Test that SDK MCP tools can be called and executed with allowed_tools.
    /// Equivalent to Python test_sdk_mcp_tool_execution.
    /// </summary>
    [Fact]
    public async Task SdkMcpToolExecution()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var executions = new List<string>();

        var echoTool = SdkMcpTool.Create<EchoArgs>(
            "echo",
            "Echo back the input text",
            async args =>
            {
                executions.Add("echo");
                return SdkMcpToolResult.FromText($"Echo: {args.Text}");
            });

        var serverConfig = SdkMcpServer.Create(
            "test",
            "1.0.0",
            [echoTool]);

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["test"] = serverConfig
            },
            AllowedTools = ["mcp__test__echo"]
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync("Call the mcp__test__echo tool with any text");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Check if the actual C# function was called
        Assert.Contains("echo", executions);
    }

    /// <summary>
    /// Test that disallowed_tools prevents SDK MCP tool execution.
    /// Equivalent to Python test_sdk_mcp_permission_enforcement.
    /// </summary>
    [Fact]
    public async Task SdkMcpPermissionEnforcement()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var executions = new List<string>();

        var echoTool = SdkMcpTool.Create<EchoArgs>(
            "echo",
            "Echo back the input text",
            async args =>
            {
                executions.Add("echo");
                return SdkMcpToolResult.FromText($"Echo: {args.Text}");
            });

        var greetTool = SdkMcpTool.Create<GreetArgs>(
            "greet",
            "Greet a person by name",
            async args =>
            {
                executions.Add("greet");
                return SdkMcpToolResult.FromText($"Hello, {args.Name}!");
            });

        var serverConfig = SdkMcpServer.Create(
            "test",
            "1.0.0",
            [echoTool, greetTool]);

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["test"] = serverConfig
            },
            DisallowedTools = ["mcp__test__echo"],  // Block echo tool
            AllowedTools = ["mcp__test__greet"]     // But allow greet
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(
            "Use the echo tool to echo 'test' and use greet tool to greet 'Alice'");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Check actual function executions
        Assert.DoesNotContain("echo", executions);
        Assert.Contains("greet", executions);
    }

    /// <summary>
    /// Test that multiple SDK MCP tools can be called in sequence.
    /// Equivalent to Python test_sdk_mcp_multiple_tools.
    /// </summary>
    [Fact]
    public async Task SdkMcpMultipleTools()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var executions = new List<string>();

        var echoTool = SdkMcpTool.Create<EchoArgs>(
            "echo",
            "Echo back the input text",
            async args =>
            {
                executions.Add("echo");
                return SdkMcpToolResult.FromText($"Echo: {args.Text}");
            });

        var greetTool = SdkMcpTool.Create<GreetArgs>(
            "greet",
            "Greet a person by name",
            async args =>
            {
                executions.Add("greet");
                return SdkMcpToolResult.FromText($"Hello, {args.Name}!");
            });

        var serverConfig = SdkMcpServer.Create(
            "multi",
            "1.0.0",
            [echoTool, greetTool]);

        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["multi"] = serverConfig
            },
            AllowedTools = ["mcp__multi__echo", "mcp__multi__greet"]
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(
            "Call mcp__multi__echo with text='test' and mcp__multi__greet with name='Bob'");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Both tools should have been executed
        Assert.Contains("echo", executions);
        Assert.Contains("greet", executions);
    }

    /// <summary>
    /// Test SDK MCP tool behavior without explicit allowed_tools.
    /// Equivalent to Python test_sdk_mcp_without_permissions.
    /// </summary>
    [Fact]
    public async Task SdkMcpWithoutPermissions()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var executions = new List<string>();

        var echoTool = SdkMcpTool.Create<EchoArgs>(
            "echo",
            "Echo back the input text",
            async args =>
            {
                executions.Add("echo");
                return SdkMcpToolResult.FromText($"Echo: {args.Text}");
            });

        var serverConfig = SdkMcpServer.Create(
            "noperm",
            "1.0.0",
            [echoTool]);

        // No allowed_tools specified
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["noperm"] = serverConfig
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync("Call the mcp__noperm__echo tool");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // SDK MCP tool should NOT have been executed without allowed_tools
        Assert.DoesNotContain("echo", executions);
    }
}
