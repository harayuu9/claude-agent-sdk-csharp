using ClaudeAgentSdk;

namespace UnitTest.E2E;

/// <summary>
/// End-to-end tests for tool permission callbacks with real Claude API calls.
/// Equivalent to Python e2e-tests/test_tool_permissions.py
/// </summary>
[Trait("Category", "E2E")]
public class ToolPermissionsE2ETests : E2ETestBase
{
    /// <summary>
    /// Test that can_use_tool callback gets invoked.
    /// Equivalent to Python test_permission_callback_gets_called.
    /// </summary>
    [Fact]
    public async Task PermissionCallbackGetsCalled()
    {
        SkipIfCannotRunE2E();

        var callbackInvocations = new List<string>();

        // Define permission callback
        async Task<PermissionResult> PermissionCallback(
            string toolName,
            Dictionary<string, object?> input,
            ToolPermissionContext context)
        {
            callbackInvocations.Add(toolName);
            return new PermissionResultAllow();
        }

        var options = new ClaudeAgentOptions
        {
            CanUseTool = PermissionCallback,
            Model = DefaultTestModel
        };

        var ct = TestContext.Current.CancellationToken;
        await using var client = new ClaudeSDKClient(options);

        // CanUseTool requires streaming mode, so pass null for initial prompt
        // then use QueryAsync
        await client.ConnectAsync(ct: ct);
        await client.QueryAsync("Write 'hello world' to /tmp/test.txt", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Just consume messages
        }

        // Verify callback was invoked for Write tool
        Assert.Contains("Write", callbackInvocations);
    }

    /// <summary>
    /// Test that permission callback can deny tool use.
    /// </summary>
    [Fact]
    public async Task PermissionCallbackCanDeny()
    {
        SkipIfCannotRunE2E();

        var callbackInvocations = new List<string>();
        var deniedTools = new List<string>();

        // Define permission callback that denies Write but allows others
        async Task<PermissionResult> PermissionCallback(
            string toolName,
            Dictionary<string, object?> input,
            ToolPermissionContext context)
        {
            callbackInvocations.Add(toolName);

            if (toolName == "Write")
            {
                deniedTools.Add(toolName);
                return new PermissionResultDeny
                {
                    Message = "Write operations are not allowed"
                };
            }

            return new PermissionResultAllow();
        }

        var options = new ClaudeAgentOptions
        {
            CanUseTool = PermissionCallback,
            Model = DefaultTestModel
        };

        var ct = TestContext.Current.CancellationToken;
        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(ct: ct);
        await client.QueryAsync("Write 'hello world' to /tmp/test.txt", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Just consume messages
        }

        // Verify Write tool was denied
        Assert.Contains("Write", deniedTools);
    }

    /// <summary>
    /// Test that permission callback receives correct input data.
    /// </summary>
    [Fact]
    public async Task PermissionCallbackReceivesInput()
    {
        SkipIfCannotRunE2E();

        var capturedInputs = new List<(string ToolName, Dictionary<string, object?> Input)>();

        // Define permission callback that captures input
        async Task<PermissionResult> PermissionCallback(
            string toolName,
            Dictionary<string, object?> input,
            ToolPermissionContext context)
        {
            capturedInputs.Add((toolName, new Dictionary<string, object?>(input)));
            return new PermissionResultAllow();
        }

        var options = new ClaudeAgentOptions
        {
            CanUseTool = PermissionCallback,
            Model = DefaultTestModel
        };

        var ct = TestContext.Current.CancellationToken;
        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(ct: ct);
        await client.QueryAsync("Read the file /tmp/test.txt", ct: ct);

        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            // Just consume messages
        }

        // Verify callback received input data
        Assert.NotEmpty(capturedInputs);

        // Find Read tool invocation and check it has input
        var readInvocation = capturedInputs.FirstOrDefault(x => x.ToolName == "Read");
        if (readInvocation != default)
        {
            Assert.NotNull(readInvocation.Input);
        }
    }
}
