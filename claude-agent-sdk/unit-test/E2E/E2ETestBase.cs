using System.Diagnostics;
using ClaudeAgentSdk;

namespace UnitTest.E2E;

/// <summary>
/// Base class for E2E tests that require Claude CLI access.
/// Supports both API key authentication and logged-in sessions.
/// </summary>
public abstract class E2ETestBase
{
    /// <summary>
    /// Gets the project root directory (claude-agent-sdk folder).
    /// Traverses upward from the test assembly location to find the project root.
    /// </summary>
    protected static string ProjectRoot { get; } = FindProjectRoot();

    /// <summary>
    /// Default model for E2E tests - uses Haiku for cost efficiency.
    /// Override with E2E_TEST_MODEL environment variable if needed.
    /// </summary>
    protected static string DefaultTestModel { get; } =
        Environment.GetEnvironmentVariable("E2E_TEST_MODEL")
        ?? "claude-haiku-4-5-20251001";

    private static string FindProjectRoot()
    {
        // Start from the directory containing the test assembly
        var dir = AppContext.BaseDirectory;

        // Traverse up to find the directory containing claude-agent-sdk.csproj
        while (!string.IsNullOrEmpty(dir))
        {
            var csprojPath = Path.Combine(dir, "claude-agent-sdk", "claude-agent-sdk.csproj");
            if (File.Exists(csprojPath))
            {
                return dir;
            }

            // Also check if we're already in the project folder
            csprojPath = Path.Combine(dir, "claude-agent-sdk.csproj");
            if (File.Exists(csprojPath))
            {
                return Path.GetDirectoryName(dir) ?? dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        // Fallback to current directory if not found
        return Directory.GetCurrentDirectory();
    }

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
        return IsClaudeCliLoggedIn();
    }

    /// <summary>
    /// Skips the test if E2E tests cannot be run.
    /// Throws SkipException to mark test as skipped (not failed, not passed).
    /// </summary>
    protected static void SkipIfCannotRunE2E()
    {
        if (!CanRunE2ETests())
        {
            Assert.Skip("E2E tests require ANTHROPIC_API_KEY environment variable or Claude CLI logged in");
        }
    }

    /// <summary>
    /// Cache for IsClaudeCliLoggedIn result to avoid multiple API calls.
    /// </summary>
    private static bool? _isClaudeCliLoggedInCache;

    /// <summary>
    /// Checks if Claude CLI is installed and logged in by attempting a simple request.
    /// Results are cached to minimize API calls during test runs.
    /// </summary>
    private static bool IsClaudeCliLoggedIn()
    {
        // Return cached result if available
        if (_isClaudeCliLoggedInCache.HasValue)
        {
            return _isClaudeCliLoggedInCache.Value;
        }

        try
        {
            var claudePath = GetClaudeCliPath();
            var startInfo = new ProcessStartInfo
            {
                FileName = claudePath,
                // Use Haiku model for cost efficiency
                Arguments = "-p \"Say only: OK\" --max-turns 1 --model claude-haiku-4-5-20251001",
                UseShellExecute = true,  // Required for .cmd files on Windows
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _isClaudeCliLoggedInCache = false;
                return false;
            }

            process.WaitForExit(30000);
            _isClaudeCliLoggedInCache = process.ExitCode == 0;
            return _isClaudeCliLoggedInCache.Value;
        }
        catch
        {
            _isClaudeCliLoggedInCache = false;
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

        // On Windows, claude is typically installed as a .cmd file via npm
        // Use "claude" without extension to let Windows resolve it
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
    protected static async Task<List<Message>> CollectMessagesAsync(ClaudeSDKClient client, CancellationToken ct = default)
    {
        var messages = new List<Message>();
        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            messages.Add(message);
        }
        return messages;
    }
}

