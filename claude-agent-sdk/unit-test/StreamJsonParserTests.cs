using System.Text.Json;
using ClaudeAgentSdk;

namespace UnitTest;

/// <summary>
/// Tests for the reusable <see cref="StreamJsonParser"/>.
/// Mirrors the buffering edge cases covered by <see cref="SubprocessBufferingTests"/>,
/// but drives the parser directly via an IAsyncEnumerable&lt;string&gt; instead of a
/// subprocess transport, plus parser-specific cases (non-JSON skipping, empty input,
/// cancellation).
/// </summary>
public class StreamJsonParserTests
{
    private static async IAsyncEnumerable<string> Chunks(params string[] chunks)
    {
        foreach (var c in chunks)
        {
            yield return c;
        }
        await Task.CompletedTask;
    }

    private static async Task<List<Dictionary<string, object?>>> CollectAsync(
        IAsyncEnumerable<string> chunks, int? maxBufferSize = null)
    {
        var messages = new List<Dictionary<string, object?>>();
        await foreach (var msg in StreamJsonParser.ParseAsync(
                           chunks, maxBufferSize, TestContext.Current.CancellationToken))
        {
            messages.Add(msg);
        }
        return messages;
    }

    [Fact]
    public async Task MultipleJsonObjectsOnSingleChunk()
    {
        var line = JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "message", ["id"] = "msg1" })
                   + "\n"
                   + JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "result", ["id"] = "res1" });

        var messages = await CollectAsync(Chunks(line));

        Assert.Equal(2, messages.Count);
        Assert.Equal("msg1", messages[0]["id"]?.ToString());
        Assert.Equal("res1", messages[1]["id"]?.ToString());
    }

    [Fact]
    public async Task JsonWithEmbeddedNewlines()
    {
        var line = JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "message", ["content"] = "Line 1\nLine 2" })
                   + "\n"
                   + JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "result", ["data"] = "A\nB" });

        var messages = await CollectAsync(Chunks(line));

        Assert.Equal(2, messages.Count);
        Assert.Equal("Line 1\nLine 2", messages[0]["content"]?.ToString());
        Assert.Equal("A\nB", messages[1]["data"]?.ToString());
    }

    [Fact]
    public async Task MultipleNewlinesBetweenObjects()
    {
        var line = JsonSerializer.Serialize(new Dictionary<string, object> { ["id"] = "msg1" })
                   + "\n\n\n"
                   + JsonSerializer.Serialize(new Dictionary<string, object> { ["id"] = "res1" });

        var messages = await CollectAsync(Chunks(line));

        Assert.Equal(2, messages.Count);
        Assert.Equal("msg1", messages[0]["id"]?.ToString());
        Assert.Equal("res1", messages[1]["id"]?.ToString());
    }

    [Fact]
    public async Task SplitJsonAcrossMultipleChunks()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["text"] = new string('x', 1000)
        });

        var messages = await CollectAsync(Chunks(json[..100], json[100..250], json[250..]));

        Assert.Single(messages);
        Assert.Equal("assistant", messages[0]["type"]?.ToString());
    }

    [Fact]
    public async Task SkipsNonJsonLines()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "system" });
        var messages = await CollectAsync(Chunks("[SandboxDebug] starting up", json, "plain log line"));

        Assert.Single(messages);
        Assert.Equal("system", messages[0]["type"]?.ToString());
    }

    [Fact]
    public async Task EmptyInputYieldsNothing()
    {
        var messages = await CollectAsync(Chunks("", "   ", "\n"));
        Assert.Empty(messages);
    }

    [Fact]
    public async Task BufferSizeExceededThrows()
    {
        var hugeIncomplete = "{\"data\": \"" + new string('x', StreamJsonParser.DefaultMaxBufferSize + 1000);

        var ex = await Assert.ThrowsAsync<CLIJSONDecodeException>(async () =>
        {
            await foreach (var _ in StreamJsonParser.ParseAsync(
                               Chunks(hugeIncomplete), null, TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Contains("exceeded maximum buffer size", ex.Message);
    }

    [Fact]
    public async Task CustomBufferSizeRespected()
    {
        const int customLimit = 512;
        var hugeIncomplete = "{\"data\": \"" + new string('x', customLimit + 10);

        var ex = await Assert.ThrowsAsync<CLIJSONDecodeException>(async () =>
        {
            await foreach (var _ in StreamJsonParser.ParseAsync(
                               Chunks(hugeIncomplete), customLimit, TestContext.Current.CancellationToken))
            {
            }
        });

        Assert.Contains($"maximum buffer size of {customLimit} bytes", ex.Message);
    }

    [Fact]
    public async Task PreCancelledTokenThrows()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = "system" });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in StreamJsonParser.ParseAsync(Chunks(json), null, cts.Token))
            {
            }
        });
    }
}
