using System.Text.Json;
using ClaudeAgentSdk;

namespace UnitTest.E2E;

/// <summary>
/// End-to-end tests for agents and setting sources with real Claude API calls.
/// Equivalent to Python e2e-tests/test_agents_and_settings.py
/// </summary>
[Trait("Category", "E2E")]
public class AgentsAndSettingsE2ETests : E2ETestBase
{
    /// <summary>
    /// Test that custom agent definitions work.
    /// Equivalent to Python test_agent_definition.
    /// </summary>
    [Fact]
    public async Task AgentDefinition()
    {
        SkipIfCannotRunE2E();

        var options = new ClaudeAgentOptions
        {
            Agents = new Dictionary<string, ClaudeAgentSdk.AgentDefinition>
            {
                ["test-agent"] = new ClaudeAgentSdk.AgentDefinition
                {
                    Description = "A test agent for verification",
                    Prompt = "You are a test agent. Always respond with 'Test agent activated'",
                    Tools = ["Read"],
                    Model = AgentModel.Sonnet
                }
            },
            MaxTurns = 1
        };

        var ct = TestContext.Current.CancellationToken;
        await using var client = new ClaudeSDKClient(options);
        await client.ConnectAsync(ct: ct);
        await client.QueryAsync("What is 2 + 2?", ct: ct);

        // Check that agent is available in init message
        await foreach (var message in client.ReceiveResponseAsync(ct))
        {
            if (message is SystemMessage systemMessage && systemMessage.Subtype == "init")
            {
                var agents = GetListFromData(systemMessage.Data, "agents");
                Assert.NotNull(agents);
                Assert.Contains("test-agent", agents);
                break;
            }
        }
    }

    /// <summary>
    /// Test that filesystem-based agents load via setting_sources and produce full response.
    /// Equivalent to Python test_filesystem_agent_loading.
    /// </summary>
    [Fact]
    public async Task FilesystemAgentLoading()
    {
        SkipIfCannotRunE2E();

        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Create a temporary project with a filesystem agent
            var agentsDir = Path.Combine(tmpDir, ".claude", "agents");
            Directory.CreateDirectory(agentsDir);

            // Create a test agent file
            var agentFile = Path.Combine(agentsDir, "fs-test-agent.md");
            File.WriteAllText(agentFile, """
                ---
                name: fs-test-agent
                description: A filesystem test agent for SDK testing
                tools: Read
                ---

                # Filesystem Test Agent

                You are a simple test agent. When asked a question, provide a brief, helpful answer.
                """);

            var options = new ClaudeAgentOptions
            {
                SettingSources = [SettingSource.Project],
                Cwd = tmpDir,
                MaxTurns = 1
            };
            var ct = TestContext.Current.CancellationToken;

            var messages = new List<Message>();
            await using var client = new ClaudeSDKClient(options);
            await client.ConnectAsync(ct: ct);
            await client.QueryAsync("Say hello in exactly 3 words", ct: ct);

            await foreach (var msg in client.ReceiveResponseAsync(ct))
            {
                messages.Add(msg);
            }

            // Must have at least init, assistant, result
            var messageTypes = messages.Select(m => m.GetType().Name).ToList();

            Assert.Contains("SystemMessage", messageTypes);
            Assert.Contains("AssistantMessage", messageTypes);
            Assert.Contains("ResultMessage", messageTypes);

            // Find the init message and check for the filesystem agent
            foreach (var msg in messages)
            {
                if (msg is SystemMessage systemMessage && systemMessage.Subtype == "init")
                {
                    var agents = GetListFromData(systemMessage.Data, "agents");
                    Assert.Contains("fs-test-agent", agents);
                    break;
                }
            }

            // On Windows, wait for file handles to be released before cleanup
            if (OperatingSystem.IsWindows())
            {
                await Task.Delay(500, ct);
            }
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tmpDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Test that default (no setting_sources) loads no settings.
    /// Equivalent to Python test_setting_sources_default.
    /// </summary>
    [Fact]
    public async Task SettingSourcesDefault()
    {
        SkipIfCannotRunE2E();

        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Create a temporary project with local settings
            var claudeDir = Path.Combine(tmpDir, ".claude");
            Directory.CreateDirectory(claudeDir);

            // Create local settings with custom outputStyle
            var settingsFile = Path.Combine(claudeDir, "settings.local.json");
            File.WriteAllText(settingsFile, """{"outputStyle": "local-test-style"}""");

            // Don't provide setting_sources - should default to no settings
            var options = new ClaudeAgentOptions
            {
                Cwd = tmpDir,
                MaxTurns = 1
            };
            var ct = TestContext.Current.CancellationToken;

            await using var client = new ClaudeSDKClient(options);
            await client.ConnectAsync(ct: ct);
            await client.QueryAsync("What is 2 + 2?", ct: ct);

            // Check that settings were NOT loaded
            await foreach (var message in client.ReceiveResponseAsync(ct))
            {
                if (message is SystemMessage systemMessage && systemMessage.Subtype == "init")
                {
                    var outputStyle = GetStringFromData(systemMessage.Data, "output_style");
                    Assert.NotEqual("local-test-style", outputStyle);
                    Assert.Equal("default", outputStyle);
                    break;
                }
            }

            // On Windows, wait for file handles to be released before cleanup
            if (OperatingSystem.IsWindows())
            {
                await Task.Delay(500, ct);
            }
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tmpDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Test that setting_sources=['user'] excludes project settings.
    /// Equivalent to Python test_setting_sources_user_only.
    /// </summary>
    [Fact]
    public async Task SettingSourcesUserOnly()
    {
        SkipIfCannotRunE2E();

        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Create a temporary project with a slash command
            var commandsDir = Path.Combine(tmpDir, ".claude", "commands");
            Directory.CreateDirectory(commandsDir);

            var testCommand = Path.Combine(commandsDir, "testcmd.md");
            File.WriteAllText(testCommand, """
                ---
                description: Test command
                ---

                This is a test command.
                """);

            // Use setting_sources=["user"] to exclude project settings
            var options = new ClaudeAgentOptions
            {
                SettingSources = [SettingSource.User],
                Cwd = tmpDir,
                MaxTurns = 1
            };
            var ct = TestContext.Current.CancellationToken;

            await using var client = new ClaudeSDKClient(options);
            await client.ConnectAsync(ct: ct);
            await client.QueryAsync("What is 2 + 2?", ct: ct);

            // Check that project command is NOT available
            await foreach (var message in client.ReceiveResponseAsync(ct))
            {
                if (message is SystemMessage systemMessage && systemMessage.Subtype == "init")
                {
                    var commands = GetListFromData(systemMessage.Data, "slash_commands");
                    Assert.DoesNotContain("testcmd", commands);
                    break;
                }
            }

            // On Windows, wait for file handles to be released before cleanup
            if (OperatingSystem.IsWindows())
            {
                await Task.Delay(500, ct);
            }
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tmpDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Test that setting_sources=['user', 'project', 'local'] includes local settings.
    /// Equivalent to Python test_setting_sources_project_included.
    /// </summary>
    [Fact]
    public async Task SettingSourcesProjectIncluded()
    {
        SkipIfCannotRunE2E();

        var tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);

        try
        {
            // Create a temporary project with local settings
            var claudeDir = Path.Combine(tmpDir, ".claude");
            Directory.CreateDirectory(claudeDir);

            // Create local settings with custom outputStyle
            var settingsFile = Path.Combine(claudeDir, "settings.local.json");
            File.WriteAllText(settingsFile, """{"outputStyle": "local-test-style"}""");

            // Use setting_sources=["user", "project", "local"] to include local settings
            var options = new ClaudeAgentOptions
            {
                SettingSources = [SettingSource.User, SettingSource.Project, SettingSource.Local],
                Cwd = tmpDir,
                MaxTurns = 1
            };
            var ct = TestContext.Current.CancellationToken;

            await using var client = new ClaudeSDKClient(options);
            await client.ConnectAsync(ct: ct);
            await client.QueryAsync("What is 2 + 2?", ct: ct);

            // Check that settings WERE loaded
            await foreach (var message in client.ReceiveResponseAsync(ct))
            {
                if (message is SystemMessage systemMessage && systemMessage.Subtype == "init")
                {
                    var outputStyle = GetStringFromData(systemMessage.Data, "output_style");
                    Assert.Equal("local-test-style", outputStyle);
                    break;
                }
            }

            // On Windows, wait for file handles to be released before cleanup
            if (OperatingSystem.IsWindows())
            {
                await Task.Delay(500, ct);
            }
        }
        finally
        {
            // Cleanup
            try
            {
                Directory.Delete(tmpDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private static List<string> GetListFromData(Dictionary<string, object?>? data, string key)
    {
        if (data == null || !data.TryGetValue(key, out var value))
        {
            return [];
        }

        if (value is List<object?> objList)
        {
            return objList.Select(o => o?.ToString() ?? "").ToList();
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return jsonElement.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .ToList();
        }

        return [];
    }

    private static string? GetStringFromData(Dictionary<string, object?>? data, string key)
    {
        if (data == null || !data.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value is string str)
        {
            return str;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
        {
            return jsonElement.GetString();
        }

        return value?.ToString();
    }

    #endregion
}
