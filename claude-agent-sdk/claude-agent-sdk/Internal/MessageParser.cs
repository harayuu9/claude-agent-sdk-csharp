using System.Text.Json;

namespace ClaudeAgentSdk.Internal;

/// <summary>
/// Message parser for Claude Code SDK responses.
/// </summary>
public static class MessageParser
{
    /// <summary>
    /// Parse message from CLI output into typed Message objects.
    /// </summary>
    /// <param name="data">Raw message JsonElement from CLI output</param>
    /// <returns>Parsed Message object</returns>
    /// <exception cref="MessageParseException">If parsing fails or message type is unrecognized</exception>
    public static Message ParseMessage(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            throw new MessageParseException(
                $"Invalid message data type (expected object, got {data.ValueKind})",
                JsonElementToDict(data)
            );
        }

        if (!data.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            throw new MessageParseException("Message missing 'type' field", JsonElementToDict(data));
        }

        var messageType = typeElement.GetString();

        return messageType switch
        {
            "user" => ParseUserMessage(data),
            "assistant" => ParseAssistantMessage(data),
            "system" => ParseSystemMessage(data),
            "result" => ParseResultMessage(data),
            "stream_event" => ParseStreamEvent(data),
            _ => throw new MessageParseException($"Unknown message type: {messageType}", JsonElementToDict(data))
        };
    }

    private static UserMessage ParseUserMessage(JsonElement data)
    {
        try
        {
            var parentToolUseId = data.TryGetProperty("parent_tool_use_id", out var ptui) && ptui.ValueKind == JsonValueKind.String
                ? ptui.GetString()
                : null;
            var uuid = data.TryGetProperty("uuid", out var uuidElem) && uuidElem.ValueKind == JsonValueKind.String
                ? uuidElem.GetString()
                : null;

            var messageContent = data.GetProperty("message").GetProperty("content");

            if (messageContent.ValueKind == JsonValueKind.Array)
            {
                var contentBlocks = ParseContentBlocks(messageContent);
                return new UserMessage
                {
                    Content = contentBlocks,
                    Uuid = uuid,
                    ParentToolUseId = parentToolUseId
                };
            }

            // Content is a string
            return new UserMessage
            {
                Content =
                [
                    new TextBlock
                    {
                        Text = messageContent.GetString() ?? ""
                    }
                ],
                Uuid = uuid,
                ParentToolUseId = parentToolUseId
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MessageParseException(
                $"Missing required field in user message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
        catch (InvalidOperationException e)
        {
            throw new MessageParseException(
                $"Missing required field in user message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
    }

    private static AssistantMessage ParseAssistantMessage(JsonElement data)
    {
        try
        {
            var messageElement = data.GetProperty("message");
            var contentArray = messageElement.GetProperty("content");
            var contentBlocks = ParseContentBlocks(contentArray);

            var model = messageElement.GetProperty("model").GetString() ?? "";

            var parentToolUseId = data.TryGetProperty("parent_tool_use_id", out var ptui) && ptui.ValueKind == JsonValueKind.String
                ? ptui.GetString()
                : null;

            AssistantMessageError? error = null;
            if (messageElement.TryGetProperty("error", out var errorElem) && errorElem.ValueKind == JsonValueKind.String)
            {
                var errorStr = errorElem.GetString();
                error = errorStr switch
                {
                    "authentication_failed" => AssistantMessageError.AuthenticationFailed,
                    "billing_error" => AssistantMessageError.BillingError,
                    "rate_limit" => AssistantMessageError.RateLimit,
                    "invalid_request" => AssistantMessageError.InvalidRequest,
                    "server_error" => AssistantMessageError.ServerError,
                    _ => AssistantMessageError.Unknown
                };
            }

            return new AssistantMessage
            {
                Content = contentBlocks,
                Model = model,
                ParentToolUseId = parentToolUseId,
                Error = error
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MessageParseException(
                $"Missing required field in assistant message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
        catch (InvalidOperationException e)
        {
            throw new MessageParseException(
                $"Missing required field in assistant message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
    }

    private static SystemMessage ParseSystemMessage(JsonElement data)
    {
        try
        {
            var subtype = data.GetProperty("subtype").GetString() ?? "";

            return new SystemMessage
            {
                Subtype = subtype,
                Data = JsonElementToDict(data) ?? new Dictionary<string, object?>()
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MessageParseException(
                $"Missing required field in system message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
        catch (InvalidOperationException e)
        {
            throw new MessageParseException(
                $"Missing required field in system message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
    }

    private static ResultMessage ParseResultMessage(JsonElement data)
    {
        try
        {
            var subtype = data.GetProperty("subtype").GetString() ?? "";
            var durationMs = data.GetProperty("duration_ms").GetInt32();
            var durationApiMs = data.GetProperty("duration_api_ms").GetInt32();
            var isError = data.GetProperty("is_error").GetBoolean();
            var numTurns = data.GetProperty("num_turns").GetInt32();
            var sessionId = data.GetProperty("session_id").GetString() ?? "";

            double? totalCostUsd = null;
            if (data.TryGetProperty("total_cost_usd", out var costElem) && costElem.ValueKind == JsonValueKind.Number)
            {
                totalCostUsd = costElem.GetDouble();
            }

            Dictionary<string, object?>? usage = null;
            if (data.TryGetProperty("usage", out var usageElem) && usageElem.ValueKind == JsonValueKind.Object)
            {
                usage = JsonElementToDict(usageElem);
            }

            string? result = null;
            if (data.TryGetProperty("result", out var resultElem) && resultElem.ValueKind == JsonValueKind.String)
            {
                result = resultElem.GetString();
            }

            object? structuredOutput = null;
            if (data.TryGetProperty("structured_output", out var soElem) && soElem.ValueKind != JsonValueKind.Null)
            {
                structuredOutput = JsonElementToObject(soElem);
            }

            return new ResultMessage
            {
                Subtype = subtype,
                DurationMs = durationMs,
                DurationApiMs = durationApiMs,
                IsError = isError,
                NumTurns = numTurns,
                SessionId = sessionId,
                TotalCostUsd = totalCostUsd,
                Usage = usage,
                Result = result,
                StructuredOutput = structuredOutput
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MessageParseException(
                $"Missing required field in result message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
        catch (InvalidOperationException e)
        {
            throw new MessageParseException(
                $"Missing required field in result message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
    }

    private static StreamEvent ParseStreamEvent(JsonElement data)
    {
        try
        {
            var uuid = data.GetProperty("uuid").GetString() ?? "";
            var sessionId = data.GetProperty("session_id").GetString() ?? "";
            var eventElement = data.GetProperty("event");

            var parentToolUseId = data.TryGetProperty("parent_tool_use_id", out var ptui) && ptui.ValueKind == JsonValueKind.String
                ? ptui.GetString()
                : null;

            return new StreamEvent
            {
                Uuid = uuid,
                SessionId = sessionId,
                Event = JsonElementToDict(eventElement) ?? new Dictionary<string, object?>(),
                ParentToolUseId = parentToolUseId
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MessageParseException(
                $"Missing required field in stream_event message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
        catch (InvalidOperationException e)
        {
            throw new MessageParseException(
                $"Missing required field in stream_event message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
    }

    private static List<ContentBlock> ParseContentBlocks(JsonElement content)
    {
        var blocks = new List<ContentBlock>();

        foreach (var block in content.EnumerateArray())
        {
            if (!block.TryGetProperty("type", out var typeElem))
            {
                continue;
            }

            var blockType = typeElem.GetString();

            switch (blockType)
            {
                case "text":
                    blocks.Add(new TextBlock
                    {
                        Text = block.GetProperty("text").GetString() ?? ""
                    });
                    break;

                case "thinking":
                    blocks.Add(new ThinkingBlock
                    {
                        Thinking = block.GetProperty("thinking").GetString() ?? "",
                        Signature = block.GetProperty("signature").GetString() ?? ""
                    });
                    break;

                case "tool_use":
                    blocks.Add(new ToolUseBlock
                    {
                        Id = block.GetProperty("id").GetString() ?? "",
                        Name = block.GetProperty("name").GetString() ?? "",
                        Input = JsonElementToDict(block.GetProperty("input")) ?? new Dictionary<string, object?>()
                    });
                    break;

                case "tool_result":
                    object? toolResultContent = null;
                    if (block.TryGetProperty("content", out var contentElem) && contentElem.ValueKind != JsonValueKind.Null)
                    {
                        toolResultContent = JsonElementToObject(contentElem);
                    }

                    bool? isError = null;
                    if (block.TryGetProperty("is_error", out var isErrorElem) && isErrorElem.ValueKind == JsonValueKind.True || isErrorElem.ValueKind == JsonValueKind.False)
                    {
                        isError = isErrorElem.GetBoolean();
                    }

                    blocks.Add(new ToolResultBlock
                    {
                        ToolUseId = block.GetProperty("tool_use_id").GetString() ?? "",
                        Content = toolResultContent,
                        IsError = isError
                    });
                    break;
            }
        }

        return blocks;
    }

    /// <summary>
    /// Convert a JsonElement to a Dictionary.
    /// </summary>
    internal static Dictionary<string, object?>? JsonElementToDict(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    /// <summary>
    /// Convert a JsonElement to an appropriate .NET object.
    /// </summary>
    internal static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => JsonElementToDict(element),
            _ => null
        };
    }
}
