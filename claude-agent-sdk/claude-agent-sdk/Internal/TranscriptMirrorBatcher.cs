using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClaudeAgentSdk.Internal;

/// <summary>
/// Batching layer between transcript_mirror stdout frames and a SessionStore.
/// Accumulates frames and flushes to ISessionStore.AppendAsync either when a result
/// message arrives (explicit flush) or when the pending buffer exceeds size thresholds.
/// </summary>
internal sealed class TranscriptMirrorBatcher
{
    private const int MaxPendingEntries = 500;
    private const int MaxPendingBytes = 1 << 20; // 1 MiB
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(60);

    private readonly ISessionStore _store;
    private readonly string _projectsDir;
    private readonly Func<SessionKey?, string, Task> _onError;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private List<MirrorEntry> _pending = [];
    private int _pendingEntries;
    private int _pendingBytes;

    internal TranscriptMirrorBatcher(
        ISessionStore store,
        string projectsDir,
        Func<SessionKey?, string, Task> onError,
        ILogger? logger = null)
    {
        _store = store;
        _projectsDir = projectsDir;
        _onError = onError;
        _logger = logger;
    }

    internal void Enqueue(string filePath, List<object> entries)
    {
        var size = entries.Count * 100; // approximate
        _pending.Add(new MirrorEntry(filePath, entries, size));
        _pendingEntries += entries.Count;
        _pendingBytes += size;

        if (_pendingEntries > MaxPendingEntries || _pendingBytes > MaxPendingBytes)
        {
            _ = DrainAsync();
        }
    }

    internal async Task FlushAsync()
    {
        await DrainAsync();
    }

    internal async Task CloseAsync()
    {
        try
        {
            await FlushAsync();
        }
        catch (Exception e)
        {
            _logger?.LogDebug(e, "[TranscriptMirrorBatcher] close flush failed");
        }
    }

    private async Task DrainAsync()
    {
        var items = _pending;
        _pending = [];
        _pendingEntries = 0;
        _pendingBytes = 0;

        var errors = new List<(SessionKey Key, string Message)>();

        await _lock.WaitAsync();
        try
        {
            if (items.Count == 0)
                return;

            // Coalesce by file path
            var byPath = new Dictionary<string, List<object>>();
            foreach (var item in items)
            {
                if (!byPath.TryGetValue(item.FilePath, out var bucket))
                {
                    bucket = [];
                    byPath[item.FilePath] = bucket;
                }
                bucket.AddRange(item.Entries);
            }

            foreach (var (filePath, entries) in byPath)
            {
                if (entries.Count == 0)
                    continue;

                var key = FilePathToSessionKey(filePath, _projectsDir);
                if (key == null)
                {
                    _logger?.LogWarning(
                        "[SessionStore] dropping mirror frame: filePath {FilePath} is not under {ProjectsDir}",
                        filePath, _projectsDir);
                    continue;
                }

                try
                {
                    var storeEntries = ConvertToStoreEntries(entries);
                    using var cts = new CancellationTokenSource(SendTimeout);
                    await _store.AppendAsync(key, storeEntries).WaitAsync(cts.Token);
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "[TranscriptMirrorBatcher] flush failed for {FilePath}", filePath);
                    errors.Add((key, e.Message));
                }
            }
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "[TranscriptMirrorBatcher] DrainAsync raised");
            return;
        }
        finally
        {
            _lock.Release();
        }

        foreach (var (key, msg) in errors)
        {
            try
            {
                await _onError(key, msg);
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "[TranscriptMirrorBatcher] on_error callback raised");
            }
        }
    }

    private static List<SessionStoreEntry> ConvertToStoreEntries(List<object> entries)
    {
        var result = new List<SessionStoreEntry>();
        foreach (var entry in entries)
        {
            if (entry is Dictionary<string, object?> dict)
            {
                var storeEntry = new SessionStoreEntry
                {
                    Type = dict.GetValueOrDefault("type")?.ToString() ?? "unknown",
                    Uuid = dict.GetValueOrDefault("uuid")?.ToString(),
                    Timestamp = dict.GetValueOrDefault("timestamp")?.ToString()
                };
                result.Add(storeEntry);
            }
            else
            {
                var json = JsonSerializer.Serialize(entry);
                var parsed = JsonSerializer.Deserialize<SessionStoreEntry>(json);
                if (parsed != null)
                    result.Add(parsed);
            }
        }
        return result;
    }

    internal static SessionKey? FilePathToSessionKey(string filePath, string projectsDir)
    {
        try
        {
            var rel = Path.GetRelativePath(projectsDir, filePath);
            if (rel.StartsWith("..") || Path.IsPathRooted(rel))
                return null;

            var parts = rel.Replace('\\', '/').Split('/');
            if (parts.Length < 2)
                return null;

            var projectKey = parts[0];
            var second = parts[1];

            // Main transcript: <project_key>/<session_id>.jsonl
            if (parts.Length == 2 && second.EndsWith(".jsonl"))
            {
                return new SessionKey
                {
                    ProjectKey = projectKey,
                    SessionId = second[..^".jsonl".Length]
                };
            }

            // Subagent transcript: <project_key>/<session_id>/subagents/.../agent-<id>.jsonl
            if (parts.Length >= 4)
            {
                var subpathParts = parts[2..];
                var last = subpathParts[^1];
                if (last.EndsWith(".jsonl"))
                    subpathParts[^1] = last[..^".jsonl".Length];

                return new SessionKey
                {
                    ProjectKey = projectKey,
                    SessionId = second,
                    Subpath = string.Join("/", subpathParts)
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private sealed record MirrorEntry(string FilePath, List<object> Entries, int Bytes);
}
