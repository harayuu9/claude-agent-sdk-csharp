using ClaudeAgentSdk;

namespace unit_test.E2E;

/// <summary>
/// End-to-end tests for stderr callback functionality.
/// Equivalent to Python e2e-tests/test_stderr_callback.py
/// </summary>
[Trait("Category", "E2E")]
public class StderrCallbackE2ETests : E2ETestBase
{
    /// <summary>
    /// Test that stderr callback receives debug output when enabled.
    /// Equivalent to Python test_stderr_callback_captures_debug_output.
    /// </summary>
    [Fact]
    public async Task StderrCallbackCapturesDebugOutput()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var stderrLines = new List<string>();

        void CaptureStderr(string line)
        {
            stderrLines.Add(line);
        }

        // Enable debug mode to generate stderr output
        var options = new ClaudeAgentOptions
        {
            Stderr = CaptureStderr,
            ExtraArgs = new Dictionary<string, string?>
            {
                ["debug-to-stderr"] = null  // Flag without value
            }
        };

        // Run a simple query
        await foreach (var _ in ClaudeAgent.QueryAsync("What is 1+1?", options))
        {
            // Just consume messages
        }

        // Verify we captured debug output
        Assert.NotEmpty(stderrLines);
        Assert.Contains(stderrLines, line => line.Contains("[DEBUG]"));
    }

    /// <summary>
    /// Test that stderr callback works but receives no output without debug mode.
    /// Equivalent to Python test_stderr_callback_without_debug.
    /// </summary>
    [Fact]
    public async Task StderrCallbackWithoutDebug()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var stderrLines = new List<string>();

        void CaptureStderr(string line)
        {
            stderrLines.Add(line);
        }

        // No debug mode enabled
        var options = new ClaudeAgentOptions
        {
            Stderr = CaptureStderr
        };

        // Run a simple query
        await foreach (var _ in ClaudeAgent.QueryAsync("What is 1+1?", options))
        {
            // Just consume messages
        }

        // Should work but capture minimal/no output without debug
        Assert.Empty(stderrLines);
    }
}
