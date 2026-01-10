using System.Text.Json;
using ClaudeAgentSdk;

namespace Examples;

/// <summary>
/// Examples demonstrating the tools option and how tools appear in the system message.
/// </summary>
public static class ToolsOptionExamples
{
    /// <summary>
    /// Example with tools as a specific array of tool names.
    /// </summary>
    public static async Task ToolsArrayExampleAsync()
    {
        Console.WriteLine("=== Tools Array Example ===");
        Console.WriteLine("Setting tools=['Read', 'Glob', 'Grep']\n");

        var options = new ClaudeAgentOptions
        {
            Tools = new[] { "Read", "Glob", "Grep" },
            MaxTurns = 1
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "What tools do you have available? Just list them briefly.",
                           options: options))
        {
            switch (message)
            {
                case SystemMessage { Subtype: "init", Data: var data }:
                    var tools = ExtractTools(data);
                    Console.WriteLine($"Tools from system message: [{string.Join(", ", tools)}]\n");
                    break;

                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Claude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage when resultMessage.TotalCostUsd is { } cost:
                    Console.WriteLine($"\nCost: ${cost:F4}");
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example with tools as an empty array (disables built-in tools).
    /// </summary>
    public static async Task ToolsEmptyArrayExampleAsync()
    {
        Console.WriteLine("=== Tools Empty Array Example ===");
        Console.WriteLine("Setting tools=[] (disables all built-in tools)\n");

        var options = new ClaudeAgentOptions
        {
            Tools = Array.Empty<string>(),
            MaxTurns = 1
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "What tools do you have available? Just list them briefly.",
                           options: options))
        {
            switch (message)
            {
                case SystemMessage { Subtype: "init", Data: var data }:
                    var tools = ExtractTools(data);
                    Console.WriteLine($"Tools from system message: [{string.Join(", ", tools)}]\n");
                    break;

                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Claude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage when resultMessage.TotalCostUsd is { } cost:
                    Console.WriteLine($"\nCost: ${cost:F4}");
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example with tools preset (all default Claude Code tools).
    /// </summary>
    public static async Task ToolsPresetExampleAsync()
    {
        Console.WriteLine("=== Tools Preset Example ===");
        Console.WriteLine("Setting tools={'type': 'preset', 'preset': 'claude_code'}\n");

        var options = new ClaudeAgentOptions
        {
            Tools = new ToolsPreset { Preset = "claude_code" },
            MaxTurns = 1
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "What tools do you have available? Just list them briefly.",
                           options: options))
        {
            switch (message)
            {
                case SystemMessage { Subtype: "init", Data: var data }:
                    var tools = ExtractTools(data);
                    var preview = tools.Count > 5 ? $"{string.Join(", ", tools.Take(5))}..." : string.Join(", ", tools);
                    Console.WriteLine($"Tools from system message ({tools.Count} tools): [{preview}]\n");
                    break;

                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Claude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage when resultMessage.TotalCostUsd is { } cost:
                    Console.WriteLine($"\nCost: ${cost:F4}");
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all tools option examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await ToolsArrayExampleAsync();
        await ToolsEmptyArrayExampleAsync();
        await ToolsPresetExampleAsync();
    }

    private static List<string> ExtractTools(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("tools", out var toolsObj) || toolsObj is null)
        {
            return [];
        }

        return toolsObj switch
        {
            JsonElement jsonElement => ExtractFromJsonElement(jsonElement),
            IEnumerable<object?> enumerable when toolsObj is not string => ExtractFromEnumerable(enumerable),
            _ => [toolsObj.ToString() ?? string.Empty]
        };
    }

    private static List<string> ExtractFromJsonElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [element.ToString()];
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var nameProp))
            {
                list.Add(nameProp.ToString());
            }
            else
            {
                list.Add(item.ToString());
            }
        }

        return list;
    }

    private static List<string> ExtractFromEnumerable(IEnumerable<object?> enumerable)
    {
        var list = new List<string>();
        foreach (var item in enumerable)
        {
            switch (item)
            {
                case null:
                    continue;
                case JsonElement jsonElement:
                    list.AddRange(ExtractFromJsonElement(jsonElement));
                    break;
                default:
                    list.Add(item.ToString() ?? string.Empty);
                    break;
            }
        }

        return list;
    }
}
