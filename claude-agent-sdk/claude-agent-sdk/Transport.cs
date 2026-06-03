namespace ClaudeAgentSdk;

/// <summary>
/// Abstract transport for Claude communication.
///
/// This is a supported extension point. Implement it to bridge Claude Code's
/// <c>stream-json</c> protocol over a custom channel — for example a remote
/// Claude Code instance relayed via WebSocket/vsock — so that local and remote
/// execution converge on the same <see cref="Message"/> stream. Pass an instance
/// to <see cref="ClaudeAgent"/>, <see cref="Query"/>, or <see cref="ClaudeSDKClient"/>.
///
/// Implementations are expected to:
/// <list type="bullet">
///   <item>emit raw JSON message dictionaries from <see cref="ReadMessagesAsync"/>
///   (see <see cref="StreamJsonParser"/> for the canonical stream-json parsing helper),</item>
///   <item>accept newline-delimited JSON via <see cref="WriteAsync"/>,</item>
///   <item>and signal end-of-input via <see cref="EndInputAsync"/>.</item>
/// </list>
///
/// This is a low-level transport interface that handles raw I/O with the Claude
/// process or service. The <c>Query</c> class builds on top of this to implement
/// the control protocol and message routing.
///
/// API stability: this contract follows the package's semantic versioning;
/// breaking changes occur only on a major (or documented preview) version bump.
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
