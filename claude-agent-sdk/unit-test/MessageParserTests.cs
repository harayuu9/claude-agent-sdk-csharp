using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Internal;

namespace unit_test;

/// <summary>
/// Tests for MessageParser error handling and message parsing.
/// </summary>
public class MessageParserTests
{
    /// <summary>
    /// Helper method to parse JSON string to JsonElement.
    /// </summary>
    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement;

    /// <summary>
    /// Helper to get content blocks from UserMessage.
    /// </summary>
    private static List<ContentBlock> GetContentBlocks(UserMessage message) =>
        (List<ContentBlock>)message.Content;

    #region Valid User Message Tests

    [Fact]
    public void TestParseValidUserMessage()
    {
        var json = """
        {
            "type": "user",
            "message": {"content": [{"type": "text", "text": "Hello"}]}
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        var content = GetContentBlocks(userMessage);
        Assert.Single(content);
        var textBlock = Assert.IsType<TextBlock>(content[0]);
        Assert.Equal("Hello", textBlock.Text);
    }

    [Fact]
    public void TestParseUserMessageWithUuid()
    {
        var json = """
        {
            "type": "user",
            "uuid": "msg-abc123-def456",
            "message": {"content": [{"type": "text", "text": "Hello"}]}
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Equal("msg-abc123-def456", userMessage.Uuid);
        var content = GetContentBlocks(userMessage);
        Assert.Single(content);
    }

    [Fact]
    public void TestParseUserMessageWithToolUse()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {"type": "text", "text": "Let me read this file"},
                    {
                        "type": "tool_use",
                        "id": "tool_456",
                        "name": "Read",
                        "input": {"file_path": "/example.txt"}
                    }
                ]
            }
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        var content = GetContentBlocks(userMessage);
        Assert.Equal(2, content.Count);
        Assert.IsType<TextBlock>(content[0]);
        var toolUseBlock = Assert.IsType<ToolUseBlock>(content[1]);
        Assert.Equal("tool_456", toolUseBlock.Id);
        Assert.Equal("Read", toolUseBlock.Name);
        Assert.Equal("/example.txt", toolUseBlock.Input["file_path"]);
    }

    [Fact]
    public void TestParseUserMessageWithToolResult()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "tool_result",
                        "tool_use_id": "tool_789",
                        "content": "File contents here"
                    }
                ]
            }
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        var content = GetContentBlocks(userMessage);
        Assert.Single(content);
        var toolResultBlock = Assert.IsType<ToolResultBlock>(content[0]);
        Assert.Equal("tool_789", toolResultBlock.ToolUseId);
        Assert.Equal("File contents here", toolResultBlock.Content);
    }

    [Fact]
    public void TestParseUserMessageWithToolResultError()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "tool_result",
                        "tool_use_id": "tool_error",
                        "content": "File not found",
                        "is_error": true
                    }
                ]
            }
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        var content = GetContentBlocks(userMessage);
        Assert.Single(content);
        var toolResultBlock = Assert.IsType<ToolResultBlock>(content[0]);
        Assert.Equal("tool_error", toolResultBlock.ToolUseId);
        Assert.Equal("File not found", toolResultBlock.Content);
        Assert.True(toolResultBlock.IsError);
    }

    [Fact]
    public void TestParseUserMessageWithMixedContent()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {"type": "text", "text": "Here's what I found:"},
                    {
                        "type": "tool_use",
                        "id": "use_1",
                        "name": "Search",
                        "input": {"query": "test"}
                    },
                    {
                        "type": "tool_result",
                        "tool_use_id": "use_1",
                        "content": "Search results"
                    },
                    {"type": "text", "text": "What do you think?"}
                ]
            }
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        var content = GetContentBlocks(userMessage);
        Assert.Equal(4, content.Count);
        Assert.IsType<TextBlock>(content[0]);
        Assert.IsType<ToolUseBlock>(content[1]);
        Assert.IsType<ToolResultBlock>(content[2]);
        Assert.IsType<TextBlock>(content[3]);
    }

    [Fact]
    public void TestParseUserMessageInsideSubagent()
    {
        var json = """
        {
            "type": "user",
            "message": {"content": [{"type": "text", "text": "Hello"}]},
            "parent_tool_use_id": "toolu_01Xrwd5Y13sEHtzScxR77So8"
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Equal("toolu_01Xrwd5Y13sEHtzScxR77So8", userMessage.ParentToolUseId);
    }

    #endregion

    #region Valid Assistant Message Tests

    [Fact]
    public void TestParseValidAssistantMessage()
    {
        var json = """
        {
            "type": "assistant",
            "message": {
                "content": [
                    {"type": "text", "text": "Hello"},
                    {
                        "type": "tool_use",
                        "id": "tool_123",
                        "name": "Read",
                        "input": {"file_path": "/test.txt"}
                    }
                ],
                "model": "claude-opus-4-1-20250805"
            }
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var assistantMessage = Assert.IsType<AssistantMessage>(message);
        Assert.Equal(2, assistantMessage.Content.Count);
        Assert.IsType<TextBlock>(assistantMessage.Content[0]);
        Assert.IsType<ToolUseBlock>(assistantMessage.Content[1]);
    }

    [Fact]
    public void TestParseAssistantMessageWithThinking()
    {
        var json = """
        {
            "type": "assistant",
            "message": {
                "content": [
                    {
                        "type": "thinking",
                        "thinking": "I'm thinking about the answer...",
                        "signature": "sig-123"
                    },
                    {"type": "text", "text": "Here's my response"}
                ],
                "model": "claude-opus-4-1-20250805"
            }
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var assistantMessage = Assert.IsType<AssistantMessage>(message);
        Assert.Equal(2, assistantMessage.Content.Count);
        var thinkingBlock = Assert.IsType<ThinkingBlock>(assistantMessage.Content[0]);
        Assert.Equal("I'm thinking about the answer...", thinkingBlock.Thinking);
        Assert.Equal("sig-123", thinkingBlock.Signature);
        var textBlock = Assert.IsType<TextBlock>(assistantMessage.Content[1]);
        Assert.Equal("Here's my response", textBlock.Text);
    }

    [Fact]
    public void TestParseAssistantMessageInsideSubagent()
    {
        var json = """
        {
            "type": "assistant",
            "message": {
                "content": [
                    {"type": "text", "text": "Hello"},
                    {
                        "type": "tool_use",
                        "id": "tool_123",
                        "name": "Read",
                        "input": {"file_path": "/test.txt"}
                    }
                ],
                "model": "claude-opus-4-1-20250805"
            },
            "parent_tool_use_id": "toolu_01Xrwd5Y13sEHtzScxR77So8"
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var assistantMessage = Assert.IsType<AssistantMessage>(message);
        Assert.Equal("toolu_01Xrwd5Y13sEHtzScxR77So8", assistantMessage.ParentToolUseId);
    }

    #endregion

    #region Valid System Message Tests

    [Fact]
    public void TestParseValidSystemMessage()
    {
        var json = """{"type": "system", "subtype": "start"}""";
        var message = MessageParser.ParseMessage(ParseJson(json));

        var systemMessage = Assert.IsType<SystemMessage>(message);
        Assert.Equal("start", systemMessage.Subtype);
    }

    #endregion

    #region Valid Result Message Tests

    [Fact]
    public void TestParseValidResultMessage()
    {
        var json = """
        {
            "type": "result",
            "subtype": "success",
            "duration_ms": 1000,
            "duration_api_ms": 500,
            "is_error": false,
            "num_turns": 2,
            "session_id": "session_123"
        }
        """;
        var message = MessageParser.ParseMessage(ParseJson(json));

        var resultMessage = Assert.IsType<ResultMessage>(message);
        Assert.Equal("success", resultMessage.Subtype);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void TestParseInvalidDataType()
    {
        var json = "\"not a dict\"";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Invalid message data type", exception.Message);
        Assert.Contains("expected object", exception.Message);
    }

    [Fact]
    public void TestParseMissingTypeField()
    {
        var json = """{"message": {"content": []}}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Message missing 'type' field", exception.Message);
    }

    [Fact]
    public void TestParseUnknownMessageType()
    {
        var json = """{"type": "unknown_type"}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Unknown message type: unknown_type", exception.Message);
    }

    [Fact]
    public void TestParseUserMessageMissingFields()
    {
        var json = """{"type": "user"}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Missing required field in user message", exception.Message);
    }

    [Fact]
    public void TestParseAssistantMessageMissingFields()
    {
        var json = """{"type": "assistant"}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Missing required field in assistant message", exception.Message);
    }

    [Fact]
    public void TestParseSystemMessageMissingFields()
    {
        var json = """{"type": "system"}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Missing required field in system message", exception.Message);
    }

    [Fact]
    public void TestParseResultMessageMissingFields()
    {
        var json = """{"type": "result", "subtype": "success"}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.Contains("Missing required field in result message", exception.Message);
    }

    [Fact]
    public void TestMessageParseExceptionContainsData()
    {
        var json = """{"type": "unknown", "some": "data"}""";
        var exception = Assert.Throws<MessageParseException>(() =>
            MessageParser.ParseMessage(ParseJson(json)));

        Assert.NotNull(exception.Data);
        Assert.Equal("unknown", exception.Data["type"]);
        Assert.Equal("data", exception.Data["some"]);
    }

    #endregion
}
