using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Example demonstrating tool permission callbacks.
/// </summary>
public static class ToolPermissionExamples
{
    private sealed record ToolUsage(
        string Tool,
        Dictionary<string, object?> Input,
        List<PermissionUpdate> Suggestions);

    private static readonly List<ToolUsage> ToolUsageLog = [];

    /// <summary>
    /// Run the tool permission callback example.
    /// </summary>
    public static async Task RunAsync()
    {
        ToolUsageLog.Clear();

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("Tool Permission Callback Example");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine("\nThis example demonstrates how to:");
        Console.WriteLine("1. Allow/deny tools based on type");
        Console.WriteLine("2. Modify tool inputs for safety");
        Console.WriteLine("3. Log tool usage");
        Console.WriteLine("4. Prompt for unknown tools");
        Console.WriteLine(new string('=', 60));

        var options = new ClaudeAgentOptions
        {
            CanUseTool = MyPermissionCallbackAsync,
            PermissionMode = PermissionMode.Default,
            Cwd = "."
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();

        Console.WriteLine("\nSending query to Claude...");
        await client.QueryAsync(
            "Please do the following:\n" +
            "1. List the files in the current directory\n" +
            "2. Create a simple hello world script at hello.py\n" +
            "3. Run the script to test it");

        Console.WriteLine("\nReceiving response...");
        var messageCount = 0;

        await foreach (var message in client.ReceiveResponseAsync())
        {
            messageCount++;

            switch (message)
            {
                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"\nClaude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage:
                    Console.WriteLine("\nTask completed!");
                    Console.WriteLine($"Duration: {resultMessage.DurationMs}ms");
                    if (resultMessage.TotalCostUsd is { } cost)
                    {
                        Console.WriteLine($"Cost: ${cost:F4}");
                    }
                    Console.WriteLine($"Messages processed: {messageCount}");
                    break;
            }
        }

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("Tool Usage Summary");
        Console.WriteLine(new string('=', 60));
        for (var i = 0; i < ToolUsageLog.Count; i++)
        {
            var usage = ToolUsageLog[i];
            Console.WriteLine($"\n{i + 1}. Tool: {usage.Tool}");
            Console.WriteLine($"   Input: {ExampleHelper.SerializeObject(usage.Input)}");
            if (usage.Suggestions.Count > 0)
            {
                Console.WriteLine($"   Suggestions: {usage.Suggestions.Count}");
            }
        }

        Console.WriteLine();
    }

    private static Task<PermissionResult> MyPermissionCallbackAsync(
        string toolName,
        Dictionary<string, object?> input,
        ToolPermissionContext context)
    {
        // Log the tool request
        var clonedInput = new Dictionary<string, object?>(input);
        ToolUsageLog.Add(new ToolUsage(toolName, clonedInput, context.Suggestions));

        Console.WriteLine($"\nTool Permission Request: {toolName}");
        Console.WriteLine($"   Input: {ExampleHelper.SerializeObject(input)}");

        // Always allow read operations
        if (toolName is "Read" or "Glob" or "Grep")
        {
            Console.WriteLine($"   Allowing {toolName} (read-only operation)");
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        }

        // Deny write operations to system directories or redirect to safe paths
        if (toolName is "Write" or "Edit" or "MultiEdit")
        {
            var filePath = GetStringValue(input, "file_path");
            if (filePath.StartsWith("/etc/", StringComparison.OrdinalIgnoreCase) ||
                filePath.StartsWith("/usr/", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"   Denying write to system directory: {filePath}");
                return Task.FromResult<PermissionResult>(new PermissionResultDeny
                {
                    Message = $"Cannot write to system directory: {filePath}"
                });
            }

            if (!filePath.StartsWith("/tmp/", StringComparison.OrdinalIgnoreCase) &&
                !filePath.StartsWith("./", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(filePath))
            {
                var safePath = $"./safe_output/{Path.GetFileName(filePath)}";
                Console.WriteLine($"   Redirecting write from {filePath} to {safePath}");
                var modifiedInput = new Dictionary<string, object?>(input)
                {
                    ["file_path"] = safePath
                };

                return Task.FromResult<PermissionResult>(new PermissionResultAllow
                {
                    UpdatedInput = modifiedInput
                });
            }
        }

        // Check dangerous bash commands
        if (toolName == "Bash")
        {
            var command = GetStringValue(input, "command");
            var dangerousCommands = new[] { "rm -rf", "sudo", "chmod 777", "dd if=", "mkfs" };

            foreach (var dangerous in dangerousCommands)
            {
                if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"   Denying dangerous command: {command}");
                    return Task.FromResult<PermissionResult>(new PermissionResultDeny
                    {
                        Message = $"Dangerous command pattern detected: {dangerous}"
                    });
                }
            }

            Console.WriteLine($"   Allowing bash command: {command}");
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        }

        // For all other tools, ask the user
        Console.WriteLine($"   Unknown tool: {toolName}");
        Console.WriteLine($"      Input: {ExampleHelper.SerializeObject(input)}");
        Console.Write("   Allow this tool? (y/N): ");
        var userInput = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (userInput is "y" or "yes")
        {
            return Task.FromResult<PermissionResult>(new PermissionResultAllow());
        }

        return Task.FromResult<PermissionResult>(new PermissionResultDeny
        {
            Message = "User denied permission"
        });
    }

    private static string GetStringValue(Dictionary<string, object?> input, string key)
    {
        if (!input.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return value.ToString() ?? string.Empty;
    }
}
