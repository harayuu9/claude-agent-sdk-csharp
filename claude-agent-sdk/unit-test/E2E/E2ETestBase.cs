using System.Diagnostics;
using ClaudeAgentSdk;

namespace unit_test.E2E;

/// <summary>
/// Base class for E2E tests that require Claude CLI access.
/// Supports both API key authentication and logged-in sessions.
/// </summary>
public abstract class E2ETestBase
{
    /// <summary>
    /// Checks if E2E tests can be run (API key set or CLI logged in).
    /// </summary>
    protected static bool CanRunE2ETests()
    {
        // Check for API key first
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            return true;
        }

        // Check if Claude CLI is available and logged in
        return IsClaudeCliAvailable();
    }

    /// <summary>
    /// Checks if E2E tests should be skipped.
    /// Returns true if tests should be skipped.
    /// </summary>
    protected static bool ShouldSkipE2E(out string reason)
    {
        if (!CanRunE2ETests())
        {
            reason = "E2E tests require ANTHROPIC_API_KEY environment variable or Claude CLI logged in (run 'claude auth')";
            return true;
        }
        reason = "";
        return false;
    }

    /// <summary>
    /// Checks if Claude CLI is installed and accessible.
    /// </summary>
    private static bool IsClaudeCliAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetClaudeCliPath(),
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the path to the Claude CLI executable.
    /// </summary>
    private static string GetClaudeCliPath()
    {
        // Check for custom path first
        var customPath = Environment.GetEnvironmentVariable("CLAUDE_CLI_PATH");
        if (!string.IsNullOrEmpty(customPath))
        {
            return customPath;
        }

        // Default paths based on platform
        if (OperatingSystem.IsWindows())
        {
            return "claude.exe";
        }

        return "claude";
    }

    /// <summary>
    /// Creates ClaudeAgentOptions with common E2E test settings.
    /// </summary>
    protected static ClaudeAgentOptions CreateE2EOptions(Action<ClaudeAgentOptions>? configure = null)
    {
        var options = new ClaudeAgentOptions();
        configure?.Invoke(options);
        return options;
    }

    /// <summary>
    /// Collects all messages from a ClaudeSDKClient response until ResultMessage.
    /// </summary>
    protected static async Task<List<Message>> CollectMessagesAsync(ClaudeSDKClient client)
    {
        var messages = new List<Message>();
        await foreach (var message in client.ReceiveResponseAsync())
        {
            messages.Add(message);
        }
        return messages;
    }
}

