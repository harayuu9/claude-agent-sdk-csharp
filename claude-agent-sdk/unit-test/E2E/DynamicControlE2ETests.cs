using ClaudeAgentSdk;

namespace UnitTest.E2E;

/// <summary>
/// End-to-end tests for dynamic control features with real Claude API calls.
/// Equivalent to Python e2e-tests/test_dynamic_control.py
/// </summary>
[Trait("Category", "E2E")]
public class DynamicControlE2ETests : E2ETestBase
{
    /// <summary>
    /// Test that permission mode can be changed dynamically during a session.
    /// Equivalent to Python test_set_permission_mode.
    /// </summary>
    [Fact]
    public async Task SetPermissionMode()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions
        {
            PermissionMode = PermissionMode.Default
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        // Change permission mode to acceptEdits
        await client.SetPermissionModeAsync(PermissionMode.AcceptEdits);

        // Make a query that would normally require permission
        await client.QueryAsync("What is 2+2? Just respond with the number.");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Change back to default
        await client.SetPermissionModeAsync(PermissionMode.Default);

        // Make another query
        await client.QueryAsync("What is 3+3? Just respond with the number.");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }
    }

    /// <summary>
    /// Test that model can be changed dynamically during a session.
    /// Equivalent to Python test_set_model.
    /// </summary>
    [Fact]
    public async Task SetModel()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions();

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        // Start with default model
        await client.QueryAsync("What is 1+1? Just the number.");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Consume default model response
        }

        // Switch to Haiku model
        await client.SetModelAsync("claude-3-5-haiku-20241022");

        await client.QueryAsync("What is 2+2? Just the number.");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Consume Haiku model response
        }

        // Switch back to default (null means default)
        await client.SetModelAsync(null);

        await client.QueryAsync("What is 3+3? Just the number.");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Consume response
        }
    }

    /// <summary>
    /// Test that interrupt can be sent during a session.
    /// Equivalent to Python test_interrupt.
    /// </summary>
    [Fact]
    public async Task Interrupt()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions();

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        // Start a query
        await client.QueryAsync("Count from 1 to 100 slowly.");

        // Send interrupt (may or may not stop the response depending on timing)
        try
        {
            await client.InterruptAsync();
        }
        catch (Exception)
        {
            // Interrupt may throw depending on timing, which is expected
        }

        // Consume any remaining messages
        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages after interrupt
        }
    }
}
