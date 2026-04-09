# Claude Agent SDK for C#

[![NuGet](https://img.shields.io/nuget/v/ClaudeAgentSdk.CSharp.svg)](https://www.nuget.org/packages/ClaudeAgentSdk.CSharp/)
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
- 画像入力サポート（base64、URL、ローカルファイル）
- MCP (Model Context Protocol) によるカスタムツールのサポート
- 拡張構成を備えたカスタムエージェント定義
- イベント処理用のHooks（PreToolUse、PostToolUse、Notification等）
- エージェントコンテキスト付きツール権限制御
- セッション管理（一覧、リネーム、タグ、フォーク、削除）
- MCPサーバー制御（ステータス、再接続、トグル）
- コンテキスト使用量モニタリング
- タスク管理（開始、進捗、停止）
- 拡張思考設定
- `IAsyncEnumerable`によるストリーミングサポート

## 前提条件

- [.NET 10.0](https://dotnet.microsoft.com/download) 以降
- [Claude Code CLI](https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview) がインストール・設定済みであること
  - `claude`コマンドが実行できることを確認してください

## インストール

### NuGetパッケージ

```bash
dotnet add package ClaudeAgentSdk.CSharp
```

またはPackage Manager経由:

```powershell
Install-Package ClaudeAgentSdk.CSharp
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
    MaxTurns = 10,
    // 新機能: 拡張思考
    Thinking = new ThinkingConfigAdaptive(),
    // 新機能: エフォートレベル
    Effort = "high",
    // 新機能: セッションID
    SessionId = "my-session-id",
    // 新機能: タスクバジェット
    TaskBudget = new TaskBudget { Total = 100000 }
};

await foreach (var message in Query.RunAsync("Webサーバーを作成して", options))
{
    // ...
}
```

### 画像入力

`ImageBlock`を使用して、テキストと一緒に画像をClaudeに送信できます。base64エンコードデータ、URL、ローカルファイルに対応しています。

```csharp
// ローカルファイルから（拡張子からメディアタイプを自動検出）
await client.QueryAsync([
    new TextBlock { Text = "この画像には何が写っていますか？" },
    ImageBlock.FromFile("photo.png")
]);

// URLから
await client.QueryAsync([
    new TextBlock { Text = "この画像を説明してください。" },
    ImageBlock.FromUrl("https://example.com/image.jpg")
]);

// base64データから
await client.QueryAsync([
    ImageBlock.FromBase64(base64String, "image/png")
]);

// 非同期ファイル読み込み
var imageBlock = await ImageBlock.FromFileAsync("large-photo.jpg");
await client.QueryAsync([
    new TextBlock { Text = "この写真を分析してください。" },
    imageBlock
]);
```

自動検出に対応するファイル拡張子: `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`
明示的にメディアタイプを指定することも可能です: `ImageBlock.FromFile("image.bmp", "image/bmp")`

> **注意**: これはC# SDK独自の機能であり、元のPython SDKには含まれていません。

### ファイルからのシステムプロンプト

```csharp
var options = new ClaudeAgentOptions
{
    SystemPrompt = new SystemPromptFile { Path = "/path/to/prompt.txt" }
};
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

var server = SdkMcpServer.FromType<CalculatorTools>("calculator");
```

### MCPサーバー制御

MCPサーバー接続をランタイムで監視・制御します：

```csharp
await using var client = new ClaudeSDKClient(options);
await client.ConnectAsync();

// 全MCPサーバーのステータスを取得
var status = await client.GetMcpStatusAsync();
foreach (var server in status.McpServers)
{
    Console.WriteLine($"{server.Name}: {server.Status}");
}

// 失敗したサーバーを再接続
await client.ReconnectMcpServerAsync("my-server");

// サーバーのオン/オフを切り替え
await client.ToggleMcpServerAsync("my-server", enabled: false);
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
            Model = "sonnet",
            // 新フィールド
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
                Hooks =
                [
                    async (input, toolUseId, context) =>
                    {
                        if (input is PreToolUseHookInput preToolUse)
                        {
                            var command = preToolUse.ToolInput["command"]?.ToString();
                            if (command?.Contains("rm -rf") == true)
                                return new SyncHookJsonOutput { Continue = false, StopReason = "危険なコマンドがブロックされました" };
                        }
                        return new SyncHookJsonOutput { Continue = true };
                    }
                ]
            }
        ],
        // 新しいHookイベント
        [HookEvent.PostToolUseFailure] =
        [
            new HookMatcher
            {
                Hooks =
                [
                    async (input, toolUseId, context) =>
                    {
                        if (input is PostToolUseFailureHookInput failure)
                            Console.WriteLine($"ツール {failure.ToolName} が失敗: {failure.Error}");
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
                            Console.WriteLine($"通知: {notification.Message}");
                        return new SyncHookJsonOutput { Continue = true };
                    }
                ]
            }
        ]
    }
};
```

### ツール権限制御

エージェントコンテキスト付きのきめ細かなツール実行制御：

```csharp
var options = new ClaudeAgentOptions
{
    CanUseTool = async (toolName, input, context) =>
    {
        // コンテキストからtool_use_idとagent_idにアクセス可能
        Console.WriteLine($"ツール: {toolName}, ToolUseId: {context.ToolUseId}, AgentId: {context.AgentId}");

        if (toolName == "Bash")
        {
            var command = input["command"]?.ToString();
            if (command?.Contains("sudo") == true)
                return new PermissionResultDeny { Message = "sudoは許可されていません" };
        }
        return new PermissionResultAllow();
    }
};
```

### コンテキスト使用量モニタリング

トークン使用量とコンテキストウィンドウを監視します：

```csharp
await using var client = new ClaudeSDKClient(options);
await client.ConnectAsync("Hello");

var usage = await client.GetContextUsageAsync();
Console.WriteLine($"総トークン: {usage.TotalTokens}/{usage.MaxTokens} ({usage.Percentage:F1}%)");
foreach (var category in usage.Categories)
{
    Console.WriteLine($"  {category.Name}: {category.Tokens} トークン");
}
```

### タスク管理

バックグラウンドタスクの監視と制御：

```csharp
await using var client = new ClaudeSDKClient(options);
await client.ConnectAsync("複雑な分析を実行して");

await foreach (var msg in client.ReceiveMessagesAsync())
{
    switch (msg)
    {
        case TaskStartedMessage started:
            Console.WriteLine($"タスク開始: {started.Description}");
            break;
        case TaskProgressMessage progress:
            Console.WriteLine($"タスク進捗: {progress.Description} ({progress.Usage.TotalTokens} トークン)");
            break;
        case TaskNotificationMessage notification:
            Console.WriteLine($"タスク {notification.Status}: {notification.Summary}");
            break;
        case RateLimitEvent rateLimit:
            Console.WriteLine($"レート制限: {rateLimit.RateLimitInfo.Status}");
            break;
        case ResultMessage result:
            Console.WriteLine($"完了！ コスト: ${result.TotalCostUsd}");
            break;
    }
}

// 実行中のタスクを停止
await client.StopTaskAsync("task-id");
```

### 拡張思考

Claudeの思考動作を設定します：

```csharp
// アダプティブ思考（Claudeが思考タイミングを判断）
var options = new ClaudeAgentOptions
{
    Thinking = new ThinkingConfigAdaptive()
};

// 固定思考バジェット
var options2 = new ClaudeAgentOptions
{
    Thinking = new ThinkingConfigEnabled { BudgetTokens = 10000 }
};

// 思考を無効化
var options3 = new ClaudeAgentOptions
{
    Thinking = new ThinkingConfigDisabled()
};
```

### セッション管理

Claudeのセッション履歴をプログラムで管理します：

```csharp
using ClaudeAgentSdk;

// セッション一覧
var sessions = Sessions.ListSessions(directory: "/path/to/project");
foreach (var session in sessions)
{
    Console.WriteLine($"{session.SessionId}: {session.Summary} (更新: {session.LastModified})");
}

// セッション情報を取得
var info = Sessions.GetSessionInfo("session-uuid", directory: "/path/to/project");

// セッションメッセージを読み取り
var messages = Sessions.GetSessionMessages("session-uuid");
foreach (var msg in messages)
{
    Console.WriteLine($"[{msg.Type}] {msg.Uuid}");
}

// セッションをリネーム
SessionMutations.RenameSession("session-uuid", "カスタムタイトル");

// セッションにタグを付ける
SessionMutations.TagSession("session-uuid", "重要");

// セッションをフォーク
var result = SessionMutations.ForkSession("session-uuid", title: "フォーク実験");
Console.WriteLine($"新セッション: {result.SessionId}");

// セッションを削除
SessionMutations.DeleteSession("session-uuid");
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
