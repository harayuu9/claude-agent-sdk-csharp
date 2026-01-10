using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Example of streaming partial messages via include_partial_messages.
/// </summary>
public static class PartialMessagesExamples
{
    /// <summary>
    /// Run the partial messages example.
    /// </summary>
    public static async Task RunAsync()
    {
        var options = new ClaudeAgentOptions
        {
            IncludePartialMessages = true,
            Model = "claude-sonnet-4-5",
            MaxTurns = 2,
            Env = new Dictionary<string, string>
            {
                ["MAX_THINKING_TOKENS"] = "8000"
            }
        };

        await using var client = new ClaudeSDKClient(options);

        try
        {
            await client.ConnectAsync();

            var prompt = "Think of three jokes, then tell one";
            Console.WriteLine("Partial Message Streaming Example");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Prompt: {prompt}\n");

            await client.QueryAsync(prompt);

            await foreach (var message in client.ReceiveResponseAsync())
            {
                switch (message)
                {
                    case StreamEvent streamEvent:
                        ExampleHelper.DisplayStreamEvent(streamEvent);
                        break;
                    case UserMessage userMessage:
                        ExampleHelper.DisplayUserMessage(userMessage);
                        break;
                    case AssistantMessage assistantMessage:
                        ExampleHelper.DisplayAssistantMessage(assistantMessage);
                        break;
                    default:
                        ExampleHelper.DisplayMessage(message);
                        break;
                }
            }
        }
        finally
        {
            await client.DisposeAsync();
        }

        Console.WriteLine();
    }
}
