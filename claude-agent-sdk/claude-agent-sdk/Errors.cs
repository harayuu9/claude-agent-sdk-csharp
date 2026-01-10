namespace ClaudeAgentSdk;

/// <summary>
/// Base exception for all Claude SDK errors.
/// </summary>
public class ClaudeSDKException : Exception
{
    public ClaudeSDKException()
    {
    }

    public ClaudeSDKException(string message) : base(message)
    {
    }

    public ClaudeSDKException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when unable to connect to Claude Code.
/// </summary>
public class CLIConnectionException : ClaudeSDKException
{
    public CLIConnectionException()
    {
    }

    public CLIConnectionException(string message) : base(message)
    {
    }

    public CLIConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when Claude Code is not found or not installed.
/// </summary>
public class CLINotFoundException : CLIConnectionException
{
    public string? CliPath { get; }

    public CLINotFoundException(string message = "Claude Code not found", string? cliPath = null)
        : base(cliPath != null ? $"{message}: {cliPath}" : message)
    {
        CliPath = cliPath;
    }

    public CLINotFoundException(string message, string? cliPath, Exception innerException)
        : base(cliPath != null ? $"{message}: {cliPath}" : message, innerException)
    {
        CliPath = cliPath;
    }
}

/// <summary>
/// Raised when the CLI process fails.
/// </summary>
public class ProcessException : ClaudeSDKException
{
    public int? ExitCode { get; }
    public string? Stderr { get; }

    public ProcessException(string message, int? exitCode = null, string? stderr = null)
        : base(BuildMessage(message, exitCode, stderr))
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    public ProcessException(string message, int? exitCode, string? stderr, Exception innerException)
        : base(BuildMessage(message, exitCode, stderr), innerException)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    private static string BuildMessage(string message, int? exitCode, string? stderr)
    {
        if (exitCode.HasValue)
        {
            message = $"{message} (exit code: {exitCode})";
        }
        if (!string.IsNullOrEmpty(stderr))
        {
            message = $"{message}\nError output: {stderr}";
        }
        return message;
    }
}

/// <summary>
/// Raised when unable to decode JSON from CLI output.
/// </summary>
public class CLIJSONDecodeException : ClaudeSDKException
{
    public string Line { get; }
    public Exception OriginalError { get; }

    public CLIJSONDecodeException(string line, Exception originalError)
        : base($"Failed to decode JSON: {(line.Length > 100 ? line[..100] + "..." : line)}", originalError)
    {
        Line = line;
        OriginalError = originalError;
    }
}

/// <summary>
/// Raised when unable to parse a message from CLI output.
/// </summary>
public class MessageParseException : ClaudeSDKException
{
    public new Dictionary<string, object?>? Data { get; }

    public MessageParseException(string message, Dictionary<string, object?>? data = null)
        : base(message)
    {
        Data = data;
    }

    public MessageParseException(string message, Dictionary<string, object?>? data, Exception innerException)
        : base(message, innerException)
    {
        Data = data;
    }
}
