using System.Text.Json;
using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Examples demonstrating setting_sources control.
/// </summary>
public static class SettingSourcesExamples
{
    /// <summary>
    /// Default behavior - no settings loaded.
    /// </summary>
    public static async Task DefaultBehaviorAsync()
    {
        Console.WriteLine("=== Default Behavior Example ===");
        Console.WriteLine("Setting sources: None (default)");
        Console.WriteLine("Expected: No custom slash commands will be available\n");

        var sdkDir = ExampleHelper.GetSdkRootPath();

        var options = new ClaudeAgentOptions
        {
            Cwd = sdkDir
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("What is 2 + 2?");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is SystemMessage { Subtype: "init", Data: var data })
            {
                var commands = ExtractSlashCommands(data);
                Console.WriteLine($"Available slash commands: [{string.Join(", ", commands)}]");
                if (commands.Contains("commit", StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine("/commit is available (unexpected)");
                }
                else
                {
                    Console.WriteLine("/commit is NOT available (expected - no settings loaded)");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Load only user-level settings, excluding project settings.
    /// </summary>
    public static async Task UserOnlyAsync()
    {
        Console.WriteLine("=== User Settings Only Example ===");
        Console.WriteLine("Setting sources: ['user']");
        Console.WriteLine("Expected: Project slash commands (like /commit) will NOT be available\n");

        var sdkDir = ExampleHelper.GetSdkRootPath();

        var options = new ClaudeAgentOptions
        {
            SettingSources = [SettingSource.User],
            Cwd = sdkDir
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("What is 2 + 2?");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is SystemMessage { Subtype: "init", Data: var data })
            {
                var commands = ExtractSlashCommands(data);
                Console.WriteLine($"Available slash commands: [{string.Join(", ", commands)}]");
                if (commands.Contains("commit", StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine("/commit is available (unexpected)");
                }
                else
                {
                    Console.WriteLine("/commit is NOT available (expected)");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Load both project and user settings.
    /// </summary>
    public static async Task ProjectAndUserAsync()
    {
        Console.WriteLine("=== Project + User Settings Example ===");
        Console.WriteLine("Setting sources: ['user', 'project']");
        Console.WriteLine("Expected: Project slash commands (like /commit) WILL be available\n");

        var sdkDir = ExampleHelper.GetSdkRootPath();

        var options = new ClaudeAgentOptions
        {
            SettingSources = [SettingSource.User, SettingSource.Project],
            Cwd = sdkDir
        };

        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync();
        await client.QueryAsync("What is 2 + 2?");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is SystemMessage { Subtype: "init", Data: var data })
            {
                var commands = ExtractSlashCommands(data);
                Console.WriteLine($"Available slash commands: [{string.Join(", ", commands)}]");
                if (commands.Contains("commit", StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine("/commit is available (expected)");
                }
                else
                {
                    Console.WriteLine("/commit is NOT available (may require project settings)");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all setting sources examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("Starting Claude SDK Setting Sources Examples...");
        Console.WriteLine(new string('=', 50) + "\n");

        await DefaultBehaviorAsync();
        ExampleHelper.PrintSeparator();

        await UserOnlyAsync();
        ExampleHelper.PrintSeparator();

        await ProjectAndUserAsync();
    }

    private static List<string> ExtractSlashCommands(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("slash_commands", out var commandsObj) || commandsObj is null)
        {
            return [];
        }

        return commandsObj switch
        {
            JsonElement jsonElement => ExtractFromJson(jsonElement),
            IEnumerable<object?> enumerable when commandsObj is not string => enumerable
                .Where(item => item is not null)
                .Select(item => item!.ToString() ?? string.Empty)
                .ToList(),
            _ => [commandsObj.ToString() ?? string.Empty]
        };
    }

    private static List<string> ExtractFromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [element.ToString()];
        }

        return element.EnumerateArray()
            .Select(item => item.ToString())
            .Where(value => !string.IsNullOrEmpty(value))
            .ToList();
    }
}
