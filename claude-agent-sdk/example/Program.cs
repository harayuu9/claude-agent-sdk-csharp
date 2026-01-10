using Examples;
using Examples.Helpers;

Console.WriteLine("Claude Agent SDK - C# Examples");
Console.WriteLine("==============================\n");

var streamingOptions = new List<MenuOption>
{
    new("s1", "Streaming: Basic streaming", StreamingModeExamples.BasicStreamingAsync),
    new("s2", "Streaming: Multi-turn conversation", StreamingModeExamples.MultiTurnConversationAsync),
    new("s3", "Streaming: Concurrent send/receive", StreamingModeExamples.ConcurrentResponsesAsync),
    new("s4", "Streaming: Interrupt handling", StreamingModeExamples.WithInterruptAsync),
    new("s5", "Streaming: Manual message handling", StreamingModeExamples.ManualMessageHandlingAsync),
    new("s6", "Streaming: Custom options", StreamingModeExamples.WithOptionsAsync),
    new("s7", "Streaming: Async iterable prompt", StreamingModeExamples.AsyncIterablePromptAsync),
    new("s8", "Streaming: Bash command tool use", StreamingModeExamples.BashCommandAsync),
    new("s9", "Streaming: Control protocol + interrupt", StreamingModeExamples.ControlProtocolAsync),
    new("s10", "Streaming: Error handling", StreamingModeExamples.ErrorHandlingAsync)
};

var mainOptions = new List<MenuOption>
{
    new("1", "Quick start examples (all)", QuickStartExamples.RunAllAsync),
    new("2", "Streaming mode examples (all)", StreamingModeExamples.RunAllAsync),
    new("3", "System prompt examples", SystemPromptExamples.RunAllAsync),
    new("4", "Max budget examples", MaxBudgetExamples.RunAllAsync),
    new("5", "Tools option examples", ToolsOptionExamples.RunAllAsync),
    new("6", "MCP calculator example", McpCalculatorExamples.RunAsync),
    new("7", "Hooks examples", HooksExamples.RunAllAsync),
    new("8", "Tool permission callback example", ToolPermissionExamples.RunAsync),
    new("9", "Agents examples", AgentsExamples.RunAllAsync),
    new("10", "Setting sources examples", SettingSourcesExamples.RunAllAsync),
    new("11", "Partial messages example", PartialMessagesExamples.RunAsync),
    new("12", "Stderr callback example", StderrCallbackExamples.RunAsync),
    new("13", "Filesystem agents example", FilesystemAgentsExamples.RunAsync),
    new("14", "Plugin example", PluginExamples.RunAsync),
    new("all", "Run all examples", RunAllExamplesAsync)
};

var optionLookup = mainOptions
    .Concat(streamingOptions)
    .ToDictionary(opt => opt.Key, opt => opt, StringComparer.OrdinalIgnoreCase);

while (true)
{
    PrintMenu(mainOptions, streamingOptions);
    var choice = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(choice))
    {
        continue;
    }

    if (choice.Equals("q", StringComparison.OrdinalIgnoreCase) ||
        choice.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        choice.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (!optionLookup.TryGetValue(choice, out var option))
    {
        Console.WriteLine("Unknown selection. Please try again.\n");
        continue;
    }

    try
    {
        await option.Action();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error running example: {ex.Message}");
    }
}

return;

static void PrintMenu(IEnumerable<MenuOption> mainOptions, IEnumerable<MenuOption> streamingOptions)
{
    Console.WriteLine("Select an example to run (q to quit):");
    foreach (var opt in mainOptions)
    {
        Console.WriteLine($"  {opt.Key.PadRight(4)} {opt.Description}");
    }

    Console.WriteLine("\nStreaming sub-options:");
    foreach (var opt in streamingOptions)
    {
        Console.WriteLine($"  {opt.Key.PadRight(4)} {opt.Description}");
    }

    Console.Write("\nChoice: ");
}

static async Task RunAllExamplesAsync()
{
    await QuickStartExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await StreamingModeExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await SystemPromptExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await MaxBudgetExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await ToolsOptionExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await McpCalculatorExamples.RunAsync();
    ExampleHelper.PrintSeparator();

    await HooksExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await ToolPermissionExamples.RunAsync();
    ExampleHelper.PrintSeparator();

    await AgentsExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await SettingSourcesExamples.RunAllAsync();
    ExampleHelper.PrintSeparator();

    await PartialMessagesExamples.RunAsync();
    ExampleHelper.PrintSeparator();

    await StderrCallbackExamples.RunAsync();
    ExampleHelper.PrintSeparator();

    await FilesystemAgentsExamples.RunAsync();
    ExampleHelper.PrintSeparator();

    await PluginExamples.RunAsync();
}

record MenuOption(string Key, string Description, Func<Task> Action);