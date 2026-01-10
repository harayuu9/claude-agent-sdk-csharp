using ClaudeAgentSdk;
using unit_test.Helpers;

namespace unit_test;

/// <summary>
/// Tests for ClaudeSDKClient streaming functionality.
/// Ported from Python test_streaming_client.py.
/// </summary>
public class ClaudeSDKClientStreamingTests
{
    /// <summary>
    /// Test automatic connection when using async disposable pattern.
    /// Python: test_auto_connect_with_context_manager
    /// </summary>
    [Fact]
    public async Task AutoConnectWithAsyncUsing()
    {
        var transport = new StreamingMockTransport();

        await using (var client = new ClaudeSDKClient(transport: transport))
        {
            await client.ConnectAsync();

            // Verify connect was called
            Assert.Equal(1, transport.ConnectCallCount);
            Assert.True(client.IsConnected);
        }

        // Verify disconnect was called on exit
        Assert.Equal(1, transport.CloseCallCount);
    }

    /// <summary>
    /// Test manual connect and disconnect.
    /// Python: test_manual_connect_disconnect
    /// </summary>
    [Fact]
    public async Task ManualConnectDisconnect()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);

        await client.ConnectAsync();

        // Verify connect was called
        Assert.Equal(1, transport.ConnectCallCount);
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();

        // Verify disconnect was called
        Assert.Equal(1, transport.CloseCallCount);
        Assert.False(client.IsConnected);
    }

    /// <summary>
    /// Test connecting with a string prompt.
    /// Python: test_connect_with_string_prompt
    /// </summary>
    [Fact]
    public async Task ConnectWithStringPrompt()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);

        await client.ConnectAsync("Hello Claude");

        // Verify connected
        Assert.True(client.IsConnected);
        Assert.Equal(1, transport.ConnectCallCount);
    }

    /// <summary>
    /// Test connecting with an async enumerable.
    /// Python: test_connect_with_async_iterable
    /// </summary>
    [Fact]
    public async Task ConnectWithAsyncEnumerable()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);

        async IAsyncEnumerable<Dictionary<string, object?>> MessageStream()
        {
            yield return new Dictionary<string, object?>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = "Hi"
                }
            };
            await Task.Yield();
            yield return new Dictionary<string, object?>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = "Bye"
                }
            };
        }

        await client.ConnectAsync(MessageStream());

        // Verify connected
        Assert.True(client.IsConnected);
    }

    /// <summary>
    /// Test sending a query.
    /// Python: test_query
    /// </summary>
    [Fact]
    public async Task Query_SendsUserMessage()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        await client.QueryAsync("Test message");

        // Verify write was called with correct format
        // Should have at least 2 writes: init request and user message
        Assert.True(transport.WrittenMessages.Count >= 2);

        // Verify user message was sent
        Assert.True(transport.HasUserMessage("Test message"));
    }

    /// <summary>
    /// Test sending a message with custom session ID.
    /// Python: test_send_message_with_session_id
    /// </summary>
    [Fact]
    public async Task QueryWithSessionId()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        await client.QueryAsync("Test", sessionId: "custom-session");

        // Find user message with session_id
        var sessionId = transport.GetSessionIdFromUserMessage();
        Assert.Equal("custom-session", sessionId);
    }

    /// <summary>
    /// Test sending message when not connected raises error.
    /// Python: test_send_message_not_connected
    /// </summary>
    [Fact]
    public async Task QueryNotConnected_ThrowsException()
    {
        var client = new ClaudeSDKClient();

        await Assert.ThrowsAsync<CLIConnectionException>(async () =>
        {
            await client.QueryAsync("Test");
        });
    }

    /// <summary>
    /// Test receiving messages.
    /// Python: test_receive_messages
    /// </summary>
    [Fact]
    public async Task ReceiveMessages()
    {
        var transport = new StreamingMockTransport();

        // Queue messages to be received
        transport.EnqueueAssistantMessage("Hello!");
        transport.EnqueueUserMessage("Hi there");

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        var messages = new List<Message>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var msg in client.ReceiveMessagesAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(2, messages.Count);
        Assert.IsType<AssistantMessage>(messages[0]);
        var assistantMsg = (AssistantMessage)messages[0];
        Assert.IsType<TextBlock>(assistantMsg.Content[0]);
        Assert.Equal("Hello!", ((TextBlock)assistantMsg.Content[0]).Text);

        Assert.IsType<UserMessage>(messages[1]);
        var userMsg = (UserMessage)messages[1];
        Assert.Equal("Hi there", userMsg.Content);
    }

    /// <summary>
    /// Test receive_response stops at ResultMessage.
    /// Python: test_receive_response
    /// </summary>
    [Fact]
    public async Task ReceiveResponse_StopsAtResultMessage()
    {
        var transport = new StreamingMockTransport();

        // Queue messages: assistant -> result -> assistant (should not see)
        transport.EnqueueAssistantMessage("Answer");
        transport.EnqueueResultMessage();
        transport.EnqueueAssistantMessage("Should not see this");

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        var messages = new List<Message>();
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            messages.Add(msg);
        }

        // Should only get 2 messages (assistant + result)
        Assert.Equal(2, messages.Count);
        Assert.IsType<AssistantMessage>(messages[0]);
        Assert.IsType<ResultMessage>(messages[1]);
    }

    /// <summary>
    /// Test interrupt functionality.
    /// Python: test_interrupt
    /// </summary>
    [Fact]
    public async Task Interrupt_SendsControlRequest()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        await client.InterruptAsync();

        // Check that an interrupt control request was sent
        Assert.True(transport.HasControlRequest("interrupt"));
    }

    /// <summary>
    /// Test interrupt when not connected raises error.
    /// Python: test_interrupt_not_connected
    /// </summary>
    [Fact]
    public async Task InterruptNotConnected_ThrowsException()
    {
        var client = new ClaudeSDKClient();

        await Assert.ThrowsAsync<CLIConnectionException>(async () =>
        {
            await client.InterruptAsync();
        });
    }

    /// <summary>
    /// Test client initialization with options.
    /// Python: test_client_with_options
    /// </summary>
    [Fact]
    public async Task ClientWithOptions()
    {
        var options = new ClaudeAgentOptions
        {
            Cwd = "/custom/path",
            AllowedTools = ["Read", "Write"],
            SystemPrompt = "Be helpful"
        };

        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(options: options, transport: transport);

        await client.ConnectAsync();

        // Verify connected
        Assert.True(client.IsConnected);
        Assert.Equal(1, transport.ConnectCallCount);
    }

    /// <summary>
    /// Test concurrent sending and receiving messages.
    /// Python: test_concurrent_send_receive
    /// </summary>
    [Fact]
    public async Task ConcurrentSendReceive()
    {
        var transport = new StreamingMockTransport();

        // Queue messages to be received (delayed)
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            transport.EnqueueAssistantMessage("Response 1");
            await Task.Delay(100);
            transport.EnqueueResultMessage();
        });

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        // Start receiving in background
        var receiveTask = Task.Run(async () =>
        {
            await foreach (var msg in client.ReceiveResponseAsync())
            {
                return msg;
            }
            return null;
        });

        // Send message while receiving
        await client.QueryAsync("Question 1");

        // Wait for first message
        var firstMsg = await receiveTask;
        Assert.NotNull(firstMsg);
        Assert.IsType<AssistantMessage>(firstMsg);
    }
}

/// <summary>
/// Test edge cases and error scenarios.
/// Ported from Python TestClaudeSDKClientEdgeCases.
/// </summary>
public class ClaudeSDKClientEdgeCasesTests
{
    /// <summary>
    /// Test receiving messages when not connected.
    /// Python: test_receive_messages_not_connected
    /// </summary>
    [Fact]
    public async Task ReceiveMessagesNotConnected_ThrowsException()
    {
        var client = new ClaudeSDKClient();

        await Assert.ThrowsAsync<CLIConnectionException>(async () =>
        {
            await foreach (var _ in client.ReceiveMessagesAsync())
            {
                // Should not reach here
            }
        });
    }

    /// <summary>
    /// Test receive_response when not connected.
    /// Python: test_receive_response_not_connected
    /// </summary>
    [Fact]
    public async Task ReceiveResponseNotConnected_ThrowsException()
    {
        var client = new ClaudeSDKClient();

        await Assert.ThrowsAsync<CLIConnectionException>(async () =>
        {
            await foreach (var _ in client.ReceiveResponseAsync())
            {
                // Should not reach here
            }
        });
    }

    /// <summary>
    /// Test connecting twice throws exception.
    /// Python: test_double_connect (behavior differs - C# throws, Python allows)
    /// </summary>
    [Fact]
    public async Task DoubleConnect_ThrowsException()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);

        await client.ConnectAsync();

        // Second connect should throw
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.ConnectAsync();
        });
    }

    /// <summary>
    /// Test disconnecting without connecting first.
    /// Python: test_disconnect_without_connect
    /// </summary>
    [Fact]
    public async Task DisconnectWithoutConnect_NoException()
    {
        var client = new ClaudeSDKClient();

        // Should not raise error
        await client.DisconnectAsync();
    }

    /// <summary>
    /// Test context manager cleans up on exception.
    /// Python: test_context_manager_with_exception
    /// </summary>
    [Fact]
    public async Task AsyncUsingWithException_CleansUp()
    {
        var transport = new StreamingMockTransport();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using (var client = new ClaudeSDKClient(transport: transport))
            {
                await client.ConnectAsync();
                throw new InvalidOperationException("Test error");
            }
        });

        // Disconnect should still be called
        Assert.Equal(1, transport.CloseCallCount);
    }

    /// <summary>
    /// Test collecting messages with list comprehension pattern.
    /// Python: test_receive_response_list_comprehension
    /// </summary>
    [Fact]
    public async Task ReceiveResponseToList()
    {
        var transport = new StreamingMockTransport();

        // Queue messages
        transport.EnqueueAssistantMessage("Hello");
        transport.EnqueueAssistantMessage("World");
        transport.EnqueueResultMessage();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        // Test list comprehension pattern from docstring
        var messages = new List<Message>();
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            messages.Add(msg);
        }

        Assert.Equal(3, messages.Count);
        Assert.All(messages, msg => Assert.True(msg is AssistantMessage or ResultMessage));
        Assert.IsType<ResultMessage>(messages[^1]);
    }

    /// <summary>
    /// Test SetPermissionModeAsync sends control request.
    /// </summary>
    [Fact]
    public async Task SetPermissionMode_SendsControlRequest()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        await client.SetPermissionModeAsync(PermissionMode.AcceptEdits);

        Assert.True(transport.HasControlRequest("set_permission_mode"));
    }

    /// <summary>
    /// Test SetModelAsync sends control request.
    /// </summary>
    [Fact]
    public async Task SetModel_SendsControlRequest()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        await client.SetModelAsync("claude-sonnet-4-20250514");

        Assert.True(transport.HasControlRequest("set_model"));
    }

    /// <summary>
    /// Test RewindFilesAsync sends control request.
    /// </summary>
    [Fact]
    public async Task RewindFiles_SendsControlRequest()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        await client.RewindFilesAsync("user-message-id-123");

        Assert.True(transport.HasControlRequest("rewind_files"));
    }

    /// <summary>
    /// Test ServerInfo is available after connection.
    /// </summary>
    [Fact]
    public async Task ServerInfo_AvailableAfterConnect()
    {
        var transport = new StreamingMockTransport();

        await using var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync();

        var serverInfo = client.GetServerInfo();
        Assert.NotNull(serverInfo);
    }
}
