using System.IO;
using System.Text.Json;
using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Example demonstrating how to use plugins with Claude Code SDK.
/// </summary>
public static class PluginExamples
{
    /// <summary>
    /// Run the plugin example.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Plugin Example ===\n");

        var sdkRoot = ExampleHelper.GetSdkRootPath();
        var pluginPath = Path.Combine(sdkRoot, "example", "plugins", "demo-plugin");

        var options = new ClaudeAgentOptions
        {
            Plugins =
            [
                new SdkPluginConfig
                {
                    Type = SdkPluginType.Local,
                    Path = pluginPath
                }
            ],
            MaxTurns = 1
        };

        Console.WriteLine($"Loading plugin from: {pluginPath}\n");

        var foundPlugins = false;
        await foreach (var message in Query.RunAsync(prompt: "Hello!", options: options))
        {
            if (message is SystemMessage { Subtype: "init", Data: var data })
            {
                Console.WriteLine("System initialized!");
                Console.WriteLine($"System message data keys: [{string.Join(", ", data.Keys)}]\n");

                var plugins = ExtractPlugins(data);
                if (plugins.Count > 0)
                {
                    Console.WriteLine("Plugins loaded:");
                    foreach (var plugin in plugins)
                    {
                        Console.WriteLine($"  - {plugin}");
                    }
                    foundPlugins = true;
                }
                else
                {
                    Console.WriteLine("Plugin was passed via CLI but may not appear in system message.");
                    Console.WriteLine($"Plugin path configured: {pluginPath}");
                    foundPlugins = true;
                }
            }
        }

        if (foundPlugins)
        {
            Console.WriteLine("\nPlugin successfully configured!\n");
        }
    }

    private static List<string> ExtractPlugins(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("plugins", out var pluginsObj) || pluginsObj is null)
        {
            return [];
        }

        return pluginsObj switch
        {
            JsonElement jsonElement => ExtractPluginsFromJson(jsonElement),
            IEnumerable<object?> enumerable when pluginsObj is not string => enumerable
                .Select(item => item?.ToString() ?? string.Empty)
                .Where(text => !string.IsNullOrEmpty(text))
                .ToList(),
            _ => [pluginsObj.ToString() ?? string.Empty]
        };
    }

    private static List<string> ExtractPluginsFromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [element.ToString()];
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var name = item.TryGetProperty("name", out var nameProp) ? nameProp.ToString() : "unknown";
                var path = item.TryGetProperty("path", out var pathProp) ? pathProp.ToString() : "unknown";
                list.Add($"{name} (path: {path})");
            }
            else
            {
                list.Add(item.ToString());
            }
        }

        return list;
    }
}
