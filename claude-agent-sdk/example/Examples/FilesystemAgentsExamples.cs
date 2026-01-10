using System.Text.Json;
using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Example of loading filesystem-based agents via setting_sources.
/// </summary>
public static class FilesystemAgentsExamples
{
    /// <summary>
    /// Run the filesystem agents example.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Filesystem Agents Example ===");
        Console.WriteLine("Testing: setting_sources=['project'] with .claude/agents/test-agent.md\n");

        var sdkDir = ExampleHelper.GetSdkRootPath();

        var options = new ClaudeAgentOptions
        {
            SettingSources = [SettingSource.Project],
            Cwd = sdkDir
        };

        var messageTypes = new List<string>();
        var agentsFound = new List<string>();

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("Say hello in exactly 3 words");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            messageTypes.Add(msg.GetType().Name);

            switch (msg)
            {
                case SystemMessage { Subtype: "init", Data: var data }:
                    agentsFound = ExtractAgents(data);
                    Console.WriteLine($"Init message received. Agents loaded: [{string.Join(", ", agentsFound)}]");
                    break;

                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Assistant: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage:
                    var cost = resultMessage.TotalCostUsd ?? 0;
                    Console.WriteLine($"Result: subtype={resultMessage.Subtype}, cost=${cost:F4}");
                    break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"Message types received: [{string.Join(", ", messageTypes)}]");
        Console.WriteLine($"Total messages: {messageTypes.Count}");

        var hasInit = messageTypes.Contains(nameof(SystemMessage));
        var hasAssistant = messageTypes.Contains(nameof(AssistantMessage));
        var hasResult = messageTypes.Contains(nameof(ResultMessage));
        var hasTestAgent = agentsFound.Any(agent => agent.Equals("test-agent", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine();
        if (hasInit && hasAssistant && hasResult)
        {
            Console.WriteLine("Received full response (init, assistant, result)");
        }
        else
        {
            Console.WriteLine("Did not receive full response");
            Console.WriteLine($"  - Init: {hasInit}");
            Console.WriteLine($"  - Assistant: {hasAssistant}");
            Console.WriteLine($"  - Result: {hasResult}");
        }

        if (hasTestAgent)
        {
            Console.WriteLine("test-agent was loaded from filesystem");
        }
        else
        {
            Console.WriteLine("test-agent was NOT loaded (may not exist in .claude/agents/)");
        }

        Console.WriteLine();
    }

    private static List<string> ExtractAgents(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("agents", out var agentsObj) || agentsObj is null)
        {
            return [];
        }

        return agentsObj switch
        {
            JsonElement jsonElement => ExtractAgentsFromJson(jsonElement),
            IEnumerable<object?> enumerable when agentsObj is not string => enumerable
                .Select(a => a switch
                {
                    JsonElement element => ExtractAgentName(element),
                    _ => a?.ToString() ?? string.Empty
                })
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList(),
            _ => [agentsObj.ToString() ?? string.Empty]
        };
    }

    private static List<string> ExtractAgentsFromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [element.ToString()];
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            var name = ExtractAgentName(item);
            if (!string.IsNullOrEmpty(name))
            {
                list.Add(name);
            }
        }

        return list;
    }

    private static string ExtractAgentName(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object when element.TryGetProperty("name", out var nameProp) => nameProp.ToString(),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => element.ToString()
        };
    }
}
