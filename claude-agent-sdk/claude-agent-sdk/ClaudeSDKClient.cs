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
    private MaterializedResume? _materialized;
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
    ///   <item><c>IEnumerable&lt;ContentBlock&gt;</c>: Content blocks (text, images, etc.)</item>
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

        // Fail fast on invalid session_store option combinations
        SessionStoreValidation.Validate(_options);

        // Set the entrypoint environment variable
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-csharp-client");

        // Resume/continue + session_store: load session from store
        _materialized = _customTransport == null
            ? await SessionResume.MaterializeResumeSessionAsync(_options)
            : null;

        try
        {
            await ConnectInnerAsync(prompt, ct);
        }
        catch
        {
            await DisconnectAsync();
            throw;
        }
    }

    private async Task ConnectInnerAsync(object? prompt, CancellationToken ct)
    {
        // Validate and configure options
        var configuredOptions = ValidateAndConfigureOptions(_options, prompt);

        if (_materialized != null)
        {
            configuredOptions = SessionResume.ApplyMaterializedOptions(configuredOptions, _materialized);
        }

        var effectivePrompt = prompt switch
        {
            null => EmptyInputStream(),
            string => EmptyInputStream(),
            IEnumerable<ContentBlock> => EmptyInputStream(),
            _ => prompt
        };
        _transport = _customTransport ?? new SubprocessCliTransport(effectivePrompt, configuredOptions);
        await _transport.ConnectAsync(ct);

        // Extract SDK MCP servers
        var sdkMcpServers = ExtractSdkMcpServers(configuredOptions);

        // Extract exclude_dynamic_sections from preset system prompt
        bool? excludeDynamicSections = null;
        if (configuredOptions.SystemPrompt is SystemPromptPreset preset)
        {
            excludeDynamicSections = preset.ExcludeDynamicSections;
        }

        // Create internal Query handler
        _query = new InternalQuery(
            _transport,
            isStreamingMode: true,
            configuredOptions.CanUseTool,
            configuredOptions.Hooks,
            sdkMcpServers,
            excludeDynamicSections: excludeDynamicSections,
            skills: configuredOptions.Skills);

        // Setup transcript mirror batcher if session store is configured
        if (configuredOptions.SessionStore != null)
        {
            _query.SetTranscriptMirrorBatcher(
                SessionResume.BuildMirrorBatcher(
                    configuredOptions.SessionStore,
                    _materialized,
                    configuredOptions.Env,
                    (key, error) =>
                    {
                        _query.ReportMirrorError(key, error);
                        return Task.CompletedTask;
                    }));
        }

        // Start reading messages and initialize
        _query.Start();
        await _query.InitializeAsync(configuredOptions.Agents, ct);

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
        if (_query != null)
        {
            await _query.CloseAsync();
            _query = null;
        }
        _transport = null;
        _connected = false;

        if (_materialized != null)
        {
            await _materialized.CleanupAsync();
            _materialized = null;
        }
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
            var message = MessageParser.ParseMessage(DictToJsonElement(data));
            if (message != null)
            {
                yield return message;
            }
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
    /// Send a new prompt/query with content blocks (supports text and images).
    /// </summary>
    /// <param name="contentBlocks">The content blocks to send (e.g., TextBlock, ImageBlock).</param>
    /// <param name="sessionId">Optional session ID for multi-session support.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public async Task QueryAsync(IEnumerable<ContentBlock> contentBlocks, string? sessionId = null, CancellationToken ct = default)
    {
        ThrowIfNotConnected();

        var contentArray = contentBlocks.Select(SerializeContentBlock).ToList();

        var message = new Dictionary<string, object?>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = contentArray
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

    /// <summary>
    /// Reconnect a disconnected or failed MCP server.
    /// </summary>
    /// <param name="serverName">The name of the MCP server to reconnect.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task ReconnectMcpServerAsync(string serverName, CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.ReconnectMcpServerAsync(serverName, ct);
    }

    /// <summary>
    /// Enable or disable an MCP server.
    /// </summary>
    /// <param name="serverName">The name of the MCP server to toggle.</param>
    /// <param name="enabled">Whether to enable or disable the server.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task ToggleMcpServerAsync(string serverName, bool enabled, CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.ToggleMcpServerAsync(serverName, enabled, ct);
    }

    /// <summary>
    /// Stop a running task.
    /// </summary>
    /// <param name="taskId">The ID of the task to stop.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task StopTaskAsync(string taskId, CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.StopTaskAsync(taskId, ct);
    }

    /// <summary>
    /// Get the status of all MCP server connections.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>MCP status response with server connection details.</returns>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task<McpStatusResponse> GetMcpStatusAsync(CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.GetMcpStatusAsync(ct);
    }

    /// <summary>
    /// Get the current context usage breakdown.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Context usage response with token breakdowns.</returns>
    /// <exception cref="CLIConnectionException">If not connected.</exception>
    public Task<ContextUsageResponse> GetContextUsageAsync(CancellationToken ct = default)
    {
        ThrowIfNotConnected();
        return _query!.GetContextUsageAsync(ct);
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

            case IEnumerable<ContentBlock> contentBlocks:
                yield return new Dictionary<string, object?>
                {
                    ["type"] = "user",
                    ["message"] = new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = contentBlocks.Select(SerializeContentBlock).ToList()
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
                    "Expected string, IEnumerable<ContentBlock>, IAsyncEnumerable<Dictionary<string, object?>>, or Dictionary<string, object?>.",
                    nameof(prompt));
        }
    }

    private static Dictionary<string, object?> SerializeContentBlock(ContentBlock block)
    {
        return block switch
        {
            TextBlock text => new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = text.Text
            },
            ImageBlock image => image.Source switch
            {
                Base64ImageSource base64 => new Dictionary<string, object?>
                {
                    ["type"] = "image",
                    ["source"] = new Dictionary<string, object?>
                    {
                        ["type"] = "base64",
                        ["media_type"] = base64.MediaType,
                        ["data"] = base64.Data
                    }
                },
                UrlImageSource url => new Dictionary<string, object?>
                {
                    ["type"] = "image",
                    ["source"] = new Dictionary<string, object?>
                    {
                        ["type"] = "url",
                        ["url"] = url.Url
                    }
                },
                _ => throw new ArgumentException($"Unsupported image source type: {image.Source.GetType()}")
            },
            DocumentBlock document => SerializeDocumentBlock(document),
            _ => throw new ArgumentException($"Unsupported content block type for serialization: {block.GetType()}")
        };
    }

    private static Dictionary<string, object?> SerializeDocumentBlock(DocumentBlock document)
    {
        var sourceDict = document.Source switch
        {
            Base64DocumentSource base64 => new Dictionary<string, object?>
            {
                ["type"] = "base64",
                ["media_type"] = base64.MediaType,
                ["data"] = base64.Data
            },
            UrlDocumentSource url => new Dictionary<string, object?>
            {
                ["type"] = "url",
                ["url"] = url.Url
            },
            PlainTextDocumentSource text => new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["media_type"] = text.MediaType,
                ["data"] = text.Data
            },
            _ => throw new ArgumentException($"Unsupported document source type: {document.Source.GetType()}")
        };

        var result = new Dictionary<string, object?>
        {
            ["type"] = "document",
            ["source"] = sourceDict
        };

        if (document.Title != null)
            result["title"] = document.Title;

        if (document.Context != null)
            result["context"] = document.Context;

        if (document.Citations != null)
            result["citations"] = new Dictionary<string, object?> { ["enabled"] = document.Citations.Enabled };

        return result;
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
