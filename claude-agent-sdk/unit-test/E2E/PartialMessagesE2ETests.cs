using ClaudeAgentSdk;

namespace UnitTest.E2E;

/// <summary>
/// End-to-end tests for include_partial_messages option with real Claude API calls.
/// These tests verify that the SDK properly handles partial message streaming.
/// Equivalent to Python e2e-tests/test_include_partial_messages.py
/// </summary>
[Trait("Category", "E2E")]
public class PartialMessagesE2ETests : E2ETestBase
{
    /// <summary>
    /// Test that include_partial_messages produces StreamEvent messages.
    /// Equivalent to Python test_include_partial_messages_stream_events.
    /// </summary>
    [Fact]
    public async Task IncludePartialMessagesStreamEvents()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions
        {
            IncludePartialMessages = true,
            Model = "claude-sonnet-4-5",
            MaxTurns = 2,
            Env = new Dictionary<string, string>
            {
                ["MAX_THINKING_TOKENS"] = "8000"
            }
        };

        var collectedMessages = new List<Message>();

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("Think of three jokes, then tell one");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            collectedMessages.Add(message);
        }

        // Verify we got the expected message types
        // Should have SystemMessage(init) at the start
        Assert.True(collectedMessages.Count > 0);
        Assert.IsType<SystemMessage>(collectedMessages[0]);
        Assert.Equal("init", ((SystemMessage)collectedMessages[0]).Subtype);

        // Should have multiple StreamEvent messages
        var streamEvents = collectedMessages.OfType<StreamEvent>().ToList();
        Assert.NotEmpty(streamEvents);

        // Check for expected StreamEvent types
        var eventTypes = streamEvents
            .Select(e => e.Event.TryGetValue("type", out var t) ? t?.ToString() : null)
            .Where(t => t != null)
            .ToList();

        Assert.Contains("message_start", eventTypes);
        Assert.Contains("content_block_start", eventTypes);
        Assert.Contains("content_block_delta", eventTypes);
        Assert.Contains("content_block_stop", eventTypes);
        Assert.Contains("message_stop", eventTypes);

        // Should have AssistantMessage messages with thinking and text
        var assistantMessages = collectedMessages.OfType<AssistantMessage>().ToList();
        Assert.NotEmpty(assistantMessages);

        // Check for thinking block in at least one AssistantMessage
        var hasThinking = assistantMessages.Any(msg =>
            msg.Content.Any(block => block is ThinkingBlock));
        Assert.True(hasThinking, "No ThinkingBlock found in AssistantMessages");

        // Check for text block (the joke) in at least one AssistantMessage
        var hasText = assistantMessages.Any(msg =>
            msg.Content.Any(block => block is TextBlock));
        Assert.True(hasText, "No TextBlock found in AssistantMessages");

        // Should end with ResultMessage
        Assert.IsType<ResultMessage>(collectedMessages.Last());
        Assert.Equal("success", ((ResultMessage)collectedMessages.Last()).Subtype);
    }

    /// <summary>
    /// Test that thinking content is streamed incrementally via deltas.
    /// Equivalent to Python test_include_partial_messages_thinking_deltas.
    /// </summary>
    [Fact]
    public async Task IncludePartialMessagesThinkingDeltas()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions
        {
            IncludePartialMessages = true,
            Model = "claude-sonnet-4-5",
            MaxTurns = 2,
            Env = new Dictionary<string, string>
            {
                ["MAX_THINKING_TOKENS"] = "8000"
            }
        };

        var thinkingDeltas = new List<string>();

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("Think step by step about what 2 + 2 equals");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            if (message is StreamEvent streamEvent)
            {
                if (streamEvent.Event.TryGetValue("type", out var eventType) &&
                    eventType?.ToString() == "content_block_delta")
                {
                    if (streamEvent.Event.TryGetValue("delta", out var deltaObj) &&
                        deltaObj is Dictionary<string, object?> delta)
                    {
                        if (delta.TryGetValue("type", out var deltaType) &&
                            deltaType?.ToString() == "thinking_delta" &&
                            delta.TryGetValue("thinking", out var thinking))
                        {
                            thinkingDeltas.Add(thinking?.ToString() ?? "");
                        }
                    }
                }
            }
        }

        // Should have received multiple thinking deltas
        Assert.NotEmpty(thinkingDeltas);

        // Combined thinking should form coherent text
        var combinedThinking = string.Join("", thinkingDeltas);
        Assert.True(combinedThinking.Length > 10, "Thinking content too short");

        // Should contain some reasoning about the calculation
        Assert.Contains("2", combinedThinking.ToLower());
    }

    /// <summary>
    /// Test that partial messages are not included when option is not set.
    /// Equivalent to Python test_partial_messages_disabled_by_default.
    /// </summary>
    [Fact]
    public async Task PartialMessagesDisabledByDefault()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions
        {
            // IncludePartialMessages not set (defaults to false)
            Model = "claude-sonnet-4-5",
            MaxTurns = 2
        };

        var collectedMessages = new List<Message>();

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("Say hello");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            collectedMessages.Add(message);
        }

        // Should NOT have any StreamEvent messages
        var streamEvents = collectedMessages.OfType<StreamEvent>().ToList();
        Assert.Empty(streamEvents);

        // Should still have the regular messages
        Assert.Contains(collectedMessages, msg => msg is SystemMessage);
        Assert.Contains(collectedMessages, msg => msg is AssistantMessage);
        Assert.Contains(collectedMessages, msg => msg is ResultMessage);
    }
}
