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
    /// <returns>Parsed Message object, or null for unknown message types (forward-compatible)</returns>
    /// <exception cref="MessageParseException">If parsing fails</exception>
    public static Message? ParseMessage(JsonElement data)
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
            "rate_limit_event" => ParseRateLimitEventTopLevel(data),
            _ => null
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

            Dictionary<string, object?>? toolUseResult = null;
            if (data.TryGetProperty("tool_use_result", out var turElem) && turElem.ValueKind == JsonValueKind.Object)
            {
                toolUseResult = JsonElementToDict(turElem);
            }

            if (messageContent.ValueKind == JsonValueKind.Array)
            {
                var contentBlocks = ParseContentBlocks(messageContent);
                return new UserMessage
                {
                    Content = contentBlocks,
                    Uuid = uuid,
                    ParentToolUseId = parentToolUseId,
                    ToolUseResult = toolUseResult
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
                ParentToolUseId = parentToolUseId,
                ToolUseResult = toolUseResult
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

            Dictionary<string, object?>? usage = null;
            if (messageElement.TryGetProperty("usage", out var usageElem) && usageElem.ValueKind == JsonValueKind.Object)
            {
                usage = JsonElementToDict(usageElem);
            }

            var messageId = messageElement.TryGetProperty("id", out var idElem) && idElem.ValueKind == JsonValueKind.String
                ? idElem.GetString()
                : null;

            var stopReason = messageElement.TryGetProperty("stop_reason", out var srElem) && srElem.ValueKind == JsonValueKind.String
                ? srElem.GetString()
                : null;

            var sessionId = data.TryGetProperty("session_id", out var sidElem) && sidElem.ValueKind == JsonValueKind.String
                ? sidElem.GetString()
                : null;

            var uuid = data.TryGetProperty("uuid", out var uuidElem) && uuidElem.ValueKind == JsonValueKind.String
                ? uuidElem.GetString()
                : null;

            // Also check for error at the top-level data
            if (error == null && data.TryGetProperty("error", out var topErrorElem) && topErrorElem.ValueKind == JsonValueKind.String)
            {
                var errorStr = topErrorElem.GetString();
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
                Error = error,
                Usage = usage,
                MessageId = messageId,
                StopReason = stopReason,
                SessionId = sessionId,
                Uuid = uuid
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

    private static Message ParseSystemMessage(JsonElement data)
    {
        try
        {
            var subtype = data.GetProperty("subtype").GetString() ?? "";

            return subtype switch
            {
                "task_started" => ParseTaskStartedMessage(data),
                "task_progress" => ParseTaskProgressMessage(data),
                "task_notification" => ParseTaskNotificationMessage(data),
                "rate_limit" => ParseRateLimitEvent(data),
                _ => new SystemMessage
                {
                    Subtype = subtype,
                    Data = JsonElementToDict(data) ?? new Dictionary<string, object?>()
                }
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

    private static TaskStartedMessage ParseTaskStartedMessage(JsonElement data)
    {
        return new TaskStartedMessage
        {
            TaskId = data.GetProperty("task_id").GetString() ?? "",
            Description = data.GetProperty("description").GetString() ?? "",
            Uuid = data.GetProperty("uuid").GetString() ?? "",
            SessionId = data.GetProperty("session_id").GetString() ?? "",
            ToolUseId = data.TryGetProperty("tool_use_id", out var tui) && tui.ValueKind == JsonValueKind.String
                ? tui.GetString() : null,
            TaskType = data.TryGetProperty("task_type", out var tt) && tt.ValueKind == JsonValueKind.String
                ? tt.GetString() : null
        };
    }

    private static TaskProgressMessage ParseTaskProgressMessage(JsonElement data)
    {
        TaskUsage? usage = null;
        if (data.TryGetProperty("usage", out var usageElem) && usageElem.ValueKind == JsonValueKind.Object)
        {
            usage = new TaskUsage
            {
                TotalTokens = usageElem.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0,
                ToolUses = usageElem.TryGetProperty("tool_uses", out var tu) ? tu.GetInt32() : 0,
                DurationMs = usageElem.TryGetProperty("duration_ms", out var dm) ? dm.GetInt32() : 0
            };
        }

        return new TaskProgressMessage
        {
            TaskId = data.GetProperty("task_id").GetString() ?? "",
            Description = data.GetProperty("description").GetString() ?? "",
            Usage = usage ?? new TaskUsage { TotalTokens = 0, ToolUses = 0, DurationMs = 0 },
            Uuid = data.GetProperty("uuid").GetString() ?? "",
            SessionId = data.GetProperty("session_id").GetString() ?? "",
            ToolUseId = data.TryGetProperty("tool_use_id", out var tui) && tui.ValueKind == JsonValueKind.String
                ? tui.GetString() : null,
            LastToolName = data.TryGetProperty("last_tool_name", out var ltn) && ltn.ValueKind == JsonValueKind.String
                ? ltn.GetString() : null
        };
    }

    private static TaskNotificationMessage ParseTaskNotificationMessage(JsonElement data)
    {
        var statusStr = data.GetProperty("status").GetString() ?? "completed";
        var status = statusStr switch
        {
            "failed" => TaskNotificationStatus.Failed,
            "stopped" => TaskNotificationStatus.Stopped,
            _ => TaskNotificationStatus.Completed
        };

        TaskUsage? usage = null;
        if (data.TryGetProperty("usage", out var usageElem) && usageElem.ValueKind == JsonValueKind.Object)
        {
            usage = new TaskUsage
            {
                TotalTokens = usageElem.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0,
                ToolUses = usageElem.TryGetProperty("tool_uses", out var tu) ? tu.GetInt32() : 0,
                DurationMs = usageElem.TryGetProperty("duration_ms", out var dm) ? dm.GetInt32() : 0
            };
        }

        return new TaskNotificationMessage
        {
            TaskId = data.GetProperty("task_id").GetString() ?? "",
            Status = status,
            OutputFile = data.TryGetProperty("output_file", out var of) && of.ValueKind == JsonValueKind.String
                ? of.GetString() ?? "" : "",
            Summary = data.TryGetProperty("summary", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? "" : "",
            Uuid = data.GetProperty("uuid").GetString() ?? "",
            SessionId = data.GetProperty("session_id").GetString() ?? "",
            ToolUseId = data.TryGetProperty("tool_use_id", out var tui) && tui.ValueKind == JsonValueKind.String
                ? tui.GetString() : null,
            Usage = usage
        };
    }

    private static RateLimitEvent ParseRateLimitEvent(JsonElement data)
    {
        var rlInfoElem = data.GetProperty("rate_limit_info");
        var statusStr = rlInfoElem.GetProperty("status").GetString() ?? "allowed";
        var status = statusStr switch
        {
            "allowed_warning" => RateLimitStatus.AllowedWarning,
            "rejected" => RateLimitStatus.Rejected,
            _ => RateLimitStatus.Allowed
        };

        RateLimitType? rlType = null;
        if (rlInfoElem.TryGetProperty("rate_limit_type", out var rltElem) && rltElem.ValueKind == JsonValueKind.String)
        {
            rlType = rltElem.GetString() switch
            {
                "five_hour" => RateLimitType.FiveHour,
                "seven_day" => RateLimitType.SevenDay,
                "seven_day_opus" => RateLimitType.SevenDayOpus,
                "seven_day_sonnet" => RateLimitType.SevenDaySonnet,
                "overage" => RateLimitType.Overage,
                _ => null
            };
        }

        RateLimitStatus? overageStatus = null;
        if (rlInfoElem.TryGetProperty("overage_status", out var osElem) && osElem.ValueKind == JsonValueKind.String)
        {
            overageStatus = osElem.GetString() switch
            {
                "allowed_warning" => RateLimitStatus.AllowedWarning,
                "rejected" => RateLimitStatus.Rejected,
                _ => RateLimitStatus.Allowed
            };
        }

        var rateLimitInfo = new RateLimitInfo
        {
            Status = status,
            ResetsAt = rlInfoElem.TryGetProperty("resets_at", out var raElem) && raElem.ValueKind == JsonValueKind.Number
                ? raElem.GetInt64() : null,
            RateLimitTypeValue = rlType,
            Utilization = rlInfoElem.TryGetProperty("utilization", out var uElem) && uElem.ValueKind == JsonValueKind.Number
                ? uElem.GetDouble() : null,
            OverageStatus = overageStatus,
            OverageResetsAt = rlInfoElem.TryGetProperty("overage_resets_at", out var oraElem) && oraElem.ValueKind == JsonValueKind.Number
                ? oraElem.GetInt64() : null,
            OverageDisabledReason = rlInfoElem.TryGetProperty("overage_disabled_reason", out var odrElem) && odrElem.ValueKind == JsonValueKind.String
                ? odrElem.GetString() : null,
            Raw = JsonElementToDict(rlInfoElem)
        };

        return new RateLimitEvent
        {
            RateLimitInfo = rateLimitInfo,
            Uuid = data.GetProperty("uuid").GetString() ?? "",
            SessionId = data.GetProperty("session_id").GetString() ?? ""
        };
    }

    private static RateLimitEvent ParseRateLimitEventTopLevel(JsonElement data)
    {
        try
        {
            var rlInfoElem = data.GetProperty("rate_limit_info");
            var statusStr = rlInfoElem.GetProperty("status").GetString() ?? "allowed";
            var status = statusStr switch
            {
                "allowed_warning" => RateLimitStatus.AllowedWarning,
                "rejected" => RateLimitStatus.Rejected,
                _ => RateLimitStatus.Allowed
            };

            RateLimitType? rlType = null;
            // Top-level rate_limit_event uses camelCase field names
            if (rlInfoElem.TryGetProperty("rateLimitType", out var rltElem) && rltElem.ValueKind == JsonValueKind.String)
            {
                rlType = rltElem.GetString() switch
                {
                    "five_hour" => RateLimitType.FiveHour,
                    "seven_day" => RateLimitType.SevenDay,
                    "seven_day_opus" => RateLimitType.SevenDayOpus,
                    "seven_day_sonnet" => RateLimitType.SevenDaySonnet,
                    "overage" => RateLimitType.Overage,
                    _ => null
                };
            }
            // Also check snake_case for compatibility
            else if (rlInfoElem.TryGetProperty("rate_limit_type", out rltElem) && rltElem.ValueKind == JsonValueKind.String)
            {
                rlType = rltElem.GetString() switch
                {
                    "five_hour" => RateLimitType.FiveHour,
                    "seven_day" => RateLimitType.SevenDay,
                    "seven_day_opus" => RateLimitType.SevenDayOpus,
                    "seven_day_sonnet" => RateLimitType.SevenDaySonnet,
                    "overage" => RateLimitType.Overage,
                    _ => null
                };
            }

            RateLimitStatus? overageStatus = null;
            if (rlInfoElem.TryGetProperty("overageStatus", out var osElem) && osElem.ValueKind == JsonValueKind.String)
            {
                overageStatus = osElem.GetString() switch
                {
                    "allowed_warning" => RateLimitStatus.AllowedWarning,
                    "rejected" => RateLimitStatus.Rejected,
                    _ => RateLimitStatus.Allowed
                };
            }
            else if (rlInfoElem.TryGetProperty("overage_status", out osElem) && osElem.ValueKind == JsonValueKind.String)
            {
                overageStatus = osElem.GetString() switch
                {
                    "allowed_warning" => RateLimitStatus.AllowedWarning,
                    "rejected" => RateLimitStatus.Rejected,
                    _ => RateLimitStatus.Allowed
                };
            }

            var rateLimitInfo = new RateLimitInfo
            {
                Status = status,
                ResetsAt = TryGetInt64(rlInfoElem, "resetsAt") ?? TryGetInt64(rlInfoElem, "resets_at"),
                RateLimitTypeValue = rlType,
                Utilization = TryGetDouble(rlInfoElem, "utilization"),
                OverageStatus = overageStatus,
                OverageResetsAt = TryGetInt64(rlInfoElem, "overageResetsAt") ?? TryGetInt64(rlInfoElem, "overage_resets_at"),
                OverageDisabledReason = TryGetString(rlInfoElem, "overageDisabledReason") ?? TryGetString(rlInfoElem, "overage_disabled_reason"),
                Raw = JsonElementToDict(rlInfoElem)
            };

            return new RateLimitEvent
            {
                RateLimitInfo = rateLimitInfo,
                Uuid = data.GetProperty("uuid").GetString() ?? "",
                SessionId = data.GetProperty("session_id").GetString() ?? ""
            };
        }
        catch (KeyNotFoundException e)
        {
            throw new MessageParseException(
                $"Missing required field in rate_limit_event message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
        catch (InvalidOperationException e)
        {
            throw new MessageParseException(
                $"Missing required field in rate_limit_event message: {e.Message}",
                JsonElementToDict(data),
                e
            );
        }
    }

    private static long? TryGetInt64(JsonElement elem, string propertyName)
    {
        if (elem.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.Number)
            return val.GetInt64();
        return null;
    }

    private static double? TryGetDouble(JsonElement elem, string propertyName)
    {
        if (elem.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.Number)
            return val.GetDouble();
        return null;
    }

    private static string? TryGetString(JsonElement elem, string propertyName)
    {
        if (elem.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
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

            string? stopReason = null;
            if (data.TryGetProperty("stop_reason", out var srElem) && srElem.ValueKind == JsonValueKind.String)
            {
                stopReason = srElem.GetString();
            }

            Dictionary<string, object?>? modelUsage = null;
            if (data.TryGetProperty("model_usage", out var muElem) && muElem.ValueKind == JsonValueKind.Object)
            {
                modelUsage = JsonElementToDict(muElem);
            }

            List<object>? permissionDenials = null;
            if (data.TryGetProperty("permission_denials", out var pdElem) && pdElem.ValueKind == JsonValueKind.Array)
            {
                permissionDenials = pdElem.EnumerateArray().Select(JsonElementToObject).ToList()!;
            }

            List<string>? errors = null;
            if (data.TryGetProperty("errors", out var errElem) && errElem.ValueKind == JsonValueKind.Array)
            {
                errors = errElem.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
            }

            string? uuid = null;
            if (data.TryGetProperty("uuid", out var uuidElem) && uuidElem.ValueKind == JsonValueKind.String)
            {
                uuid = uuidElem.GetString();
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
                StructuredOutput = structuredOutput,
                StopReason = stopReason,
                ModelUsage = modelUsage,
                PermissionDenials = permissionDenials,
                Errors = errors,
                Uuid = uuid
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
