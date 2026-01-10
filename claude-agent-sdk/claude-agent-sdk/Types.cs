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
    [JsonPropertyName("default")]
    Default,
    [JsonPropertyName("acceptEdits")]
    AcceptEdits,
    [JsonPropertyName("plan")]
    Plan,
    [JsonPropertyName("bypassPermissions")]
    BypassPermissions
}

/// <summary>
/// SDK Beta features - see https://docs.anthropic.com/en/api/beta-headers
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SdkBeta>))]
public enum SdkBeta
{
    [JsonPropertyName("context-1m-2025-08-07")]
    Context1M_2025_08_07
}

/// <summary>
/// Setting source types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SettingSource>))]
public enum SettingSource
{
    [JsonPropertyName("user")]
    User,
    [JsonPropertyName("project")]
    Project,
    [JsonPropertyName("local")]
    Local
}

/// <summary>
/// Permission update destination types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionUpdateDestination>))]
public enum PermissionUpdateDestination
{
    [JsonPropertyName("userSettings")]
    UserSettings,
    [JsonPropertyName("projectSettings")]
    ProjectSettings,
    [JsonPropertyName("localSettings")]
    LocalSettings,
    [JsonPropertyName("session")]
    Session
}

/// <summary>
/// Permission behavior types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionBehavior>))]
public enum PermissionBehavior
{
    [JsonPropertyName("allow")]
    Allow,
    [JsonPropertyName("deny")]
    Deny,
    [JsonPropertyName("ask")]
    Ask
}

/// <summary>
/// Hook event types. Due to setup limitations, the SDK does not support
/// SessionStart, SessionEnd, and Notification hooks.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HookEvent>))]
public enum HookEvent
{
    [JsonPropertyName("PreToolUse")]
    PreToolUse,
    [JsonPropertyName("PostToolUse")]
    PostToolUse,
    [JsonPropertyName("UserPromptSubmit")]
    UserPromptSubmit,
    [JsonPropertyName("Stop")]
    Stop,
    [JsonPropertyName("SubagentStop")]
    SubagentStop,
    [JsonPropertyName("PreCompact")]
    PreCompact
}

/// <summary>
/// Assistant message error types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistantMessageError>))]
public enum AssistantMessageError
{
    [JsonPropertyName("authentication_failed")]
    AuthenticationFailed,
    [JsonPropertyName("billing_error")]
    BillingError,
    [JsonPropertyName("rate_limit")]
    RateLimit,
    [JsonPropertyName("invalid_request")]
    InvalidRequest,
    [JsonPropertyName("server_error")]
    ServerError,
    [JsonPropertyName("unknown")]
    Unknown
}

/// <summary>
/// Permission update types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionUpdateType>))]
public enum PermissionUpdateType
{
    [JsonPropertyName("addRules")]
    AddRules,
    [JsonPropertyName("replaceRules")]
    ReplaceRules,
    [JsonPropertyName("removeRules")]
    RemoveRules,
    [JsonPropertyName("setMode")]
    SetMode,
    [JsonPropertyName("addDirectories")]
    AddDirectories,
    [JsonPropertyName("removeDirectories")]
    RemoveDirectories
}

/// <summary>
/// Model types for agent definition.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentModel>))]
public enum AgentModel
{
    [JsonPropertyName("sonnet")]
    Sonnet,
    [JsonPropertyName("opus")]
    Opus,
    [JsonPropertyName("haiku")]
    Haiku,
    [JsonPropertyName("inherit")]
    Inherit
}

/// <summary>
/// Pre-compact trigger types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PreCompactTrigger>))]
public enum PreCompactTrigger
{
    [JsonPropertyName("manual")]
    Manual,
    [JsonPropertyName("auto")]
    Auto
}

/// <summary>
/// SDK plugin types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SdkPluginType>))]
public enum SdkPluginType
{
    [JsonPropertyName("local")]
    Local
}

/// <summary>
/// MCP server types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpServerType>))]
public enum McpServerType
{
    [JsonPropertyName("stdio")]
    Stdio,
    [JsonPropertyName("sse")]
    Sse,
    [JsonPropertyName("http")]
    Http,
    [JsonPropertyName("sdk")]
    Sdk
}

#endregion

#region Preset Types

/// <summary>
/// System prompt preset configuration.
/// </summary>
public record SystemPromptPreset
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "preset";

    [JsonPropertyName("preset")]
    public string Preset { get; init; } = "claude_code";

    [JsonPropertyName("append")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Append { get; init; }
}

/// <summary>
/// Tools preset configuration.
/// </summary>
public record ToolsPreset
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "preset";

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
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tools { get; init; }

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
    [JsonPropertyName("toolName")]
    public required string ToolName { get; init; }

    [JsonPropertyName("ruleContent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RuleContent { get; init; }
}

/// <summary>
/// Permission update configuration.
/// </summary>
public record PermissionUpdate
{
    [JsonPropertyName("type")]
    public required PermissionUpdateType Type { get; init; }

    [JsonPropertyName("rules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PermissionRuleValue>? Rules { get; init; }

    [JsonPropertyName("behavior")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionBehavior? Behavior { get; init; }

    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionMode? Mode { get; init; }

    [JsonPropertyName("directories")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Directories { get; init; }

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
    [JsonPropertyName("behavior")]
    public abstract string Behavior { get; }
}

/// <summary>
/// Allow permission result.
/// </summary>
public record PermissionResultAllow : PermissionResult
{
    [JsonPropertyName("behavior")]
    public override string Behavior => "allow";

    [JsonPropertyName("updatedInput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? UpdatedInput { get; init; }

    [JsonPropertyName("updatedPermissions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PermissionUpdate>? UpdatedPermissions { get; init; }
}

/// <summary>
/// Deny permission result.
/// </summary>
public record PermissionResultDeny : PermissionResult
{
    [JsonPropertyName("behavior")]
    public override string Behavior => "deny";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

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
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("transcript_path")]
    public required string TranscriptPath { get; init; }

    [JsonPropertyName("cwd")]
    public required string Cwd { get; init; }

    [JsonPropertyName("permission_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PermissionMode { get; init; }

    [JsonPropertyName("hook_event_name")]
    public abstract string HookEventName { get; }
}

/// <summary>
/// Input data for PreToolUse hook events.
/// </summary>
public record PreToolUseHookInput : BaseHookInput
{
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "PreToolUse";

    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    [JsonPropertyName("tool_input")]
    public required Dictionary<string, object?> ToolInput { get; init; }
}

/// <summary>
/// Input data for PostToolUse hook events.
/// </summary>
public record PostToolUseHookInput : BaseHookInput
{
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "PostToolUse";

    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    [JsonPropertyName("tool_input")]
    public required Dictionary<string, object?> ToolInput { get; init; }

    [JsonPropertyName("tool_response")]
    public object? ToolResponse { get; init; }
}

/// <summary>
/// Input data for UserPromptSubmit hook events.
/// </summary>
public record UserPromptSubmitHookInput : BaseHookInput
{
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "UserPromptSubmit";

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }
}

/// <summary>
/// Input data for Stop hook events.
/// </summary>
public record StopHookInput : BaseHookInput
{
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "Stop";

    [JsonPropertyName("stop_hook_active")]
    public required bool StopHookActive { get; init; }
}

/// <summary>
/// Input data for SubagentStop hook events.
/// </summary>
public record SubagentStopHookInput : BaseHookInput
{
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "SubagentStop";

    [JsonPropertyName("stop_hook_active")]
    public required bool StopHookActive { get; init; }
}

/// <summary>
/// Input data for PreCompact hook events.
/// </summary>
public record PreCompactHookInput : BaseHookInput
{
    [JsonPropertyName("hook_event_name")]
    public override string HookEventName => "PreCompact";

    [JsonPropertyName("trigger")]
    public required PreCompactTrigger Trigger { get; init; }

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
    [JsonPropertyName("hookEventName")]
    public abstract string HookEventName { get; }
}

/// <summary>
/// Hook-specific output for PreToolUse events.
/// </summary>
public record PreToolUseHookSpecificOutput : HookSpecificOutput
{
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "PreToolUse";

    [JsonPropertyName("permissionDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PermissionBehavior? PermissionDecision { get; init; }

    [JsonPropertyName("permissionDecisionReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PermissionDecisionReason { get; init; }

    [JsonPropertyName("updatedInput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? UpdatedInput { get; init; }
}

/// <summary>
/// Hook-specific output for PostToolUse events.
/// </summary>
public record PostToolUseHookSpecificOutput : HookSpecificOutput
{
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "PostToolUse";

    [JsonPropertyName("additionalContext")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalContext { get; init; }
}

/// <summary>
/// Hook-specific output for UserPromptSubmit events.
/// </summary>
public record UserPromptSubmitHookSpecificOutput : HookSpecificOutput
{
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "UserPromptSubmit";

    [JsonPropertyName("additionalContext")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdditionalContext { get; init; }
}

/// <summary>
/// Hook-specific output for SessionStart events.
/// </summary>
public record SessionStartHookSpecificOutput : HookSpecificOutput
{
    [JsonPropertyName("hookEventName")]
    public override string HookEventName => "SessionStart";

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
    [JsonPropertyName("type")]
    public abstract McpServerType Type { get; }
}

/// <summary>
/// MCP stdio server configuration.
/// </summary>
public record McpStdioServerConfig : McpServerConfig
{
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Stdio;

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("args")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Args { get; init; }

    [JsonPropertyName("env")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Env { get; init; }
}

/// <summary>
/// MCP SSE server configuration.
/// </summary>
public record McpSseServerConfig : McpServerConfig
{
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Sse;

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// MCP HTTP server configuration.
/// </summary>
public record McpHttpServerConfig : McpServerConfig
{
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Http;

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// SDK MCP server configuration.
/// </summary>
public record McpSdkServerConfig : McpServerConfig
{
    [JsonPropertyName("type")]
    public override McpServerType Type => McpServerType.Sdk;

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The MCP server instance.
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
    [JsonPropertyName("type")]
    public SdkPluginType Type { get; init; } = SdkPluginType.Local;

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
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// Thinking content block.
/// </summary>
public record ThinkingBlock : ContentBlock
{
    [JsonPropertyName("thinking")]
    public required string Thinking { get; init; }

    [JsonPropertyName("signature")]
    public required string Signature { get; init; }
}

/// <summary>
/// Tool use content block.
/// </summary>
public record ToolUseBlock : ContentBlock
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }
}

/// <summary>
/// Tool result content block.
/// </summary>
public record ToolResultBlock : ContentBlock
{
    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; init; }

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
    /// Content can be a string or a list of content blocks.
    /// </summary>
    [JsonPropertyName("content")]
    public required object Content { get; init; }

    [JsonPropertyName("uuid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uuid { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentToolUseId { get; init; }
}

/// <summary>
/// Assistant message with content blocks.
/// </summary>
public record AssistantMessage : Message
{
    [JsonPropertyName("content")]
    public required List<ContentBlock> Content { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("parent_tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentToolUseId { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssistantMessageError? Error { get; init; }
}

/// <summary>
/// System message with metadata.
/// </summary>
public record SystemMessage : Message
{
    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    [JsonPropertyName("data")]
    public required Dictionary<string, object?> Data { get; init; }
}

/// <summary>
/// Result message with cost and usage information.
/// </summary>
public record ResultMessage : Message
{
    [JsonPropertyName("subtype")]
    public required string Subtype { get; init; }

    [JsonPropertyName("duration_ms")]
    public required int DurationMs { get; init; }

    [JsonPropertyName("duration_api_ms")]
    public required int DurationApiMs { get; init; }

    [JsonPropertyName("is_error")]
    public required bool IsError { get; init; }

    [JsonPropertyName("num_turns")]
    public required int NumTurns { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    [JsonPropertyName("total_cost_usd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TotalCostUsd { get; init; }

    [JsonPropertyName("usage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Usage { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; init; }

    [JsonPropertyName("structured_output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? StructuredOutput { get; init; }
}

/// <summary>
/// Stream event for partial message updates during streaming.
/// </summary>
public record StreamEvent : Message
{
    [JsonPropertyName("uuid")]
    public required string Uuid { get; init; }

    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// The raw Anthropic API stream event.
    /// </summary>
    [JsonPropertyName("event")]
    public required Dictionary<string, object?> Event { get; init; }

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
    [JsonPropertyName("subtype")]
    public abstract string Subtype { get; }
}

/// <summary>
/// Interrupt request.
/// </summary>
public record SdkControlInterruptRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "interrupt";
}

/// <summary>
/// Permission request for tool usage.
/// </summary>
public record SdkControlPermissionRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "can_use_tool";

    [JsonPropertyName("tool_name")]
    public required string ToolName { get; init; }

    [JsonPropertyName("input")]
    public required Dictionary<string, object?> Input { get; init; }

    [JsonPropertyName("permission_suggestions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? PermissionSuggestions { get; init; }

    [JsonPropertyName("blocked_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BlockedPath { get; init; }
}

/// <summary>
/// Initialize request.
/// </summary>
public record SdkControlInitializeRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "initialize";

    [JsonPropertyName("hooks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<HookEvent, object>? Hooks { get; init; }
}

/// <summary>
/// Set permission mode request.
/// </summary>
public record SdkControlSetPermissionModeRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "set_permission_mode";

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }
}

/// <summary>
/// Hook callback request.
/// </summary>
public record SdkHookCallbackRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "hook_callback";

    [JsonPropertyName("callback_id")]
    public required string CallbackId { get; init; }

    [JsonPropertyName("input")]
    public object? Input { get; init; }

    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; init; }
}

/// <summary>
/// MCP message request.
/// </summary>
public record SdkControlMcpMessageRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "mcp_message";

    [JsonPropertyName("server_name")]
    public required string ServerName { get; init; }

    [JsonPropertyName("message")]
    public object? Message { get; init; }
}

/// <summary>
/// Rewind files request.
/// </summary>
public record SdkControlRewindFilesRequest : SdkControlRequestBase
{
    [JsonPropertyName("subtype")]
    public override string Subtype => "rewind_files";

    [JsonPropertyName("user_message_id")]
    public required string UserMessageId { get; init; }
}

/// <summary>
/// SDK control request wrapper.
/// </summary>
public record SdkControlRequest
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "control_request";

    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    [JsonPropertyName("request")]
    public required SdkControlRequestBase Request { get; init; }
}

/// <summary>
/// Success control response.
/// </summary>
public record ControlResponse
{
    [JsonPropertyName("subtype")]
    public string Subtype { get; init; } = "success";

    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    [JsonPropertyName("response")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? Response { get; init; }
}

/// <summary>
/// Error control response.
/// </summary>
public record ControlErrorResponse
{
    [JsonPropertyName("subtype")]
    public string Subtype { get; init; } = "error";

    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }
}

/// <summary>
/// SDK control response wrapper.
/// </summary>
public record SdkControlResponse
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "control_response";

    /// <summary>
    /// Response can be either ControlResponse or ControlErrorResponse.
    /// </summary>
    [JsonPropertyName("response")]
    public required object Response { get; init; }
}

#endregion
