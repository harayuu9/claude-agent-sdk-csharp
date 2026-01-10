using ClaudeAgentSdk;

namespace Examples;

/// <summary>
/// Examples demonstrating max budget configuration.
/// </summary>
public static class MaxBudgetExamples
{
    /// <summary>
    /// Example without budget limit.
    /// </summary>
    public static async Task WithoutBudgetAsync()
    {
        Console.WriteLine("=== Without Budget Limit ===");

        await foreach (var message in Query.RunAsync(prompt: "What is 2 + 2?"))
        {
            switch (message)
            {
                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Claude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage:
                    if (resultMessage.TotalCostUsd is { } cost)
                    {
                        Console.WriteLine($"Total cost: ${cost:F4}");
                    }
                    Console.WriteLine($"Status: {resultMessage.Subtype}");
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example with a comfortable budget that will not be exceeded.
    /// </summary>
    public static async Task WithReasonableBudgetAsync()
    {
        Console.WriteLine("=== With Reasonable Budget ($0.10) ===");

        var options = new ClaudeAgentOptions
        {
            MaxBudgetUsd = 0.10
        };

        await foreach (var message in Query.RunAsync(prompt: "What is 2 + 2?", options: options))
        {
            switch (message)
            {
                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Claude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage:
                    if (resultMessage.TotalCostUsd is { } cost)
                    {
                        Console.WriteLine($"Total cost: ${cost:F4}");
                    }
                    Console.WriteLine($"Status: {resultMessage.Subtype}");
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example with a tight budget likely to be exceeded.
    /// </summary>
    public static async Task WithTightBudgetAsync()
    {
        Console.WriteLine("=== With Tight Budget ($0.0001) ===");

        var options = new ClaudeAgentOptions
        {
            MaxBudgetUsd = 0.0001
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "Read the README.md file and summarize it",
                           options: options))
        {
            switch (message)
            {
                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Claude: {textBlock.Text}");
                        }
                    }
                    break;

                case ResultMessage resultMessage:
                    if (resultMessage.TotalCostUsd is { } cost)
                    {
                        Console.WriteLine($"Total cost: ${cost:F4}");
                    }

                    Console.WriteLine($"Status: {resultMessage.Subtype}");

                    if (resultMessage.Subtype == "error_max_budget_usd")
                    {
                        Console.WriteLine("Budget limit exceeded!");
                        Console.WriteLine("Note: The cost may exceed the budget by up to one API call's worth.");
                    }
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all budget examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        Console.WriteLine("This example demonstrates using max_budget_usd to control API costs.\n");

        await WithoutBudgetAsync();
        await WithReasonableBudgetAsync();
        await WithTightBudgetAsync();

        Console.WriteLine("Note: Budget checking happens after each API call completes,");
        Console.WriteLine("so the final cost may slightly exceed the specified budget.\n");
    }
}
