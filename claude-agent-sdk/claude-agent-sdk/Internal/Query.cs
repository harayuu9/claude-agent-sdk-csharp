using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSdk.Internal;

#region SDK MCP Server Abstraction

/// <summary>
/// Represents information about an MCP server.
/// </summary>
internal record SdkMcpServerInfo
{
    /// <summary>
    /// The name of the server.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The version of the server.
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// Represents an MCP tool definition.
/// </summary>
internal record SdkMcpTool
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The description of the tool.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The JSON schema for the tool's input.
    /// </summary>
    public object? InputSchema { get; init; }
}

/// <summary>
/// Represents a content item in an MCP tool result.
/// </summary>
internal record SdkMcpContent
{
    /// <summary>
    /// Text content.
    /// </summary>
    public string? Text { get; init; }
}

/// <summary>
/// Represents the result of an MCP tool call.
/// </summary>
internal record SdkMcpToolResult
{
    /// <summary>
    /// The content items in the result.
    /// </summary>
    public required IReadOnlyList<SdkMcpContent> Content { get; init; }

    /// <summary>
    /// Whether the tool call resulted in an error.
    /// </summary>
    public bool IsError { get; init; }
}

/// <summary>
/// Abstraction for SDK MCP servers.
/// This interface allows the Query class to work with MCP servers without
/// directly depending on the ModelContextProtocol SDK, which allows for
/// easier testing and API version independence.
/// </summary>
internal interface ISdkMcpServer
{
    /// <summary>
    /// Information about the server.
    /// </summary>
    SdkMcpServerInfo ServerInfo { get; }

    /// <summary>
    /// List available tools.
    /// </summary>
    Task<IReadOnlyList<SdkMcpTool>> ListToolsAsync(CancellationToken ct = default);

    /// <summary>
    /// Call a tool with the specified arguments.
    /// </summary>
    Task<SdkMcpToolResult> CallToolAsync(
        string toolName,
        Dictionary<string, object?> arguments,
        CancellationToken ct = default);
}

#endregion

/// <summary>
/// Handles bidirectional control protocol on top of Transport.
///
/// This class manages:
/// - Control request/response routing
/// - Hook callbacks
/// - Tool permission callbacks
/// - Message streaming
/// - Initialization handshake
/// </summary>
internal sealed class Query : IAsyncEnumerable<Dictionary<string, object?>>, IAsyncDisposable
{
    private readonly ILogger<Query>? _logger;

    // Dependencies
    private readonly Transport.Transport _transport;
    private readonly bool _isStreamingMode;
    private readonly CanUseTool? _canUseTool;
    private readonly Dictionary<HookEvent, List<HookMatcher>>? _hooks;
    private readonly Dictionary<string, ISdkMcpServer>? _sdkMcpServers;
    private readonly TimeSpan _initializeTimeout;

    // Control protocol state
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, object?>>> _pendingControlResponses = new();
    private readonly Dictionary<string, HookCallback> _hookCallbacks = new();
    private int _nextCallbackId;
    private int _requestCounter;

    // Message stream
    private readonly Channel<Dictionary<string, object?>> _messageChannel;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private bool _initialized;
    private bool _closed;
    private Dictionary<string, object?>? _initializationResult;

    // Result tracking for SDK MCP servers
    private readonly TaskCompletionSource<bool> _firstResultEvent = new();
    private readonly TimeSpan _streamCloseTimeout;

    /// <summary>
    /// Whether the query has been initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Whether the query has been closed.
    /// </summary>
    public bool IsClosed => _closed;

    /// <summary>
    /// The initialization result from the control protocol.
    /// </summary>
    public Dictionary<string, object?>? InitializationResult => _initializationResult;

    /// <summary>
    /// Initialize Query with transport and callbacks.
    /// This constructor is internal because Transport is an internal type.
    /// Use the Client class to create Query instances.
    /// </summary>
    /// <param name="transport">Low-level transport for I/O</param>
    /// <param name="isStreamingMode">Whether using streaming (bidirectional) mode</param>
    /// <param name="canUseTool">Optional callback for tool permission requests</param>
    /// <param name="hooks">Optional hook configurations</param>
    /// <param name="sdkMcpServers">Optional SDK MCP server instances</param>
    /// <param name="initializeTimeout">Timeout for the initialize request</param>
    /// <param name="logger">Optional logger</param>
    internal Query(
        Transport.Transport transport,
        bool isStreamingMode,
        CanUseTool? canUseTool = null,
        Dictionary<HookEvent, List<HookMatcher>>? hooks = null,
        Dictionary<string, ISdkMcpServer>? sdkMcpServers = null,
        TimeSpan? initializeTimeout = null,
        ILogger<Query>? logger = null)
    {
        _transport = transport;
        _isStreamingMode = isStreamingMode;
        _canUseTool = canUseTool;
        _hooks = hooks;
        _sdkMcpServers = sdkMcpServers;
        _initializeTimeout = initializeTimeout ?? TimeSpan.FromSeconds(60);
        _logger = logger;

        _messageChannel = Channel.CreateBounded<Dictionary<string, object?>>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        var envTimeout = Environment.GetEnvironmentVariable("CLAUDE_CODE_STREAM_CLOSE_TIMEOUT");
        _streamCloseTimeout = TimeSpan.FromMilliseconds(
            double.TryParse(envTimeout, out var ms) ? ms : 60000);
    }

    #region Initialization

    /// <summary>
    /// Initialize control protocol if in streaming mode.
    /// </summary>
    /// <returns>Initialize response with supported commands, or null if not streaming</returns>
    public async Task<Dictionary<string, object?>?> InitializeAsync(CancellationToken ct = default)
    {
        if (!_isStreamingMode)
        {
            return null;
        }

        // Build hooks configuration for initialization
        var hooksConfig = BuildHooksConfig();

        // Send initialize request
        var request = new Dictionary<string, object?>
        {
            ["subtype"] = "initialize",
            ["hooks"] = hooksConfig.Count > 0 ? hooksConfig : null
        };

        // Use longer timeout for initialize since MCP servers may take time to start
        var response = await SendControlRequestAsync(request, _initializeTimeout, ct);
        _initialized = true;
        _initializationResult = response;
        return response;
    }

    private Dictionary<string, object?> BuildHooksConfig()
    {
        var hooksConfig = new Dictionary<string, object?>();

        if (_hooks == null)
        {
            return hooksConfig;
        }

        foreach (var (eventType, matchers) in _hooks)
        {
            if (matchers.Count == 0)
            {
                continue;
            }

            var matcherConfigs = new List<Dictionary<string, object?>>();
            foreach (var matcher in matchers)
            {
                var callbackIds = new List<string>();
                foreach (var hook in matcher.Hooks)
                {
                    var callbackId = $"hook_{_nextCallbackId++}";
                    _hookCallbacks[callbackId] = hook;
                    callbackIds.Add(callbackId);
                }

                var hookMatcherConfig = new Dictionary<string, object?>
                {
                    ["matcher"] = matcher.Matcher,
                    ["hookCallbackIds"] = callbackIds
                };

                if (matcher.Timeout.HasValue)
                {
                    hookMatcherConfig["timeout"] = matcher.Timeout.Value;
                }

                matcherConfigs.Add(hookMatcherConfig);
            }

            hooksConfig[eventType.ToString()] = matcherConfigs;
        }

        return hooksConfig;
    }

    #endregion

    #region Message Reading

    /// <summary>
    /// Start reading messages from transport.
    /// </summary>
    public void Start()
    {
        if (_readTask != null)
        {
            return;
        }

        _readCts = new CancellationTokenSource();
        _readTask = ReadMessagesLoopAsync(_readCts.Token);
    }

    private async Task ReadMessagesLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _transport.ReadMessagesAsync(ct))
            {
                if (_closed)
                {
                    break;
                }

                var msgType = GetStringValue(message, "type");

                switch (msgType)
                {
                    case "control_response":
                        HandleControlResponse(message);
                        continue;

                    case "control_request":
                        // Handle incoming control requests from CLI
                        _ = HandleControlRequestAsync(message);
                        continue;

                    case "control_cancel_request":
                        // TODO: Implement cancellation support
                        continue;
                }

                // Track results for proper stream closure
                if (msgType == "result")
                {
                    _firstResultEvent.TrySetResult(true);
                }

                // Regular SDK messages go to the stream
                await _messageChannel.Writer.WriteAsync(message, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Task was cancelled - this is expected behavior
            _logger?.LogDebug("Read task cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fatal error in message reader");

            // Signal all pending control requests so they fail fast instead of timing out
            foreach (var (requestId, tcs) in _pendingControlResponses)
            {
                tcs.TrySetException(ex);
            }

            // Put error in stream so iterators can handle it
            await _messageChannel.Writer.WriteAsync(
                new Dictionary<string, object?> { ["type"] = "error", ["error"] = ex.Message },
                CancellationToken.None);
        }
        finally
        {
            // Always signal end of stream
            await _messageChannel.Writer.WriteAsync(
                new Dictionary<string, object?> { ["type"] = "end" },
                CancellationToken.None);
        }
    }

    private void HandleControlResponse(Dictionary<string, object?> message)
    {
        var responseValue = message.GetValueOrDefault("response");
        Dictionary<string, object?>? response = null;

        if (responseValue is Dictionary<string, object?> dict)
        {
            response = dict;
        }
        else if (responseValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            response = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonElement.GetRawText());
        }

        if (response == null)
        {
            return;
        }

        var requestId = GetStringValue(response, "request_id");
        if (requestId == null || !_pendingControlResponses.TryRemove(requestId, out var tcs))
        {
            return;
        }

        var subtype = GetStringValue(response, "subtype");
        if (subtype == "error")
        {
            var error = GetStringValue(response, "error") ?? "Unknown error";
            tcs.TrySetException(new ClaudeSDKException(error));
        }
        else
        {
            tcs.TrySetResult(response);
        }
    }

    /// <summary>
    /// Helper method to get string value from dictionary, handling JsonElement.
    /// </summary>
    private static string? GetStringValue(Dictionary<string, object?>? dict, string key)
    {
        if (dict == null || !dict.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value is string str)
        {
            return str;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            return jsonElement.GetString();
        }

        return value?.ToString();
    }

    #endregion

    #region Control Request Handling

    private async Task HandleControlRequestAsync(Dictionary<string, object?> request)
    {
        var requestId = request.GetValueOrDefault("request_id") as string ?? "";
        var requestData = request.GetValueOrDefault("request") as Dictionary<string, object?>;

        if (requestData == null)
        {
            await SendErrorResponseAsync(requestId, "Invalid request data");
            return;
        }

        var subtype = requestData.GetValueOrDefault("subtype") as string;

        try
        {
            var responseData = subtype switch
            {
                "can_use_tool" => await HandleToolPermissionAsync(requestData),
                "hook_callback" => await HandleHookCallbackAsync(requestData),
                "mcp_message" => await HandleMcpMessageAsync(requestData),
                _ => throw new InvalidOperationException($"Unsupported control request subtype: {subtype}")
            };

            await SendSuccessResponseAsync(requestId, responseData);
        }
        catch (Exception ex)
        {
            await SendErrorResponseAsync(requestId, ex.Message);
        }
    }

    private async Task<Dictionary<string, object?>> HandleToolPermissionAsync(Dictionary<string, object?> requestData)
    {
        if (_canUseTool == null)
        {
            throw new InvalidOperationException("canUseTool callback is not provided");
        }

        var toolName = requestData.GetValueOrDefault("tool_name") as string ?? "";
        var originalInput = requestData.GetValueOrDefault("input") as Dictionary<string, object?> ?? new();

        var permissionSuggestions = new List<PermissionUpdate>();
        if (requestData.GetValueOrDefault("permission_suggestions") is List<object?> suggestions)
        {
            foreach (var suggestion in suggestions)
            {
                if (suggestion is Dictionary<string, object?> dict)
                {
                    var json = JsonSerializer.Serialize(dict);
                    var update = JsonSerializer.Deserialize<PermissionUpdate>(json);
                    if (update != null)
                    {
                        permissionSuggestions.Add(update);
                    }
                }
            }
        }

        var context = new ToolPermissionContext
        {
            Signal = null,
            Suggestions = permissionSuggestions
        };

        var result = await _canUseTool(toolName, originalInput, context);

        return result switch
        {
            PermissionResultAllow allow => new Dictionary<string, object?>
            {
                ["behavior"] = "allow",
                ["updatedInput"] = allow.UpdatedInput ?? originalInput,
                ["updatedPermissions"] = allow.UpdatedPermissions?.Select(p => p.ToDict()).ToList()
            },
            PermissionResultDeny deny => new Dictionary<string, object?>
            {
                ["behavior"] = "deny",
                ["message"] = deny.Message,
                ["interrupt"] = deny.Interrupt
            },
            _ => throw new InvalidOperationException(
                $"Tool permission callback must return PermissionResult (PermissionResultAllow or PermissionResultDeny), got {result.GetType()}")
        };
    }

    private async Task<Dictionary<string, object?>> HandleHookCallbackAsync(Dictionary<string, object?> requestData)
    {
        var callbackId = requestData.GetValueOrDefault("callback_id") as string ?? "";

        if (!_hookCallbacks.TryGetValue(callbackId, out var callback))
        {
            throw new InvalidOperationException($"No hook callback found for ID: {callbackId}");
        }

        var input = ParseHookInput(requestData.GetValueOrDefault("input"));
        var toolUseId = requestData.GetValueOrDefault("tool_use_id") as string;

        var hookOutput = await callback(input, toolUseId, new HookContext { Signal = null });

        return ConvertHookOutputForCli(hookOutput);
    }

    private static BaseHookInput ParseHookInput(object? inputObj)
    {
        if (inputObj is not Dictionary<string, object?> input)
        {
            throw new InvalidOperationException("Hook input is not a valid dictionary");
        }

        var hookEventName = input.GetValueOrDefault("hook_event_name") as string;
        var json = JsonSerializer.Serialize(input);

        return hookEventName switch
        {
            "PreToolUse" => JsonSerializer.Deserialize<PreToolUseHookInput>(json)!,
            "PostToolUse" => JsonSerializer.Deserialize<PostToolUseHookInput>(json)!,
            "UserPromptSubmit" => JsonSerializer.Deserialize<UserPromptSubmitHookInput>(json)!,
            "Stop" => JsonSerializer.Deserialize<StopHookInput>(json)!,
            "SubagentStop" => JsonSerializer.Deserialize<SubagentStopHookInput>(json)!,
            "PreCompact" => JsonSerializer.Deserialize<PreCompactHookInput>(json)!,
            _ => throw new InvalidOperationException($"Unknown hook event name: {hookEventName}")
        };
    }

    /// <summary>
    /// Convert Python-safe field names to CLI-expected field names.
    /// The C# SDK uses standard names, but we need to ensure correct serialization.
    /// </summary>
    private static Dictionary<string, object?> ConvertHookOutputForCli(HookJsonOutput output)
    {
        var json = JsonSerializer.Serialize(output);
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
        return dict;
    }

    private async Task<Dictionary<string, object?>> HandleMcpMessageAsync(Dictionary<string, object?> requestData)
    {
        var serverName = requestData.GetValueOrDefault("server_name") as string;
        var message = requestData.GetValueOrDefault("message") as Dictionary<string, object?>;

        if (string.IsNullOrEmpty(serverName) || message == null)
        {
            throw new InvalidOperationException("Missing server_name or message for MCP request");
        }

        var mcpResponse = await HandleSdkMcpRequestAsync(serverName, message);
        return new Dictionary<string, object?> { ["mcp_response"] = mcpResponse };
    }

    #endregion

    #region SDK MCP Request Handling

    private async Task<Dictionary<string, object?>> HandleSdkMcpRequestAsync(
        string serverName, Dictionary<string, object?> message)
    {
        if (_sdkMcpServers == null || !_sdkMcpServers.TryGetValue(serverName, out var server))
        {
            return new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = message.GetValueOrDefault("id"),
                ["error"] = new Dictionary<string, object?>
                {
                    ["code"] = -32601,
                    ["message"] = $"Server '{serverName}' not found"
                }
            };
        }

        var method = message.GetValueOrDefault("method") as string;
        var @params = message.GetValueOrDefault("params") as Dictionary<string, object?> ?? new();

        try
        {
            return method switch
            {
                "initialize" => BuildMcpInitializeResponse(message, server),
                "tools/list" => await HandleMcpToolsListAsync(message, server),
                "tools/call" => await HandleMcpToolsCallAsync(message, server, @params),
                "notifications/initialized" => new Dictionary<string, object?>
                {
                    ["jsonrpc"] = "2.0",
                    ["result"] = new Dictionary<string, object?>()
                },
                _ => new Dictionary<string, object?>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = message.GetValueOrDefault("id"),
                    ["error"] = new Dictionary<string, object?>
                    {
                        ["code"] = -32601,
                        ["message"] = $"Method '{method}' not found"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = message.GetValueOrDefault("id"),
                ["error"] = new Dictionary<string, object?>
                {
                    ["code"] = -32603,
                    ["message"] = ex.Message
                }
            };
        }
    }

    private static Dictionary<string, object?> BuildMcpInitializeResponse(
        Dictionary<string, object?> message, ISdkMcpServer server)
    {
        // Handle MCP initialization - hardcoded for tools only, no listChanged
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = message.GetValueOrDefault("id"),
            ["result"] = new Dictionary<string, object?>
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new Dictionary<string, object?>
                {
                    ["tools"] = new Dictionary<string, object?>() // Tools capability without listChanged
                },
                ["serverInfo"] = new Dictionary<string, object?>
                {
                    ["name"] = server.ServerInfo.Name,
                    ["version"] = server.ServerInfo.Version ?? "1.0.0"
                }
            }
        };
    }

    private static async Task<Dictionary<string, object?>> HandleMcpToolsListAsync(
        Dictionary<string, object?> message, ISdkMcpServer server)
    {
        var tools = await server.ListToolsAsync();

        var toolsData = tools.Select(tool => new Dictionary<string, object?>
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.InputSchema
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = message.GetValueOrDefault("id"),
            ["result"] = new Dictionary<string, object?> { ["tools"] = toolsData }
        };
    }

    private static async Task<Dictionary<string, object?>> HandleMcpToolsCallAsync(
        Dictionary<string, object?> message, ISdkMcpServer server, Dictionary<string, object?> @params)
    {
        var toolName = @params.GetValueOrDefault("name") as string ?? "";
        var arguments = @params.GetValueOrDefault("arguments") as Dictionary<string, object?> ?? new();

        var result = await server.CallToolAsync(toolName, arguments);

        var content = new List<Dictionary<string, object?>>();
        foreach (var item in result.Content)
        {
            if (item.Text != null)
            {
                content.Add(new Dictionary<string, object?>
                {
                    ["type"] = "text",
                    ["text"] = item.Text
                });
            }
        }

        var responseData = new Dictionary<string, object?> { ["content"] = content };
        if (result.IsError)
        {
            responseData["is_error"] = true;
        }

        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = message.GetValueOrDefault("id"),
            ["result"] = responseData
        };
    }

    #endregion

    #region Control Request Sending

    private async Task<Dictionary<string, object?>> SendControlRequestAsync(
        Dictionary<string, object?> request,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        if (!_isStreamingMode)
        {
            throw new InvalidOperationException("Control requests require streaming mode");
        }

        // Generate unique request ID
        var requestId = $"req_{Interlocked.Increment(ref _requestCounter)}_{Guid.NewGuid():N}";
        var tcs = new TaskCompletionSource<Dictionary<string, object?>>();
        _pendingControlResponses[requestId] = tcs;

        // Build and send request
        var controlRequest = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = requestId,
            ["request"] = request
        };

        await _transport.WriteAsync(JsonSerializer.Serialize(controlRequest) + "\n", ct);

        // Wait for response with timeout
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(effectiveTimeout);

        try
        {
            var completedTask = await Task.WhenAny(
                tcs.Task,
                Task.Delay(Timeout.Infinite, cts.Token));

            if (completedTask != tcs.Task)
            {
                throw new TimeoutException($"Control request timeout: {request.GetValueOrDefault("subtype")}");
            }

            var result = await tcs.Task;
            return result.GetValueOrDefault("response") as Dictionary<string, object?> ?? new();
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Control request timeout: {request.GetValueOrDefault("subtype")}");
        }
        finally
        {
            _pendingControlResponses.TryRemove(requestId, out _);
        }
    }

    private async Task SendSuccessResponseAsync(string requestId, Dictionary<string, object?> responseData)
    {
        var response = new Dictionary<string, object?>
        {
            ["type"] = "control_response",
            ["response"] = new Dictionary<string, object?>
            {
                ["subtype"] = "success",
                ["request_id"] = requestId,
                ["response"] = responseData
            }
        };

        await _transport.WriteAsync(JsonSerializer.Serialize(response) + "\n");
    }

    private async Task SendErrorResponseAsync(string requestId, string error)
    {
        var response = new Dictionary<string, object?>
        {
            ["type"] = "control_response",
            ["response"] = new Dictionary<string, object?>
            {
                ["subtype"] = "error",
                ["request_id"] = requestId,
                ["error"] = error
            }
        };

        await _transport.WriteAsync(JsonSerializer.Serialize(response) + "\n");
    }

    #endregion

    #region Control Commands

    /// <summary>
    /// Send interrupt control request.
    /// </summary>
    public Task InterruptAsync(CancellationToken ct = default)
        => SendControlRequestAsync(new Dictionary<string, object?> { ["subtype"] = "interrupt" }, ct: ct);

    /// <summary>
    /// Change permission mode.
    /// </summary>
    public Task SetPermissionModeAsync(PermissionMode mode, CancellationToken ct = default)
        => SendControlRequestAsync(
            new Dictionary<string, object?>
            {
                ["subtype"] = "set_permission_mode",
                ["mode"] = mode.ToString()
            }, ct: ct);

    /// <summary>
    /// Change the AI model.
    /// </summary>
    public Task SetModelAsync(string? model, CancellationToken ct = default)
        => SendControlRequestAsync(
            new Dictionary<string, object?>
            {
                ["subtype"] = "set_model",
                ["model"] = model
            }, ct: ct);

    /// <summary>
    /// Rewind tracked files to their state at a specific user message.
    /// Requires file checkpointing to be enabled via the enable_file_checkpointing option.
    /// </summary>
    /// <param name="userMessageId">UUID of the user message to rewind to</param>
    /// <param name="ct">Cancellation token</param>
    public Task RewindFilesAsync(string userMessageId, CancellationToken ct = default)
        => SendControlRequestAsync(
            new Dictionary<string, object?>
            {
                ["subtype"] = "rewind_files",
                ["user_message_id"] = userMessageId
            }, ct: ct);

    #endregion

    #region Input/Output Streaming

    /// <summary>
    /// Stream input messages to transport.
    /// If SDK MCP servers or hooks are present, waits for the first result
    /// before closing stdin to allow bidirectional control protocol communication.
    /// </summary>
    public async Task StreamInputAsync(
        IAsyncEnumerable<Dictionary<string, object?>> stream,
        CancellationToken ct = default)
    {
        try
        {
            await foreach (var message in stream.WithCancellation(ct))
            {
                if (_closed)
                {
                    break;
                }

                await _transport.WriteAsync(JsonSerializer.Serialize(message) + "\n", ct);
            }

            // If we have SDK MCP servers or hooks that need bidirectional communication,
            // wait for first result before closing the channel
            var hasHooks = _hooks != null && _hooks.Count > 0;
            if (_sdkMcpServers?.Count > 0 || hasHooks)
            {
                _logger?.LogDebug(
                    "Waiting for first result before closing stdin (sdkMcpServers={McpCount}, hasHooks={HasHooks})",
                    _sdkMcpServers?.Count ?? 0, hasHooks);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_streamCloseTimeout);

                try
                {
                    await _firstResultEvent.Task.WaitAsync(cts.Token);
                    _logger?.LogDebug("Received first result, closing input stream");
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogDebug("Timed out waiting for first result, closing input stream");
                }
            }

            // After all messages sent (and result received if needed), end input
            await _transport.EndInputAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Error streaming input");
        }
    }

    /// <summary>
    /// Receive SDK messages (not control messages).
    /// </summary>
    public async IAsyncEnumerable<Dictionary<string, object?>> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in _messageChannel.Reader.ReadAllAsync(ct))
        {
            var msgType = message.GetValueOrDefault("type") as string;

            // Check for special messages
            if (msgType == "end")
            {
                yield break;
            }

            if (msgType == "error")
            {
                throw new ClaudeSDKException(message.GetValueOrDefault("error") as string ?? "Unknown error");
            }

            yield return message;
        }
    }

    /// <summary>
    /// Return async iterator for messages.
    /// </summary>
    public IAsyncEnumerator<Dictionary<string, object?>> GetAsyncEnumerator(CancellationToken ct = default)
        => ReceiveMessagesAsync(ct).GetAsyncEnumerator(ct);

    #endregion

    #region Cleanup

    /// <summary>
    /// Close the query and transport.
    /// </summary>
    public async Task CloseAsync()
    {
        await DisposeAsync();
    }

    /// <summary>
    /// Dispose the query and release resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;

        // Cancel reading task
        _readCts?.Cancel();
        if (_readTask != null)
        {
            try
            {
                await _readTask;
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        // Complete the message channel
        _messageChannel.Writer.TryComplete();

        // Close transport
        await _transport.CloseAsync();

        // Dispose resources
        _readCts?.Dispose();
    }

    #endregion
}
