using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgentSdk;

#region Enums

/// <summary>
/// Permission modes for the agent.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionMode>))]
public enum PermissionMode
{
    /// <summary>
    /// Default permission mode requiring explicit approval for sensitive operations.
    /// </summary>
    [JsonStringEnumMemberName("default")]
    Default,

    /// <summary>
    /// Automatically accept file edits without requiring approval.
    /// </summary>
    [JsonStringEnumMemberName("acceptEdits")]
    AcceptEdits,

    /// <summary>
    /// Plan mode for reviewing changes before execution.
    /// </summary>
    [JsonStringEnumMemberName("plan")]
    Plan,

    /// <summary>
    /// Bypass all permission checks (use with caution).
    /// </summary>
    [JsonStringEnumMemberName("bypassPermissions")]
    BypassPermissions
}

/// <summary>
/// SDK Beta features - see https://docs.anthropic.com/en/api/beta-headers
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SdkBeta>))]
public enum SdkBeta
{
    /// <summary>
    /// 1M context window beta feature (2025-08-07).
    /// </summary>
    [JsonStringEnumMemberName("context-1m-2025-08-07")]
    Context1M_2025_08_07
}

/// <summary>
/// Setting source types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SettingSource>))]
public enum SettingSource
{
    /// <summary>
    /// User-level settings.
    /// </summary>
    [JsonStringEnumMemberName("user")]
    User,

    /// <summary>
    /// Project-level settings.
    /// </summary>
    [JsonStringEnumMemberName("project")]
    Project,

    /// <summary>
    /// Local settings specific to the current environment.
    /// </summary>
    [JsonStringEnumMemberName("local")]
    Local
}

/// <summary>
/// Permission update destination types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionUpdateDestination>))]
public enum PermissionUpdateDestination
{
    /// <summary>
    /// Apply permission updates to user settings.
    /// </summary>
    [JsonStringEnumMemberName("userSettings")]
    UserSettings,

    /// <summary>
    /// Apply permission updates to project settings.
    /// </summary>
    [JsonStringEnumMemberName("projectSettings")]
    ProjectSettings,

    /// <summary>
    /// Apply permission updates to local settings.
    /// </summary>
    [JsonStringEnumMemberName("localSettings")]
    LocalSettings,

    /// <summary>
    /// Apply permission updates to the current session only.
    /// </summary>
    [JsonStringEnumMemberName("session")]
    Session
}

/// <summary>
/// Permission behavior types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionBehavior>))]
public enum PermissionBehavior
{
    /// <summary>
    /// Allow the operation.
    /// </summary>
    [JsonStringEnumMemberName("allow")]
    Allow,

    /// <summary>
    /// Deny the operation.
    /// </summary>
    [JsonStringEnumMemberName("deny")]
    Deny,

    /// <summary>
    /// Ask the user for permission.
    /// </summary>
    [JsonStringEnumMemberName("ask")]
    Ask
}

/// <summary>
/// Hook event types. Due to setup limitations, the SDK does not support
/// SessionStart, SessionEnd, and Notification hooks.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HookEvent>))]
public enum HookEvent
{
    /// <summary>
    /// Triggered before a tool is used.
    /// </summary>
    [JsonStringEnumMemberName("PreToolUse")]
    PreToolUse,

    /// <summary>
    /// Triggered after a tool is used.
    /// </summary>
    [JsonStringEnumMemberName("PostToolUse")]
    PostToolUse,

    /// <summary>
    /// Triggered when a user prompt is submitted.
    /// </summary>
    [JsonStringEnumMemberName("UserPromptSubmit")]
    UserPromptSubmit,

    /// <summary>
    /// Triggered when the agent stops.
    /// </summary>
    [JsonStringEnumMemberName("Stop")]
    Stop,

    /// <summary>
    /// Triggered when a subagent stops.
    /// </summary>
    [JsonStringEnumMemberName("SubagentStop")]
    SubagentStop,

    /// <summary>
    /// Triggered before context compaction.
    /// </summary>
    [JsonStringEnumMemberName("PreCompact")]
    PreCompact
}

/// <summary>
/// Assistant message error types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistantMessageError>))]
public enum AssistantMessageError
{
    /// <summary>
    /// Authentication failed.
    /// </summary>
    [JsonStringEnumMemberName("authentication_failed")]
    AuthenticationFailed,

    /// <summary>
    /// Billing error occurred.
    /// </summary>
    [JsonStringEnumMemberName("billing_error")]
    BillingError,

    /// <summary>
    /// Rate limit exceeded.
    /// </summary>
    [JsonStringEnumMemberName("rate_limit")]
    RateLimit,

    /// <summary>
    /// Invalid request error.
    /// </summary>
    [JsonStringEnumMemberName("invalid_request")]
    InvalidRequest,

    /// <summary>
    /// Server error occurred.
    /// </summary>
    [JsonStringEnumMemberName("server_error")]
    ServerError,

    /// <summary>
    /// Unknown error type.
    /// </summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown
}

/// <summary>
/// Permission update types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionUpdateType>))]
public enum PermissionUpdateType
{
    /// <summary>
    /// Add new permission rules.
    /// </summary>
    [JsonStringEnumMemberName("addRules")]
    AddRules,

    /// <summary>
    /// Replace existing permission rules.
    /// </summary>
    [JsonStringEnumMemberName("replaceRules")]
    ReplaceRules,

    /// <summary>
    /// Remove permission rules.
    /// </summary>
    [JsonStringEnumMemberName("removeRules")]
    RemoveRules,

    /// <summary>
    /// Set the permission mode.
    /// </summary>
    [JsonStringEnumMemberName("setMode")]
    SetMode,

    /// <summary>
    /// Add directories to the allowed list.
    /// </summary>
    [JsonStringEnumMemberName("addDirectories")]
    AddDirectories,

    /// <summary>
    /// Remove directories from the allowed list.
    /// </summary>
    [JsonStringEnumMemberName("removeDirectories")]
    RemoveDirectories
}

/// <summary>
/// Model types for agent definition.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentModel>))]
public enum AgentModel
{
    /// <summary>
    /// Claude Sonnet model.
    /// </summary>
    [JsonStringEnumMemberName("sonnet")]
    Sonnet,

    /// <summary>
    /// Claude Opus model.
    /// </summary>
    [JsonStringEnumMemberName("opus")]
    Opus,

    /// <summary>
    /// Claude Haiku model.
    /// </summary>
    [JsonStringEnumMemberName("haiku")]
    Haiku,

    /// <summary>
    /// Inherit model from parent agent.
    /// </summary>
    [JsonStringEnumMemberName("inherit")]
    Inherit
}

/// <summary>
/// Pre-compact trigger types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PreCompactTrigger>))]
public enum PreCompactTrigger
{
    /// <summary>
    /// Manually triggered compaction.
    /// </summary>
    [JsonStringEnumMemberName("manual")]
    Manual,

    /// <summary>
    /// Automatically triggered compaction.
    /// </summary>
    [JsonStringEnumMemberName("auto")]
    Auto
}

/// <summary>
/// SDK plugin types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SdkPluginType>))]
public enum SdkPluginType
{
    /// <summary>
    /// Local plugin loaded from the file system.
    /// </summary>
    [JsonStringEnumMemberName("local")]
    Local
}

/// <summary>
/// MCP server types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpServerType>))]
public enum McpServerType
{
    /// <summary>
    /// Standard I/O based MCP server.
    /// </summary>
    [JsonStringEnumMemberName("stdio")]
    Stdio,

    /// <summary>
    /// Server-Sent Events based MCP server.
    /// </summary>
    [JsonStringEnumMemberName("sse")]
    Sse,

    /// <summary>
    /// HTTP based MCP server.
    /// </summary>
    [JsonStringEnumMemberName("http")]
    Http,

    /// <summary>
    /// SDK-based MCP server running in-process.
    /// </summary>
    [JsonStringEnumMemberName("sdk")]
    Sdk
}

#endregion

#region Preset Types

/// <summary>
/// System prompt preset configuration.
/// </summary>
public record SystemPromptPreset
{
    /// <summary>
    /// Gets the type of the system prompt configuration.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "preset";

    /// <summary>
    /// Gets the preset name to use.
    /// </summary>
    [JsonPropertyName("preset")]
    public string Preset { get; init; } = "claude_code";

    /// <summary>
    /// Gets the optional text to append to the preset.
    /// </summary>
    [JsonPropertyName("append")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Append { get; init; }
}

/// <summary>
/// Tools preset configuration.
/// </summary>
public record ToolsPreset
{
    /// <summary>
    /// Gets the type of the tools configuration.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "preset";

    /// <summary>
    /// Gets the preset name to use for tools.
    /// </summary>
    [JsonPropertyName("preset")]
    public string Preset { get; init; } = "claude_code";
}

#endregion

#region Agent Definition

/// <summary>
/// Agent definition configuration.
/// </summary>
public record AgentDefinition
{
    /// <summary>
    /// Gets the description of the agent.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Gets the prompt used to configure the agent's behavior.
    /// </summary>
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the list of tools available to the agent.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tools { get; init; }

    /// <summary>
    /// Gets the model to use for the agent.
    /// </summary>
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentModel? Model { get; init; }
}

#endregion

#region Permission Types

/// <summary>
/// Permission rule value.
/// </summary>
public record PermissionRuleValue
{
    /// <summary>
    /// Gets the name of the tool this rule applies to.
    /// </summary>
    [JsonPropertyName("toolName")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the content of the permission rule.
    /// </summary>
    [JsonPropertyName("ruleContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuleContent { get; init; }
}

/// <summary>
/// Permission update configuration.
/// </summary>
public record PermissionUpdate
{
    /// <summary>
    /// Gets the type of permission update.
    /// </summary>
    [JsonPropertyName("type")]
    public required PermissionUpdateType Type { get; init; }

    /// <summary>
    /// Gets the list of permission rules to update.
    /// </summary>
    [JsonPropertyName("rules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PermissionRuleValue>? Rules { get; init; }

    /// <summary>
    /// Gets the permission behavior for the rules.
    /// </summary>
    [JsonPropertyName("behavior")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionBehavior? Behavior { get; init; }

    /// <summary>
    /// Gets the permission mode to set.
    /// </summary>
    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionMode? Mode { get; init; }

    /// <summary>
    /// Gets the list of directories to add or remove.
    /// </summary>
    [JsonPropertyName("directories")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Directories { get; init; }

    /// <summary>
    /// Gets the destination for the permission update.
    /// </summary>
    [JsonPropertyName("destination")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionUpdateDestination? Destination { get; init; }

    /// <summary>
    /// Convert PermissionUpdate to dictionary format matching TypeScript control protocol.
    /// </summary>
    public Dictionary<string, object?> ToDict()
    {
        var result = new Dictionary<string, object?>
        {
            ["type"] = Type.ToString()
        };

        if (Destination is not null)
        {
            result["destination"] = Destination.ToString();
        }

        if (Type is PermissionUpdateType.AddRules or PermissionUpdateType.ReplaceRules or PermissionUpdateType.RemoveRules)
        {
            if (Rules is not null)
            {
                result["rules"] = Rules.Select(rule => new Dictionary<string, object?>
                {
                    ["toolName"] = rule.ToolName,
                    ["ruleContent"] = rule.RuleContent
                }).ToList();
            }
            if (Behavior is not null)
            {
                result["behavior"] = Behavior.ToString();
            }
        }
        else if (Type is PermissionUpdateType.SetMode)
        {
            if (Mode is not null)
            {
                result["mode"] = Mode.ToString();
            }
        }
        else if (Type is PermissionUpdateType.AddDirectories or PermissionUpdateType.RemoveDirectories)
        {
            if (Directories is not null)
            {
                result["directories"] = Directories;
            }
        }

        return result;
    }
}

/// <summary>
/// Context information for tool permission callbacks.
/// </summary>
public record ToolPermissionContext
{
    /// <summary>
    /// Future: abort signal support.
    /// </summary>
    [JsonPropertyName("signal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Signal { get; init; }

    /// <summary>
    /// Permission suggestions from CLI.
    /// </summary>
    [JsonPropertyName("suggestions")]
    public List<PermissionUpdate> Suggestions { get; init; } = [];
}

/// <summary>
/// Base class for permission results.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "behavior")]
[JsonDerivedType(typeof(PermissionResultAllow), "allow")]
[JsonDerivedType(typeof(PermissionResultDeny), "deny")]
public abstract record PermissionResult
{
    /// <summary>
    /// Gets the behavior of the permission result.
    /// </summary>
    [JsonPropertyName("behavior")]
    public abstract string Behavior { get; }
}

/// <summary>
/// Allow permission result.
/// </summary>
public record PermissionResultAllow : PermissionResult
{
    /// <summary>
    /// Gets the behavior of the permission result.
    /// </summary>
    [JsonPropertyName("behavior")]
    public override string Behavior => "allow";

    /// <summary>
    /// Gets the updated input parameters after permission processing.
    /// </summary>
    [JsonPropertyName("updatedInput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? UpdatedInput { get; init; }

    /// <summary>
    /// Gets the updated permission rules to apply.
    /// </summary>
    [JsonPropertyName("updatedPermissions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PermissionUpdate>? UpdatedPermissions { get; init; }
}

/// <summary>
/// Deny permission result.
/// </summary>
public record PermissionResultDeny : PermissionResult
{
    /// <summary>
    /// Gets the behavior of the permission result.
    /// </summary>
    [JsonPropertyName("behavior")]
    public override string Behavior => "deny";

    /// <summary>
    /// Gets the message explaining why the permission was denied.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    /// <summary>
    /// Gets a value indicating whether to interrupt the agent execution.
    /// </summary>
    [JsonPropertyName("interrupt")]
    public bool Interrupt { get; init; } = false;
}

/// <summary>
/// Delegate for tool permission callback.
/// </summary>
public delegate Task<PermissionResult> CanUseTool(
    string toolName,
    Dictionary<string, object?> input,
    ToolPermissionContext context
);

#endregion

#region Hook Input Types

/// <summary>
/// Base hook input fields present across many hook events.
/// </summary>
public abstract record BaseHookInput
{
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the path to the transcript file.
    /// </summary>
    [JsonPropertyName("transcript_path")]
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets the current working directory.
    /// </summary>
    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }

    /// <summary>
    /// Gets the current permission mode.
    /// </summary>
    [JsonPropertyName("permission_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PermissionMode { get; init; }

    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public abstract string HookEventName { get; }
}

/// <summary>
/// Input data for PreToolUse hook events.
/// </summary>
public record PreToolUseHookInput : BaseHookInput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "PreToolUse";

    /// <summary>
    /// Gets the name of the tool being used.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the input parameters for the tool.
    /// </summary>
    [JsonPropertyName("tool_input")]
    public required Dictionary<string, object?> ToolInput { get; init; }
}

/// <summary>
/// Input data for PostToolUse hook events.
/// </summary>
public record PostToolUseHookInput : BaseHookInput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "PostToolUse";

    /// <summary>
    /// Gets the name of the tool that was used.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the input parameters that were passed to the tool.
    /// </summary>
    [JsonPropertyName("tool_input")]
    public required Dictionary<string, object?> ToolInput { get; init; }

    /// <summary>
    /// Gets the response from the tool execution.
    /// </summary>
    [JsonPropertyName("tool_response")]
    public object? ToolResponse { get; init; }
}

/// <summary>
/// Input data for UserPromptSubmit hook events.
/// </summary>
public record UserPromptSubmitHookInput : BaseHookInput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "UserPromptSubmit";

    /// <summary>
    /// Gets the user's prompt text.
    /// </summary>
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }
}

/// <summary>
/// Input data for Stop hook events.
/// </summary>
public record StopHookInput : BaseHookInput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "Stop";

    /// <summary>
    /// Gets a value indicating whether the stop hook is active.
    /// </summary>
    [JsonPropertyName("stop_hook_active")]
    public required bool StopHookActive { get; init; }
}

/// <summary>
/// Input data for SubagentStop hook events.
/// </summary>
public record SubagentStopHookInput : BaseHookInput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "SubagentStop";

    /// <summary>
    /// Gets a value indicating whether the stop hook is active.
    /// </summary>
    [JsonPropertyName("stop_hook_active")]
    public required bool StopHookActive { get; init; }
}

/// <summary>
/// Input data for PreCompact hook events.
/// </summary>
public record PreCompactHookInput : BaseHookInput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "PreCompact";

    /// <summary>
    /// Gets the trigger that initiated the compaction.
    /// </summary>
    [JsonPropertyName("trigger")]
    public required PreCompactTrigger Trigger { get; init; }

    /// <summary>
    /// Gets the custom instructions for the compaction.
    /// </summary>
    [JsonPropertyName("custom_instructions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomInstructions { get; init; }
}

/// <summary>
/// Union type alias for all hook inputs.
/// </summary>
/// <remarks>
/// Use pattern matching to handle different hook input types:
/// <code>
/// switch (hookInput)
/// {
///     case PreToolUseHookInput preToolUse: ...
///     case PostToolUseHookInput postToolUse: ...
/// }
/// </code>
/// </remarks>
public abstract record HookInput : BaseHookInput;

#endregion

#region Hook Specific Output Types

/// <summary>
/// Base class for hook-specific outputs.
/// </summary>
public abstract record HookSpecificOutput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public abstract string HookEventName { get; }
}

/// <summary>
/// Hook-specific output for PreToolUse events.
/// </summary>
public record PreToolUseHookSpecificOutput : HookSpecificOutput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "PreToolUse";

    /// <summary>
    /// Gets the permission decision for the tool use.
    /// </summary>
    [JsonPropertyName("permissionDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionBehavior? PermissionDecision { get; init; }

    /// <summary>
    /// Gets the reason for the permission decision.
    /// </summary>
    [JsonPropertyName("permissionDecisionReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PermissionDecisionReason { get; init; }

    /// <summary>
    /// Gets the updated input parameters for the tool.
    /// </summary>
    [JsonPropertyName("updatedInput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? UpdatedInput { get; init; }
}

/// <summary>
/// Hook-specific output for PostToolUse events.
/// </summary>
public record PostToolUseHookSpecificOutput : HookSpecificOutput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "PostToolUse";

    /// <summary>
    /// Gets additional context information to include.
    /// </summary>
    [JsonPropertyName("additionalContext")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalContext { get; init; }
}

/// <summary>
/// Hook-specific output for UserPromptSubmit events.
/// </summary>
public record UserPromptSubmitHookSpecificOutput : HookSpecificOutput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "UserPromptSubmit";

    /// <summary>
    /// Gets additional context information to include.
    /// </summary>
    [JsonPropertyName("additionalContext")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalContext { get; init; }
}

/// <summary>
/// Hook-specific output for SessionStart events.
/// </summary>
public record SessionStartHookSpecificOutput : HookSpecificOutput
{
    /// <summary>
    /// Gets the name of the hook event.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "SessionStart";

    /// <summary>
    /// Gets additional context information to include.
    /// </summary>
    [JsonPropertyName("additionalContext")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalContext { get; init; }
}

#endregion

#region Hook JSON Output Types

/// <summary>
/// Base class for hook JSON outputs.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(AsyncHookJsonOutput))]
[JsonDerivedType(typeof(SyncHookJsonOutput))]
public abstract record HookJsonOutput;

/// <summary>
/// Async hook output that defers hook execution.
/// </summary>
public record AsyncHookJsonOutput : HookJsonOutput
{
    /// <summary>
    /// Set to true to defer hook execution.
    /// Note: This is converted to "async" when sent to the CLI.
    /// </summary>
    [JsonPropertyName("async")]
    public bool Async { get; init; } = true;

    /// <summary>
    /// Optional timeout in milliseconds for the async operation.
    /// </summary>
    [JsonPropertyName("asyncTimeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AsyncTimeout { get; init; }
}

/// <summary>
/// Synchronous hook output with control and decision fields.
/// </summary>
public record SyncHookJsonOutput : HookJsonOutput
{
    /// <summary>
    /// Whether Claude should proceed after hook execution (default: true).
    /// Note: This is converted to "continue" when sent to the CLI.
    /// </summary>
    [JsonPropertyName("continue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Continue { get; init; }

    /// <summary>
    /// Hide stdout from transcript mode (default: false).
    /// </summary>
    [JsonPropertyName("suppressOutput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SuppressOutput { get; init; }

    /// <summary>
    /// Message shown when continue is false.
    /// </summary>
    [JsonPropertyName("stopReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StopReason { get; init; }

    /// <summary>
    /// Set to "block" to indicate blocking behavior.
    /// Note: "approve" is deprecated for PreToolUse (use permissionDecision instead).
    /// </summary>
    [JsonPropertyName("decision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Decision { get; init; }

    /// <summary>
    /// Warning message displayed to the user.
    /// </summary>
    [JsonPropertyName("systemMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemMessage { get; init; }

    /// <summary>
    /// Feedback message for Claude about the decision.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>
    /// Hook-specific outputs.
    /// </summary>
    [JsonPropertyName("hookSpecificOutput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HookSpecificOutput? HookSpecificOutput { get; init; }
}

/// <summary>
/// Context information for hook callbacks.
/// </summary>
public record HookContext
{
    /// <summary>
    /// Reserved for future abort signal support. Currently always null.
    /// </summary>
    [JsonPropertyName("signal")]
    public object? Signal { get; init; }
}

/// <summary>
/// Delegate for hook callbacks.
/// </summary>
/// <param name="input">Strongly-typed hook input with discriminated unions based on hook_event_name.</param>
/// <param name="toolUseId">Optional tool use identifier.</param>
/// <param name="context">Hook context with abort signal support (currently placeholder).</param>
public delegate Task<HookJsonOutput> HookCallback(
    BaseHookInput input,
    string? toolUseId,
    HookContext context
);

/// <summary>
/// Hook matcher configuration.
/// </summary>
public record HookMatcher
{
    /// <summary>
    /// See https://docs.anthropic.com/en/docs/claude-code/hooks#structure for the
    /// expected string value. For example, for PreToolUse, the matcher can be
    /// a tool name like "Bash" or a combination of tool names like
    /// "Write|MultiEdit|Edit".
    /// </summary>
    [JsonPropertyName("matcher")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Matcher { get; init; }

    /// <summary>
    /// A list of callback functions with function signature HookCallback.
    /// </summary>
    [JsonIgnore]
    public List<HookCallback> Hooks { get; init; } = [];

    /// <summary>
    /// Timeout in seconds for all hooks in this matcher (default: 60).
    /// </summary>
    [JsonPropertyName("timeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Timeout { get; init; }
}

#endregion

#region MCP Server Config Types

/// <summary>
/// Base class for MCP server configurations.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(McpStdioServerConfig), "stdio")]
[JsonDerivedType(typeof(McpSseServerConfig), "sse")]
[JsonDerivedType(typeof(McpHttpServerConfig), "http")]
[JsonDerivedType(typeof(McpSdkServerConfig), "sdk")]
public abstract record McpServerConfig
{
    /// <summary>
    /// Gets the type of the MCP server.
    /// </summary>
    [JsonPropertyName("type")]
    public abstract McpServerType Type { get; }
}

/// <summary>
/// MCP stdio server configuration.
/// </summary>
public record McpStdioServerConfig : McpServerConfig
{
    /// <summary>
    /// Gets the type of the MCP server.
    /// </summary>
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Stdio;

    /// <summary>
    /// Gets the command to execute.
    /// </summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// Gets the arguments to pass to the command.
    /// </summary>
    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Args { get; init; }

    /// <summary>
    /// Gets the environment variables for the process.
    /// </summary>
    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; init; }
}

/// <summary>
/// MCP SSE server configuration.
/// </summary>
public record McpSseServerConfig : McpServerConfig
{
    /// <summary>
    /// Gets the type of the MCP server.
    /// </summary>
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Sse;

    /// <summary>
    /// Gets the URL of the SSE server.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// Gets the HTTP headers to send with requests.
    /// </summary>
    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// MCP HTTP server configuration.
/// </summary>
public record McpHttpServerConfig : McpServerConfig
{
    /// <summary>
    /// Gets the type of the MCP server.
    /// </summary>
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Http;

    /// <summary>
    /// Gets the URL of the HTTP server.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// Gets the HTTP headers to send with requests.
    /// </summary>
    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// SDK MCP server configuration.
/// </summary>
public record McpSdkServerConfig : McpServerConfig
{
    /// <summary>
    /// Gets the type of the MCP server.
    /// </summary>
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Sdk;

    /// <summary>
    /// Gets the name of the SDK server.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the MCP server instance.
    /// </summary>
    [JsonIgnore]
    public required object Instance { get; init; }
}

#endregion

#region Plugin Config Types

/// <summary>
/// SDK plugin configuration.
/// Currently only local plugins are supported via the 'local' type.
/// </summary>
public record SdkPluginConfig
{
    /// <summary>
    /// Gets the type of the plugin.
    /// </summary>
    [JsonPropertyName("type")]
    public SdkPluginType Type { get; init; } = SdkPluginType.Local;

    /// <summary>
    /// Gets the path to the plugin.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

#endregion

#region Sandbox Settings Types

/// <summary>
/// Network configuration for sandbox.
/// </summary>
public record SandboxNetworkConfig
{
    /// <summary>
    /// Unix socket paths accessible in sandbox (e.g., SSH agents).
    /// </summary>
    [JsonPropertyName("allowUnixSockets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AllowUnixSockets { get; init; }

    /// <summary>
    /// Allow all Unix sockets (less secure).
    /// </summary>
    [JsonPropertyName("allowAllUnixSockets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllowAllUnixSockets { get; init; }

    /// <summary>
    /// Allow binding to localhost ports (macOS only).
    /// </summary>
    [JsonPropertyName("allowLocalBinding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllowLocalBinding { get; init; }

    /// <summary>
    /// HTTP proxy port if bringing your own proxy.
    /// </summary>
    [JsonPropertyName("httpProxyPort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? HttpProxyPort { get; init; }

    /// <summary>
    /// SOCKS5 proxy port if bringing your own proxy.
    /// </summary>
    [JsonPropertyName("socksProxyPort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SocksProxyPort { get; init; }
}

/// <summary>
/// Violations to ignore in sandbox.
/// </summary>
public record SandboxIgnoreViolations
{
    /// <summary>
    /// File paths for which violations should be ignored.
    /// </summary>
    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? File { get; init; }

    /// <summary>
    /// Network hosts for which violations should be ignored.
    /// </summary>
    [JsonPropertyName("network")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Network { get; init; }
}

/// <summary>
/// Sandbox settings configuration.
/// This controls how Claude Code sandboxes bash commands for filesystem
/// and network isolation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Important:</b> Filesystem and network restrictions are configured via permission
/// rules, not via these sandbox settings:
/// </para>
/// <list type="bullet">
/// <item>Filesystem read restrictions: Use Read deny rules</item>
/// <item>Filesystem write restrictions: Use Edit allow/deny rules</item>
/// <item>Network restrictions: Use WebFetch allow/deny rules</item>
/// </list>
/// </remarks>
public record SandboxSettings
{
    /// <summary>
    /// Enable bash sandboxing (macOS/Linux only). Default: false.
    /// </summary>
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; init; }

    /// <summary>
    /// Auto-approve bash commands when sandboxed. Default: true.
    /// </summary>
    [JsonPropertyName("autoAllowBashIfSandboxed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AutoAllowBashIfSandboxed { get; init; }

    /// <summary>
    /// Commands that should run outside the sandbox (e.g., ["git", "docker"]).
    /// </summary>
    [JsonPropertyName("excludedCommands")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ExcludedCommands { get; init; }

    /// <summary>
    /// Allow commands to bypass sandbox via dangerouslyDisableSandbox.
    /// When false, all commands must run sandboxed (or be in excludedCommands). Default: true.
    /// </summary>
    [JsonPropertyName("allowUnsandboxedCommands")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllowUnsandboxedCommands { get; init; }

    /// <summary>
    /// Network configuration for sandbox.
    /// </summary>
    [JsonPropertyName("network")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SandboxNetworkConfig? Network { get; init; }

    /// <summary>
    /// Violations to ignore.
    /// </summary>
    [JsonPropertyName("ignoreViolations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SandboxIgnoreViolations? IgnoreViolations { get; init; }

    /// <summary>
    /// Enable weaker sandbox for unprivileged Docker environments
    /// (Linux only). Reduces security. Default: false.
    /// </summary>
    [JsonPropertyName("enableWeakerNestedSandbox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableWeakerNestedSandbox { get; init; }
}

#endregion

#region Content Block Types

/// <summary>
/// Base class for content blocks.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(ThinkingBlock), "thinking")]
[JsonDerivedType(typeof(ToolUseBlock), "tool_use")]
[JsonDerivedType(typeof(ToolResultBlock), "tool_result")]
public abstract record ContentBlock;

/// <summary>
/// Text content block.
/// </summary>
public record TextBlock : ContentBlock
{
    /// <summary>
    /// Gets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// Thinking content block.
/// </summary>
public record ThinkingBlock : ContentBlock
{
    /// <summary>
    /// Gets the thinking content.
    /// </summary>
    [JsonPropertyName("thinking")]
    public required string Thinking { get; init; }

    /// <summary>
    /// Gets the signature of the thinking block.
    /// </summary>
    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}

/// <summary>
/// Tool use content block.
/// </summary>
public record ToolUseBlock : ContentBlock
{
    /// <summary>
    /// Gets the unique identifier for this tool use.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name of the tool being used.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the input parameters for the tool.
    /// </summary>
    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }
}

/// <summary>
/// Tool result content block.
/// </summary>
public record ToolResultBlock : ContentBlock
{
    /// <summary>
    /// Gets the ID of the tool use this result corresponds to.
    /// </summary>
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    /// <summary>
    /// Gets the content of the tool result.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; init; }

    /// <summary>
    /// Gets a value indicating whether the tool execution resulted in an error.
    /// </summary>
    [JsonPropertyName("is_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }
}

#endregion

#region Message Types

/// <summary>
/// Base class for messages.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UserMessage), "user")]
[JsonDerivedType(typeof(AssistantMessage), "assistant")]
[JsonDerivedType(typeof(SystemMessage), "system")]
[JsonDerivedType(typeof(ResultMessage), "result")]
[JsonDerivedType(typeof(StreamEvent), "stream_event")]
public abstract record Message;

/// <summary>
/// User message.
/// </summary>
public record UserMessage : Message
{
    /// <summary>
    /// Gets the content of the message (string or list of content blocks).
    /// </summary>
    [JsonPropertyName("content")]
    public required List<ContentBlock> Content { get; init; }

    /// <summary>
    /// Gets the unique identifier for this message.
    /// </summary>
    [JsonPropertyName("uuid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uuid { get; init; }

    /// <summary>
    /// Gets the parent tool use ID if this message is a response to a tool.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentToolUseId { get; init; }
}

/// <summary>
/// Assistant message with content blocks.
/// </summary>
public record AssistantMessage : Message
{
    /// <summary>
    /// Gets the content blocks of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public required List<ContentBlock> Content { get; init; }

    /// <summary>
    /// Gets the model used to generate this message.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// Gets the parent tool use ID if this message is part of a tool execution.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentToolUseId { get; init; }

    /// <summary>
    /// Gets the error type if an error occurred.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssistantMessageError? Error { get; init; }
}

/// <summary>
/// System message with metadata.
/// </summary>
public record SystemMessage : Message
{
    /// <summary>
    /// Gets the subtype of the system message.
    /// </summary>
    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    /// <summary>
    /// Gets the data associated with the system message.
    /// </summary>
    [JsonPropertyName("data")]
    public required Dictionary<string, object?> Data { get; init; }
}

/// <summary>
/// Result message with cost and usage information.
/// </summary>
public record ResultMessage : Message
{
    /// <summary>
    /// Gets the subtype of the result message.
    /// </summary>
    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    /// <summary>
    /// Gets the total duration in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }

    /// <summary>
    /// Gets the API duration in milliseconds.
    /// </summary>
    [JsonPropertyName("duration_api_ms")]
    public required int DurationApiMs { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result represents an error.
    /// </summary>
    [JsonPropertyName("is_error")]
    public required bool IsError { get; init; }

    /// <summary>
    /// Gets the number of turns in the conversation.
    /// </summary>
    [JsonPropertyName("num_turns")]
    public required int NumTurns { get; init; }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the total cost in USD.
    /// </summary>
    [JsonPropertyName("total_cost_usd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TotalCostUsd { get; init; }

    /// <summary>
    /// Gets the token usage information.
    /// </summary>
    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Usage { get; init; }

    /// <summary>
    /// Gets the result text.
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; init; }

    /// <summary>
    /// Gets the structured output if output format was specified.
    /// </summary>
    [JsonPropertyName("structured_output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StructuredOutput { get; init; }
}

/// <summary>
/// Stream event for partial message updates during streaming.
/// </summary>
public record StreamEvent : Message
{
    /// <summary>
    /// Gets the unique identifier for this event.
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the raw Anthropic API stream event.
    /// </summary>
    [JsonPropertyName("event")]
    public required Dictionary<string, object?> Event { get; init; }

    /// <summary>
    /// Gets the parent tool use ID if this event is part of a tool execution.
    /// </summary>
    [JsonPropertyName("parent_tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentToolUseId { get; init; }
}

#endregion

#region Claude Agent Options

/// <summary>
/// Query options for Claude SDK.
/// </summary>
public record ClaudeAgentOptions
{
    /// <summary>
    /// List of tool names or a tools preset configuration.
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Tools { get; init; }

    /// <summary>
    /// List of allowed tool names.
    /// </summary>
    [JsonPropertyName("allowed_tools")]
    public List<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// System prompt string or preset configuration.
    /// </summary>
    [JsonPropertyName("system_prompt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? SystemPrompt { get; init; }

    /// <summary>
    /// MCP server configurations. Can be a dictionary of configs, a string path, or a Path.
    /// </summary>
    [JsonPropertyName("mcp_servers")]
    public object McpServers { get; init; } = new Dictionary<string, McpServerConfig>();

    /// <summary>
    /// Permission mode for the agent.
    /// </summary>
    [JsonPropertyName("permission_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionMode? PermissionMode { get; init; }

    /// <summary>
    /// Continue from previous conversation.
    /// </summary>
    [JsonPropertyName("continue_conversation")]
    public bool ContinueConversation { get; init; } = false;

    /// <summary>
    /// Session ID to resume.
    /// </summary>
    [JsonPropertyName("resume")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Resume { get; init; }

    /// <summary>
    /// Maximum number of turns.
    /// </summary>
    [JsonPropertyName("max_turns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTurns { get; init; }

    /// <summary>
    /// Maximum budget in USD.
    /// </summary>
    [JsonPropertyName("max_budget_usd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxBudgetUsd { get; init; }

    /// <summary>
    /// List of disallowed tool names.
    /// </summary>
    [JsonPropertyName("disallowed_tools")]
    public List<string> DisallowedTools { get; init; } = [];

    /// <summary>
    /// Model name to use.
    /// </summary>
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>
    /// Fallback model name.
    /// </summary>
    [JsonPropertyName("fallback_model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FallbackModel { get; init; }

    /// <summary>
    /// Beta features - see https://docs.anthropic.com/en/api/beta-headers
    /// </summary>
    [JsonPropertyName("betas")]
    public List<SdkBeta> Betas { get; init; } = [];

    /// <summary>
    /// Permission prompt tool name.
    /// </summary>
    [JsonPropertyName("permission_prompt_tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PermissionPromptToolName { get; init; }

    /// <summary>
    /// Current working directory.
    /// </summary>
    [JsonPropertyName("cwd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cwd { get; init; }

    /// <summary>
    /// CLI path.
    /// </summary>
    [JsonPropertyName("cli_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CliPath { get; init; }

    /// <summary>
    /// Settings string.
    /// </summary>
    [JsonPropertyName("settings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Settings { get; init; }

    /// <summary>
    /// Additional directories to add.
    /// </summary>
    [JsonPropertyName("add_dirs")]
    public List<string> AddDirs { get; init; } = [];

    /// <summary>
    /// Environment variables.
    /// </summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; init; } = [];

    /// <summary>
    /// Pass arbitrary CLI flags.
    /// </summary>
    [JsonPropertyName("extra_args")]
    public Dictionary<string, string?> ExtraArgs { get; init; } = [];

    /// <summary>
    /// Max bytes when buffering CLI stdout.
    /// </summary>
    [JsonPropertyName("max_buffer_size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxBufferSize { get; init; }

    /// <summary>
    /// Callback for stderr output from CLI.
    /// </summary>
    [JsonIgnore]
    public Action<string>? Stderr { get; init; }

    /// <summary>
    /// Tool permission callback.
    /// </summary>
    [JsonIgnore]
    public CanUseTool? CanUseTool { get; init; }

    /// <summary>
    /// Hook configurations.
    /// </summary>
    [JsonIgnore]
    public Dictionary<HookEvent, List<HookMatcher>>? Hooks { get; init; }

    /// <summary>
    /// User identifier.
    /// </summary>
    [JsonPropertyName("user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; init; }

    /// <summary>
    /// Partial message streaming support.
    /// </summary>
    [JsonPropertyName("include_partial_messages")]
    public bool IncludePartialMessages { get; init; } = false;

    /// <summary>
    /// When true resumed sessions will fork to a new session ID rather than
    /// continuing the previous session.
    /// </summary>
    [JsonPropertyName("fork_session")]
    public bool ForkSession { get; init; } = false;

    /// <summary>
    /// Agent definitions for custom agents.
    /// </summary>
    [JsonPropertyName("agents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, AgentDefinition>? Agents { get; init; }

    /// <summary>
    /// Setting sources to load (user, project, local).
    /// </summary>
    [JsonPropertyName("setting_sources")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SettingSource>? SettingSources { get; init; }

    /// <summary>
    /// Sandbox configuration for bash command isolation.
    /// Filesystem and network restrictions are derived from permission rules (Read/Edit/WebFetch),
    /// not from these sandbox settings.
    /// </summary>
    [JsonPropertyName("sandbox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SandboxSettings? Sandbox { get; init; }

    /// <summary>
    /// Plugin configurations for custom plugins.
    /// </summary>
    [JsonPropertyName("plugins")]
    public List<SdkPluginConfig> Plugins { get; init; } = [];

    /// <summary>
    /// Max tokens for thinking blocks.
    /// </summary>
    [JsonPropertyName("max_thinking_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxThinkingTokens { get; init; }

    /// <summary>
    /// Output format for structured outputs (matches Messages API structure).
    /// Example: {"type": "json_schema", "schema": {"type": "object", "properties": {...}}}
    /// </summary>
    [JsonPropertyName("output_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? OutputFormat { get; init; }

    /// <summary>
    /// Enable file checkpointing to track file changes during the session.
    /// When enabled, files can be rewound to their state at any user message.
    /// </summary>
    [JsonPropertyName("enable_file_checkpointing")]
    public bool EnableFileCheckpointing { get; init; } = false;
}

#endregion

#region SDK MCP Tool Types

/// <summary>
/// Represents a content item in an MCP tool result.
/// </summary>
public record SdkMcpContent
{
    /// <summary>
    /// Content type (e.g., "text", "image").
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Text content.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Image data (base64 encoded).
    /// </summary>
    public string? Data { get; init; }

    /// <summary>
    /// Image MIME type (e.g., "image/png").
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Create a text content item.
    /// </summary>
    public static SdkMcpContent CreateText(string text) => new()
    {
        Type = "text",
        Text = text
    };

    /// <summary>
    /// Create an image content item.
    /// </summary>
    public static SdkMcpContent CreateImage(string data, string mimeType) => new()
    {
        Type = "image",
        Data = data,
        MimeType = mimeType
    };
}

/// <summary>
/// Represents the result of an MCP tool call.
/// </summary>
public record SdkMcpToolResult
{
    /// <summary>
    /// The content items in the result.
    /// </summary>
    public required IReadOnlyList<SdkMcpContent> Content { get; init; }

    /// <summary>
    /// Whether the tool call resulted in an error.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Create a successful text result.
    /// </summary>
    public static SdkMcpToolResult FromText(string text) => new()
    {
        Content = [SdkMcpContent.CreateText(text)]
    };

    /// <summary>
    /// Create an error result.
    /// </summary>
    public static SdkMcpToolResult FromError(string errorMessage) => new()
    {
        Content = [SdkMcpContent.CreateText(errorMessage)],
        IsError = true
    };
}

/// <summary>
/// Non-generic base interface for SDK MCP tools.
/// </summary>
public interface ISdkMcpToolDefinition
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The description of the tool.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The JSON schema for the tool's input.
    /// </summary>
    object InputSchema { get; }

    /// <summary>
    /// Call the tool with the specified arguments.
    /// </summary>
    Task<SdkMcpToolResult> CallAsync(Dictionary<string, object?> arguments, CancellationToken ct = default);
}

/// <summary>
/// Represents an MCP tool definition with a strongly-typed handler.
/// </summary>
/// <typeparam name="T">The input type for the tool.</typeparam>
public record SdkMcpTool<T> : ISdkMcpToolDefinition
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The description of the tool.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The handler function for the tool.
    /// </summary>
    public required Func<T, CancellationToken, Task<SdkMcpToolResult>> Handler { get; init; }

    /// <summary>
    /// The JSON schema for the tool's input. Auto-generated from T if not provided.
    /// </summary>
    public object? Schema { get; init; }

    /// <summary>
    /// Get the input schema, generating from type T if not provided.
    /// </summary>
    object ISdkMcpToolDefinition.InputSchema => Schema ?? GenerateSchemaFromType(typeof(T));

    /// <summary>
    /// Call the tool with dictionary arguments.
    /// </summary>
    async Task<SdkMcpToolResult> ISdkMcpToolDefinition.CallAsync(Dictionary<string, object?> arguments, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(arguments);
        var input = JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize arguments to {typeof(T).Name}");
        return await Handler(input, ct);
    }

    private static object GenerateSchemaFromType(Type type)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propName = JsonNamingPolicy.SnakeCaseLower.ConvertName(prop.Name);
            properties[propName] = GetJsonSchemaType(prop.PropertyType);

            // Check if property is required (non-nullable value type or has required modifier)
            if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
            {
                required.Add(propName);
            }
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private static Dictionary<string, object> GetJsonSchemaType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
            return new Dictionary<string, object> { ["type"] = "string" };
        if (underlyingType == typeof(int) || underlyingType == typeof(long))
            return new Dictionary<string, object> { ["type"] = "integer" };
        if (underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal))
            return new Dictionary<string, object> { ["type"] = "number" };
        if (underlyingType == typeof(bool))
            return new Dictionary<string, object> { ["type"] = "boolean" };

        return new Dictionary<string, object> { ["type"] = "string" };
    }
}

/// <summary>
/// Static helper methods for creating SDK MCP tools.
/// </summary>
public static class SdkMcpTool
{
    /// <summary>
    /// Create a new MCP tool with a strongly-typed handler.
    /// </summary>
    public static SdkMcpTool<T> Create<T>(
        string name,
        string description,
        Func<T, CancellationToken, Task<SdkMcpToolResult>> handler,
        object? schema = null)
    {
        return new SdkMcpTool<T>
        {
            Name = name,
            Description = description,
            Handler = handler,
            Schema = schema
        };
    }

    /// <summary>
    /// Create a new MCP tool with a handler that doesn't use cancellation token.
    /// </summary>
    public static SdkMcpTool<T> Create<T>(
        string name,
        string description,
        Func<T, Task<SdkMcpToolResult>> handler,
        object? schema = null)
    {
        return new SdkMcpTool<T>
        {
            Name = name,
            Description = description,
            Handler = (args, _) => handler(args),
            Schema = schema
        };
    }
}

#endregion

#region SDK Control Protocol Types

/// <summary>
/// Base class for SDK control requests.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "subtype")]
[JsonDerivedType(typeof(SdkControlInterruptRequest), "interrupt")]
[JsonDerivedType(typeof(SdkControlPermissionRequest), "can_use_tool")]
[JsonDerivedType(typeof(SdkControlInitializeRequest), "initialize")]
[JsonDerivedType(typeof(SdkControlSetPermissionModeRequest), "set_permission_mode")]
[JsonDerivedType(typeof(SdkHookCallbackRequest), "hook_callback")]
[JsonDerivedType(typeof(SdkControlMcpMessageRequest), "mcp_message")]
[JsonDerivedType(typeof(SdkControlRewindFilesRequest), "rewind_files")]
public abstract record SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public abstract string Subtype { get; }
}

/// <summary>
/// Interrupt request.
/// </summary>
public record SdkControlInterruptRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "interrupt";
}

/// <summary>
/// Permission request for tool usage.
/// </summary>
public record SdkControlPermissionRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "can_use_tool";

    /// <summary>
    /// Gets the name of the tool requesting permission.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the input parameters for the tool.
    /// </summary>
    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }

    /// <summary>
    /// Gets the suggested permission updates from the CLI.
    /// </summary>
    [JsonPropertyName("permission_suggestions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? PermissionSuggestions { get; init; }

    /// <summary>
    /// Gets the path that was blocked, if applicable.
    /// </summary>
    [JsonPropertyName("blocked_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BlockedPath { get; init; }
}

/// <summary>
/// Initialize request.
/// </summary>
public record SdkControlInitializeRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "initialize";

    /// <summary>
    /// Gets the hook configurations.
    /// </summary>
    [JsonPropertyName("hooks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<HookEvent, object>? Hooks { get; init; }
}

/// <summary>
/// Set permission mode request.
/// </summary>
public record SdkControlSetPermissionModeRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "set_permission_mode";

    /// <summary>
    /// Gets the permission mode to set.
    /// </summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; init; }
}

/// <summary>
/// Hook callback request.
/// </summary>
public record SdkHookCallbackRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "hook_callback";

    /// <summary>
    /// Gets the callback identifier.
    /// </summary>
    [JsonPropertyName("callback_id")]
    public required string CallbackId { get; init; }

    /// <summary>
    /// Gets the input data for the callback.
    /// </summary>
    [JsonPropertyName("input")]
    public object? Input { get; init; }

    /// <summary>
    /// Gets the tool use ID associated with this callback.
    /// </summary>
    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; init; }
}

/// <summary>
/// MCP message request.
/// </summary>
public record SdkControlMcpMessageRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "mcp_message";

    /// <summary>
    /// Gets the name of the MCP server.
    /// </summary>
    [JsonPropertyName("server_name")]
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets the message to send to the server.
    /// </summary>
    [JsonPropertyName("message")]
    public object? Message { get; init; }
}

/// <summary>
/// Rewind files request.
/// </summary>
public record SdkControlRewindFilesRequest : SdkControlRequestBase
{
    /// <summary>
    /// Gets the subtype of the control request.
    /// </summary>
    [JsonPropertyName("subtype")]
    public override string Subtype => "rewind_files";

    /// <summary>
    /// Gets the user message ID to rewind files to.
    /// </summary>
    [JsonPropertyName("user_message_id")]
    public required string UserMessageId { get; init; }
}

/// <summary>
/// SDK control request wrapper.
/// </summary>
public record SdkControlRequest
{
    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "control_request";

    /// <summary>
    /// Gets the unique identifier for this request.
    /// </summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the control request payload.
    /// </summary>
    [JsonPropertyName("request")]
    public required SdkControlRequestBase Request { get; init; }
}

/// <summary>
/// Success control response.
/// </summary>
public record ControlResponse
{
    /// <summary>
    /// Gets the subtype of the response.
    /// </summary>
    [JsonPropertyName("subtype")]
    public string Subtype { get; init; } = "success";

    /// <summary>
    /// Gets the request ID this response corresponds to.
    /// </summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the response data.
    /// </summary>
    [JsonPropertyName("response")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Response { get; init; }
}

/// <summary>
/// Error control response.
/// </summary>
public record ControlErrorResponse
{
    /// <summary>
    /// Gets the subtype of the response.
    /// </summary>
    [JsonPropertyName("subtype")]
    public string Subtype { get; init; } = "error";

    /// <summary>
    /// Gets the request ID this response corresponds to.
    /// </summary>
    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}

/// <summary>
/// SDK control response wrapper.
/// </summary>
public record SdkControlResponse
{
    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "control_response";

    /// <summary>
    /// Gets the response (can be either ControlResponse or ControlErrorResponse).
    /// </summary>
    [JsonPropertyName("response")]
    public required object Response { get; init; }
}

#endregion
