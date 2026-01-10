using System.Text.Json;
using ClaudeAgentSdk;

namespace unit_test;

public class ErrorsTest
{
    [Fact]
    public void TestBaseError()
    {
        var error = new ClaudeSDKException("Something went wrong");
        Assert.Equal("Something went wrong", error.Message);
        Assert.IsAssignableFrom<Exception>(error);
    }

    [Fact]
    public void TestCLINotFoundException()
    {
        var error = new CLINotFoundException("Claude Code not found");
        Assert.IsAssignableFrom<ClaudeSDKException>(error);
        Assert.Contains("Claude Code not found", error.Message);
    }

    [Fact]
    public void TestConnectionError()
    {
        var error = new CLIConnectionException("Failed to connect to CLI");
        Assert.IsAssignableFrom<ClaudeSDKException>(error);
        Assert.Contains("Failed to connect to CLI", error.Message);
    }

    [Fact]
    public void TestProcessError()
    {
        var error = new ProcessException("Process failed", exitCode: 1, stderr: "Command not found");
        Assert.Equal(1, error.ExitCode);
        Assert.Equal("Command not found", error.Stderr);
        Assert.Contains("Process failed", error.Message);
        Assert.Contains("exit code: 1", error.Message);
        // C# uses "Error output:" prefix for stderr in message
        Assert.Contains("Error output: Command not found", error.Message);
    }

    [Fact]
    public void TestJSONDecodeError()
    {
        JsonException? originalError = null;
        try
        {
            JsonSerializer.Deserialize<object>("{invalid json}");
        }
        catch (JsonException e)
        {
            originalError = e;
        }

        Assert.NotNull(originalError);
        var error = new CLIJSONDecodeException("{invalid json}", originalError);
        Assert.Equal("{invalid json}", error.Line);
        Assert.Equal(originalError, error.OriginalError);
        Assert.Contains("Failed to decode JSON", error.Message);
    }
}
