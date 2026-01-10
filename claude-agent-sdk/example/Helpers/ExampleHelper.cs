using System.IO;
using System.Text.Json;
using ClaudeAgentSdk;

namespace Examples.Helpers;

/// <summary>
/// Shared utilities for displaying messages in examples.
/// </summary>
public static class ExampleHelper
{
    /// <summary>
    /// Standardized message display function.
    /// - UserMessage: "User: content"
    /// - AssistantMessage: "Claude: content"
    /// - SystemMessage: ignored
    /// - ResultMessage: "Result ended" + cost if available
    /// </summary>
    public static void DisplayMessage(Message msg)
    {
        switch (msg)
        {
            case UserMessage userMsg:
                DisplayUserMessage(userMsg);
                break;

            case AssistantMessage assistantMsg:
                DisplayAssistantMessage(assistantMsg);
                break;

            case SystemMessage:
                // Ignore system messages
                break;

            case ResultMessage resultMsg:
                Console.WriteLine("Result ended");
                if (resultMsg.TotalCostUsd is > 0)
                {
                    Console.WriteLine($"Cost: ${resultMsg.TotalCostUsd:F4}");
                }
                break;

            case StreamEvent streamEvent:
                DisplayStreamEvent(streamEvent);
                break;
        }
    }

    /// <summary>
    /// Display a user message.
    /// </summary>
    public static void DisplayUserMessage(UserMessage msg)
    {
        // Content can be a string or a list of content blocks
        if (msg.Content is string text)
        {
            Console.WriteLine($"User: {text}");
        }
        else if (msg.Content is IEnumerable<object> blocks)
        {
            foreach (var block in blocks)
            {
                if (block is TextBlock textBlock)
                {
                    Console.WriteLine($"User: {textBlock.Text}");
                }
                else if (block is ToolResultBlock toolResult)
                {
                    var content = toolResult.Content?.ToString() ?? "None";
                    var preview = content.Length > 100 ? content[..100] + "..." : content;
                    Console.WriteLine($"Tool Result (id: {toolResult.ToolUseId}): {preview}");
                }
            }
        }
    }

    /// <summary>
    /// Display a stream event message in a compact form.
    /// </summary>
    public static void DisplayStreamEvent(StreamEvent streamEvent)
    {
        if (streamEvent.Event.TryGetValue("type", out var type))
        {
            Console.WriteLine($"[StreamEvent] type={type}");
        }
        else
        {
            Console.WriteLine($"[StreamEvent] {SerializeObject(streamEvent.Event)}");
        }
    }

    /// <summary>
    /// Display an assistant message.
    /// </summary>
    public static void DisplayAssistantMessage(AssistantMessage msg)
    {
        foreach (var block in msg.Content)
        {
            switch (block)
            {
                case TextBlock textBlock:
                    Console.WriteLine($"Claude: {textBlock.Text}");
                    break;

                case ToolUseBlock toolUse:
                    Console.WriteLine($"Tool Use: {toolUse.Name} (id: {toolUse.Id})");
                    if (toolUse.Name == "Bash" && toolUse.Input.TryGetValue("command", out var cmd))
                    {
                        Console.WriteLine($"  Command: {cmd}");
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Runs an example with proper error handling.
    /// </summary>
    public static async Task RunExampleAsync(string name, Func<Task> example)
    {
        Console.WriteLine($"=== {name} ===");
        Console.WriteLine();

        try
        {
            await example();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Print a separator line.
    /// </summary>
    public static void PrintSeparator()
    {
        Console.WriteLine(new string('-', 50));
        Console.WriteLine();
    }

    /// <summary>
    /// Resolve the SDK repository root from the compiled binary path.
    /// </summary>
    public static string GetSdkRootPath()
    {
        // Binary is typically at example/bin/Debug/netX.Y/ - walk up to the repo root.
        var candidate = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        return Directory.Exists(candidate) ? candidate : Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Serialize an object to JSON for display.
    /// </summary>
    public static string SerializeObject(object? value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }
}
