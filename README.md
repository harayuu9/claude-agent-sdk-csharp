# Claude Agent SDK for C#

[![NuGet](https://img.shields.io/nuget/v/ClaudeAgentSdk.svg)](https://www.nuget.org/packages/ClaudeAgentSdk/)
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
- MCP (Model Context Protocol) support for custom tools
- Custom agent definitions
- Hooks for event handling (PreToolUse, PostToolUse, etc.)
- Tool permission control
- Streaming support with `IAsyncEnumerable`

## Prerequisites

- [.NET 10.0](https://dotnet.microsoft.com/download) or later
- [Claude Code CLI](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) installed and configured
  - Run `claude` command to verify installation

## Installation

### NuGet Package

```bash
dotnet add package ClaudeAgentSdk
```

Or via Package Manager:

```powershell
Install-Package ClaudeAgentSdk
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
    MaxTurns = 10
};

await foreach (var message in Query.RunAsync("Create a web server", options))
{
    // ...
}
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

var server = SdkMcpServer.FromType<CalculatorTools>();
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
            Model = AgentModel.Sonnet
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
                Hooks = [ValidateBashCommandAsync]
            }
        ]
    }
};

async Task<HookResult> ValidateBashCommandAsync(HookInput input)
{
    var command = input.ToolInput["command"]?.ToString();
    if (command?.Contains("rm -rf") == true)
        return HookResult.Block("Dangerous command blocked");
    return HookResult.Allow();
}
```

### Tool Permission Control

Fine-grained control over tool execution:

```csharp
var options = new ClaudeAgentOptions
{
    CanUseTool = async (callback) =>
    {
        if (callback.ToolName == "Bash")
        {
            var command = callback.Input["command"]?.ToString();
            if (command?.Contains("sudo") == true)
                return ToolPermissionResult.Deny("sudo not allowed");
        }
        return ToolPermissionResult.Allow();
    }
};
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
