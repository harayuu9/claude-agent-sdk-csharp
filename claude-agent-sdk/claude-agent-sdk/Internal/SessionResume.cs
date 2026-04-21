using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSdk.Internal;

/// <summary>
/// Result of MaterializeResumeSession.
/// </summary>
internal sealed class MaterializedResume : IAsyncDisposable
{
    internal string ConfigDir { get; }
    internal string ResumeSessionId { get; }

    internal MaterializedResume(string configDir, string resumeSessionId)
    {
        ConfigDir = configDir;
        ResumeSessionId = resumeSessionId;
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
    }

    internal async Task CleanupAsync()
    {
        try
        {
            if (Directory.Exists(ConfigDir))
            {
                await Task.Run(() =>
                {
                    try { Directory.Delete(ConfigDir, recursive: true); }
                    catch { /* best-effort cleanup */ }
                });
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// Materialize a SessionStore-backed resume into a temp CLAUDE_CONFIG_DIR.
/// </summary>
internal static class SessionResume
{
    private static readonly Regex UuidRegex = new(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static ClaudeAgentOptions ApplyMaterializedOptions(
        ClaudeAgentOptions options, MaterializedResume materialized)
    {
        var env = new Dictionary<string, string>(options.Env)
        {
            ["CLAUDE_CONFIG_DIR"] = materialized.ConfigDir
        };

        return options with
        {
            Env = env,
            Resume = materialized.ResumeSessionId,
            ContinueConversation = false
        };
    }

    internal static TranscriptMirrorBatcher BuildMirrorBatcher(
        ISessionStore store,
        MaterializedResume? materialized,
        Dictionary<string, string> env,
        Func<SessionKey?, string, Task> onError,
        ILogger? logger = null)
    {
        var projectsDir = materialized != null
            ? Path.Combine(materialized.ConfigDir, "projects")
            : GetProjectsDir(env);

        return new TranscriptMirrorBatcher(store, projectsDir, onError, logger);
    }

    internal static async Task<MaterializedResume?> MaterializeResumeSessionAsync(
        ClaudeAgentOptions options)
    {
        var store = options.SessionStore;
        if (store == null)
            return null;
        if (options.Resume == null && !options.ContinueConversation)
            return null;

        var timeoutMs = options.LoadTimeoutMs;
        var projectKey = Sessions.ProjectKeyForDirectory(options.Cwd);

        string? sessionId;
        List<SessionStoreEntry>? entries;

        if (options.Resume != null)
        {
            if (!UuidRegex.IsMatch(options.Resume))
                return null;

            using var cts = new CancellationTokenSource(timeoutMs);
            var key = new SessionKey { ProjectKey = projectKey, SessionId = options.Resume };
            entries = await store.LoadAsync(key).WaitAsync(cts.Token);
            if (entries == null || entries.Count == 0)
                return null;
            sessionId = options.Resume;
        }
        else
        {
            // continue_conversation: pick most recent session
            using var cts = new CancellationTokenSource(timeoutMs);
            var listing = await store.ListSessionsAsync(projectKey).WaitAsync(cts.Token);
            if (listing.Count == 0)
                return null;

            var mostRecent = listing.OrderByDescending(e => e.Mtime).First();
            sessionId = mostRecent.SessionId;

            var key = new SessionKey { ProjectKey = projectKey, SessionId = sessionId };
            entries = await store.LoadAsync(key).WaitAsync(cts.Token);
            if (entries == null || entries.Count == 0)
                return null;
        }

        var tmpBase = Path.Combine(Path.GetTempPath(), $"claude-resume-{Guid.NewGuid():N}");
        try
        {
            var projectDir = Path.Combine(tmpBase, "projects", projectKey);
            Directory.CreateDirectory(projectDir);

            WriteJsonl(Path.Combine(projectDir, $"{sessionId}.jsonl"), entries);
            CopyAuthFiles(tmpBase, options.Env);

            // Materialize subagent transcripts if store supports it
            if (SessionStoreValidation.StoreImplements(store, "ListSubkeys"))
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                await MaterializeSubkeysAsync(store, tmpBase, projectDir, projectKey, sessionId, cts.Token);
            }
        }
        catch
        {
            try { Directory.Delete(tmpBase, recursive: true); } catch { }
            throw;
        }

        return new MaterializedResume(tmpBase, sessionId);
    }

    private static void WriteJsonl(string path, List<SessionStoreEntry> entries)
    {
        using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        foreach (var entry in entries)
        {
            writer.WriteLine(JsonSerializer.Serialize(entry));
        }
    }

    private static void CopyAuthFiles(string tmpBase, Dictionary<string, string> env)
    {
        var sourceConfigDir = env.GetValueOrDefault("CLAUDE_CONFIG_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        var credentialsFile = Path.Combine(sourceConfigDir, ".credentials.json");
        if (File.Exists(credentialsFile))
        {
            var dest = Path.Combine(tmpBase, ".credentials.json");
            File.Copy(credentialsFile, dest, overwrite: true);
        }
    }

    private static async Task MaterializeSubkeysAsync(
        ISessionStore store,
        string tmpBase,
        string projectDir,
        string projectKey,
        string sessionId,
        CancellationToken ct)
    {
        var subkeys = await store.ListSubkeysAsync(
            new SessionListSubkeysKey { ProjectKey = projectKey, SessionId = sessionId }).WaitAsync(ct);

        foreach (var subpath in subkeys)
        {
            var key = new SessionKey
            {
                ProjectKey = projectKey,
                SessionId = sessionId,
                Subpath = subpath
            };

            var entries = await store.LoadAsync(key).WaitAsync(ct);
            if (entries == null || entries.Count == 0)
                continue;

            // Write to <projectDir>/<sessionId>/<subpath>.jsonl
            var subFile = Path.Combine(projectDir, sessionId, subpath + ".jsonl");
            var subDir = Path.GetDirectoryName(subFile);
            if (subDir != null)
                Directory.CreateDirectory(subDir);

            WriteJsonl(subFile, entries);
        }
    }

    private static string GetProjectsDir(Dictionary<string, string> env)
    {
        var configDir = env.GetValueOrDefault("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
            return Path.Combine(configDir, "projects");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "projects");
    }
}
