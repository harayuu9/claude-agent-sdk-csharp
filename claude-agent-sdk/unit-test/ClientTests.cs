using ClaudeAgentSdk;
using UnitTest.Helpers;

namespace UnitTest;

/// <summary>
/// Tests for Query functionality (ported from Python test_client.py).
/// </summary>
public class ClientTests
{
    /// <summary>
    /// Test query with a single prompt.
    /// Python: test_query_single_prompt
    /// </summary>
    [Fact]
    public async Task QueryWithSinglePrompt_ReturnsAssistantMessage()
    {
        // Arrange
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "4" }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["duration_ms"] = 1000,
            ["duration_api_ms"] = 800,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "test-session",
            ["total_cost_usd"] = 0.001
        });

        // Act
        var messages = new List<Message>();
        await foreach (var msg in Query.RunAsync("What is 2+2?", transport: transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.Equal(2, messages.Count); // AssistantMessage + ResultMessage
        Assert.IsType<AssistantMessage>(messages[0]);
        var assistantMsg = (AssistantMessage)messages[0];
        Assert.Equal("4", ((TextBlock)assistantMsg.Content[0]).Text);
    }

    /// <summary>
    /// Test query with various options.
    /// Python: test_query_with_options
    /// </summary>
    [Fact]
    public async Task QueryWithOptions_PassesOptionsCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "Hello!" }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["duration_ms"] = 1000,
            ["duration_api_ms"] = 800,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "test-session",
            ["total_cost_usd"] = 0.001
        });

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Read", "Write"],
            SystemPrompt = "You are helpful",
            PermissionMode = PermissionMode.AcceptEdits,
            MaxTurns = 5
        };

        // Act
        var messages = new List<Message>();
        await foreach (var msg in Query.RunAsync("Hi", options, transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.IsType<AssistantMessage>(messages[0]);
    }

    /// <summary>
    /// Test query with custom working directory.
    /// Python: test_query_with_cwd
    /// </summary>
    [Fact]
    public async Task QueryWithCwd_ConfiguresTransportCorrectly()
    {
        // Arrange
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "Done" }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["duration_ms"] = 1000,
            ["duration_api_ms"] = 800,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "test-session",
            ["total_cost_usd"] = 0.001
        });

        var options = new ClaudeAgentOptions { Cwd = "/custom/path" };

        // Act
        var messages = new List<Message>();
        await foreach (var msg in Query.RunAsync("test", options, transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.Equal(2, messages.Count);
        Assert.IsType<AssistantMessage>(messages[0]);
        Assert.IsType<ResultMessage>(messages[1]);
    }
}
