using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Example demonstrating stderr callback for capturing CLI debug output.
/// </summary>
public static class StderrCallbackExamples
{
    /// <summary>
    /// Capture stderr output from the CLI using a callback.
    /// </summary>
    public static async Task RunAsync()
    {
        var stderrMessages = new List<string>();

        void StderrCallback(string message)
        {
            stderrMessages.Add(message);
            if (message.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Error detected: {message}");
            }
        }

        var options = new ClaudeAgentOptions
        {
            Stderr = StderrCallback,
            ExtraArgs = new Dictionary<string, string?>
            {
                ["debug-to-stderr"] = null
            }
        };

        Console.WriteLine("Running query with stderr capture...");

        await foreach (var message in Query.RunAsync(
                           prompt: "What is 2+2?",
                           options: options))
        {
            switch (message)
            {
                case AssistantMessage assistantMessage:
                    foreach (var block in assistantMessage.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            Console.WriteLine($"Response: {textBlock.Text}");
                        }
                    }
                    break;
                default:
                    ExampleHelper.DisplayMessage(message);
                    break;
            }
        }

        Console.WriteLine($"\nCaptured {stderrMessages.Count} stderr lines");
        if (stderrMessages.Count > 0)
        {
            Console.WriteLine("First stderr line:");
            Console.WriteLine(stderrMessages[0].Length > 100
                ? stderrMessages[0][..100]
                : stderrMessages[0]);
        }

        Console.WriteLine();
    }
}
