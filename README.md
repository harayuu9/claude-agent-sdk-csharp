# Claude Agent SDK for C#

[![NuGet](https://img.shields.io/nuget/v/ClaudeAgentSdk.CSharp.svg)](https://www.nuget.org/packages/ClaudeAgentSdk.CSharp/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A C# SDK for integrating with [Claude Code](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) CLI, enabling you to build AI-powered applications with Claude.

> **Note**: This project is a C# port of the official [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python).

[日本語版 README](README.ja.md)

## Overview

Claude Agent SDK for C# provides two main APIs for interacting with Claude Code:

- **`Query.RunAsync()` / `ClaudeAgent.QueryAsync()`** - Stateless, one-shot queries
- **`ClaudeSDKClient`** - Stateful, interactive conversations with full control

### Key Features

- Two flexible APIs for different use cases
- Image input support (base64, URL, local file)
- MCP (Model Context Protocol) support for custom tools
- Custom agent definitions with extended configuration
- Hooks for event handling (PreToolUse, PostToolUse, Notification, etc.)
- Tool permission control with agent context
- Session management (list, rename, tag, fork, delete)
- MCP server control (status, reconnect, toggle)
- Context usage monitoring
- Task management (start, progress, stop)
- Extended thinking configuration
- Streaming support with `IAsyncEnumerable`

## Prerequisites

- [.NET 10.0](https://dotnet.microsoft.com/download) or later
- [Claude Code CLI](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) installed and configured
  - Run `claude` command to verify installation

## Installation

### NuGet Package

```bash
dotnet add package ClaudeAgentSdk.CSharp
```

Or via Package Manager:

```powershell
Install-Package ClaudeAgentSdk.CSharp
```

### From Source

```bash
git clone https://github.com/harayuu9/claude-agent-sdk-csharp.git
cd claude-agent-sdk-csharp
dotnet build
```

## Quick Start

### Simple Query

```csharp
using ClaudeAgentSdk;

// One-shot query
await foreach (var message in Query.RunAsync("What is 2 + 2?"))
{
    if (message is AssistantMessage assistantMessage)
    {
        foreach (var block in assistantMessage.Content)
        {
            if (block is TextBlock textBlock)
                Console.WriteLine(textBlock.Text);
        }
    }
}
```

### Interactive Conversation

```csharp
using ClaudeAgentSdk;

await using var client = new ClaudeSDKClient();

// Start conversation
await client.ConnectAsync("What is the capital of France?");
await foreach (var msg in client.ReceiveResponseAsync())
{
    // Process response
}

// Follow-up question
await client.QueryAsync("What's its population?");
await foreach (var msg in client.ReceiveResponseAsync())
{
    // Process response
}
```

## Features

### Options

Configure the SDK behavior with `ClaudeAgentOptions`:

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = "You are a helpful Python expert.",
    Cwd = "/path/to/project",
    AllowedTools = ["Read", "Write", "Bash"],
    PermissionMode = PermissionMode.AcceptEdits,
    MaxTurns = 10,
    // New: Extended thinking
    Thinking = new ThinkingConfigAdaptive(),
    // New: Effort level
    Effort = "high",
    // New: Session ID
    SessionId = "my-session-id",
    // New: Task budget
    TaskBudget = new TaskBudget { Total = 100000 }
};

await foreach (var message in Query.RunAsync("Create a web server", options))
{
    // ...
}
```

### Image Input

Send images to Claude alongside text using `ImageBlock`. Supports base64-encoded data, URLs, and local files.

```csharp
// From a local file (media type auto-detected from extension)
await client.QueryAsync([
    new TextBlock { Text = "What is in this image?" },
    ImageBlock.FromFile("photo.png")
]);

// From a URL
await client.QueryAsync([
    new TextBlock { Text = "Describe this image." },
    ImageBlock.FromUrl("https://example.com/image.jpg")
]);

// From base64 data
await client.QueryAsync([
    ImageBlock.FromBase64(base64String, "image/png")
]);

// Async file loading
var imageBlock = await ImageBlock.FromFileAsync("large-photo.jpg");
await client.QueryAsync([
    new TextBlock { Text = "Analyze this photo." },
    imageBlock
]);
```

Supported file extensions for auto-detection: `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`.
You can also pass an explicit media type: `ImageBlock.FromFile("image.bmp", "image/bmp")`.

> **Note**: This is a C# SDK-unique feature not available in the original Python SDK.

### System Prompt from File

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = new SystemPromptFile { Path = "/path/to/prompt.txt" }
};
```

### MCP Tools

Define custom tools using the Model Context Protocol:

```csharp
// Define a tool
var addTool = SdkMcpTool.Create<CalcArgs>(
    "add",
    "Add two numbers",
    async args => SdkMcpToolResult.FromText($"Result: {args.A + args.B}"));

// Create an MCP server
var server = SdkMcpServer.Create("calculator", "1.0.0", [addTool]);

var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["calc"] = server
    }
};
```

Or use the `[Tool]` attribute:

```csharp
public class CalculatorTools
{
    [Tool("add", "Add two numbers")]
    public async Task<SdkMcpToolResult> Add(CalcArgs args)
    {
        return SdkMcpToolResult.FromText($"Result: {args.A + args.B}");
    }
}

var server = SdkMcpServer.FromType<CalculatorTools>("calculator");
```

### MCP Server Control

Monitor and control MCP server connections at runtime:

```csharp
await using var client = new ClaudeSDKClient(options);
await client.ConnectAsync();

// Get status of all MCP servers
var status = await client.GetMcpStatusAsync();
foreach (var server in status.McpServers)
{
    Console.WriteLine($"{server.Name}: {server.Status}");
}

// Reconnect a failed server
await client.ReconnectMcpServerAsync("my-server");

// Toggle a server on/off
await client.ToggleMcpServerAsync("my-server", enabled: false);
```

### Custom Agents

Define specialized agents for specific tasks:

```csharp
var options = new ClaudeAgentOptions
{
    Agents = new Dictionary<string, AgentDefinition>
    {
        ["code-reviewer"] = new AgentDefinition
        {
            Description = "Reviews code for issues and best practices",
            Prompt = "You are a code reviewer. Analyze code for bugs, security issues, and style.",
            Tools = ["Read", "Grep", "Glob"],
            Model = "sonnet",
            // New fields
            DisallowedTools = ["Write", "Bash"],
            Skills = ["code-review"],
            MaxTurns = 5,
            Background = false,
            Effort = "high",
            PermissionMode = PermissionMode.Default
        }
    }
};
```

### Hooks

Handle events during execution:

```csharp
var options = new ClaudeAgentOptions
{
    Hooks = new Dictionary<HookEvent, List<HookMatcher>>
    {
        [HookEvent.PreToolUse] =
        [
            new HookMatcher
            {
                Matcher = "Bash",
                Hooks =
                [
                    async (input, toolUseId, context) =>
                    {
                        if (input is PreToolUseHookInput preToolUse)
                        {
                            var command = preToolUse.ToolInput["command"]?.ToString();
                            if (command?.Contains("rm -rf") == true)
                                return new SyncHookJsonOutput { Continue = false, StopReason = "Dangerous command blocked" };
                        }
                        return new SyncHookJsonOutput { Continue = true };
                    }
                ]
            }
        ],
        // New hook events
        [HookEvent.PostToolUseFailure] =
        [
            new HookMatcher
            {
                Hooks =
                [
                    async (input, toolUseId, context) =>
                    {
                        if (input is PostToolUseFailureHookInput failure)
                            Console.WriteLine($"Tool {failure.ToolName} failed: {failure.Error}");
                        return new SyncHookJsonOutput { Continue = true };
                    }
                ]
            }
        ],
        [HookEvent.Notification] =
        [
            new HookMatcher
            {
                Hooks =
                [
                    async (input, toolUseId, context) =>
                    {
                        if (input is NotificationHookInput notification)
                            Console.WriteLine($"Notification: {notification.Message}");
                        return new SyncHookJsonOutput { Continue = true };
                    }
                ]
            }
        ]
    }
};
```

### Tool Permission Control

Fine-grained control over tool execution with agent context:

```csharp
var options = new ClaudeAgentOptions
{
    CanUseTool = async (toolName, input, context) =>
    {
        // Access tool_use_id and agent_id from context
        Console.WriteLine($"Tool: {toolName}, ToolUseId: {context.ToolUseId}, AgentId: {context.AgentId}");

        if (toolName == "Bash")
        {
            var command = input["command"]?.ToString();
            if (command?.Contains("sudo") == true)
                return new PermissionResultDeny { Message = "sudo not allowed" };
        }
        return new PermissionResultAllow();
    }
};
```

### Context Usage Monitoring

Monitor token usage and context window:

```csharp
await using var client = new ClaudeSDKClient(options);
await client.ConnectAsync("Hello");

var usage = await client.GetContextUsageAsync();
Console.WriteLine($"Total tokens: {usage.TotalTokens}/{usage.MaxTokens} ({usage.Percentage:F1}%)");
foreach (var category in usage.Categories)
{
    Console.WriteLine($"  {category.Name}: {category.Tokens} tokens");
}
```

### Task Management

Monitor and control background tasks:

```csharp
await using var client = new ClaudeSDKClient(options);
await client.ConnectAsync("Run a complex analysis");

await foreach (var msg in client.ReceiveMessagesAsync())
{
    switch (msg)
    {
        case TaskStartedMessage started:
            Console.WriteLine($"Task started: {started.Description}");
            break;
        case TaskProgressMessage progress:
            Console.WriteLine($"Task progress: {progress.Description} ({progress.Usage.TotalTokens} tokens)");
            break;
        case TaskNotificationMessage notification:
            Console.WriteLine($"Task {notification.Status}: {notification.Summary}");
            break;
        case RateLimitEvent rateLimit:
            Console.WriteLine($"Rate limit: {rateLimit.RateLimitInfo.Status}");
            break;
        case ResultMessage result:
            Console.WriteLine($"Done! Cost: ${result.TotalCostUsd}");
            break;
    }
}

// Stop a running task
await client.StopTaskAsync("task-id");
```

### Extended Thinking

Configure Claude's thinking behavior:

```csharp
// Adaptive thinking (Claude decides when to think)
var options = new ClaudeAgentOptions
{
    Thinking = new ThinkingConfigAdaptive()
};

// Fixed thinking budget
var options2 = new ClaudeAgentOptions
{
    Thinking = new ThinkingConfigEnabled { BudgetTokens = 10000 }
};

// Disable thinking
var options3 = new ClaudeAgentOptions
{
    Thinking = new ThinkingConfigDisabled()
};
```

### Session Management

Manage Claude session history programmatically:

```csharp
using ClaudeAgentSdk;

// List sessions
var sessions = Sessions.ListSessions(directory: "/path/to/project");
foreach (var session in sessions)
{
    Console.WriteLine($"{session.SessionId}: {session.Summary} (modified: {session.LastModified})");
}

// Get session info
var info = Sessions.GetSessionInfo("session-uuid", directory: "/path/to/project");

// Read session messages
var messages = Sessions.GetSessionMessages("session-uuid");
foreach (var msg in messages)
{
    Console.WriteLine($"[{msg.Type}] {msg.Uuid}");
}

// Rename a session
SessionMutations.RenameSession("session-uuid", "My Custom Title");

// Tag a session
SessionMutations.TagSession("session-uuid", "important");

// Fork a session
var result = SessionMutations.ForkSession("session-uuid", title: "Forked experiment");
Console.WriteLine($"New session: {result.SessionId}");

// Delete a session
SessionMutations.DeleteSession("session-uuid");
```

## Examples

The `example/` directory contains comprehensive examples demonstrating all SDK features:

```bash
cd claude-agent-sdk/example
dotnet run
```

Available examples:
- Quick Start - Basic usage patterns
- Streaming Mode - Interactive conversations
- System Prompt - Custom system prompts
- MCP Calculator - Custom tool integration
- Hooks - Event handling
- Tool Permissions - Access control
- Custom Agents - Specialized agents
- And more...

## Testing

Run unit tests:

```bash
cd claude-agent-sdk/unit-test
dotnet test
```

Run E2E tests (requires Claude CLI):

```bash
dotnet test --filter "FullyQualifiedName~E2E"
```

## License

MIT License - see [LICENSE](LICENSE) for details.

## Related Projects

- [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python) - Official Python SDK (original implementation)
- [Claude Code](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) - Claude Code CLI documentation
