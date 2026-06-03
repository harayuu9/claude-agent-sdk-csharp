using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk;

/// <summary>
/// Reusable parser for Claude Code's <c>stream-json</c> output.
///
/// It accumulates raw text chunks (subprocess stdout lines, WebSocket/vsock
/// frames, etc.), splits embedded newlines, skips non-JSON lines such as
/// <c>[SandboxDebug]</c> diagnostics, and yields each complete JSON object as a
/// <see cref="Dictionary{TKey,TValue}"/>. Partial JSON is buffered speculatively
/// until it parses or the buffer limit is exceeded.
///
/// This is the canonical parsing helper shared by the built-in subprocess
/// transport and custom <see cref="Transport"/> implementations — for example a
/// transport that relays a remote <c>claude --output-format=stream-json</c>
/// over WebSocket/vsock so that local and remote execution converge on the same
/// <see cref="Message"/> stream.
/// </summary>
public static class StreamJsonParser
{
    /// <summary>
    /// Default speculative-buffer size limit (1 MiB).
    /// </summary>
    public const int DefaultMaxBufferSize = 1024 * 1024;

    /// <summary>
    /// Parse a stream of raw text chunks into <c>stream-json</c> message objects.
    /// </summary>
    /// <param name="chunks">
    /// Raw text chunks from the underlying transport. A chunk may be a full line,
    /// several newline-separated JSON objects, or a fragment of a single object;
    /// chunk boundaries do not need to align with message boundaries.
    /// </param>
    /// <param name="maxBufferSize">
    /// Speculative buffer limit in bytes. When a single buffered JSON message
    /// exceeds it, a <see cref="CLIJSONDecodeException"/> is thrown. When null,
    /// <see cref="DefaultMaxBufferSize"/> is used.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async stream of parsed JSON message dictionaries.</returns>
    /// <exception cref="CLIJSONDecodeException">
    /// Thrown when a buffered JSON message exceeds <paramref name="maxBufferSize"/>.
    /// </exception>
    public static async IAsyncEnumerable<Dictionary<string, object?>> ParseAsync(
        IAsyncEnumerable<string> chunks,
        int? maxBufferSize = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var limit = maxBufferSize ?? DefaultMaxBufferSize;
        var jsonBuffer = new StringBuilder();

        await foreach (var chunk in chunks.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkStr = chunk.Trim();
            if (string.IsNullOrEmpty(chunkStr))
                continue;

            // A chunk may bundle several newline-separated JSON objects.
            var jsonLines = chunkStr.Split('\n');

            foreach (var jsonLine in jsonLines)
            {
                var trimmedLine = jsonLine.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                // Skip non-JSON lines (e.g., [SandboxDebug] output) when not mid-parse.
                if (jsonBuffer.Length == 0 && !trimmedLine.StartsWith('{'))
                {
                    continue;
                }

                // Keep accumulating partial JSON until we can parse it.
                jsonBuffer.Append(trimmedLine);

                if (jsonBuffer.Length > limit)
                {
                    var bufferLength = jsonBuffer.Length;
                    jsonBuffer.Clear();
                    throw new CLIJSONDecodeException(
                        $"JSON message exceeded maximum buffer size of {limit} bytes",
                        new ArgumentException($"Buffer size {bufferLength} exceeds limit {limit}"));
                }

                Dictionary<string, object?>? data = null;
                var parseSucceeded = false;
                try
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonBuffer.ToString());
                    jsonBuffer.Clear();
                    parseSucceeded = true;
                }
                catch (JsonException)
                {
                    // We are speculatively decoding the buffer until we get a full
                    // JSON object. If there is an actual issue, we raise an error
                    // after exceeding the configured limit.
                }

                if (parseSucceeded && data != null)
                {
                    yield return data;
                }
            }
        }
    }
}
