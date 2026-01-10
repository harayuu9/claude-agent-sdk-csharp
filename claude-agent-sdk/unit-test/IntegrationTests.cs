using ClaudeAgentSdk;
using unit_test.Helpers;

namespace unit_test;

/// <summary>
/// Integration tests for Claude SDK.
/// These tests verify end-to-end functionality with mocked transport responses.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public async Task SimpleQueryResponse()
    {
        // Arrange - Mock messages
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "2 + 2 equals 4" }
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
        await foreach (var msg in Query.RunAsync(prompt: "What is 2 + 2?", transport: transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.Equal(2, messages.Count);

        // Check assistant message
        Assert.IsType<AssistantMessage>(messages[0]);
        var assistantMsg = (AssistantMessage)messages[0];
        Assert.Single(assistantMsg.Content);
        Assert.IsType<TextBlock>(assistantMsg.Content[0]);
        Assert.Equal("2 + 2 equals 4", ((TextBlock)assistantMsg.Content[0]).Text);

        // Check result message
        Assert.IsType<ResultMessage>(messages[1]);
        var resultMsg = (ResultMessage)messages[1];
        Assert.Equal(0.001, resultMsg.TotalCostUsd);
        Assert.Equal("test-session", resultMsg.SessionId);
    }

    [Fact]
    public async Task QueryWithToolUse()
    {
        // Arrange - Mock messages with tool use
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "Let me read that file for you." },
                    new Dictionary<string, object?>
                    {
                        ["type"] = "tool_use",
                        ["id"] = "tool-123",
                        ["name"] = "Read",
                        ["input"] = new Dictionary<string, object?> { ["file_path"] = "/test.txt" }
                    }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["duration_ms"] = 1500,
            ["duration_api_ms"] = 1200,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "test-session-2",
            ["total_cost_usd"] = 0.002
        });

        var options = new ClaudeAgentOptions { AllowedTools = ["Read"] };

        // Act
        var messages = new List<Message>();
        await foreach (var msg in Query.RunAsync(prompt: "Read /test.txt", options: options, transport: transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.Equal(2, messages.Count);

        // Check assistant message with tool use
        Assert.IsType<AssistantMessage>(messages[0]);
        var assistantMsg = (AssistantMessage)messages[0];
        Assert.Equal(2, assistantMsg.Content.Count);
        Assert.IsType<TextBlock>(assistantMsg.Content[0]);
        Assert.Equal("Let me read that file for you.", ((TextBlock)assistantMsg.Content[0]).Text);
        Assert.IsType<ToolUseBlock>(assistantMsg.Content[1]);
        var toolUse = (ToolUseBlock)assistantMsg.Content[1];
        Assert.Equal("Read", toolUse.Name);
        Assert.Equal("/test.txt", toolUse.Input["file_path"]?.ToString());
    }

    [Fact]
    public async Task CliNotFound()
    {
        // Arrange - Use a non-existent CLI path
        var options = new ClaudeAgentOptions
        {
            CliPath = "/non/existent/path/to/claude-cli-that-does-not-exist"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CLINotFoundException>(async () =>
        {
            await foreach (var _ in Query.RunAsync(prompt: "test", options: options))
            {
                // Should not reach here
            }
        });

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ContinuationOption()
    {
        // Arrange - Mock messages for continuation
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "Continuing from previous conversation" }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "success",
            ["duration_ms"] = 500,
            ["duration_api_ms"] = 400,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "test-session-continue",
            ["total_cost_usd"] = 0.001
        });

        var options = new ClaudeAgentOptions
        {
            ContinueConversation = true
        };

        // Act
        var messages = new List<Message>();
        await foreach (var msg in Query.RunAsync(prompt: "Continue", options: options, transport: transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.NotEmpty(messages);
        Assert.IsType<AssistantMessage>(messages[0]);
        var assistantMsg = (AssistantMessage)messages[0];
        Assert.IsType<TextBlock>(assistantMsg.Content[0]);
        Assert.Equal("Continuing from previous conversation", ((TextBlock)assistantMsg.Content[0]).Text);
    }

    [Fact]
    public async Task MaxBudgetUsdOption()
    {
        // Arrange - Mock messages that exceed budget
        var transport = new MockTransport();
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = new List<object>
                {
                    new Dictionary<string, object?> { ["type"] = "text", ["text"] = "Starting to read..." }
                },
                ["model"] = "claude-opus-4-1-20250805"
            }
        });
        transport.MessagesToRead.Add(new Dictionary<string, object?>
        {
            ["type"] = "result",
            ["subtype"] = "error_max_budget_usd",
            ["duration_ms"] = 500,
            ["duration_api_ms"] = 400,
            ["is_error"] = false,
            ["num_turns"] = 1,
            ["session_id"] = "test-session-budget",
            ["total_cost_usd"] = 0.0002,
            ["usage"] = new Dictionary<string, object?>
            {
                ["input_tokens"] = 100,
                ["output_tokens"] = 50
            }
        });

        var options = new ClaudeAgentOptions
        {
            MaxBudgetUsd = 0.0001
        };

        // Act
        var messages = new List<Message>();
        await foreach (var msg in Query.RunAsync(prompt: "Read the readme", options: options, transport: transport))
        {
            messages.Add(msg);
        }

        // Assert
        Assert.Equal(2, messages.Count);

        // Check result message
        Assert.IsType<ResultMessage>(messages[1]);
        var resultMsg = (ResultMessage)messages[1];
        Assert.Equal("error_max_budget_usd", resultMsg.Subtype);
        Assert.False(resultMsg.IsError);
        Assert.Equal(0.0002, resultMsg.TotalCostUsd);
        Assert.NotNull(resultMsg.TotalCostUsd);
        Assert.True(resultMsg.TotalCostUsd > 0);
    }
}
