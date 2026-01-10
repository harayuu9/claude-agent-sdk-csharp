using ClaudeAgentSdk;

namespace UnitTest;

/// <summary>
/// Test message type creation and validation.
/// </summary>
public class MessageTypesTests
{
    [Fact]
    public void UserMessageCreation()
    {
        var msg = new UserMessage
        {
            Content =
            [
                new TextBlock
                {
                    Text = "Hello, Claude!"
                }
            ]
        };
        Assert.Equal("Hello, Claude!", (msg.Content[0] as TextBlock)?.Text);
    }

    [Fact]
    public void AssistantMessageWithText()
    {
        var textBlock = new TextBlock { Text = "Hello, human!" };
        var msg = new AssistantMessage
        {
            Content = [textBlock],
            Model = "claude-opus-4-1-20250805"
        };
        var block = Assert.Single(msg.Content);
        Assert.Equal("Hello, human!", ((TextBlock)block).Text);
    }

    [Fact]
    public void AssistantMessageWithThinking()
    {
        var thinkingBlock = new ThinkingBlock
        {
            Thinking = "I'm thinking...",
            Signature = "sig-123"
        };
        var msg = new AssistantMessage
        {
            Content = [thinkingBlock],
            Model = "claude-opus-4-1-20250805"
        };
        var block = Assert.Single(msg.Content);
        Assert.Equal("I'm thinking...", ((ThinkingBlock)block).Thinking);
        Assert.Equal("sig-123", ((ThinkingBlock)block).Signature);
    }

    [Fact]
    public void ToolUseBlockTest()
    {
        var block = new ToolUseBlock
        {
            Id = "tool-123",
            Name = "Read",
            Input = new Dictionary<string, object?> { ["file_path"] = "/test.txt" }
        };
        Assert.Equal("tool-123", block.Id);
        Assert.Equal("Read", block.Name);
        Assert.Equal("/test.txt", block.Input["file_path"]);
    }

    [Fact]
    public void ToolResultBlockTest()
    {
        var block = new ToolResultBlock
        {
            ToolUseId = "tool-123",
            Content = "File contents here",
            IsError = false
        };
        Assert.Equal("tool-123", block.ToolUseId);
        Assert.Equal("File contents here", block.Content);
        Assert.False(block.IsError);
    }

    [Fact]
    public void ResultMessageTest()
    {
        var msg = new ResultMessage
        {
            Subtype = "success",
            DurationMs = 1500,
            DurationApiMs = 1200,
            IsError = false,
            NumTurns = 1,
            SessionId = "session-123",
            TotalCostUsd = 0.01
        };
        Assert.Equal("success", msg.Subtype);
        Assert.Equal(0.01, msg.TotalCostUsd);
        Assert.Equal("session-123", msg.SessionId);
    }
}

/// <summary>
/// Test Options configuration.
/// </summary>
public class OptionsTests
{
    [Fact]
    public void DefaultOptions()
    {
        var options = new ClaudeAgentOptions();
        Assert.Empty(options.AllowedTools);
        Assert.Null(options.SystemPrompt);
        Assert.Null(options.PermissionMode);
        Assert.False(options.ContinueConversation);
        Assert.Empty(options.DisallowedTools);
    }

    [Fact]
    public void OptionsWithTools()
    {
        var options = new ClaudeAgentOptions
        {
            AllowedTools = ["Read", "Write", "Edit"],
            DisallowedTools = ["Bash"]
        };
        Assert.Equal(["Read", "Write", "Edit"], options.AllowedTools);
        Assert.Equal(["Bash"], options.DisallowedTools);
    }

    [Fact]
    public void OptionsWithPermissionMode()
    {
        var options = new ClaudeAgentOptions { PermissionMode = PermissionMode.BypassPermissions };
        Assert.Equal(PermissionMode.BypassPermissions, options.PermissionMode);

        var optionsPlan = new ClaudeAgentOptions { PermissionMode = PermissionMode.Plan };
        Assert.Equal(PermissionMode.Plan, optionsPlan.PermissionMode);

        var optionsDefault = new ClaudeAgentOptions { PermissionMode = PermissionMode.Default };
        Assert.Equal(PermissionMode.Default, optionsDefault.PermissionMode);

        var optionsAccept = new ClaudeAgentOptions { PermissionMode = PermissionMode.AcceptEdits };
        Assert.Equal(PermissionMode.AcceptEdits, optionsAccept.PermissionMode);
    }

    [Fact]
    public void OptionsWithSystemPromptString()
    {
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = "You are a helpful assistant."
        };
        Assert.Equal("You are a helpful assistant.", options.SystemPrompt);
    }

    [Fact]
    public void OptionsWithSystemPromptPreset()
    {
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new Dictionary<string, object>
            {
                ["type"] = "preset",
                ["preset"] = "claude_code"
            }
        };
        var prompt = Assert.IsType<Dictionary<string, object>>(options.SystemPrompt);
        Assert.Equal("preset", prompt["type"]);
        Assert.Equal("claude_code", prompt["preset"]);
    }

    [Fact]
    public void OptionsWithSystemPromptPresetAndAppend()
    {
        var options = new ClaudeAgentOptions
        {
            SystemPrompt = new Dictionary<string, object>
            {
                ["type"] = "preset",
                ["preset"] = "claude_code",
                ["append"] = "Be concise."
            }
        };
        var prompt = Assert.IsType<Dictionary<string, object>>(options.SystemPrompt);
        Assert.Equal("preset", prompt["type"]);
        Assert.Equal("claude_code", prompt["preset"]);
        Assert.Equal("Be concise.", prompt["append"]);
    }

    [Fact]
    public void OptionsWithSessionContinuation()
    {
        var options = new ClaudeAgentOptions
        {
            ContinueConversation = true,
            Resume = "session-123"
        };
        Assert.True(options.ContinueConversation);
        Assert.Equal("session-123", options.Resume);
    }

    [Fact]
    public void OptionsWithModelSpecification()
    {
        var options = new ClaudeAgentOptions
        {
            Model = "claude-sonnet-4-5",
            PermissionPromptToolName = "CustomTool"
        };
        Assert.Equal("claude-sonnet-4-5", options.Model);
        Assert.Equal("CustomTool", options.PermissionPromptToolName);
    }
}