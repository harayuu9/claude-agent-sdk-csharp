using ClaudeAgentSdk;
using Examples.Helpers;

namespace Examples;

/// <summary>
/// Example showing an in-process MCP calculator server using [Tool] attributes.
/// </summary>
public static class McpCalculatorExamples
{
    private sealed class CalculatorTools
    {
        [Tool("add", "Add two numbers")]
        public Task<SdkMcpToolResult> AddAsync(BinaryArgs args)
        {
            var result = args.A + args.B;
            return Task.FromResult(SdkMcpToolResult.FromText($"{args.A} + {args.B} = {result}"));
        }

        [Tool("subtract", "Subtract one number from another")]
        public Task<SdkMcpToolResult> SubtractAsync(BinaryArgs args)
        {
            var result = args.A - args.B;
            return Task.FromResult(SdkMcpToolResult.FromText($"{args.A} - {args.B} = {result}"));
        }

        [Tool("multiply", "Multiply two numbers")]
        public Task<SdkMcpToolResult> MultiplyAsync(BinaryArgs args)
        {
            var result = args.A * args.B;
            return Task.FromResult(SdkMcpToolResult.FromText($"{args.A} × {args.B} = {result}"));
        }

        [Tool("divide", "Divide one number by another")]
        public Task<SdkMcpToolResult> DivideAsync(BinaryArgs args)
        {
            if (Math.Abs(args.B) < double.Epsilon)
            {
                return Task.FromResult(SdkMcpToolResult.FromError("Error: Division by zero is not allowed"));
            }

            var result = args.A / args.B;
            return Task.FromResult(SdkMcpToolResult.FromText($"{args.A} ÷ {args.B} = {result}"));
        }

        [Tool("sqrt", "Calculate square root")]
        public Task<SdkMcpToolResult> SquareRootAsync(UnaryArgs args)
        {
            if (args.N < 0)
            {
                return Task.FromResult(SdkMcpToolResult.FromError(
                    $"Error: Cannot calculate square root of negative number {args.N}"));
            }

            var result = Math.Sqrt(args.N);
            return Task.FromResult(SdkMcpToolResult.FromText($"√{args.N} = {result}"));
        }

        [Tool("power", "Raise a number to a power")]
        public Task<SdkMcpToolResult> PowerAsync(PowerArgs args)
        {
            var result = Math.Pow(args.Base, args.Exponent);
            return Task.FromResult(SdkMcpToolResult.FromText($"{args.Base}^{args.Exponent} = {result}"));
        }
    }

    private sealed record BinaryArgs(double A, double B);
    private sealed record UnaryArgs(double N);
    private sealed record PowerArgs(double Base, double Exponent);

    /// <summary>
    /// Run example calculations using the SDK MCP server with the streaming client.
    /// </summary>
    public static async Task RunAsync()
    {
        // Create the calculator server with all tools.
        var calculatorServer = SdkMcpServerExtensions.FromType<CalculatorTools>("calculator", "2.0.0");

        // Configure Claude to use the calculator server with allowed tools.
        var options = new ClaudeAgentOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["calc"] = calculatorServer
            },
            AllowedTools =
            [
                "mcp__calc__add",
                "mcp__calc__subtract",
                "mcp__calc__multiply",
                "mcp__calc__divide",
                "mcp__calc__sqrt",
                "mcp__calc__power"
            ]
        };

        var prompts = new[]
        {
            "List your tools",
            "Calculate 15 + 27",
            "What is 100 divided by 7?",
            "Calculate the square root of 144",
            "What is 2 raised to the power of 8?",
            "Calculate (12 + 8) * 3 - 10"
        };

        foreach (var prompt in prompts)
        {
            Console.WriteLine($"\nPrompt: {prompt}");
            Console.WriteLine(new string('=', 50));

            await using var client = new ClaudeSDKClient(options);
            await client.ConnectAsync();
            await client.QueryAsync(prompt);

            await foreach (var message in client.ReceiveResponseAsync())
            {
                DisplayMessage(message);
            }
        }

        Console.WriteLine();
    }

    private static void DisplayMessage(Message message)
    {
        switch (message)
        {
            case UserMessage userMessage:
                foreach (var block in userMessage.Content)
                {
                    if (block is TextBlock textBlock)
                    {
                        Console.WriteLine($"User: {textBlock.Text}");
                    }
                    else if (block is ToolResultBlock toolResult)
                    {
                        var contentPreview = toolResult.Content?.ToString() ?? "None";
                        Console.WriteLine($"Tool Result: {contentPreview[..Math.Min(100, contentPreview.Length)]}");
                    }
                }
                break;

            case AssistantMessage assistantMessage:
                foreach (var block in assistantMessage.Content)
                {
                    switch (block)
                    {
                        case TextBlock textBlock:
                            Console.WriteLine($"Claude: {textBlock.Text}");
                            break;
                        case ToolUseBlock toolUse:
                            Console.WriteLine($"Using tool: {toolUse.Name}");
                            if (toolUse.Input is not null && toolUse.Input.Count > 0)
                            {
                                Console.WriteLine($"  Input: {ExampleHelper.SerializeObject(toolUse.Input)}");
                            }
                            break;
                    }
                }
                break;

            case ResultMessage resultMessage:
                Console.WriteLine("Result ended");
                if (resultMessage.TotalCostUsd is { } cost)
                {
                    Console.WriteLine($"Cost: ${cost:F6}");
                }
                break;
        }
    }
}
