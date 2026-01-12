namespace ClaudeAgentSdk;

/// <summary>
/// Base exception for all Claude SDK errors.
/// </summary>
public class ClaudeSDKException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeSDKException"/> class.
    /// </summary>
    public ClaudeSDKException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeSDKException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ClaudeSDKException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeSDKException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ClaudeSDKException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when unable to connect to Claude Code.
/// </summary>
public class CLIConnectionException : ClaudeSDKException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CLIConnectionException"/> class.
    /// </summary>
    public CLIConnectionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CLIConnectionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CLIConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CLIConnectionException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CLIConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Raised when Claude Code is not found or not installed.
/// </summary>
public class CLINotFoundException : CLIConnectionException
{
    /// <summary>
    /// Gets the path to the CLI that was not found.
    /// </summary>
    public string? CliPath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CLINotFoundException"/> class with an optional message and CLI path.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="cliPath">The path to the CLI that was not found.</param>
    public CLINotFoundException(string message = "Claude Code not found", string? cliPath = null)
        : base(cliPath != null ? $"{message}: {cliPath}" : message)
    {
        CliPath = cliPath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CLINotFoundException"/> class with a message, CLI path, and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="cliPath">The path to the CLI that was not found.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
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
    /// <summary>
    /// Gets the exit code of the process, if available.
    /// </summary>
    public int? ExitCode { get; }

    /// <summary>
    /// Gets the standard error output from the process, if available.
    /// </summary>
    public string? Stderr { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessException"/> class with a message, exit code, and stderr.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="exitCode">The exit code of the process.</param>
    /// <param name="stderr">The standard error output from the process.</param>
    public ProcessException(string message, int? exitCode = null, string? stderr = null)
        : base(BuildMessage(message, exitCode, stderr))
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessException"/> class with a message, exit code, stderr, and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="exitCode">The exit code of the process.</param>
    /// <param name="stderr">The standard error output from the process.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
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
    /// <summary>
    /// Gets the line that failed to decode.
    /// </summary>
    public string Line { get; }

    /// <summary>
    /// Gets the original exception that caused the decode failure.
    /// </summary>
    public Exception OriginalError { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CLIJSONDecodeException"/> class with the line that failed to decode.
    /// </summary>
    /// <param name="line">The line that failed to decode.</param>
    /// <param name="originalError">The original exception that caused the decode failure.</param>
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
    /// <summary>
    /// Gets the raw data that failed to parse.
    /// </summary>
    public new Dictionary<string, object?>? Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageParseException"/> class with a message and optional data.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="data">The raw data that failed to parse.</param>
    public MessageParseException(string message, Dictionary<string, object?>? data = null)
        : base(message)
    {
        Data = data;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageParseException"/> class with a message, data, and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="data">The raw data that failed to parse.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MessageParseException(string message, Dictionary<string, object?>? data, Exception innerException)
        : base(message, innerException)
    {
        Data = data;
    }
}
