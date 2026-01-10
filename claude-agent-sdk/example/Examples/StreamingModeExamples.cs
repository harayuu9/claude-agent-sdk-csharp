using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Comprehensive examples of using ClaudeSDKClient for streaming mode.
/// Demonstrates various patterns for building applications with the streaming interface.
/// </summary>
public static class StreamingModeExamples
{
    /// <summary>
    /// Basic streaming with async using pattern.
    /// </summary>
    public static async Task BasicStreamingAsync()
    {
        Console.WriteLine("=== Basic Streaming Example ===");

        await using var client = new ClaudeSDKClient();
        Console.WriteLine("User: What is 2+2?");
        await client.ConnectAsync("What is 2+2?");

        // Receive complete response using the helper method
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Multi-turn conversation using receive_response helper.
    /// </summary>
    public static async Task MultiTurnConversationAsync()
    {
        Console.WriteLine("=== Multi-Turn Conversation Example ===");

        await using var client = new ClaudeSDKClient();
        await client.ConnectAsync();

        // First turn
        Console.WriteLine("User: What's the capital of France?");
        await client.QueryAsync("What's the capital of France?");

        // Extract and print response
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        // Second turn - follow-up
        Console.WriteLine("\nUser: What's the population of that city?");
        await client.QueryAsync("What's the population of that city?");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Handle responses while sending new messages.
    /// </summary>
    public static async Task ConcurrentResponsesAsync()
    {
        Console.WriteLine("=== Concurrent Send/Receive Example ===");

        await using var client = new ClaudeSDKClient();
        await client.ConnectAsync();

        using var cts = new CancellationTokenSource();

        // Background task to continuously receive messages
        async Task ReceiveMessagesAsync()
        {
            try
            {
                await foreach (var message in client.ReceiveMessagesAsync(cts.Token))
                {
                    ExampleHelper.DisplayMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled
            }
        }

        // Start receiving in background
        var receiveTask = Task.Run(ReceiveMessagesAsync);

        // Send multiple messages with delays
        var questions = new[]
        {
            "What is 2 + 2?",
            "What is the square root of 144?",
            "What is 10% of 80?"
        };

        foreach (var question in questions)
        {
            Console.WriteLine($"\nUser: {question}");
            await client.QueryAsync(question);
            await Task.Delay(3000); // Wait between messages
        }

        // Give time for final responses
        await Task.Delay(2000);

        // Clean up
        await cts.CancelAsync();
        await receiveTask;

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate interrupt capability.
    /// </summary>
    public static async Task WithInterruptAsync()
    {
        Console.WriteLine("=== Interrupt Example ===");
        Console.WriteLine("IMPORTANT: Interrupts require active message consumption.");

        await using var client = new ClaudeSDKClient();

        // Start a long-running task
        Console.WriteLine("\nUser: Count from 1 to 100 slowly");
        await client.ConnectAsync("Count from 1 to 100 slowly, with a brief pause between each number");

        // Create a background task to consume messages
        var messagesReceived = new List<Message>();

        async Task ConsumeMessagesAsync()
        {
            await foreach (var message in client.ReceiveResponseAsync())
            {
                messagesReceived.Add(message);
                ExampleHelper.DisplayMessage(message);
            }
        }

        // Start consuming messages in the background
        var consumeTask = Task.Run(ConsumeMessagesAsync);

        // Wait 2 seconds then send interrupt
        await Task.Delay(2000);
        Console.WriteLine("\n[After 2 seconds, sending interrupt...]");
        await client.InterruptAsync();

        // Wait for the consume task to finish processing the interrupt
        await consumeTask;

        // Send new instruction after interrupt
        Console.WriteLine("\nUser: Never mind, just tell me a quick joke");
        await client.QueryAsync("Never mind, just tell me a quick joke");

        // Get the joke
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            ExampleHelper.DisplayMessage(msg);
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Manually handle message stream for custom logic.
    /// </summary>
    public static async Task ManualMessageHandlingAsync()
    {
        Console.WriteLine("=== Manual Message Handling Example ===");

        await using var client = new ClaudeSDKClient();
        await client.ConnectAsync("List 5 programming languages and their main use cases");

        // Manually process messages with custom logic
        var languagesFound = new List<string>();
        var knownLanguages = new[] { "Python", "JavaScript", "Java", "C++", "Go", "Rust", "Ruby", "C#" };

        await foreach (var message in client.ReceiveMessagesAsync())
        {
            if (message is AssistantMessage assistantMsg)
            {
                foreach (var block in assistantMsg.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");

                        // Custom logic: extract language names
                        foreach (var lang in knownLanguages)
                        {
                            if (textBlock.Text.Contains(lang) && !languagesFound.Contains(lang))
                            {
                                languagesFound.Add(lang);
                                Console.WriteLine($"Found language: {lang}");
                            }
                        }
                    }
                }
            }
            else if (message is ResultMessage)
            {
                ExampleHelper.DisplayMessage(message);
                Console.WriteLine($"Total languages mentioned: {languagesFound.Count}");
                break;
            }
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Use ClaudeAgentOptions to configure the client.
    /// </summary>
    public static async Task WithOptionsAsync()
    {
        Console.WriteLine("=== Custom Options Example ===");

        // Configure options
        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Read", "Write"], // Allow file operations
            SystemPrompt = "You are a helpful coding assistant."
        };

        await using var client = new ClaudeSDKClient(options);
        Console.WriteLine("User: Create a simple hello.txt file with a greeting message");
        await client.ConnectAsync("Create a simple hello.txt file with a greeting message");

        var toolUses = new List<string>();

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is AssistantMessage assistantMsg)
            {
                ExampleHelper.DisplayMessage(msg);
                foreach (var block in assistantMsg.Content)
                {
                    if (block is ToolUseBlock toolUse)
                    {
                        toolUses.Add(toolUse.Name);
                    }
                }
            }
            else
            {
                ExampleHelper.DisplayMessage(msg);
            }
        }

        if (toolUses.Count > 0)
        {
            Console.WriteLine($"Tools used: {string.Join(", ", toolUses)}");
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate query with async iterable messages.
    /// </summary>
    public static async Task AsyncIterablePromptAsync()
    {
        Console.WriteLine("=== Async Iterable Prompt Example ===");

        static async IAsyncEnumerable<Dictionary<string, object?>> CreateMessageStream()
        {
            Console.WriteLine("User: Hello! I have multiple questions.");
            yield return new Dictionary<string, object?>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object?> { ["role"] = "user", ["content"] = "Hello! I have multiple questions." },
                ["parent_tool_use_id"] = null,
                ["session_id"] = "qa-session"
            };

            await Task.Delay(100); // Small delay between messages

            Console.WriteLine("User: First, what's the capital of Japan?");
            yield return new Dictionary<string, object?>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object?> { ["role"] = "user", ["content"] = "First, what's the capital of Japan?" },
                ["parent_tool_use_id"] = null,
                ["session_id"] = "qa-session"
            };

            await Task.Delay(100);

            Console.WriteLine("User: Second, what's 15% of 200?");
            yield return new Dictionary<string, object?>
            {
                ["type"] = "user",
                ["message"] = new Dictionary<string, object?> { ["role"] = "user", ["content"] = "Second, what's 15% of 200?" },
                ["parent_tool_use_id"] = null,
                ["session_id"] = "qa-session"
            };
        }

        await using var client = new ClaudeSDKClient();

        // Send async iterable of messages
        await client.ConnectAsync(CreateMessageStream());

        // Receive the three responses
        for (var i = 0; i < 3; i++)
        {
            await foreach (var msg in client.ReceiveResponseAsync())
            {
                ExampleHelper.DisplayMessage(msg);
            }
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Example showing tool use blocks when running bash commands.
    /// </summary>
    public static async Task BashCommandAsync()
    {
        Console.WriteLine("=== Bash Command Example ===");

        await using var client = new ClaudeSDKClient();
        Console.WriteLine("User: Run a bash echo command");
        await client.ConnectAsync("Run a bash echo command that says 'Hello from bash!'");

        // Track all message types received
        var messageTypes = new HashSet<string>();

        await foreach (var msg in client.ReceiveMessagesAsync())
        {
            messageTypes.Add(msg.GetType().Name);

            switch (msg)
            {
                case UserMessage userMsg:
                    ExampleHelper.DisplayUserMessage(userMsg);
                    break;

                case AssistantMessage assistantMsg:
                    ExampleHelper.DisplayAssistantMessage(assistantMsg);
                    break;

                case ResultMessage resultMsg:
                    Console.WriteLine("Result ended");
                    if (resultMsg.TotalCostUsd is > 0)
                    {
                        Console.WriteLine($"Cost: ${resultMsg.TotalCostUsd:F4}");
                    }
                    goto exitLoop;
            }
        }

    exitLoop:
        Console.WriteLine($"\nMessage types received: {string.Join(", ", messageTypes)}");
        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate server info and interrupt capabilities.
    /// </summary>
    public static async Task ControlProtocolAsync()
    {
        Console.WriteLine("=== Control Protocol Example ===");
        Console.WriteLine("Shows server info retrieval and interrupt capability\n");

        await using var client = new ClaudeSDKClient();
        await client.ConnectAsync();

        // 1. Get server initialization info
        Console.WriteLine("1. Getting server info...");
        var serverInfo = client.GetServerInfo();

        if (serverInfo != null)
        {
            Console.WriteLine("Server info retrieved successfully!");
            if (serverInfo.TryGetValue("commands", out var commands) && commands is IEnumerable<object> cmdList)
            {
                Console.WriteLine($"  - Available commands: {cmdList.Count()}");
            }
            if (serverInfo.TryGetValue("output_style", out var style))
            {
                Console.WriteLine($"  - Output style: {style}");
            }
        }
        else
        {
            Console.WriteLine("No server info available (may not be in streaming mode)");
        }

        Console.WriteLine("\n2. Testing interrupt capability...");

        // Start a long-running task
        Console.WriteLine("User: Count from 1 to 20 slowly");
        await client.QueryAsync("Count from 1 to 20 slowly, pausing between each number");

        // Start consuming messages in background to enable interrupt
        var messages = new List<Message>();

        async Task ConsumeAsync()
        {
            await foreach (var msg in client.ReceiveResponseAsync())
            {
                messages.Add(msg);
                if (msg is AssistantMessage assistantMsg)
                {
                    foreach (var block in assistantMsg.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            // Print first 50 chars to show progress
                            var preview = textBlock.Text.Length > 50 ? textBlock.Text[..50] + "..." : textBlock.Text;
                            Console.WriteLine($"Claude: {preview}");
                            break;
                        }
                    }
                }
                if (msg is ResultMessage)
                {
                    break;
                }
            }
        }

        var consumeTask = Task.Run(ConsumeAsync);

        // Wait a moment then interrupt
        await Task.Delay(2000);
        Console.WriteLine("\n[Sending interrupt after 2 seconds...]");

        try
        {
            await client.InterruptAsync();
            Console.WriteLine("Interrupt sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Interrupt failed: {ex.Message}");
        }

        // Wait for task to complete
        await consumeTask;

        // Send new query after interrupt
        Console.WriteLine("\nUser: Just say 'Hello!'");
        await client.QueryAsync("Just say 'Hello!'");

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is AssistantMessage assistantMsg)
            {
                foreach (var block in assistantMsg.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"Claude: {textBlock.Text}");
                    }
                }
            }
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Demonstrate proper error handling.
    /// </summary>
    public static async Task ErrorHandlingAsync()
    {
        Console.WriteLine("=== Error Handling Example ===");

        var client = new ClaudeSDKClient();

        try
        {
            await client.ConnectAsync();

            // Send a message that will take time to process
            Console.WriteLine("User: Run a bash sleep command for 60 seconds not in the background");
            await client.QueryAsync("Run a bash sleep command for 60 seconds not in the background");

            // Try to receive response with a short timeout
            var messages = new List<Message>();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                await foreach (var msg in client.ReceiveResponseAsync(cts.Token))
                {
                    messages.Add(msg);
                    if (msg is AssistantMessage assistantMsg)
                    {
                        foreach (var block in assistantMsg.Content)
                        {
                            if (block is TextBlock textBlock)
                            {
                                var preview = textBlock.Text.Length > 50 ? textBlock.Text[..50] + "..." : textBlock.Text;
                                Console.WriteLine($"Claude: {preview}");
                            }
                        }
                    }
                    else if (msg is ResultMessage)
                    {
                        ExampleHelper.DisplayMessage(msg);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nResponse timeout after 10 seconds - demonstrating graceful handling");
                Console.WriteLine($"Received {messages.Count} messages before timeout");
            }
        }
        catch (CLIConnectionException ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
        finally
        {
            // Always disconnect
            await client.DisposeAsync();
        }

        Console.WriteLine("\n");
    }

    /// <summary>
    /// Run all streaming mode examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await BasicStreamingAsync();
        ExampleHelper.PrintSeparator();

        await MultiTurnConversationAsync();
        ExampleHelper.PrintSeparator();

        await ConcurrentResponsesAsync();
        ExampleHelper.PrintSeparator();

        await WithInterruptAsync();
        ExampleHelper.PrintSeparator();

        await ManualMessageHandlingAsync();
        ExampleHelper.PrintSeparator();

        await WithOptionsAsync();
        ExampleHelper.PrintSeparator();

        await AsyncIterablePromptAsync();
        ExampleHelper.PrintSeparator();

        await BashCommandAsync();
        ExampleHelper.PrintSeparator();

        await ControlProtocolAsync();
        ExampleHelper.PrintSeparator();

        await ErrorHandlingAsync();
    }
}
