using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSdk.Internal.Transport;

/// <summary>
/// Subprocess transport using Claude Code CLI.
/// </summary>
internal class SubprocessCliTransport : Transport
{
    private const int DefaultMaxBufferSize = 1024 * 1024; // 1MB buffer limit
    private const string MinimumClaudeCodeVersion = "2.0.0";
    private const string SdkVersion = "0.1.0"; // TODO: Get from assembly version

    // Platform-specific command line length limits
    // Windows cmd.exe has a limit of 8191 characters, use 8000 for safety
    private static readonly int CmdLengthLimit = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 8000 : 100000;

    private readonly object _prompt;
    private readonly bool _isStreaming;
    private readonly ClaudeAgentOptions _options;
    private readonly string _cliPath;
    private readonly string? _cwd;
    private readonly int _maxBufferSize;
    private readonly List<string> _tempFiles = [];
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger? _logger;

    private Process? _process;
    private StreamReader? _stdoutReader;
    private StreamWriter? _stdinWriter;
    private StreamReader? _stderrReader;
    private Task? _stderrTask;
    private CancellationTokenSource? _stderrCts;
    private bool _ready;
    private Exception? _exitError;

    /// <summary>
    /// Creates a new subprocess CLI transport.
    /// </summary>
    /// <param name="prompt">The prompt string or IAsyncEnumerable for streaming.</param>
    /// <param name="options">Claude agent options.</param>
    /// <param name="logger">Optional logger for structured logging.</param>
    public SubprocessCliTransport(object prompt, ClaudeAgentOptions options, ILogger? logger = null)
    {
        _prompt = prompt;
        _isStreaming = prompt is not string;
        _options = options;
        _logger = logger;
        _cliPath = options.CliPath ?? FindCli();
        _cwd = options.Cwd;
        _maxBufferSize = options.MaxBufferSize ?? DefaultMaxBufferSize;
    }

    /// <inheritdoc />
    public override bool IsReady => _ready;

    /// <inheritdoc />
    public override async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_process != null)
            return;

        var skipVersionCheck = Environment.GetEnvironmentVariable("CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK");
        if (string.IsNullOrEmpty(skipVersionCheck))
        {
            await CheckClaudeVersionAsync(cancellationToken).ConfigureAwait(false);
        }

        var command = BuildCommand();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command[0],
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = ShouldPipeStderr(),
                CreateNoWindow = true,
                WorkingDirectory = _cwd ?? Environment.CurrentDirectory,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };

            // Add command arguments
            for (var i = 1; i < command.Count; i++)
            {
                startInfo.ArgumentList.Add(command[i]);
            }

            // Merge environment variables: system -> user -> SDK required
            foreach (var kvp in _options.Env)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-csharp";
            startInfo.Environment["CLAUDE_AGENT_SDK_VERSION"] = SdkVersion;

            if (_options.EnableFileCheckpointing)
            {
                startInfo.Environment["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"] = "true";
            }

            if (!string.IsNullOrEmpty(_cwd))
            {
                startInfo.Environment["PWD"] = _cwd;
            }

            _process = Process.Start(startInfo);
            if (_process == null)
            {
                throw new CLIConnectionException("Failed to start Claude Code process");
            }

            _stdoutReader = _process.StandardOutput;

            // Setup stderr stream if piped
            if (ShouldPipeStderr())
            {
                _stderrReader = _process.StandardError;
                _stderrCts = new CancellationTokenSource();
                _stderrTask = HandleStderrAsync(_stderrCts.Token);
            }

            // Setup stdin for streaming mode
            if (_isStreaming)
            {
                _stdinWriter = _process.StandardInput;
            }
            else
            {
                // String mode: close stdin immediately
                _process.StandardInput.Close();
            }

            _ready = true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2) // File not found
        {
            // Check if the error comes from the working directory or the CLI
            if (!string.IsNullOrEmpty(_cwd) && !Directory.Exists(_cwd))
            {
                var error = new CLIConnectionException($"Working directory does not exist: {_cwd}", ex);
                _exitError = error;
                throw error;
            }

            var cliError = new CLINotFoundException("Claude Code not found", _cliPath, ex);
            _exitError = cliError;
            throw cliError;
        }
        catch (Exception ex)
        {
            var error = new CLIConnectionException($"Failed to start Claude Code: {ex.Message}", ex);
            _exitError = error;
            throw error;
        }
    }

    /// <inheritdoc />
    public override async Task CloseAsync()
    {
        // Clean up temporary files first (before early return)
        foreach (var tempFile in _tempFiles)
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
        _tempFiles.Clear();

        if (_process == null)
        {
            _ready = false;
            return;
        }

        // Cancel stderr task if active
        if (_stderrCts != null)
        {
            try
            {
                await _stderrCts.CancelAsync();
                if (_stderrTask != null)
                {
                    await _stderrTask.ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
            finally
            {
                _stderrCts.Dispose();
                _stderrCts = null;
            }
        }

        // Close stdin stream (acquire lock to prevent race with concurrent writes)
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _ready = false; // Set inside lock to prevent TOCTOU with WriteAsync()
            if (_stdinWriter != null)
            {
                try
                {
                    _stdinWriter.Close();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                _stdinWriter = null;
            }
        }
        finally
        {
            _writeLock.Release();
        }

        if (_stderrReader != null)
        {
            try
            {
                _stderrReader.Close();
            }
            catch
            {
                // Ignore errors during cleanup
            }
            _stderrReader = null;
        }

        // Terminate and wait for process
        if (!_process.HasExited)
        {
            try
            {
                _process.Kill();
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        _process.Dispose();
        _process = null;
        _stdoutReader = null;
        _stdinWriter = null;
        _stderrReader = null;
        _exitError = null;
    }

    /// <inheritdoc />
    public override async Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // All checks inside lock to prevent TOCTOU races with CloseAsync()/EndInputAsync()
            if (!_ready || _stdinWriter == null)
            {
                throw new CLIConnectionException("ProcessTransport is not ready for writing");
            }

            if (_process != null && _process.HasExited)
            {
                throw new CLIConnectionException(
                    $"Cannot write to terminated process (exit code: {_process.ExitCode})");
            }

            if (_exitError != null)
            {
                throw new CLIConnectionException(
                    $"Cannot write to process that exited with error: {_exitError.Message}", _exitError);
            }

            try
            {
                await _stdinWriter.WriteAsync(data).ConfigureAwait(false);
                await _stdinWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _ready = false;
                _exitError = new CLIConnectionException($"Failed to write to process stdin: {ex.Message}", ex);
                throw _exitError;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public override async Task EndInputAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_stdinWriter != null)
            {
                try
                {
                    _stdinWriter.Close();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                _stdinWriter = null;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Dictionary<string, object?>> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_process == null || _stdoutReader == null)
        {
            throw new CLIConnectionException("Not connected");
        }

        var jsonBuffer = new StringBuilder();

        // Process stdout messages
        while (await _stdoutReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lineStr = line.Trim();
            if (string.IsNullOrEmpty(lineStr))
                continue;

            // Accumulate partial JSON until we can parse it
            var jsonLines = lineStr.Split('\n');

            foreach (var jsonLine in jsonLines)
            {
                var trimmedLine = jsonLine.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;

                // Keep accumulating partial JSON until we can parse it
                jsonBuffer.Append(trimmedLine);

                if (jsonBuffer.Length > _maxBufferSize)
                {
                    var bufferLength = jsonBuffer.Length;
                    jsonBuffer.Clear();
                    throw new CLIJSONDecodeException(
                        $"JSON message exceeded maximum buffer size of {_maxBufferSize} bytes",
                        new ArgumentException($"Buffer size {bufferLength} exceeds limit {_maxBufferSize}"));
                }

                Dictionary<string, object?>? data = null;
                var parseSucceeded = false;
                try
                {
                    data = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonBuffer.ToString());
                    jsonBuffer.Clear();
                    parseSucceeded = true;
                }
                catch (JsonException)
                {
                    // We are speculatively decoding the buffer until we get
                    // a full JSON object. If there is an actual issue, we
                    // raise an error after exceeding the configured limit.
                }

                if (parseSucceeded && data != null)
                {
                    yield return data;
                }
            }
        }

        // Check process completion and handle errors
        int returnCode;
        try
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            returnCode = _process.ExitCode;
        }
        catch
        {
            returnCode = -1;
        }

        // Use exit code for error detection
        if (returnCode != 0)
        {
            _exitError = new ProcessException(
                $"Command failed with exit code {returnCode}",
                returnCode,
                "Check stderr output for details");
            throw _exitError;
        }
    }

    #region Private Methods

    private bool ShouldPipeStderr()
    {
        return _options.Stderr != null || _options.ExtraArgs.ContainsKey("debug-to-stderr");
    }

    private async Task HandleStderrAsync(CancellationToken cancellationToken)
    {
        if (_stderrReader == null)
            return;

        try
        {
            while (await _stderrReader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var lineStr = line.TrimEnd();
                if (string.IsNullOrEmpty(lineStr))
                    continue;

                // Call the stderr callback if provided
                _options.Stderr?.Invoke(lineStr);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch
        {
            // Ignore other errors during stderr reading
        }
    }

    private string FindCli()
    {
        // First, check for bundled CLI
        var bundledCli = FindBundledCli();
        if (bundledCli != null)
            return bundledCli;

        // Fall back to system-wide search
        var systemCli = Which("claude");
        if (systemCli != null)
            return systemCli;

        // Check common locations
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var locations = new[]
        {
            Path.Combine(homeDir, ".npm-global", "bin", "claude"),
            "/usr/local/bin/claude",
            Path.Combine(homeDir, ".local", "bin", "claude"),
            Path.Combine(homeDir, "node_modules", ".bin", "claude"),
            Path.Combine(homeDir, ".yarn", "bin", "claude"),
            Path.Combine(homeDir, ".claude", "local", "claude"),
        };

        // On Windows, also check for .exe and .cmd extensions
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var path in locations)
            {
                var exePath = path + ".exe";
                if (File.Exists(exePath))
                    return exePath;
                var cmdPath = path + ".cmd";
                if (File.Exists(cmdPath))
                    return cmdPath;
            }
        }
        else
        {
            foreach (var path in locations)
            {
                if (File.Exists(path))
                    return path;
            }
        }

        throw new CLINotFoundException(
            "Claude Code not found. Install with:\n" +
            "  npm install -g @anthropic-ai/claude-code\n" +
            "\nIf already installed locally, try:\n" +
            "  export PATH=\"$HOME/node_modules/.bin:$PATH\"\n" +
            "\nOr provide the path via ClaudeAgentOptions:\n" +
            "  new ClaudeAgentOptions { CliPath = \"/path/to/claude\" }");
    }

    private string? FindBundledCli()
    {
        // Determine the CLI binary name based on platform
        var cliName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "claude.exe" : "claude";

        // Get the path to the bundled CLI
        // The _bundled directory is in the same package as this module
        var assemblyLocation = typeof(SubprocessCliTransport).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);
        if (assemblyDir == null)
            return null;

        var bundledPath = Path.Combine(assemblyDir, "_bundled", cliName);

        if (File.Exists(bundledPath))
        {
            _logger?.LogDebug($"Using bundled Claude Code CLI: {bundledPath}");
            return bundledPath;
        }

        return null;
    }

    private static string? Which(string command)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        var pathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var paths = pathEnv.Split(pathSeparator);

        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };

        foreach (var path in paths)
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(path, command + ext);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        return null;
    }

    private string? BuildSettingsValue()
    {
        var hasSettings = _options.Settings != null;
        var hasSandbox = _options.Sandbox != null;

        if (!hasSettings && !hasSandbox)
            return null;

        // If only settings path and no sandbox, pass through as-is
        if (hasSettings && !hasSandbox)
            return _options.Settings;

        // If we have sandbox settings, we need to merge into a JSON object
        var settingsObj = new Dictionary<string, object?>();

        if (hasSettings)
        {
            var settingsStr = _options.Settings!.Trim();
            // Check if settings is a JSON string or a file path
            if (settingsStr.StartsWith("{") && settingsStr.EndsWith("}"))
            {
                // Parse JSON string
                try
                {
                    settingsObj = JsonSerializer.Deserialize<Dictionary<string, object?>>(settingsStr) ?? new();
                }
                catch (JsonException)
                {
                    // If parsing fails, treat as file path
                    _logger?.LogDebug($"Failed to parse settings as JSON, treating as file path: {settingsStr}");
                    if (File.Exists(settingsStr))
                    {
                        var content = File.ReadAllText(settingsStr);
                        settingsObj = JsonSerializer.Deserialize<Dictionary<string, object?>>(content) ?? new();
                    }
                }
            }
            else
            {
                // It's a file path - read and parse
                if (File.Exists(settingsStr))
                {
                    var content = File.ReadAllText(settingsStr);
                    settingsObj = JsonSerializer.Deserialize<Dictionary<string, object?>>(content) ?? new();
                }
                else
                {
                    _logger?.LogDebug($"Settings file not found: {settingsStr}");
                }
            }
        }

        // Merge sandbox settings
        if (hasSandbox)
        {
            settingsObj["sandbox"] = _options.Sandbox;
        }

        return JsonSerializer.Serialize(settingsObj);
    }

    private List<string> BuildCommand()
    {
        var cmd = new List<string> { _cliPath, "--output-format", "stream-json", "--verbose" };

        // Handle system prompt
        if (_options.SystemPrompt == null)
        {
            cmd.AddRange(["--system-prompt", ""]);
        }
        else if (_options.SystemPrompt is string systemPromptStr)
        {
            cmd.AddRange(["--system-prompt", systemPromptStr]);
        }
        else if (_options.SystemPrompt is SystemPromptPreset preset)
        {
            if (preset.Type == "preset" && !string.IsNullOrEmpty(preset.Append))
            {
                cmd.AddRange(["--append-system-prompt", preset.Append]);
            }
        }

        // Handle tools option (base set of tools)
        if (_options.Tools != null)
        {
            if (_options.Tools is IList<string> toolsList)
            {
                if (toolsList.Count == 0)
                {
                    cmd.AddRange(["--tools", ""]);
                }
                else
                {
                    cmd.AddRange(["--tools", string.Join(",", toolsList)]);
                }
            }
            else
            {
                // Preset object - 'claude_code' preset maps to 'default'
                cmd.AddRange(["--tools", "default"]);
            }
        }

        if (_options.AllowedTools.Count > 0)
        {
            cmd.AddRange(["--allowedTools", string.Join(",", _options.AllowedTools)]);
        }

        if (_options.MaxTurns.HasValue)
        {
            cmd.AddRange(["--max-turns", _options.MaxTurns.Value.ToString()]);
        }

        if (_options.MaxBudgetUsd.HasValue)
        {
            cmd.AddRange(["--max-budget-usd", _options.MaxBudgetUsd.Value.ToString()]);
        }

        if (_options.DisallowedTools.Count > 0)
        {
            cmd.AddRange(["--disallowedTools", string.Join(",", _options.DisallowedTools)]);
        }

        if (!string.IsNullOrEmpty(_options.Model))
        {
            cmd.AddRange(["--model", _options.Model]);
        }

        if (!string.IsNullOrEmpty(_options.FallbackModel))
        {
            cmd.AddRange(["--fallback-model", _options.FallbackModel]);
        }

        if (_options.Betas.Count > 0)
        {
            var betaValues = _options.Betas.Select(GetBetaValue);
            cmd.AddRange(["--betas", string.Join(",", betaValues)]);
        }

        if (!string.IsNullOrEmpty(_options.PermissionPromptToolName))
        {
            cmd.AddRange(["--permission-prompt-tool", _options.PermissionPromptToolName]);
        }

        if (_options.PermissionMode.HasValue)
        {
            cmd.AddRange(["--permission-mode", GetPermissionModeValue(_options.PermissionMode.Value)]);
        }

        if (_options.ContinueConversation)
        {
            cmd.Add("--continue");
        }

        if (!string.IsNullOrEmpty(_options.Resume))
        {
            cmd.AddRange(["--resume", _options.Resume]);
        }

        // Handle settings and sandbox: merge sandbox into settings if both are provided
        var settingsValue = BuildSettingsValue();
        if (!string.IsNullOrEmpty(settingsValue))
        {
            cmd.AddRange(["--settings", settingsValue]);
        }

        if (_options.AddDirs.Count > 0)
        {
            foreach (var directory in _options.AddDirs)
            {
                cmd.AddRange(["--add-dir", directory]);
            }
        }

        // Handle MCP servers
        if (_options.McpServers is Dictionary<string, McpServerConfig> mcpServersDict && mcpServersDict.Count > 0)
        {
            var serversForCli = new Dictionary<string, object>();
            foreach (var (name, config) in mcpServersDict)
            {
                if (config is McpSdkServerConfig sdkConfig)
                {
                    // For SDK servers, pass everything except the instance field
                    serversForCli[name] = new { type = "sdk", name = sdkConfig.Name };
                }
                else
                {
                    // For external servers, pass as-is
                    serversForCli[name] = config;
                }
            }

            if (serversForCli.Count > 0)
            {
                var mcpConfigJson = JsonSerializer.Serialize(new { mcpServers = serversForCli });
                cmd.AddRange(["--mcp-config", mcpConfigJson]);
            }
        }
        else if (_options.McpServers is string mcpServersPath)
        {
            cmd.AddRange(["--mcp-config", mcpServersPath]);
        }

        if (_options.IncludePartialMessages)
        {
            cmd.Add("--include-partial-messages");
        }

        if (_options.ForkSession)
        {
            cmd.Add("--fork-session");
        }

        // Handle agents
        if (_options.Agents != null && _options.Agents.Count > 0)
        {
            var agentsJson = JsonSerializer.Serialize(_options.Agents);
            cmd.AddRange(["--agents", agentsJson]);
        }

        // Handle setting sources
        var sourcesValue = _options.SettingSources != null
            ? string.Join(",", _options.SettingSources.Select(GetSettingSourceValue))
            : "";
        cmd.AddRange(["--setting-sources", sourcesValue]);

        // Add plugin directories
        foreach (var plugin in _options.Plugins)
        {
            if (plugin.Type == SdkPluginType.Local)
            {
                cmd.AddRange(["--plugin-dir", plugin.Path]);
            }
            else
            {
                throw new ArgumentException($"Unsupported plugin type: {plugin.Type}");
            }
        }

        // Add extra args for future CLI flags
        foreach (var (flag, value) in _options.ExtraArgs)
        {
            if (value == null)
            {
                // Boolean flag without value
                cmd.Add($"--{flag}");
            }
            else
            {
                // Flag with value
                cmd.AddRange([$"--{flag}", value]);
            }
        }

        if (_options.MaxThinkingTokens.HasValue)
        {
            cmd.AddRange(["--max-thinking-tokens", _options.MaxThinkingTokens.Value.ToString()]);
        }

        // Extract schema from output_format structure if provided
        if (_options.OutputFormat != null &&
            _options.OutputFormat.TryGetValue("type", out var typeValue) &&
            typeValue?.ToString() == "json_schema" &&
            _options.OutputFormat.TryGetValue("schema", out var schemaValue) &&
            schemaValue != null)
        {
            var schemaJson = schemaValue is string s ? s : JsonSerializer.Serialize(schemaValue);
            cmd.AddRange(["--json-schema", schemaJson]);
        }

        // Add prompt handling based on mode
        // IMPORTANT: This must come AFTER all flags because everything after "--" is treated as arguments
        if (_isStreaming)
        {
            // Streaming mode: use --input-format stream-json
            cmd.AddRange(["--input-format", "stream-json"]);
        }
        else
        {
            // String mode: use --print with the prompt
            cmd.AddRange(["--print", "--", (string)_prompt]);
        }

        // Check if command line is too long (Windows limitation)
        var cmdStr = string.Join(" ", cmd);
        if (cmdStr.Length > CmdLengthLimit && _options.Agents != null)
        {
            // Command is too long - use temp file for agents
            try
            {
                var agentsIdx = cmd.IndexOf("--agents");
                if (agentsIdx >= 0 && agentsIdx + 1 < cmd.Count)
                {
                    var agentsJsonValue = cmd[agentsIdx + 1];

                    // Create a temporary file
                    var tempFile = Path.GetTempFileName();
                    tempFile = Path.ChangeExtension(tempFile, ".json");
                    File.WriteAllText(tempFile, agentsJsonValue);

                    // Track for cleanup
                    _tempFiles.Add(tempFile);

                    // Replace agents JSON with @filepath reference
                    cmd[agentsIdx + 1] = $"@{tempFile}";

                    _logger?.LogDebug(
                        $"Command line length ({cmdStr.Length}) exceeds limit ({CmdLengthLimit}). Using temp file for --agents: {tempFile}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Failed to optimize command line length: {ex.Message}");
            }
        }

        return cmd;
    }

    private async Task CheckClaudeVersionAsync(CancellationToken cancellationToken)
    {
        Process? versionProcess = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                Arguments = "-v",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            versionProcess = Process.Start(startInfo);
            if (versionProcess == null)
                return;

            var versionOutput = await versionProcess.StandardOutput.ReadToEndAsync(linkedCts.Token).ConfigureAwait(false);
            versionOutput = versionOutput.Trim();

            var match = System.Text.RegularExpressions.Regex.Match(versionOutput, @"([0-9]+\.[0-9]+\.[0-9]+)");
            if (match.Success)
            {
                var version = match.Groups[1].Value;
                var versionParts = version.Split('.').Select(int.Parse).ToArray();
                var minParts = MinimumClaudeCodeVersion.Split('.').Select(int.Parse).ToArray();

                if (CompareVersions(versionParts, minParts) < 0)
                {
                    var warning = $"Warning: Claude Code version {version} is unsupported in the Agent SDK. " +
                                  $"Minimum required version is {MinimumClaudeCodeVersion}. " +
                                  "Some features may not work correctly.";
                    _logger?.LogWarning("{Warning}", warning);
                    Console.Error.WriteLine(warning);
                }
            }
        }
        catch
        {
            // Ignore version check errors
        }
        finally
        {
            if (versionProcess != null)
            {
                try
                {
                    if (!versionProcess.HasExited)
                        versionProcess.Kill();
                    versionProcess.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private static int CompareVersions(int[] a, int[] b)
    {
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var aVal = i < a.Length ? a[i] : 0;
            var bVal = i < b.Length ? b[i] : 0;
            if (aVal != bVal)
                return aVal.CompareTo(bVal);
        }
        return 0;
    }

    private static string GetBetaValue(SdkBeta beta)
    {
        return beta switch
        {
            SdkBeta.Context1M_2025_08_07 => "context-1m-2025-08-07",
            _ => throw new ArgumentException($"Unknown beta: {beta}")
        };
    }

    private static string GetPermissionModeValue(PermissionMode mode)
    {
        return mode switch
        {
            PermissionMode.Default => "default",
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.Plan => "plan",
            PermissionMode.BypassPermissions => "bypassPermissions",
            _ => throw new ArgumentException($"Unknown permission mode: {mode}")
        };
    }

    private static string GetSettingSourceValue(SettingSource source)
    {
        return source switch
        {
            SettingSource.User => "user",
            SettingSource.Project => "project",
            SettingSource.Local => "local",
            _ => throw new ArgumentException($"Unknown setting source: {source}")
        };
    }

    #endregion
}
