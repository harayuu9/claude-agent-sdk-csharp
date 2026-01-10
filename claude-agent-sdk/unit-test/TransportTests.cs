using System.Reflection;
using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Internal.Transport;

namespace UnitTest;

/// <summary>
/// Test subprocess transport implementation.
/// </summary>
public class SubprocessCliTransportTests
{
    private const string DefaultCliPath = "/usr/bin/claude";

    private static ClaudeAgentOptions MakeOptions(
        string? cliPath = DefaultCliPath,
        string? systemPrompt = null,
        object? systemPromptObj = null,
        List<string>? allowedTools = null,
        List<string>? disallowedTools = null,
        string? model = null,
        string? fallbackModel = null,
        PermissionMode? permissionMode = null,
        int? maxTurns = null,
        bool continueConversation = false,
        string? resume = null,
        string? settings = null,
        List<string>? addDirs = null,
        Dictionary<string, string?>? extraArgs = null,
        object? mcpServers = null,
        SandboxSettings? sandbox = null,
        object? tools = null,
        int? maxThinkingTokens = null)
    {
        var options = new ClaudeAgentOptions
        {
            CliPath = cliPath,
            SystemPrompt = systemPromptObj ?? systemPrompt,
            AllowedTools = allowedTools ?? [],
            DisallowedTools = disallowedTools ?? [],
            Model = model,
            FallbackModel = fallbackModel,
            PermissionMode = permissionMode,
            MaxTurns = maxTurns,
            ContinueConversation = continueConversation,
            Resume = resume,
            Settings = settings,
            AddDirs = addDirs ?? [],
            ExtraArgs = extraArgs ?? [],
            McpServers = mcpServers ?? new Dictionary<string, McpServerConfig>(),
            Sandbox = sandbox,
            Tools = tools,
            MaxThinkingTokens = maxThinkingTokens
        };
        return options;
    }

    private static List<string> InvokeBuildCommand(SubprocessCliTransport transport)
    {
        var method = typeof(SubprocessCliTransport)
            .GetMethod("BuildCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        return (List<string>)method!.Invoke(transport, null)!;
    }

    [Fact]
    public void BuildCommandBasic()
    {
        var transport = new SubprocessCliTransport("Hello", MakeOptions());
        var cmd = InvokeBuildCommand(transport);

        Assert.Equal("/usr/bin/claude", cmd[0]);
        Assert.Contains("--output-format", cmd);
        Assert.Contains("stream-json", cmd);
        Assert.Contains("--print", cmd);
        Assert.Contains("Hello", cmd);
        Assert.Contains("--system-prompt", cmd);
        Assert.Equal("", cmd[cmd.IndexOf("--system-prompt") + 1]);
    }

    [Fact]
    public void CliPathAcceptsPath()
    {
        var path = "/usr/bin/claude";
        var transport = new SubprocessCliTransport("Hello", MakeOptions(cliPath: path));
        var cmd = InvokeBuildCommand(transport);

        Assert.Equal(path, cmd[0]);
    }

    [Fact]
    public void BuildCommandWithSystemPromptString()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(systemPrompt: "Be helpful"));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--system-prompt", cmd);
        Assert.Contains("Be helpful", cmd);
    }

    [Fact]
    public void BuildCommandWithSystemPromptPreset()
    {
        var preset = new SystemPromptPreset { Type = "preset", Preset = "claude_code" };
        var transport = new SubprocessCliTransport("test", MakeOptions(systemPromptObj: preset));
        var cmd = InvokeBuildCommand(transport);

        Assert.DoesNotContain("--system-prompt", cmd);
        Assert.DoesNotContain("--append-system-prompt", cmd);
    }

    [Fact]
    public void BuildCommandWithSystemPromptPresetAndAppend()
    {
        var preset = new SystemPromptPreset { Type = "preset", Preset = "claude_code", Append = "Be concise." };
        var transport = new SubprocessCliTransport("test", MakeOptions(systemPromptObj: preset));
        var cmd = InvokeBuildCommand(transport);

        Assert.DoesNotContain("--system-prompt", cmd);
        Assert.Contains("--append-system-prompt", cmd);
        Assert.Contains("Be concise.", cmd);
    }

    [Fact]
    public void BuildCommandWithOptions()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(
            allowedTools: ["Read", "Write"],
            disallowedTools: ["Bash"],
            model: "claude-sonnet-4-5",
            permissionMode: PermissionMode.AcceptEdits,
            maxTurns: 5
        ));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--allowedTools", cmd);
        Assert.Contains("Read,Write", cmd);
        Assert.Contains("--disallowedTools", cmd);
        Assert.Contains("Bash", cmd);
        Assert.Contains("--model", cmd);
        Assert.Contains("claude-sonnet-4-5", cmd);
        Assert.Contains("--permission-mode", cmd);
        Assert.Contains("acceptEdits", cmd);
        Assert.Contains("--max-turns", cmd);
        Assert.Contains("5", cmd);
    }

    [Fact]
    public void BuildCommandWithFallbackModel()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(
            model: "opus",
            fallbackModel: "sonnet"
        ));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--model", cmd);
        Assert.Contains("opus", cmd);
        Assert.Contains("--fallback-model", cmd);
        Assert.Contains("sonnet", cmd);
    }

    [Fact]
    public void BuildCommandWithMaxThinkingTokens()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(maxThinkingTokens: 5000));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--max-thinking-tokens", cmd);
        Assert.Contains("5000", cmd);
    }

    [Fact]
    public void BuildCommandWithAddDirs()
    {
        var dir1 = "/path/to/dir1";
        var dir2 = "/path/to/dir2";
        var transport = new SubprocessCliTransport("test", MakeOptions(addDirs: [dir1, dir2]));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--add-dir", cmd);
        var addDirIndices = cmd.Select((x, i) => (x, i)).Where(t => t.x == "--add-dir").Select(t => t.i).ToList();
        Assert.Equal(2, addDirIndices.Count);

        var dirsInCmd = addDirIndices.Select(i => cmd[i + 1]).ToList();
        Assert.Contains(dir1, dirsInCmd);
        Assert.Contains(dir2, dirsInCmd);
    }

    [Fact]
    public void SessionContinuation()
    {
        var transport = new SubprocessCliTransport("Continue from before", MakeOptions(
            continueConversation: true,
            resume: "session-123"
        ));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--continue", cmd);
        Assert.Contains("--resume", cmd);
        Assert.Contains("session-123", cmd);
    }

    [Fact]
    public void BuildCommandWithSettingsFile()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(settings: "/path/to/settings.json"));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--settings", cmd);
        Assert.Contains("/path/to/settings.json", cmd);
    }

    [Fact]
    public void BuildCommandWithSettingsJson()
    {
        var settingsJson = "{\"permissions\": {\"allow\": [\"Bash(ls:*)\"]}}";
        var transport = new SubprocessCliTransport("test", MakeOptions(settings: settingsJson));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--settings", cmd);
        Assert.Contains(settingsJson, cmd);
    }

    [Fact]
    public void BuildCommandWithExtraArgs()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(
            extraArgs: new Dictionary<string, string?>
            {
                ["new-flag"] = "value",
                ["boolean-flag"] = null,
                ["another-option"] = "test-value"
            }
        ));
        var cmd = InvokeBuildCommand(transport);
        var cmdStr = string.Join(" ", cmd);

        Assert.Contains("--new-flag value", cmdStr);
        Assert.Contains("--another-option test-value", cmdStr);
        Assert.Contains("--boolean-flag", cmd);

        var booleanIdx = cmd.IndexOf("--boolean-flag");
        Assert.True(booleanIdx == cmd.Count - 1 || cmd[booleanIdx + 1].StartsWith("--"));
    }

    [Fact]
    public void BuildCommandWithMcpServers()
    {
        var mcpServers = new Dictionary<string, McpServerConfig>
        {
            ["test-server"] = new McpStdioServerConfig
            {
                Command = "/path/to/server",
                Args = ["--option", "value"]
            }
        };

        var transport = new SubprocessCliTransport("test", MakeOptions(mcpServers: mcpServers));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--mcp-config", cmd);
        var mcpIdx = cmd.IndexOf("--mcp-config");
        var mcpConfigValue = cmd[mcpIdx + 1];

        var config = JsonSerializer.Deserialize<Dictionary<string, object?>>(mcpConfigValue);
        Assert.NotNull(config);
        Assert.True(config.ContainsKey("mcpServers"));
    }

    [Fact]
    public void BuildCommandWithMcpServersAsFilePath()
    {
        var stringPath = "/path/to/mcp-config.json";
        var transport = new SubprocessCliTransport("test", MakeOptions(mcpServers: stringPath));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--mcp-config", cmd);
        var mcpIdx = cmd.IndexOf("--mcp-config");
        Assert.Equal(stringPath, cmd[mcpIdx + 1]);
    }

    [Fact]
    public void BuildCommandWithMcpServersAsJsonString()
    {
        var jsonConfig = "{\"mcpServers\": {\"server\": {\"type\": \"stdio\", \"command\": \"test\"}}}";
        var transport = new SubprocessCliTransport("test", MakeOptions(mcpServers: jsonConfig));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--mcp-config", cmd);
        var mcpIdx = cmd.IndexOf("--mcp-config");
        Assert.Equal(jsonConfig, cmd[mcpIdx + 1]);
    }

    [Fact]
    public void BuildCommandWithSandboxOnly()
    {
        var sandbox = new SandboxSettings
        {
            Enabled = true,
            AutoAllowBashIfSandboxed = true,
            Network = new SandboxNetworkConfig
            {
                AllowLocalBinding = true,
                AllowUnixSockets = ["/var/run/docker.sock"]
            }
        };

        var transport = new SubprocessCliTransport("test", MakeOptions(sandbox: sandbox));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--settings", cmd);
        var settingsIdx = cmd.IndexOf("--settings");
        var settingsValue = cmd[settingsIdx + 1];

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsValue);
        Assert.NotNull(parsed);
        Assert.True(parsed.ContainsKey("sandbox"));
        var sandboxObj = parsed["sandbox"];
        Assert.True(sandboxObj.GetProperty("enabled").GetBoolean());
        Assert.True(sandboxObj.GetProperty("autoAllowBashIfSandboxed").GetBoolean());
        Assert.True(sandboxObj.GetProperty("network").GetProperty("allowLocalBinding").GetBoolean());
    }

    [Fact]
    public void BuildCommandWithSandboxAndSettingsJson()
    {
        var existingSettings = "{\"permissions\": {\"allow\": [\"Bash(ls:*)\"]}, \"verbose\": true}";
        var sandbox = new SandboxSettings
        {
            Enabled = true,
            ExcludedCommands = ["git", "docker"]
        };

        var transport = new SubprocessCliTransport("test", MakeOptions(settings: existingSettings, sandbox: sandbox));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--settings", cmd);
        var settingsIdx = cmd.IndexOf("--settings");
        var settingsValue = cmd[settingsIdx + 1];

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsValue);
        Assert.NotNull(parsed);
        Assert.True(parsed.ContainsKey("sandbox"));
        Assert.True(parsed["sandbox"].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void BuildCommandWithSettingsFileAndNoSandbox()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(settings: "/path/to/settings.json"));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--settings", cmd);
        var settingsIdx = cmd.IndexOf("--settings");
        Assert.Equal("/path/to/settings.json", cmd[settingsIdx + 1]);
    }

    [Fact]
    public void BuildCommandSandboxMinimal()
    {
        var sandbox = new SandboxSettings { Enabled = true };

        var transport = new SubprocessCliTransport("test", MakeOptions(sandbox: sandbox));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--settings", cmd);
        var settingsIdx = cmd.IndexOf("--settings");
        var settingsValue = cmd[settingsIdx + 1];

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsValue);
        Assert.NotNull(parsed);
        Assert.True(parsed["sandbox"].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void SandboxNetworkConfig()
    {
        var sandbox = new SandboxSettings
        {
            Enabled = true,
            Network = new SandboxNetworkConfig
            {
                AllowUnixSockets = ["/tmp/ssh-agent.sock"],
                AllowAllUnixSockets = false,
                AllowLocalBinding = true,
                HttpProxyPort = 8080,
                SocksProxyPort = 8081
            }
        };

        var transport = new SubprocessCliTransport("test", MakeOptions(sandbox: sandbox));
        var cmd = InvokeBuildCommand(transport);
        var settingsIdx = cmd.IndexOf("--settings");
        var settingsValue = cmd[settingsIdx + 1];

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsValue);
        Assert.NotNull(parsed);
        var network = parsed["sandbox"].GetProperty("network");

        Assert.Equal("/tmp/ssh-agent.sock", network.GetProperty("allowUnixSockets")[0].GetString());
        Assert.False(network.GetProperty("allowAllUnixSockets").GetBoolean());
        Assert.True(network.GetProperty("allowLocalBinding").GetBoolean());
        Assert.Equal(8080, network.GetProperty("httpProxyPort").GetInt32());
        Assert.Equal(8081, network.GetProperty("socksProxyPort").GetInt32());
    }

    [Fact]
    public void BuildCommandWithToolsArray()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(tools: new List<string> { "Read", "Edit", "Bash" }));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--tools", cmd);
        var toolsIdx = cmd.IndexOf("--tools");
        Assert.Equal("Read,Edit,Bash", cmd[toolsIdx + 1]);
    }

    [Fact]
    public void BuildCommandWithToolsEmptyArray()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions(tools: new List<string>()));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--tools", cmd);
        var toolsIdx = cmd.IndexOf("--tools");
        Assert.Equal("", cmd[toolsIdx + 1]);
    }

    [Fact]
    public void BuildCommandWithToolsPreset()
    {
        var preset = new ToolsPreset { Type = "preset", Preset = "claude_code" };
        var transport = new SubprocessCliTransport("test", MakeOptions(tools: preset));
        var cmd = InvokeBuildCommand(transport);

        Assert.Contains("--tools", cmd);
        var toolsIdx = cmd.IndexOf("--tools");
        Assert.Equal("default", cmd[toolsIdx + 1]);
    }

    [Fact]
    public void BuildCommandWithoutTools()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions());
        var cmd = InvokeBuildCommand(transport);

        Assert.DoesNotContain("--tools", cmd);
    }

    [Fact]
    public void ReadMessagesBasic()
    {
        var transport = new SubprocessCliTransport("test", MakeOptions());

        // Verify the transport can be created and has correct basic structure
        var promptField = typeof(SubprocessCliTransport)
            .GetField("_prompt", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal("test", promptField!.GetValue(transport));

        var cliPathField = typeof(SubprocessCliTransport)
            .GetField("_cliPath", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal("/usr/bin/claude", cliPathField!.GetValue(transport));
    }
}
