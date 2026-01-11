using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Internal.Transport;

namespace UnitTest;

/// <summary>
/// Mock StreamReader for testing that returns predefined lines.
/// </summary>
internal class MockStreamReader : StreamReader
{
    private readonly List<string> _lines;
    private int _index;

    public MockStreamReader(IEnumerable<string> lines) : base(new MemoryStream())
    {
        _lines = lines.ToList();
        _index = 0;
    }

    public override string? ReadLine()
    {
        if (_index >= _lines.Count)
            return null;
        return _lines[_index++];
    }

    public override Task<string?> ReadLineAsync()
    {
        return Task.FromResult(ReadLine());
    }

    public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(ReadLine());
    }
}

/// <summary>
/// Tests for subprocess transport buffering edge cases.
/// </summary>
public class SubprocessBufferingTests
{
    private const string DefaultCliPath = "/usr/bin/claude";

    private static ClaudeAgentOptions MakeOptions(int? maxBufferSize = null)
    {
        return new ClaudeAgentOptions
        {
            CliPath = DefaultCliPath,
            MaxBufferSize = maxBufferSize
        };
    }

    private static (SubprocessCliTransport transport, Process dummyProcess) CreateTransport(
        List<string> stdoutLines,
        List<string> stderrLines,
        ClaudeAgentOptions? options = null)
    {
        options ??= MakeOptions();
        var transport = new SubprocessCliTransport("test", options);

        // Create a dummy process that stays alive during the test
        var dummyProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c exit",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        dummyProcess.Start();

        // Use reflection to set internal state
        var transportType = typeof(SubprocessCliTransport);

        // Set _process field
        var processField = transportType.GetField("_process", BindingFlags.NonPublic | BindingFlags.Instance);
        processField?.SetValue(transport, dummyProcess);

        // Set _stdoutReader field
        var stdoutField = transportType.GetField("_stdoutReader", BindingFlags.NonPublic | BindingFlags.Instance);
        stdoutField?.SetValue(transport, new MockStreamReader(stdoutLines));

        // Set _stderrReader field
        var stderrField = transportType.GetField("_stderrReader", BindingFlags.NonPublic | BindingFlags.Instance);
        stderrField?.SetValue(transport, new MockStreamReader(stderrLines));

        // Set _ready field
        var readyField = transportType.GetField("_ready", BindingFlags.NonPublic | BindingFlags.Instance);
        readyField?.SetValue(transport, true);

        return (transport, dummyProcess);
    }

    private static void CleanupProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(1000);
            }
            process.Dispose();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Test parsing when multiple JSON objects are concatenated on a single line.
    /// In some environments, stdout buffering can cause multiple distinct JSON
    /// objects to be delivered as a single line with embedded newlines.
    /// </summary>
    [Fact]
    public async Task MultipleJsonObjectsOnSingleLine()
    {
        var jsonObj1 = new Dictionary<string, object>
        {
            ["type"] = "message",
            ["id"] = "msg1",
            ["content"] = "First message"
        };
        var jsonObj2 = new Dictionary<string, object>
        {
            ["type"] = "result",
            ["id"] = "res1",
            ["status"] = "completed"
        };

        var bufferedLine = JsonSerializer.Serialize(jsonObj1) + "\n" + JsonSerializer.Serialize(jsonObj2);

        var (transport, process) = CreateTransport([bufferedLine], []);
        try
        {
            var messages = new List<Dictionary<string, object?>>();
            await foreach (var msg in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
            {
                messages.Add(msg);
            }

            Assert.Equal(2, messages.Count);
            Assert.Equal("message", messages[0]["type"]?.ToString());
            Assert.Equal("msg1", messages[0]["id"]?.ToString());
            Assert.Equal("First message", messages[0]["content"]?.ToString());
            Assert.Equal("result", messages[1]["type"]?.ToString());
            Assert.Equal("res1", messages[1]["id"]?.ToString());
            Assert.Equal("completed", messages[1]["status"]?.ToString());
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test parsing JSON objects that contain newline characters in string values.
    /// </summary>
    [Fact]
    public async Task JsonWithEmbeddedNewlines()
    {
        var jsonObj1 = new Dictionary<string, object>
        {
            ["type"] = "message",
            ["content"] = "Line 1\nLine 2\nLine 3"
        };
        var jsonObj2 = new Dictionary<string, object>
        {
            ["type"] = "result",
            ["data"] = "Some\nMultiline\nContent"
        };

        var bufferedLine = JsonSerializer.Serialize(jsonObj1) + "\n" + JsonSerializer.Serialize(jsonObj2);

        var (transport, process) = CreateTransport([bufferedLine], []);
        try
        {
            var messages = new List<Dictionary<string, object?>>();
            await foreach (var msg in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
            {
                messages.Add(msg);
            }

            Assert.Equal(2, messages.Count);
            Assert.Equal("Line 1\nLine 2\nLine 3", messages[0]["content"]?.ToString());
            Assert.Equal("Some\nMultiline\nContent", messages[1]["data"]?.ToString());
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test parsing with multiple newlines between JSON objects.
    /// </summary>
    [Fact]
    public async Task MultipleNewlinesBetweenObjects()
    {
        var jsonObj1 = new Dictionary<string, object>
        {
            ["type"] = "message",
            ["id"] = "msg1"
        };
        var jsonObj2 = new Dictionary<string, object>
        {
            ["type"] = "result",
            ["id"] = "res1"
        };

        var bufferedLine = JsonSerializer.Serialize(jsonObj1) + "\n\n\n" + JsonSerializer.Serialize(jsonObj2);

        var (transport, process) = CreateTransport([bufferedLine], []);
        try
        {
            var messages = new List<Dictionary<string, object?>>();
            await foreach (var msg in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
            {
                messages.Add(msg);
            }

            Assert.Equal(2, messages.Count);
            Assert.Equal("msg1", messages[0]["id"]?.ToString());
            Assert.Equal("res1", messages[1]["id"]?.ToString());
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test parsing when a single JSON object is split across multiple stream reads.
    /// </summary>
    [Fact]
    public async Task SplitJsonAcrossMultipleReads()
    {
        var jsonObj = new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object>
            {
                ["content"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = new string('x', 1000)
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "tool_use",
                        ["id"] = "tool_123",
                        ["name"] = "Read",
                        ["input"] = new Dictionary<string, object>
                        {
                            ["file_path"] = "/test.txt"
                        }
                    }
                }
            }
        };

        var completeJson = JsonSerializer.Serialize(jsonObj);

        var part1 = completeJson[..100];
        var part2 = completeJson[100..250];
        var part3 = completeJson[250..];

        var (transport, process) = CreateTransport([part1, part2, part3], []);
        try
        {
            var messages = new List<Dictionary<string, object?>>();
            await foreach (var msg in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
            {
                messages.Add(msg);
            }

            Assert.Single(messages);
            Assert.Equal("assistant", messages[0]["type"]?.ToString());

            var message = messages[0]["message"];
            Assert.NotNull(message);

            var messageDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(message.ToString()!);
            Assert.NotNull(messageDict);
            Assert.True(messageDict.ContainsKey("content"));
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test parsing a large minified JSON (simulating the reported issue).
    /// </summary>
    [Fact]
    public async Task LargeMinifiedJson()
    {
        var largeData = new Dictionary<string, object>
        {
            ["data"] = Enumerable.Range(0, 1000).Select(i => new Dictionary<string, object>
            {
                ["id"] = i,
                ["value"] = new string('x', 100)
            }).ToList()
        };

        var jsonObj = new Dictionary<string, object>
        {
            ["type"] = "user",
            ["message"] = new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["tool_use_id"] = "toolu_016fed1NhiaMLqnEvrj5NUaj",
                        ["type"] = "tool_result",
                        ["content"] = JsonSerializer.Serialize(largeData)
                    }
                }
            }
        };

        var completeJson = JsonSerializer.Serialize(jsonObj);

        var chunkSize = 64 * 1024;
        var chunks = new List<string>();
        for (var i = 0; i < completeJson.Length; i += chunkSize)
        {
            chunks.Add(completeJson[i..Math.Min(i + chunkSize, completeJson.Length)]);
        }

        var (transport, process) = CreateTransport(chunks, []);
        try
        {
            var messages = new List<Dictionary<string, object?>>();
            await foreach (var msg in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
            {
                messages.Add(msg);
            }

            Assert.Single(messages);
            Assert.Equal("user", messages[0]["type"]?.ToString());

            var message = messages[0]["message"];
            Assert.NotNull(message);

            var messageDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(message.ToString()!);
            Assert.NotNull(messageDict);

            var content = messageDict["content"];
            Assert.NotNull(content);

            var contentList = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(content.ToString()!);
            Assert.NotNull(contentList);
            Assert.Single(contentList);
            Assert.Equal("toolu_016fed1NhiaMLqnEvrj5NUaj", contentList[0]["tool_use_id"]?.ToString());
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test that exceeding buffer size raises an appropriate error.
    /// </summary>
    [Fact]
    public async Task BufferSizeExceeded()
    {
        // Default max buffer size is 1MB (1024 * 1024)
        const int defaultMaxBufferSize = 1024 * 1024;
        var hugeIncomplete = "{\"data\": \"" + new string('x', defaultMaxBufferSize + 1000);

        var (transport, process) = CreateTransport([hugeIncomplete], []);
        try
        {
            var exception = await Assert.ThrowsAsync<CLIJSONDecodeException>(async () =>
            {
                await foreach (var _ in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
                {
                    // Should throw before returning any messages
                }
            });

            Assert.Contains("exceeded maximum buffer size", exception.Message);
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test that the configurable buffer size option is respected.
    /// </summary>
    [Fact]
    public async Task BufferSizeOption()
    {
        const int customLimit = 512;
        var hugeIncomplete = "{\"data\": \"" + new string('x', customLimit + 10);

        var options = MakeOptions(maxBufferSize: customLimit);
        var (transport, process) = CreateTransport([hugeIncomplete], [], options);
        try
        {
            var exception = await Assert.ThrowsAsync<CLIJSONDecodeException>(async () =>
            {
                await foreach (var _ in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
                {
                    // Should throw before returning any messages
                }
            });

            Assert.Contains($"maximum buffer size of {customLimit} bytes", exception.Message);
        }
        finally
        {
            CleanupProcess(process);
        }
    }

    /// <summary>
    /// Test handling a mix of complete and split JSON messages.
    /// </summary>
    [Fact]
    public async Task MixedCompleteAndSplitJson()
    {
        var msg1 = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "system",
            ["subtype"] = "start"
        });

        var largeMsg = new Dictionary<string, object>
        {
            ["type"] = "assistant",
            ["message"] = new Dictionary<string, object>
            {
                ["content"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = new string('y', 5000)
                    }
                }
            }
        };
        var largeJson = JsonSerializer.Serialize(largeMsg);

        var msg3 = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "system",
            ["subtype"] = "end"
        });

        var lines = new List<string>
        {
            msg1 + "\n",
            largeJson[..1000],
            largeJson[1000..3000],
            largeJson[3000..] + "\n" + msg3
        };

        var (transport, process) = CreateTransport(lines, []);
        try
        {
            var messages = new List<Dictionary<string, object?>>();
            await foreach (var msg in transport.ReadMessagesAsync(TestContext.Current.CancellationToken))
            {
                messages.Add(msg);
            }

            Assert.Equal(3, messages.Count);
            Assert.Equal("system", messages[0]["type"]?.ToString());
            Assert.Equal("start", messages[0]["subtype"]?.ToString());
            Assert.Equal("assistant", messages[1]["type"]?.ToString());
            Assert.Equal("system", messages[2]["type"]?.ToString());
            Assert.Equal("end", messages[2]["subtype"]?.ToString());

            // Verify the large message content
            var message = messages[1]["message"];
            Assert.NotNull(message);

            var messageDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(message.ToString()!);
            Assert.NotNull(messageDict);

            var content = messageDict["content"];
            Assert.NotNull(content);

            var contentList = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(content.ToString()!);
            Assert.NotNull(contentList);
            Assert.Single(contentList);

            var text = contentList[0]["text"]?.ToString();
            Assert.NotNull(text);
            Assert.Equal(5000, text.Length);
        }
        finally
        {
            CleanupProcess(process);
        }
    }
}
