using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeAgentSdk.Internal;
using ClaudeAgentSdk.Internal.Transport;

using InternalQuery = ClaudeAgentSdk.Internal.Query;

namespace ClaudeAgentSdk;

/// <summary>
/// Persistent connection client for Claude Code with bidirectional communication.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a stateful, connection-oriented API for interactive conversations
/// with Claude Code. Unlike the static <see cref="Query"/> class, ClaudeSDKClient maintains
/// a persistent connection that supports multi-turn conversations, interrupts, and dynamic
/// configuration changes.
/// </para>
///
/// <para><b>Key characteristics:</b></para>
/// <list type="bullet">
///   <item><b>Stateful:</b> Maintains connection state across multiple queries</item>
///   <item><b>Bidirectional:</b> Send messages and receive responses interactively</item>
///   <item><b>Interruptible:</b> Can interrupt ongoing operations</item>
///   <item><b>Configurable:</b> Change model and permission mode mid-conversation</item>
/// </list>
///
/// <para><b>When to use ClaudeSDKClient:</b></para>
/// <list type="bullet">
///   <item>Interactive conversations with follow-ups</item>
///   <item>Chat applications or REPL-like interfaces</item>
///   <item>When you need to send messages based on responses</item>
///   <item>When you need interrupt capabilities</item>
///   <item>Long-running sessions with state</item>
/// </list>
///
/// <para><b>When to use Query instead:</b></para>
/// <list type="bullet">
///   <item>Simple one-off questions</item>
///   <item>Batch processing of independent prompts</item>
///   <item>Automated scripts and CI/CD pipelines</item>
///   <item>When you know all inputs upfront</item>
/// </list>
/// </remarks>
/// <example>
/// Simple conversation:
/// <code>
/// await using var client = new ClaudeSDKClient(new ClaudeAgentOptions
/// {
///     PermissionMode = PermissionMode.BypassPermissions
/// });
///
/// await client.ConnectAsync("What is the capital of France?");
///
/// await foreach (var message in client.ReceiveResponseAsync())
/// {
///     Console.WriteLine(message);
/// }
/// </code>
/// </example>
/// <example>
/// Multi-turn conversation:
/// <code>
/// await using var client = new ClaudeSDKClient();
/// await client.ConnectAsync("Hello!");
///
/// // First response
/// await foreach (var msg in client.ReceiveResponseAsync())
/// {
///     Console.WriteLine(msg);
/// }
///
/// // Follow-up query
/// await client.QueryAsync("Can you explain that further?");
/// await foreach (var msg in client.ReceiveResponseAsync())
/// {
///     Console.WriteLine(msg);
/// }
/// </code>
/// </example>
/// <example>
/// Using control methods:
/// <code>
/// await client.InterruptAsync();
/// await client.SetModelAsync("claude-sonnet-4-20250514");
/// await client.SetPermissionModeAsync(PermissionMode.AcceptEdits);
/// </code>
/// </example>
public sealed class ClaudeSDKClient : IAsyncDisposable
{
    private readonly ClaudeAgentOptions _options;
    private readonly Transport? _customTransport;

    // Connection state
    private Transport? _transport;
    private InternalQuery? _query;
    private bool _connected;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether the client is currently connected.
    /// </summary>
    public bool IsConnected => _connected && !_disposed && _query?.IsClosed != true;

    /// <summary>
    /// Gets the server initialization information.
    /// Available after successful connection via <see cref="ConnectAsync"/>.
    /// </summary>
    public Dictionary<string, object?>? ServerInfo => _query?.InitializationResult;

    /// <summary>
    /// Creates a new ClaudeSDKClient instance.
    /// </summary>
    /// <param name="options">Configuration options. If null, defaults are used.</param>
    /// <param name="transport">Optional custom transport implementation for testing or custom I/O.</param>
    public ClaudeSDKClient(ClaudeAgentOptions? options = null, Transport? transport = null)
    {
        _options = options ?? new ClaudeAgentOptions();
        _customTransport = transport;
    }

    #region Connection Management

    /// <summary>
    /// Establish connection to Claude Code with an optional initial prompt.
    /// </summary>
    /// <param name="prompt">
    /// Optional initial prompt to send. Can be:
    /// <list type="bullet">
    ///   <item><c>null</c>: Connect without sending a prompt</item>
    ///   <item><c>string</c>: Simple text prompt</item>
    ///   <item><c>IAsyncEnumerable&lt;Dictionary&lt;string, object?&gt;&gt;</c>: Streaming messages</item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ObjectDisposedException">If the client has been disposed.</exception>
    /// <exception cref="InvalidOperationException">If already connected.</exception>
    public async Task ConnectAsync(object? prompt = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (_connected)
        {
            throw new InvalidOperationException("Already connected. Call DisconnectAsync() first.");
        }

        // Set the entrypoint environment variable
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-csharp-client");

        // Validate and configure options
        var configuredOptions = ValidateAndConfigureOptions(_options, prompt);

        // Create transport
        // When prompt is null, use empty stream for streaming mode
        var effectivePrompt = prompt ?? EmptyInputStream();
        _transport = _customTransport ?? new SubprocessCliTransport(effectivePrompt, configuredOptions);
        await _transport.ConnectAsync(ct);

        // Extract SDK MCP servers
        var sdkMcpServers = ExtractSdkMcpServers(configuredOptions);

        // Create internal Query handler
        _query = new InternalQuery(
            _transport,
            isStreamingMode: true, // Always streaming for ClaudeSDKClient
            configuredOptions.CanUseTool,
            configuredOptions.Hooks,
            sdkMcpServers);

        // Start reading messages and initialize
        _query.Start();
        await _query.InitializeAsync(ct);

        // Stream initial prompt if provided
        if (prompt != null)
        {
            var inputStream = CreateInputStream(prompt);
            _ = _query.StreamInputAsync(inputStream, ct);
        }

        _connected = true;
    }

    /// <summary>
    /// Disconnect and clean up resources.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (!_connected || _query == null)
        {
            return;
        }

        await _query.CloseAsync();
        _query = null;
        _transport = null;
        _connected = false;
    }

    /// <summary>
    /// Disposes the client and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisconnectAsync();
    }

    #endregion

    #region Message Streaming

    /// <summary>
    /// Stream all messages from Claude indefinitely until disconnection or cancellation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of messages from the conversation.</returns>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public async IAsyncEnumerable<Message> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ThrowIfNotConnected();

        await foreach (var data in _query!.ReceiveMessagesAsync(ct))
        {
            yield return MessageParser.ParseMessage(DictToJsonElement(data));
        }
    }

    /// <summary>
    /// Receive messages until a <see cref="ResultMessage"/> is received, then stop.
    /// Useful for getting a complete response to a single query.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of messages ending with ResultMessage.</returns>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public async IAsyncEnumerable<Message> ReceiveResponseAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in ReceiveMessagesAsync(ct))
        {
            yield return message;
            if (message is ResultMessage)
            {
                yield break;
            }
        }
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Send a new prompt/query to the connected Claude session.
    /// </summary>
    /// <param name="prompt">The prompt text to send.</param>
    /// <param name="sessionId">Optional session ID for multi-session support.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public async Task QueryAsync(string prompt, string? sessionId = null, CancellationToken ct = default)
    {
        ThrowIfNotConnected();

        var message = new Dictionary<string, object?>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = prompt
            }
        };

        if (sessionId != null)
        {
            message["session_id"] = sessionId;
        }

        var json = JsonSerializer.Serialize(message) + "\n";
        await _transport!.WriteAsync(json, ct);
    }

    /// <summary>
    /// Send a new prompt/query using streaming messages.
    /// </summary>
    /// <param name="messages">Async enumerable of message dictionaries.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public async Task QueryAsync(
        IAsyncEnumerable<Dictionary<string, object?>> messages,
        CancellationToken ct = default)
    {
        ThrowIfNotConnected();

        await foreach (var message in messages.WithCancellation(ct))
        {
            var json = JsonSerializer.Serialize(message) + "\n";
            await _transport!.WriteAsync(json, ct);
        }
    }

    #endregion

    #region Control Operations

    /// <summary>
    /// Interrupt the current operation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task InterruptAsync(CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.InterruptAsync(ct);
    }

    /// <summary>
    /// Change the permission mode.
    /// </summary>
    /// <param name="mode">The new permission mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task SetPermissionModeAsync(PermissionMode mode, CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.SetPermissionModeAsync(mode, ct);
    }

    /// <summary>
    /// Switch to a different model.
    /// </summary>
    /// <param name="model">The model name (e.g., "claude-sonnet-4-20250514").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task SetModelAsync(string? model, CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.SetModelAsync(model, ct);
    }

    /// <summary>
    /// Rewind tracked files to their state at a specific user message.
    /// Requires file checkpointing to be enabled via <see cref="ClaudeAgentOptions.EnableFileCheckpointing"/>.
    /// </summary>
    /// <param name="userMessageId">UUID of the user message to rewind to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task RewindFilesAsync(string userMessageId, CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.RewindFilesAsync(userMessageId, ct);
    }

    /// <summary>
    /// Get server initialization information.
    /// </summary>
    /// <returns>Server info dictionary with supported commands and configuration, or null if not connected.</returns>
    public Dictionary<string, object?>? GetServerInfo()
    {
        return ServerInfo;
    }

    #endregion

    #region Private Helpers

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClaudeSDKClient));
        }
    }

    private void ThrowIfNotConnected()
    {
        ThrowIfDisposed();
        if (!_connected || _query == null || _query.IsClosed)
        {
            throw new CLIConnectionException("Not connected. Call ConnectAsync() first.");
        }
    }

    private static ClaudeAgentOptions ValidateAndConfigureOptions(ClaudeAgentOptions options, object? prompt)
    {
        var isStreaming = prompt is not string;

        if (options.CanUseTool != null)
        {
            // canUseTool callback requires streaming mode (IAsyncEnumerable prompt)
            if (!isStreaming && prompt != null)
            {
                throw new ArgumentException(
                    "can_use_tool callback requires streaming mode. " +
                    "Please provide prompt as an IAsyncEnumerable or null instead of a string.",
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
            return options with { PermissionPromptToolName = "stdio" };
        }

        return options;
    }

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

    private static async IAsyncEnumerable<Dictionary<string, object?>> CreateInputStream(object prompt)
    {
        switch (prompt)
        {
            case string text:
                yield return new Dictionary<string, object?>
                {
                    ["type"] = "user",
                    ["message"] = new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = text
                    }
                };
                break;

            case IAsyncEnumerable<Dictionary<string, object?>> stream:
                await foreach (var msg in stream)
                {
                    yield return msg;
                }
                break;

            case Dictionary<string, object?> dict:
                yield return dict;
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported prompt type: {prompt.GetType()}. " +
                    "Expected string, IAsyncEnumerable<Dictionary<string, object?>>, or Dictionary<string, object?>.",
                    nameof(prompt));
        }
    }

    private static JsonElement DictToJsonElement(Dictionary<string, object?> data)
    {
        var json = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static async IAsyncEnumerable<Dictionary<string, object?>> EmptyInputStream()
    {
        await Task.CompletedTask;
        yield break;
    }

    #endregion
}
