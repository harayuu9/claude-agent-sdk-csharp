namespace ClaudeAgentSdk.Internal.Transport;

/// <summary>
/// Abstract transport for Claude communication.
///
/// WARNING: This internal API is exposed for custom transport implementations
/// (e.g., remote Claude Code connections). The Claude Code team may change or
/// remove this abstract class in any future release. Custom implementations
/// must be updated to match interface changes.
///
/// This is a low-level transport interface that handles raw I/O with the Claude
/// process or service. The Query class builds on top of this to implement the
/// control protocol and message routing.
/// </summary>
public abstract class Transport
{
    /// <summary>
    /// Connect the transport and prepare for communication.
    /// For subprocess transports, this starts the process.
    /// For network transports, this establishes the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write raw data to the transport.
    /// </summary>
    /// <param name="data">Raw string data to write (typically JSON + newline).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public abstract Task WriteAsync(string data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read and parse messages from the transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed JSON messages from the transport.</returns>
    public abstract IAsyncEnumerable<Dictionary<string, object?>> ReadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Close the transport connection and clean up resources.
    /// </summary>
    public abstract Task CloseAsync();

    /// <summary>
    /// Check if transport is ready for communication.
    /// </summary>
    public abstract bool IsReady { get; }

    /// <summary>
    /// End the input stream (close stdin for process transports).
    /// </summary>
    public abstract Task EndInputAsync();
}
