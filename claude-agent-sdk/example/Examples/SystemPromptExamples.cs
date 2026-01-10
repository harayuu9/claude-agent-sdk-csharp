using ClaudeAgentSdk;

namespace Examples;

/// <summary>
/// Examples demonstrating different system prompt configurations.
/// </summary>
public static class SystemPromptExamples
{
    /// <summary>
    /// Example with no system prompt (vanilla Claude).
    /// </summary>
    public static async Task NoSystemPromptAsync()
    {
        Console.WriteLine("=== No System Prompt (Vanilla Claude) ===");

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
    /// Example with system prompt string.
    /// </summary>
    public static async Task StringSystemPromptAsync()
    {
        Console.WriteLine("=== String System Prompt ===");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a pirate assistant. Respond in pirate speak."
        };

        await foreach (var message in Query.RunAsync(prompt: "What is 2 + 2?", options: options))
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
    /// Example with system prompt preset.
    /// </summary>
    public static async Task PresetSystemPromptAsync()
    {
        Console.WriteLine("=== Preset System Prompt (Default) ===");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new SystemPromptPreset { Preset = "claude_code" }
        };

        await foreach (var message in Query.RunAsync(prompt: "What is 2 + 2?", options: options))
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
    /// Example with system prompt preset and append.
    /// </summary>
    public static async Task PresetWithAppendAsync()
    {
        Console.WriteLine("=== Preset System Prompt with Append ===");

        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new SystemPromptPreset
            {
                Preset = "claude_code",
                Append = "Always end your response with a fun fact."
            }
        };

        await foreach (var message in Query.RunAsync(prompt: "What is 2 + 2?", options: options))
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
    /// Run all system prompt examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await NoSystemPromptAsync();
        await StringSystemPromptAsync();
        await PresetSystemPromptAsync();
        await PresetWithAppendAsync();
    }
}
