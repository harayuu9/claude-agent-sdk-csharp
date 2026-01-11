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

        var ct = TestContext.Current.CancellationToken;
        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(ct: ct);

        // Change permission mode to acceptEdits
        await client.SetPermissionModeAsync(PermissionMode.AcceptEdits, ct);

        // Make a query that would normally require permission
        await client.QueryAsync("What is 2+2? Just respond with the number.", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Just consume messages
        }

        // Change back to default
        await client.SetPermissionModeAsync(PermissionMode.Default, ct);

        // Make another query
        await client.QueryAsync("What is 3+3? Just respond with the number.", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
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
        var ct = TestContext.Current.CancellationToken;

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(ct: ct);

        // Start with default model
        await client.QueryAsync("What is 1+1? Just the number.", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Consume default model response
        }

        // Switch to Haiku model
        await client.SetModelAsync("claude-3-5-haiku-20241022", ct);

        await client.QueryAsync("What is 2+2? Just the number.", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Consume Haiku model response
        }

        // Switch back to default (null means default)
        await client.SetModelAsync(null, ct);

        await client.QueryAsync("What is 3+3? Just the number.", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
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
        var ct = TestContext.Current.CancellationToken;

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(ct: ct);

        // Start a query
        await client.QueryAsync("Count from 1 to 100 slowly.", ct: ct);

        // Send interrupt (may or may not stop the response depending on timing)
        try
        {
            await client.InterruptAsync(ct);
        }
        catch (Exception)
        {
            // Interrupt may throw depending on timing, which is expected
        }

        // Consume any remaining messages
        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Just consume messages after interrupt
        }
    }
}
