using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Examples demonstrating hook callbacks via ClaudeAgentOptions.
/// </summary>
public static class HooksExamples
{
    /// <summary>
    /// Basic example demonstrating hook protection.
    /// </summary>
    public static async Task PreToolUseExampleAsync()
    {
        Console.WriteLine("=== PreToolUse Example ===");
        Console.WriteLine("This example demonstrates how PreToolUse can block some bash commands but not others.\n");

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Bash"],
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PreToolUse] =
                [
                    new HookMatcher
                    {
                        Matcher = "Bash",
                        Hooks = [CheckBashCommandAsync]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        Console.WriteLine("Test 1: Trying a command that our PreToolUse hook should block...");
        Console.WriteLine("User: Run the bash command: ./foo.sh --help");
        await client.QueryAsync("Run the bash command: ./foo.sh --help");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n" + new string('=', 50) + "\n");

        Console.WriteLine("Test 2: Trying a command that our PreToolUse hook should allow...");
        Console.WriteLine("User: Run the bash command: echo 'Hello from hooks example!'");
        await client.QueryAsync("Run the bash command: echo 'Hello from hooks example!'");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate context retention across conversation via UserPromptSubmit hook.
    /// </summary>
    public static async Task UserPromptSubmitExampleAsync()
    {
        Console.WriteLine("=== UserPromptSubmit Example ===");
        Console.WriteLine("This example shows how a UserPromptSubmit hook can add context.\n");

        var options = new ClaudeAgentOptions
        {
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.UserPromptSubmit] =
                [
                    new HookMatcher
                    {
                        Matcher = null,
                        Hooks = [AddCustomInstructionsAsync]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        Console.WriteLine("User: What's my favorite color?");
        await client.QueryAsync("What's my favorite color?");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate PostToolUse hook with reason and systemMessage fields.
    /// </summary>
    public static async Task PostToolUseExampleAsync()
    {
        Console.WriteLine("=== PostToolUse Example ===");
        Console.WriteLine("This example shows how PostToolUse can provide feedback with reason and systemMessage.\n");

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
                        Hooks = [ReviewToolOutputAsync]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        Console.WriteLine("User: Run a command that will produce an error: ls /nonexistent_directory");
        await client.QueryAsync("Run this command: ls /nonexistent_directory");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate permissionDecision, reason, and systemMessage fields.
    /// </summary>
    public static async Task DecisionFieldsExampleAsync()
    {
        Console.WriteLine("=== Permission Decision Example ===");
        Console.WriteLine("This example shows how to use permissionDecision='allow'/'deny' with reason and systemMessage.\n");

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Write", "Bash"],
            Model = "claude-sonnet-4-5-20250929",
            Hooks = new Dictionary<HookEvent, List<HookMatcher>>
            {
                [HookEvent.PreToolUse] =
                [
                    new HookMatcher
                    {
                        Matcher = "Write",
                        Hooks = [StrictApprovalHookAsync]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        Console.WriteLine("Test 1: Trying to write to important_config.txt (should be blocked)...");
        Console.WriteLine("User: Write 'test' to important_config.txt");
        await client.QueryAsync("Write the text 'test data' to a file called important_config.txt");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n" + new string('=', 50) + "\n");

        Console.WriteLine("Test 2: Trying to write to regular_file.txt (should be approved)...");
        Console.WriteLine("User: Write 'test' to regular_file.txt");
        await client.QueryAsync("Write the text 'test data' to a file called regular_file.txt");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate continue and stopReason fields for execution control.
    /// </summary>
    public static async Task ContinueControlExampleAsync()
    {
        Console.WriteLine("=== Continue/Stop Control Example ===");
        Console.WriteLine("This example shows how to use continue_=False with stopReason to halt execution.\n");

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
                        Hooks = [StopOnErrorHookAsync]
                    }
                ]
            }
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        Console.WriteLine("User: Run a command that outputs 'CRITICAL ERROR'");
        await client.QueryAsync("Run this bash command: echo 'CRITICAL ERROR: system failure'");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Run all hook examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await PreToolUseExampleAsync();
        ExampleHelper.PrintSeparator();

        await UserPromptSubmitExampleAsync();
        ExampleHelper.PrintSeparator();

        await PostToolUseExampleAsync();
        ExampleHelper.PrintSeparator();

        await DecisionFieldsExampleAsync();
        ExampleHelper.PrintSeparator();

        await ContinueControlExampleAsync();
    }

    private static Task<HookJsonOutput> CheckBashCommandAsync(BaseHookInput input, string? toolUseId, HookContext context)
    {
        if (input is not PreToolUseHookInput preToolUse)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        if (!string.Equals(preToolUse.ToolName, "Bash", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        preToolUse.ToolInput.TryGetValue("command", out var commandObj);
        var command = commandObj?.ToString() ?? string.Empty;
        var blockedPatterns = new[] { "foo.sh" };

        foreach (var pattern in blockedPatterns)
        {
            if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Blocked command: {command}");
                return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
                {
                    HookSpecificOutput = new PreToolUseHookSpecificOutput
                    {
                        PermissionDecision = PermissionBehavior.Deny,
                        PermissionDecisionReason = $"Command contains invalid pattern: {pattern}"
                    }
                });
            }
        }

        return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
    }

    private static Task<HookJsonOutput> AddCustomInstructionsAsync(BaseHookInput input, string? toolUseId, HookContext context)
    {
        return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
        {
            HookSpecificOutput = new UserPromptSubmitHookSpecificOutput
            {
                AdditionalContext = "My favorite color is hot pink"
            }
        });
    }

    private static Task<HookJsonOutput> ReviewToolOutputAsync(BaseHookInput input, string? toolUseId, HookContext context)
    {
        if (input is not PostToolUseHookInput postToolUse)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        var toolResponse = postToolUse.ToolResponse?.ToString() ?? string.Empty;
        if (toolResponse.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
            {
                SystemMessage = "The command produced an error",
                Reason = "Tool execution failed - consider checking the command syntax",
                HookSpecificOutput = new PostToolUseHookSpecificOutput
                {
                    AdditionalContext = "The command encountered an error. You may want to try a different approach."
                }
            });
        }

        return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
    }

    private static Task<HookJsonOutput> StrictApprovalHookAsync(BaseHookInput input, string? toolUseId, HookContext context)
    {
        if (input is not PreToolUseHookInput preToolUse)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        if (string.Equals(preToolUse.ToolName, "Write", StringComparison.OrdinalIgnoreCase))
        {
            preToolUse.ToolInput.TryGetValue("file_path", out var filePathObj);
            var filePath = filePathObj?.ToString() ?? string.Empty;

            if (filePath.Contains("important", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Blocked Write to: {filePath}");
                return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
                {
                    Reason = "Writes to files containing 'important' in the name are not allowed for safety",
                    SystemMessage = "Write operation blocked by security policy",
                    HookSpecificOutput = new PreToolUseHookSpecificOutput
                    {
                        PermissionDecision = PermissionBehavior.Deny,
                        PermissionDecisionReason = "Security policy blocks writes to important files"
                    }
                });
            }
        }

        return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
        {
            Reason = "Tool use approved after security review",
            HookSpecificOutput = new PreToolUseHookSpecificOutput
            {
                PermissionDecision = PermissionBehavior.Allow,
                PermissionDecisionReason = "Tool passed security checks"
            }
        });
    }

    private static Task<HookJsonOutput> StopOnErrorHookAsync(BaseHookInput input, string? toolUseId, HookContext context)
    {
        if (input is not PostToolUseHookInput postToolUse)
        {
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput());
        }

        var toolResponse = postToolUse.ToolResponse?.ToString() ?? string.Empty;
        if (toolResponse.Contains("critical", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Critical error detected - stopping execution");
            return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
            {
                Continue = false,
                StopReason = "Critical error detected in tool output - execution halted for safety",
                SystemMessage = "Execution stopped due to critical error",
                HookSpecificOutput = new PostToolUseHookSpecificOutput
                {
                    AdditionalContext = "The command encountered a critical error."
                }
            });
        }

        return Task.FromResult<HookJsonOutput>(new SyncHookJsonOutput
        {
            Continue = true
        });
    }
}
