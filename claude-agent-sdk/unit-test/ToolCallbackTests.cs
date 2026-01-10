using System.Reflection;
using System.Text.Json;
using ClaudeAgentSdk;
using UnitTest.Helpers;
// Alias to resolve ambiguity between ClaudeAgentSdk.Query and ClaudeAgentSdk.Internal.Query
using InternalQuery = ClaudeAgentSdk.Internal.Query;

namespace UnitTest;

#region ToolPermissionCallbackTests

/// <summary>
/// Test tool permission callback functionality.
/// </summary>
public class ToolPermissionCallbackTests
{
    private static MethodInfo GetHandleControlRequestMethod()
    {
        return typeof(InternalQuery).GetMethod(
            "HandleControlRequestAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private static async Task InvokeHandleControlRequestAsync(InternalQuery query, Dictionary<string, object?> request)
    {
        var method = GetHandleControlRequestMethod();
        var task = (Task)method.Invoke(query, [request])!;
        await task;
    }

    [Fact]
    public async Task TestPermissionCallbackAllow()
    {
        // Test callback that allows tool execution.
        var callbackInvoked = false;

        Task<PermissionResult> AllowCallback(
            string toolName,
            Dictionary<string, object?> inputData,
            ToolPermissionContext context)
        {
            callbackInvoked = true;
            Assert.Equal("TestTool", toolName);
            Assert.Equal("value", inputData["param"]);
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        }

        var transport = new MockTransport();
        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: AllowCallback,
            hooks: null);

        // Simulate control request
        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-1",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "can_use_tool",
                ["tool_name"] = "TestTool",
                ["input"] = new Dictionary<string, object?> { ["param"] = "value" },
                ["permission_suggestions"] = new List<object?>()
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check callback was invoked
        Assert.True(callbackInvoked);

        // Check response was sent
        Assert.Single(transport.WrittenMessages);
        var response = transport.WrittenMessages[0];
        Assert.Contains("\"behavior\":\"allow\"", response.Replace(" ", ""));
    }

    [Fact]
    public async Task TestPermissionCallbackDeny()
    {
        // Test callback that denies tool execution.
        Task<PermissionResult> DenyCallback(
            string toolName,
            Dictionary<string, object?> inputData,
            ToolPermissionContext context)
        {
            return Task.FromResult<PermissionResult>(
                new PermissionResultDeny { Message = "Security policy violation" });
        }

        var transport = new MockTransport();
        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: DenyCallback,
            hooks: null);

        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-2",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "can_use_tool",
                ["tool_name"] = "DangerousTool",
                ["input"] = new Dictionary<string, object?> { ["command"] = "rm -rf /" },
                ["permission_suggestions"] = new List<object?> { "deny" }
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check response
        Assert.Single(transport.WrittenMessages);
        var response = transport.WrittenMessages[0];
        Assert.Contains("\"behavior\":\"deny\"", response.Replace(" ", ""));
        Assert.Contains("Security policy violation", response);
    }

    [Fact]
    public async Task TestPermissionCallbackInputModification()
    {
        // Test callback that modifies tool input.
        Task<PermissionResult> ModifyCallback(
            string toolName,
            Dictionary<string, object?> inputData,
            ToolPermissionContext context)
        {
            // Modify the input to add safety flag
            var modifiedInput = new Dictionary<string, object?>(inputData)
            {
                ["safe_mode"] = true
            };
            return Task.FromResult<PermissionResult>(
                new PermissionResultAllow { UpdatedInput = modifiedInput });
        }

        var transport = new MockTransport();
        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: ModifyCallback,
            hooks: null);

        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-3",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "can_use_tool",
                ["tool_name"] = "WriteTool",
                ["input"] = new Dictionary<string, object?> { ["file_path"] = "/etc/passwd" },
                ["permission_suggestions"] = new List<object?>()
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check response includes modified input
        Assert.Single(transport.WrittenMessages);
        var response = transport.WrittenMessages[0];
        Assert.Contains("\"behavior\":\"allow\"", response.Replace(" ", ""));
        Assert.Contains("safe_mode", response);
        Assert.Contains("true", response);
    }

    [Fact]
    public async Task TestCallbackExceptionHandling()
    {
        // Test that callback exceptions are properly handled.
        Task<PermissionResult> ErrorCallback(
            string toolName,
            Dictionary<string, object?> inputData,
            ToolPermissionContext context)
        {
            throw new InvalidOperationException("Callback error");
        }

        var transport = new MockTransport();
        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: ErrorCallback,
            hooks: null);

        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-5",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "can_use_tool",
                ["tool_name"] = "TestTool",
                ["input"] = new Dictionary<string, object?>(),
                ["permission_suggestions"] = new List<object?>()
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check error response was sent
        Assert.Single(transport.WrittenMessages);
        var response = transport.WrittenMessages[0];
        Assert.Contains("\"subtype\":\"error\"", response.Replace(" ", ""));
        Assert.Contains("Callback error", response);
    }
}

#endregion

#region HookCallbackTests

/// <summary>
/// Test hook callback functionality.
/// </summary>
public class HookCallbackTests
{
    private static MethodInfo GetHandleControlRequestMethod()
    {
        return typeof(InternalQuery).GetMethod(
            "HandleControlRequestAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private static async Task InvokeHandleControlRequestAsync(InternalQuery query, Dictionary<string, object?> request)
    {
        var method = GetHandleControlRequestMethod();
        var task = (Task)method.Invoke(query, [request])!;
        await task;
    }

    private static void SetHookCallback(InternalQuery query, string callbackId, HookCallback callback)
    {
        var field = typeof(InternalQuery).GetField(
            "_hookCallbacks",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var hookCallbacks = (Dictionary<string, HookCallback>)field.GetValue(query)!;
        hookCallbacks[callbackId] = callback;
    }

    [Fact]
    public async Task TestHookExecution()
    {
        // Test that hooks are called at appropriate times.
        var hookCalls = new List<Dictionary<string, object?>>();

        Task<HookJsonOutput> TestHook(
            BaseHookInput inputData,
            string? toolUseId,
            HookContext context)
        {
            hookCalls.Add(new Dictionary<string, object?>
            {
                ["input"] = inputData,
                ["tool_use_id"] = toolUseId
            });
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        var transport = new MockTransport();

        // Create hooks configuration
        var hooks = new Dictionary<HookEvent, List<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new List<HookMatcher>
            {
                new HookMatcher { Matcher = "TestTool", Hooks = [TestHook] }
            }
        };

        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: null,
            hooks: hooks);

        // Manually register the hook callback
        var callbackId = "test_hook_0";
        SetHookCallback(query, callbackId, TestHook);

        // Simulate hook callback request
        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-hook-1",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "hook_callback",
                ["callback_id"] = callbackId,
                ["input"] = new Dictionary<string, object?>
                {
                    ["hook_event_name"] = "PreToolUse",
                    ["session_id"] = "test-session",
                    ["transcript_path"] = "/tmp/test",
                    ["cwd"] = "/home/test",
                    ["tool_name"] = "TestTool",
                    ["tool_input"] = new Dictionary<string, object?> { ["test"] = "data" }
                },
                ["tool_use_id"] = "tool-123"
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check hook was called
        Assert.Single(hookCalls);
        Assert.Equal("tool-123", hookCalls[0]["tool_use_id"]);

        // Check response
        Assert.True(transport.WrittenMessages.Count > 0);
    }

    [Fact]
    public async Task TestHookOutputFields()
    {
        // Test that all SyncHookJsonOutput fields are properly handled.
        Task<HookJsonOutput> ComprehensiveHook(
            BaseHookInput inputData,
            string? toolUseId,
            HookContext context)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
            {
                Continue = true,
                SuppressOutput = false,
                StopReason = "Test stop reason",
                Decision = "block",
                SystemMessage = "Test system message",
                Reason = "Test reason for blocking",
                HookSpecificOutput = new PreToolUseHookSpecificOutput
                {
                    PermissionDecision = PermissionBehavior.Deny,
                    PermissionDecisionReason = "Security policy violation",
                    UpdatedInput = new Dictionary<string, object?> { ["modified"] = "input" }
                }
            });
        }

        var transport = new MockTransport();
        var hooks = new Dictionary<HookEvent, List<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new List<HookMatcher>
            {
                new HookMatcher { Matcher = "TestTool", Hooks = [ComprehensiveHook] }
            }
        };

        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: null,
            hooks: hooks);

        var callbackId = "test_comprehensive_hook";
        SetHookCallback(query, callbackId, ComprehensiveHook);

        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-comprehensive",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "hook_callback",
                ["callback_id"] = callbackId,
                ["input"] = new Dictionary<string, object?>
                {
                    ["hook_event_name"] = "PreToolUse",
                    ["session_id"] = "test-session",
                    ["transcript_path"] = "/tmp/test",
                    ["cwd"] = "/home/test",
                    ["tool_name"] = "TestTool",
                    ["tool_input"] = new Dictionary<string, object?> { ["test"] = "data" }
                },
                ["tool_use_id"] = "tool-456"
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check response contains all the fields
        Assert.True(transport.WrittenMessages.Count > 0);
        var lastResponse = transport.WrittenMessages[^1];

        // Parse the JSON response
        var responseData = JsonSerializer.Deserialize<Dictionary<string, object?>>(lastResponse);
        Assert.NotNull(responseData);

        // Verify the response structure
        Assert.Contains("response", responseData.Keys);
    }

    [Fact]
    public async Task TestAsyncHookOutput()
    {
        // Test AsyncHookJsonOutput type with proper async fields.
        Task<HookJsonOutput> AsyncHook(
            BaseHookInput inputData,
            string? toolUseId,
            HookContext context)
        {
            return Task.FromResult<HookJsonOutput>(new AsyncHookJsonOutput
            {
                Async = true,
                AsyncTimeout = 5000
            });
        }

        var transport = new MockTransport();
        var hooks = new Dictionary<HookEvent, List<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new List<HookMatcher>
            {
                new HookMatcher { Matcher = null, Hooks = [AsyncHook] }
            }
        };

        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: null,
            hooks: hooks);

        var callbackId = "test_async_hook";
        SetHookCallback(query, callbackId, AsyncHook);

        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-async",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "hook_callback",
                ["callback_id"] = callbackId,
                ["input"] = new Dictionary<string, object?>
                {
                    ["hook_event_name"] = "PreToolUse",
                    ["session_id"] = "test-session",
                    ["transcript_path"] = "/tmp/test",
                    ["cwd"] = "/home/test",
                    ["tool_name"] = "TestTool",
                    ["tool_input"] = new Dictionary<string, object?> { ["test"] = "async_data" }
                },
                ["tool_use_id"] = null
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check response contains async fields
        Assert.True(transport.WrittenMessages.Count > 0);
        var lastResponse = transport.WrittenMessages[^1];

        // Verify async field is present
        Assert.Contains("async", lastResponse);
        Assert.Contains("5000", lastResponse);
    }

    [Fact]
    public async Task TestFieldNameConversion()
    {
        // Test that C# field names are properly serialized to CLI format.
        Task<HookJsonOutput> ConversionTestHook(
            BaseHookInput inputData,
            string? toolUseId,
            HookContext context)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
            {
                Continue = false,
                StopReason = "Testing field conversion",
                SystemMessage = "Fields should be converted"
            });
        }

        var transport = new MockTransport();
        var hooks = new Dictionary<HookEvent, List<HookMatcher>>
        {
            [HookEvent.PreToolUse] = new List<HookMatcher>
            {
                new HookMatcher { Matcher = null, Hooks = [ConversionTestHook] }
            }
        };

        var query = new InternalQuery(
            transport: transport,
            isStreamingMode: true,
            canUseTool: null,
            hooks: hooks);

        var callbackId = "test_conversion";
        SetHookCallback(query, callbackId, ConversionTestHook);

        var request = new Dictionary<string, object?>
        {
            ["type"] = "control_request",
            ["request_id"] = "test-conversion",
            ["request"] = new Dictionary<string, object?>
            {
                ["subtype"] = "hook_callback",
                ["callback_id"] = callbackId,
                ["input"] = new Dictionary<string, object?>
                {
                    ["hook_event_name"] = "PreToolUse",
                    ["session_id"] = "test-session",
                    ["transcript_path"] = "/tmp/test",
                    ["cwd"] = "/home/test",
                    ["tool_name"] = "TestTool",
                    ["tool_input"] = new Dictionary<string, object?> { ["test"] = "data" }
                },
                ["tool_use_id"] = null
            }
        };

        await InvokeHandleControlRequestAsync(query, request);

        // Check response has correct field names
        Assert.True(transport.WrittenMessages.Count > 0);
        var lastResponse = transport.WrittenMessages[^1];

        // Verify continue field is present (not continue_)
        Assert.Contains("continue", lastResponse);
        Assert.Contains("stopReason", lastResponse);
        Assert.Contains("systemMessage", lastResponse);
    }
}

#endregion

#region ClaudeAgentOptionsIntegrationTests

/// <summary>
/// Test that callbacks work through ClaudeAgentOptions.
/// </summary>
public class ClaudeAgentOptionsIntegrationTests
{
    [Fact]
    public void TestOptionsWithCallbacks()
    {
        // Test creating options with callbacks.
        Task<PermissionResult> MyCallback(
            string toolName,
            Dictionary<string, object?> inputData,
            ToolPermissionContext context)
        {
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        }

        Task<HookJsonOutput> MyHook(
            BaseHookInput inputData,
            string? toolUseId,
            HookContext context)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        var options = new ClaudeAgentOptions
        {
            CanUseTool = MyCallback,
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PreToolUse] = new List<HookMatcher>
                {
                    new HookMatcher { Matcher = "Bash", Hooks = [MyHook] }
                }
            }
        };

        Assert.Equal(MyCallback, options.CanUseTool);
        Assert.True(options.Hooks!.ContainsKey(HookEvent.PreToolUse));
        Assert.Single(options.Hooks[HookEvent.PreToolUse]);
        Assert.Equal(MyHook, options.Hooks[HookEvent.PreToolUse][0].Hooks[0]);
    }
}

#endregion
