using ClaudeAgentSdk;

namespace unit_test.E2E;

/// <summary>
/// End-to-end tests for hook callbacks with real Claude API calls.
/// Equivalent to Python e2e-tests/test_hooks.py
/// </summary>
[Trait("Category", "E2E")]
public class HooksE2ETests : E2ETestBase
{
    /// <summary>
    /// Test that hooks with permissionDecision and reason fields work end-to-end.
    /// Equivalent to Python test_hook_with_permission_decision_and_reason.
    /// </summary>
    [Fact]
    public async Task HookWithPermissionDecisionAndReason()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var hookInvocations = new List<string>();

        // Define hook that uses permissionDecision and reason fields
        async Task<HookJsonOutput> TestHook(
            BaseHookInput input,
            string? toolUseId,
            HookContext context)
        {
            var toolName = input switch
            {
                PreToolUseHookInput preToolUse => preToolUse.ToolName,
                _ => ""
            };

            hookInvocations.Add(toolName);

            // Block Bash commands for this test
            if (toolName == "Bash")
            {
                return new SyncHookJsonOutput
                {
                    Reason = "Bash commands are blocked in this test for safety",
                    SystemMessage = "Command blocked by hook",
                    HookSpecificOutput = new PreToolUseHookSpecificOutput
                    {
                        PermissionDecision = PermissionBehavior.Deny,
                        PermissionDecisionReason = "Security policy: Bash blocked"
                    }
                };
            }

            return new SyncHookJsonOutput
            {
                Reason = "Tool approved by security review",
                HookSpecificOutput = new PreToolUseHookSpecificOutput
                {
                    PermissionDecision = PermissionBehavior.Allow,
                    PermissionDecisionReason = "Tool passed security checks"
                }
            };
        }

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Bash", "Write"],
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PreToolUse] =
                [
                    new HookMatcher
                    {
                        Matcher = "Bash",
                        Hooks = [TestHook]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync("Run this bash command: echo 'hello'");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Verify hook was called
        Assert.Contains("Bash", hookInvocations);
    }

    /// <summary>
    /// Test that hooks with continue_=False and stopReason fields work end-to-end.
    /// Equivalent to Python test_hook_with_continue_and_stop_reason.
    /// </summary>
    [Fact]
    public async Task HookWithContinueAndStopReason()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var hookInvocations = new List<string>();

        // Define PostToolUse hook that stops execution with stopReason
        async Task<HookJsonOutput> PostToolHook(
            BaseHookInput input,
            string? toolUseId,
            HookContext context)
        {
            var toolName = input switch
            {
                PostToolUseHookInput postToolUse => postToolUse.ToolName,
                _ => ""
            };

            hookInvocations.Add(toolName);

            // Test continue=false and stopReason fields
            return new SyncHookJsonOutput
            {
                Continue = false,
                StopReason = "Execution halted by test hook for validation",
                Reason = "Testing continue and stopReason fields",
                SystemMessage = "Test hook stopped execution"
            };
        }

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Bash"],
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PostToolUse] =
                [
                    new HookMatcher
                    {
                        Matcher = "Bash",
                        Hooks = [PostToolHook]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync("Run: echo 'test message'");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Verify hook was called
        Assert.Contains("Bash", hookInvocations);
    }

    /// <summary>
    /// Test that hooks with hookSpecificOutput work end-to-end.
    /// Equivalent to Python test_hook_with_additional_context.
    /// </summary>
    [Fact]
    public async Task HookWithAdditionalContext()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var hookInvocations = new List<string>();

        // Define hook that provides additional context
        async Task<HookJsonOutput> ContextHook(
            BaseHookInput input,
            string? toolUseId,
            HookContext context)
        {
            hookInvocations.Add("context_added");

            return new SyncHookJsonOutput
            {
                SystemMessage = "Additional context provided by hook",
                Reason = "Hook providing monitoring feedback",
                SuppressOutput = false,
                HookSpecificOutput = new PostToolUseHookSpecificOutput
                {
                    AdditionalContext = "The command executed successfully with hook monitoring"
                }
            };
        }

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Bash"],
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PostToolUse] =
                [
                    new HookMatcher
                    {
                        Matcher = "Bash",
                        Hooks = [ContextHook]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync("Run: echo 'testing hooks'");

        await foreach (var message in client.ReceiveResponseAsync())
        {
            // Just consume messages
        }

        // Verify hook was called
        Assert.Contains("context_added", hookInvocations);
    }
}
