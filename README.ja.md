# Claude Agent SDK for C#

[![NuGet](https://img.shields.io/nuget/v/ClaudeAgentSdk.svg)](https://www.nuget.org/packages/ClaudeAgentSdk/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

[Claude Code](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) CLIと連携するためのC# SDKです。ClaudeをベースにしたAIアプリケーションを構築できます。

> **注意**: このプロジェクトは公式の [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python) をC#に移植したものです。

[English README](README.md)

## 概要

Claude Agent SDK for C# は、Claude Codeと対話するための2つの主要なAPIを提供します：

- **`Query.RunAsync()` / `ClaudeAgent.QueryAsync()`** - ステートレスな単発クエリ
- **`ClaudeSDKClient`** - ステートフルな対話型会話

### 主な機能

- 用途に応じた2つの柔軟なAPI
- MCP (Model Context Protocol) によるカスタムツールのサポート
- カスタムエージェント定義
- イベント処理用のHooks（PreToolUse、PostToolUse等）
- ツール権限制御
- `IAsyncEnumerable`によるストリーミングサポート

## 前提条件

- [.NET 10.0](https://dotnet.microsoft.com/download) 以降
- [Claude Code CLI](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) がインストール・設定済みであること
  - `claude`コマンドが実行できることを確認してください

## インストール

### NuGetパッケージ

```bash
dotnet add package ClaudeAgentSdk
```

またはPackage Manager経由:

```powershell
Install-Package ClaudeAgentSdk
```

### ソースからビルド

```bash
git clone https://github.com/harayuu9/claude-agent-sdk-csharp.git
cd claude-agent-sdk-csharp
dotnet build
```

## クイックスタート

### シンプルなクエリ

```csharp
using ClaudeAgentSdk;

// 単発クエリ
await foreach (var message in Query.RunAsync("2 + 2 は?"))
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

### 対話型会話

```csharp
using ClaudeAgentSdk;

await using var client = new ClaudeSDKClient();

// 会話を開始
await client.ConnectAsync("フランスの首都は?");
await foreach (var msg in client.ReceiveResponseAsync())
{
    // レスポンスを処理
}

// フォローアップ質問
await client.QueryAsync("その人口は?");
await foreach (var msg in client.ReceiveResponseAsync())
{
    // レスポンスを処理
}
```

## 機能

### オプション

`ClaudeAgentOptions`でSDKの動作を設定します：

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = "あなたは親切なPythonエキスパートです。",
    Cwd = "/path/to/project",
    AllowedTools = ["Read", "Write", "Bash"],
    PermissionMode = PermissionMode.AcceptEdits,
    MaxTurns = 10
};

await foreach (var message in Query.RunAsync("Webサーバーを作成して", options))
{
    // ...
}
```

### MCPツール

Model Context Protocolを使用してカスタムツールを定義します：

```csharp
// ツールを定義
var addTool = SdkMcpTool.Create<CalcArgs>(
    "add",
    "2つの数値を加算",
    async args => SdkMcpToolResult.FromText($"結果: {args.A + args.B}"));

// MCPサーバーを作成
var server = SdkMcpServer.Create("calculator", "1.0.0", [addTool]);

var options = new ClaudeAgentOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["calc"] = server
    }
};
```

または`[Tool]`属性を使用：

```csharp
public class CalculatorTools
{
    [Tool("add", "2つの数値を加算")]
    public async Task<SdkMcpToolResult> Add(CalcArgs args)
    {
        return SdkMcpToolResult.FromText($"結果: {args.A + args.B}");
    }
}

var server = SdkMcpServer.FromType<CalculatorTools>();
```

### カスタムエージェント

特定のタスク向けに専門化されたエージェントを定義します：

```csharp
var options = new ClaudeAgentOptions
{
    Agents = new Dictionary<string, AgentDefinition>
    {
        ["code-reviewer"] = new AgentDefinition
        {
            Description = "コードの問題点とベストプラクティスをレビュー",
            Prompt = "あなたはコードレビュアーです。バグ、セキュリティ問題、スタイルを分析してください。",
            Tools = ["Read", "Grep", "Glob"],
            Model = AgentModel.Sonnet
        }
    }
};
```

### Hooks

実行中のイベントを処理します：

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
        return HookResult.Block("危険なコマンドがブロックされました");
    return HookResult.Allow();
}
```

### ツール権限制御

ツール実行のきめ細かな制御：

```csharp
var options = new ClaudeAgentOptions
{
    CanUseTool = async (callback) =>
    {
        if (callback.ToolName == "Bash")
        {
            var command = callback.Input["command"]?.ToString();
            if (command?.Contains("sudo") == true)
                return ToolPermissionResult.Deny("sudoは許可されていません");
        }
        return ToolPermissionResult.Allow();
    }
};
```

## サンプル

`example/`ディレクトリにはSDKの全機能を示す包括的なサンプルが含まれています：

```bash
cd claude-agent-sdk/example
dotnet run
```

利用可能なサンプル:
- Quick Start - 基本的な使用パターン
- Streaming Mode - 対話型会話
- System Prompt - カスタムシステムプロンプト
- MCP Calculator - カスタムツール統合
- Hooks - イベント処理
- Tool Permissions - アクセス制御
- Custom Agents - 専門化されたエージェント
- その他多数...

## テスト

ユニットテストの実行:

```bash
cd claude-agent-sdk/unit-test
dotnet test
```

E2Eテストの実行（Claude CLIが必要）:

```bash
dotnet test --filter "FullyQualifiedName~E2E"
```

## ライセンス

MIT License - 詳細は[LICENSE](LICENSE)を参照してください。

## 関連プロジェクト

- [claude-agent-sdk-python](https://github.com/anthropics/claude-agent-sdk-python) - 公式Python SDK（オリジナル実装）
- [Claude Code](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) - Claude Code CLIドキュメント
