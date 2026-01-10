using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSdk.Internal.Transport;

namespace UnitTest.Helpers;

/// <summary>
/// Mock transport for testing ClaudeSDKClient streaming functionality.
/// Supports control protocol (initialize, interrupt) auto-responses.
/// Ported from Python's create_mock_transport() in test_streaming_client.py.
/// </summary>
internal class StreamingMockTransport : Transport
{
    private readonly List<string> _writtenMessages = new();
    private readonly Channel<Dictionary<string, object?>> _messageChannel;
    private bool _connected;

    /// <summary>
    /// If true, automatically respond to initialize control requests.
    /// </summary>
    public bool AutoRespondToInitialize { get; set; } = true;

    /// <summary>
    /// If true, automatically respond to interrupt control requests.
    /// </summary>
    public bool AutoRespondToInterrupt { get; set; } = true;

    /// <summary>
    /// Gets the list of messages written to the transport.
    /// </summary>
    public IReadOnlyList<string> WrittenMessages => _writtenMessages;

    /// <summary>
    /// Gets the number of times Connect was called.
    /// </summary>
    public int ConnectCallCount { get; private set; }

    /// <summary>
    /// Gets the number of times Close was called.
    /// </summary>
    public int CloseCallCount { get; private set; }

    public StreamingMockTransport()
    {
        _messageChannel = Channel.CreateUnbounded<Dictionary<string, object?>>();
    }

    public override Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = true;
        ConnectCallCount++;
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        _connected = false;
        CloseCallCount++;
        _messageChannel.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public override async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        _writtenMessages.Add(data);

        // Parse and handle control requests
        try
        {
            var msg = JsonSerializer.Deserialize<Dictionary<string, object?>>(data.Trim());
            if (msg?.GetValueOrDefault("type")?.ToString() == "control_request")
            {
                await HandleControlRequestAsync(msg);
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, ignore
        }
    }

    private async Task HandleControlRequestAsync(Dictionary<string, object?> msg)
    {
        var requestId = msg.GetValueOrDefault("request_id")?.ToString();
        var request = msg.GetValueOrDefault("request");

        if (request is not JsonElement requestElement)
        {
            return;
        }

        var subtype = requestElement.TryGetProperty("subtype", out var subtypeElement)
            ? subtypeElement.GetString()
            : null;

        if (subtype == "initialize" && AutoRespondToInitialize)
        {
            await EnqueueMessageAsync(new Dictionary<string, object?>
            {
                ["type"] = "control_response",
                ["response"] = new Dictionary<string, object?>
                {
                    ["request_id"] = requestId,
                    ["subtype"] = "success",
                    ["response"] = new Dictionary<string, object?>
                    {
                        ["commands"] = new List<object>(),
                        ["output_style"] = "default"
                    }
                }
            });
        }
        else if (subtype == "interrupt" && AutoRespondToInterrupt)
        {
            await EnqueueMessageAsync(new Dictionary<string, object?>
            {
                ["type"] = "control_response",
                ["response"] = new Dictionary<string, object?>
                {
                    ["request_id"] = requestId,
                    ["subtype"] = "success"
                }
            });
        }
        else if (subtype == "set_permission_mode" || subtype == "set_model" || subtype == "rewind_files")
        {
            // Auto-respond to other control requests
            await EnqueueMessageAsync(new Dictionary<string, object?>
            {
                ["type"] = "control_response",
                ["response"] = new Dictionary<string, object?>
                {
                    ["request_id"] = requestId,
                    ["subtype"] = "success"
                }
            });
        }
    }

    /// <summary>
    /// Enqueue a message to be read by ReadMessagesAsync.
    /// </summary>
    public async Task EnqueueMessageAsync(Dictionary<string, object?> message)
    {
        await _messageChannel.Writer.WriteAsync(message);
    }

    /// <summary>
    /// Enqueue a message synchronously.
    /// </summary>
    public void EnqueueMessage(Dictionary<string, object?> message)
    {
        _messageChannel.Writer.TryWrite(message);
    }

    /// <summary>
    /// Enqueue an assistant message.
    /// </summary>
    public void EnqueueAssistantMessage(string text)
    {
        EnqueueMessage(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = text }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
    }

    /// <summary>
    /// Enqueue a user message.
    /// </summary>
    public void EnqueueUserMessage(string content)
    {
        EnqueueMessage(new Dictionary<string, object?>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = content
            }
        });
    }

    /// <summary>
    /// Enqueue a result message.
    /// </summary>
    public void EnqueueResultMessage(string subtype = "success", bool isError = false)
    {
        EnqueueMessage(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = subtype,
            ["duration_ms"] = 1000,
            ["duration_api_ms"] = 800,
            ["is_error"] = isError,
            ["num_turns"] = 1,
            ["session_id"] = "test-session",
            ["total_cost_usd"] = 0.001
        });
    }

    public override Task EndInputAsync() => Task.CompletedTask;

    public override async IAsyncEnumerable<Dictionary<string, object?>> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var msg in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return msg;
        }
    }

    public override bool IsReady => _connected;

    /// <summary>
    /// Find a written message that matches a predicate.
    /// </summary>
    public string? FindWrittenMessage(Func<Dictionary<string, object?>, bool> predicate)
    {
        foreach (var msgStr in _writtenMessages)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<Dictionary<string, object?>>(msgStr.Trim());
                if (msg != null && predicate(msg))
                {
                    return msgStr;
                }
            }
            catch (JsonException)
            {
                // Ignore
            }
        }
        return null;
    }

    /// <summary>
    /// Check if a user message with specific content was written.
    /// </summary>
    public bool HasUserMessage(string content)
    {
        return FindWrittenMessage(msg =>
        {
            if (msg.GetValueOrDefault("type")?.ToString() != "user")
                return false;

            if (msg.GetValueOrDefault("message") is not JsonElement messageElement)
                return false;

            if (messageElement.TryGetProperty("content", out var contentElement))
            {
                return contentElement.GetString() == content;
            }

            return false;
        }) != null;
    }

    /// <summary>
    /// Check if a control request with specific subtype was written.
    /// </summary>
    public bool HasControlRequest(string subtype)
    {
        return FindWrittenMessage(msg =>
        {
            if (msg.GetValueOrDefault("type")?.ToString() != "control_request")
                return false;

            if (msg.GetValueOrDefault("request") is not JsonElement requestElement)
                return false;

            if (requestElement.TryGetProperty("subtype", out var subtypeElement))
            {
                return subtypeElement.GetString() == subtype;
            }

            return false;
        }) != null;
    }

    /// <summary>
    /// Get session ID from a written user message.
    /// </summary>
    public string? GetSessionIdFromUserMessage()
    {
        foreach (var msgStr in _writtenMessages)
        {
            try
            {
                var msg = JsonSerializer.Deserialize<Dictionary<string, object?>>(msgStr.Trim());
                if (msg?.GetValueOrDefault("type")?.ToString() == "user")
                {
                    if (msg.GetValueOrDefault("session_id") is JsonElement sessionElement)
                    {
                        return sessionElement.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore
            }
        }
        return null;
    }
}
