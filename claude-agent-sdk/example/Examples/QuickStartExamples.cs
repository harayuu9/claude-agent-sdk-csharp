using ClaudeAgentSdk;

namespace Examples;

/// <summary>
/// Quick start examples demonstrating basic SDK usage.
/// </summary>
public static class QuickStartExamples
{
    /// <summary>
    /// Basic example - simple question using Query.RunAsync().
    /// </summary>
    public static async Task BasicExampleAsync()
    {
        Console.WriteLine("=== Basic Example ===");

        await foreach (var message in Query.RunAsync(prompt: "What is 2 + 2?"))
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example with custom options.
    /// </summary>
    public static async Task WithOptionsExampleAsync()
    {
        Console.WriteLine("=== With Options Example ===");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant that explains things simply.",
            MaxTurns = 1
        };

        await foreach (var message in Query.RunAsync(
            prompt: "Explain what Python is in one sentence.",
            options: options))
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example using tools.
    /// </summary>
    public static async Task WithToolsExampleAsync()
    {
        Console.WriteLine("=== With Tools Example ===");

        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Read", "Write"],
            SystemPrompt = "You are a helpful file assistant."
        };

        await foreach (var message in Query.RunAsync(
            prompt: "Create a file called hello.txt with 'Hello, World!' in it",
            options: options))
        {
            if (message is AssistantMessage assistantMessage)
            {
                foreach (var block in assistantMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
            else if (message is ResultMessage resultMessage && resultMessage.TotalCostUsd > 0)
            {
                Console.WriteLine($"\nCost: ${resultMessage.TotalCostUsd:F4}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all quick start examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await BasicExampleAsync();
        await WithOptionsExampleAsync();
        await WithToolsExampleAsync();
    }
}
