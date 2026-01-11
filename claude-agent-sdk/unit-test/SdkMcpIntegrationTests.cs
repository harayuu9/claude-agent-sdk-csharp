/// <summary>
/// Integration tests for SDK MCP server support.
///
/// This test file verifies that SDK MCP servers work correctly through the full stack,
/// matching the Python SDK tests/test_sdk_mcp_integration.py pattern.
/// </summary>

using System.Text.Json.Serialization;
using ClaudeAgentSdk;
using InternalTypes = ClaudeAgentSdk.Internal;

namespace UnitTest;

public class SdkMcpIntegrationTests
{
    #region Argument Types

    // Note: Property names must match the JSON keys (snake_case) used in tool arguments.
    // The SDK generates schemas with snake_case property names.
    private record GreetUserArgs
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }
    }

    private record AddNumbersArgs
    {
        [JsonPropertyName("a")]
        public required double A { get; init; }

        [JsonPropertyName("b")]
        public required double B { get; init; }
    }

    private record EchoArgs
    {
        [JsonPropertyName("input")]
        public required string Input { get; init; }
    }

    private record EmptyArgs;

    private record ChartArgs
    {
        [JsonPropertyName("title")]
        public required string Title { get; init; }
    }

    #endregion

    /// <summary>
    /// Test that SDK MCP server handlers are properly registered.
    /// Equivalent to Python test_sdk_mcp_server_handlers.
    /// </summary>
    [Fact]
    public async Task SdkMcpServerHandlers()
    {
        // Track tool executions
        var toolExecutions = new List<(string Name, object Args)>();

        // Create SDK MCP server with multiple tools
        var greetUserTool = SdkMcpTool.Create<GreetUserArgs>(
            "greet_user",
            "Greets a user by name",
            async args =>
            {
                toolExecutions.Add(("greet_user", args));
                return SdkMcpToolResult.FromText($"Hello, {args.Name}!");
            });

        var addNumbersTool = SdkMcpTool.Create<AddNumbersArgs>(
            "add_numbers",
            "Adds two numbers",
            async args =>
            {
                toolExecutions.Add(("add_numbers", args));
                var result = args.A + args.B;
                return SdkMcpToolResult.FromText($"The sum is {result}");
            });

        var serverConfig = SdkMcpServer.Create(
            "test-sdk-server",
            "1.0.0",
            [greetUserTool, addNumbersTool]);

        // Verify server configuration
        Assert.Equal(McpServerType.Sdk, serverConfig.Type);
        Assert.Equal("test-sdk-server", serverConfig.Name);
        Assert.NotNull(serverConfig.Instance);

        // Get the server instance (cast to ISdkMcpServer)
        var server = (InternalTypes.ISdkMcpServer)serverConfig.Instance;

        // Test list_tools - verify tools are registered
        var tools = await server.ListToolsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, tools.Count);

        var toolNames = tools.Select(t => t.Name).ToList();
        Assert.Contains("greet_user", toolNames);
        Assert.Contains("add_numbers", toolNames);

        // Test call_tool - call greet_user
        var greetResult = await server.CallToolAsync(
            "greet_user",
            new Dictionary<string, object?> { ["name"] = "Alice" },
            TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Alice!", greetResult.Content[0].Text);
        Assert.Single(toolExecutions);
        Assert.Equal("greet_user", toolExecutions[0].Name);
        var greetArgs = (GreetUserArgs)toolExecutions[0].Args;
        Assert.Equal("Alice", greetArgs.Name);

        // Test call_tool - call add_numbers
        var addResult = await server.CallToolAsync(
            "add_numbers",
            new Dictionary<string, object?> { ["a"] = 5.0, ["b"] = 3.0 },
            TestContext.Current.CancellationToken);

        Assert.Contains("8", addResult.Content[0].Text);
        Assert.Equal(2, toolExecutions.Count);
        Assert.Equal("add_numbers", toolExecutions[1].Name);
        var addArgs = (AddNumbersArgs)toolExecutions[1].Args;
        Assert.Equal(5.0, addArgs.A);
        Assert.Equal(3.0, addArgs.B);
    }

    /// <summary>
    /// Test that tools can be created with proper schemas.
    /// Equivalent to Python test_tool_creation.
    /// </summary>
    [Fact]
    public async Task ToolCreation()
    {
        var echoTool = SdkMcpTool.Create<EchoArgs>(
            "echo",
            "Echo input",
            async args => new SdkMcpToolResult
            {
                Content = [SdkMcpContent.CreateText(args.Input)]
            });

        // Verify tool was created
        Assert.Equal("echo", echoTool.Name);
        Assert.Equal("Echo input", echoTool.Description);
        Assert.NotNull(echoTool.Handler);

        // Test the handler works
        var result = await echoTool.Handler(new EchoArgs { Input = "test" }, TestContext.Current.CancellationToken);
        Assert.Equal("test", result.Content[0].Text);
    }

    /// <summary>
    /// Test that tool errors are properly handled.
    /// Equivalent to Python test_error_handling.
    /// </summary>
    [Fact]
    public async Task ErrorHandling()
    {
        var failTool = SdkMcpTool.Create<EmptyArgs>(
            "fail",
            "Always fails",
            async args => throw new InvalidOperationException("Expected error"));

        // Verify the tool raises an error when called directly
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await failTool.Handler(new EmptyArgs(), TestContext.Current.CancellationToken));

        // Test error handling through the server
        var serverConfig = SdkMcpServer.Create("error-test", "1.0.0", [failTool]);

        var server = (InternalTypes.ISdkMcpServer)serverConfig.Instance;

        // The server should return an error result, not raise
        var result = await server.CallToolAsync("fail", new Dictionary<string, object?>(), TestContext.Current.CancellationToken);

        // MCP SDK catches exceptions and returns error results
        Assert.True(result.IsError);
        Assert.Contains("Expected error", result.Content[0].Text);
    }

    /// <summary>
    /// Test that SDK and external MCP servers can work together.
    /// Equivalent to Python test_mixed_servers.
    /// </summary>
    [Fact]
    public void MixedServers()
    {
        // Create an SDK server
        var sdkTool = SdkMcpTool.Create<EmptyArgs>(
            "sdk_tool",
            "SDK tool",
            async args => SdkMcpToolResult.FromText("from SDK"));

        var sdkServer = SdkMcpServer.Create("sdk-server", "1.0.0", [sdkTool]);

        // Create configuration with both SDK and external servers
        var externalServer = new McpStdioServerConfig
        {
            Command = "echo",
            Args = ["test"]
        };

        var mcpServers = new Dictionary<string, McpServerConfig>
        {
            ["sdk"] = sdkServer,
            ["external"] = externalServer
        };

        var options = new ClaudeAgentOptions
        {
            McpServers = mcpServers
        };

        // Verify both server types are in the configuration
        // McpServers is typed as object, so we need to cast it back
        var servers = (Dictionary<string, McpServerConfig>)options.McpServers;
        Assert.True(servers.ContainsKey("sdk"));
        Assert.True(servers.ContainsKey("external"));
        Assert.Equal(McpServerType.Sdk, servers["sdk"].Type);
        Assert.Equal(McpServerType.Stdio, servers["external"].Type);
    }

    /// <summary>
    /// Test that SDK MCP servers are created correctly.
    /// Equivalent to Python test_server_creation.
    /// </summary>
    [Fact]
    public async Task ServerCreation()
    {
        var serverConfig = SdkMcpServer.Create("test-server", "2.0.0", []);

        // Verify server configuration
        Assert.Equal(McpServerType.Sdk, serverConfig.Type);
        Assert.Equal("test-server", serverConfig.Name);
        Assert.NotNull(serverConfig.Instance);

        // Verify the server instance has the right attributes
        var server = (InternalTypes.ISdkMcpServer)serverConfig.Instance;
        Assert.Equal("test-server", server.ServerInfo.Name);
        Assert.Equal("2.0.0", server.ServerInfo.Version);

        // With no tools, ListToolsAsync should return empty list
        var tools = await server.ListToolsAsync(TestContext.Current.CancellationToken);
        Assert.Empty(tools);
    }

    /// <summary>
    /// Test that tools can return multiple content items.
    /// Equivalent to Python test_image_content_support.
    ///
    /// Note: The internal SdkMcpContent type only supports text content.
    /// This test verifies that tools can return multiple content items through the handler,
    /// and that the text content is correctly passed through the server.
    /// </summary>
    [Fact]
    public async Task ImageContentSupport()
    {
        // Create sample base64 image data (a simple 1x1 pixel PNG representation)
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, 0x00, 0x00, 0x00,
            0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0B, 0x13, 0x00, 0x00, 0x0B,
            0x13, 0x01, 0x00, 0x9A, 0x9C, 0x18, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44,
            0x41, 0x54, 0x78, 0x9C, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04,
            0x00, 0x01, 0x5D, 0x55, 0x21, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
            0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        };
        var pngData = Convert.ToBase64String(pngBytes);

        // Track tool executions
        var toolExecutions = new List<(string Name, object Args)>();

        // Create a tool that returns both text and image content
        var generateChartTool = SdkMcpTool.Create<ChartArgs>(
            "generate_chart",
            "Generates a chart and returns it as an image",
            async args =>
            {
                toolExecutions.Add(("generate_chart", args));
                return new SdkMcpToolResult
                {
                    Content =
                    [
                        SdkMcpContent.CreateText($"Generated chart: {args.Title}"),
                        SdkMcpContent.CreateImage(pngData, "image/png")
                    ]
                };
            });

        var serverConfig = SdkMcpServer.Create(
            "image-test-server",
            "1.0.0",
            [generateChartTool]);

        var server = (InternalTypes.ISdkMcpServer)serverConfig.Instance;

        // Call the chart generation tool
        var result = await server.CallToolAsync(
            "generate_chart",
            new Dictionary<string, object?> { ["title"] = "Sales Report" },
            TestContext.Current.CancellationToken);

        // Note: Internal SdkMcpContent only has Text property, so we can only verify:
        // 1. The tool was executed
        // 2. Content items were created (even if image data is lost in conversion)
        Assert.Equal(2, result.Content.Count);

        // Check text content
        Assert.Equal("Generated chart: Sales Report", result.Content[0].Text);

        // The second content item exists but its text will be null (image data lost in internal conversion)
        Assert.NotNull(result.Content[1]);

        // Verify the tool was executed correctly
        Assert.Single(toolExecutions);
        Assert.Equal("generate_chart", toolExecutions[0].Name);
        var chartArgs = (ChartArgs)toolExecutions[0].Args;
        Assert.Equal("Sales Report", chartArgs.Title);

        // Also verify the handler directly returns correct image content
        var directResult = await generateChartTool.Handler(new ChartArgs { Title = "Direct Test" }, TestContext.Current.CancellationToken);
        Assert.Equal(2, directResult.Content.Count);
        Assert.Equal("text", directResult.Content[0].Type);
        Assert.Equal("Generated chart: Direct Test", directResult.Content[0].Text);
        Assert.Equal("image", directResult.Content[1].Type);
        Assert.Equal(pngData, directResult.Content[1].Data);
        Assert.Equal("image/png", directResult.Content[1].MimeType);
    }
}
