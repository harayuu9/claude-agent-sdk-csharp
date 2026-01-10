using ClaudeAgentSdk;

namespace Examples;

/// <summary>
/// Examples showing how to use custom agents.
/// </summary>
public static class AgentsExamples
{
    /// <summary>
    /// Example using a custom code reviewer agent.
    /// </summary>
    public static async Task CodeReviewerExampleAsync()
    {
        Console.WriteLine("=== Code Reviewer Agent Example ===");

        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["code-reviewer"] = new AgentDefinition
                {
                    Description = "Reviews code for best practices and potential issues",
                    Prompt = "You are a code reviewer. Analyze code for bugs, performance issues, " +
                             "security vulnerabilities, and adherence to best practices. " +
                             "Provide constructive feedback.",
                    Tools = ["Read", "Grep"],
                    Model = AgentModel.Sonnet
                }
            }
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "Use the code-reviewer agent to review the code in src/claude_agent_sdk/types.cs",
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
                    if (resultMessage.TotalCostUsd is { } cost && cost > 0)
                    {
                        Console.WriteLine($"\nCost: ${cost:F4}");
                    }
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example using a documentation writer agent.
    /// </summary>
    public static async Task DocumentationWriterExampleAsync()
    {
        Console.WriteLine("=== Documentation Writer Agent Example ===");

        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["doc-writer"] = new AgentDefinition
                {
                    Description = "Writes comprehensive documentation",
                    Prompt = "You are a technical documentation expert. Write clear, comprehensive " +
                             "documentation with examples. Focus on clarity and completeness.",
                    Tools = ["Read", "Write", "Edit"],
                    Model = AgentModel.Sonnet
                }
            }
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "Use the doc-writer agent to explain what AgentDefinition is used for",
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
                    if (resultMessage.TotalCostUsd is { } cost && cost > 0)
                    {
                        Console.WriteLine($"\nCost: ${cost:F4}");
                    }
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example with multiple custom agents.
    /// </summary>
    public static async Task MultipleAgentsExampleAsync()
    {
        Console.WriteLine("=== Multiple Agents Example ===");

        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["analyzer"] = new AgentDefinition
                {
                    Description = "Analyzes code structure and patterns",
                    Prompt = "You are a code analyzer. Examine code structure, patterns, and architecture.",
                    Tools = ["Read", "Grep", "Glob"]
                },
                ["tester"] = new AgentDefinition
                {
                    Description = "Creates and runs tests",
                    Prompt = "You are a testing expert. Write comprehensive tests and ensure code quality.",
                    Tools = ["Read", "Write", "Bash"],
                    Model = AgentModel.Sonnet
                }
            },
            SettingSources = [SettingSource.User, SettingSource.Project]
        };

        await foreach (var message in Query.RunAsync(
                           prompt: "Use the analyzer agent to find all source files in the examples/ directory",
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
                    if (resultMessage.TotalCostUsd is { } cost && cost > 0)
                    {
                        Console.WriteLine($"\nCost: ${cost:F4}");
                    }
                    break;
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all agent examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await CodeReviewerExampleAsync();
        await DocumentationWriterExampleAsync();
        await MultipleAgentsExampleAsync();
    }
}
