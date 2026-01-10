using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeAgentSdk.Internal.Transport;

namespace ClaudeAgentSdk.Internal;

/// <summary>
/// Internal client implementation for Claude SDK.
/// Handles the query processing pipeline from prompt to messages.
/// </summary>
internal class InternalClient
{
    /// <summary>
    /// Process a query through transport and Query.
    /// </summary>
    /// <param name="prompt">The prompt string or IAsyncEnumerable for streaming.</param>
    /// <param name="options">Claude agent options.</param>
    /// <param name="transport">Optional custom transport (internal use).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of parsed messages.</returns>
    public async IAsyncEnumerable<Message> ProcessQueryAsync(
        object prompt,
        ClaudeAgentOptions options,
        Transport.Transport? transport = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. Validate and configure permission settings (matching TypeScript SDK logic)
        var configuredOptions = options;
        var isStreaming = prompt is not string;

        if (options.CanUseTool != null)
        {
            // canUseTool callback requires streaming mode (IAsyncEnumerable prompt)
            if (!isStreaming)
            {
                throw new ArgumentException(
                    "can_use_tool callback requires streaming mode. " +
                    "Please provide prompt as an IAsyncEnumerable instead of a string.",
                    nameof(options));
            }

            // canUseTool and permission_prompt_tool_name are mutually exclusive
            if (!string.IsNullOrEmpty(options.PermissionPromptToolName))
            {
                throw new ArgumentException(
                    "can_use_tool callback cannot be used with permission_prompt_tool_name. " +
                    "Please use one or the other.",
                    nameof(options));
            }

            // Automatically set permission_prompt_tool_name to "stdio" for control protocol
            configuredOptions = options with { PermissionPromptToolName = "stdio" };
        }

        // 2. Use provided transport or create subprocess transport
        var chosenTransport = transport ?? new SubprocessCliTransport(prompt, configuredOptions);

        // 3. Connect transport
        await chosenTransport.ConnectAsync(ct);

        // 4. Extract SDK MCP servers from configured options
        var sdkMcpServers = ExtractSdkMcpServers(configuredOptions);

        // 5. Create Query to handle control protocol
        var query = new Query(
            chosenTransport,
            isStreaming,
            configuredOptions.CanUseTool,
            configuredOptions.Hooks,
            sdkMcpServers);

        try
        {
            // Start reading messages
            query.Start();

            // Initialize if streaming
            if (isStreaming)
            {
                await query.InitializeAsync(ct);
            }

            // 6. Stream input if it's an IAsyncEnumerable (streaming mode)
            if (isStreaming && prompt is IAsyncEnumerable<Dictionary<string, object?>> inputStream)
            {
                // Start streaming in background (fire-and-forget)
                _ = query.StreamInputAsync(inputStream, ct);
            }
            // For string prompts, the prompt is already passed via CLI args

            // 7. Yield parsed messages
            await foreach (var data in query.ReceiveMessagesAsync(ct))
            {
                // Convert Dictionary to JsonElement for MessageParser
                var jsonElement = DictToJsonElement(data);
                yield return MessageParser.ParseMessage(jsonElement);
            }
        }
        finally
        {
            await query.CloseAsync();
        }
    }

    /// <summary>
    /// Convert Dictionary to JsonElement.
    /// </summary>
    private static JsonElement DictToJsonElement(Dictionary<string, object?> data)
    {
        var json = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Extract SDK MCP servers from options.
    /// </summary>
    private static Dictionary<string, ISdkMcpServer>? ExtractSdkMcpServers(ClaudeAgentOptions options)
    {
        if (options.McpServers is not Dictionary<string, McpServerConfig> servers)
        {
            return null;
        }

        var result = new Dictionary<string, ISdkMcpServer>();
        foreach (var (name, config) in servers)
        {
            if (config is McpSdkServerConfig sdkConfig && sdkConfig.Instance is ISdkMcpServer server)
            {
                result[name] = server;
            }
        }

        return result.Count > 0 ? result : null;
    }
}
