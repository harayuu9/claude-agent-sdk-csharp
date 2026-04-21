namespace ClaudeAgentSdk;

/// <summary>
/// In-memory ISessionStore implementation for testing and development.
/// Data is lost when the process exits.
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    private readonly Dictionary<string, List<SessionStoreEntry>> _store = new();
    private readonly Dictionary<string, long> _mtimes = new();
    private readonly Dictionary<(string ProjectKey, string SessionId), SessionSummaryEntry> _summaries = new();
    private long _lastMtime;
    private readonly object _lock = new();

    private long NextMtime()
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs <= _lastMtime)
            nowMs = _lastMtime + 1;
        _lastMtime = nowMs;
        return nowMs;
    }

    private static string KeyToString(SessionKey key)
    {
        var parts = new List<string> { key.ProjectKey, key.SessionId };
        if (!string.IsNullOrEmpty(key.Subpath))
            parts.Add(key.Subpath);
        return string.Join("/", parts);
    }

    /// <inheritdoc />
    public Task AppendAsync(SessionKey key, List<SessionStoreEntry> entries)
    {
        lock (_lock)
        {
            var k = KeyToString(key);
            if (!_store.TryGetValue(k, out var existing))
            {
                existing = [];
                _store[k] = existing;
            }
            existing.AddRange(entries);

            var nowMs = NextMtime();
            _mtimes[k] = nowMs;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<SessionStoreEntry>?> LoadAsync(SessionKey key)
    {
        lock (_lock)
        {
            var k = KeyToString(key);
            if (_store.TryGetValue(k, out var entries))
                return Task.FromResult<List<SessionStoreEntry>?>(new List<SessionStoreEntry>(entries));
            return Task.FromResult<List<SessionStoreEntry>?>(null);
        }
    }

    /// <inheritdoc />
    public Task<List<SessionStoreListEntry>> ListSessionsAsync(string projectKey)
    {
        lock (_lock)
        {
            var results = new List<SessionStoreListEntry>();
            var prefix = projectKey + "/";
            foreach (var k in _store.Keys)
            {
                if (!k.StartsWith(prefix))
                    continue;
                var rest = k[prefix.Length..];
                if (!rest.Contains('/'))
                {
                    results.Add(new SessionStoreListEntry
                    {
                        SessionId = rest,
                        Mtime = _mtimes.GetValueOrDefault(k, 0)
                    });
                }
            }
            return Task.FromResult(results);
        }
    }

    /// <inheritdoc />
    public Task<List<SessionSummaryEntry>> ListSessionSummariesAsync(string projectKey)
    {
        lock (_lock)
        {
            var results = _summaries
                .Where(kv => kv.Key.ProjectKey == projectKey)
                .Select(kv => kv.Value)
                .ToList();
            return Task.FromResult(results);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(SessionKey key)
    {
        lock (_lock)
        {
            var k = KeyToString(key);
            _store.Remove(k);
            _mtimes.Remove(k);

            if (string.IsNullOrEmpty(key.Subpath))
            {
                _summaries.Remove((key.ProjectKey, key.SessionId));
                var prefix = $"{key.ProjectKey}/{key.SessionId}/";
                var keysToRemove = _store.Keys.Where(sk => sk.StartsWith(prefix)).ToList();
                foreach (var storeKey in keysToRemove)
                {
                    _store.Remove(storeKey);
                    _mtimes.Remove(storeKey);
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<List<string>> ListSubkeysAsync(SessionListSubkeysKey key)
    {
        lock (_lock)
        {
            var prefix = $"{key.ProjectKey}/{key.SessionId}/";
            var results = _store.Keys
                .Where(k => k.StartsWith(prefix))
                .Select(k => k[prefix.Length..])
                .ToList();
            return Task.FromResult(results);
        }
    }

    /// <summary>
    /// Test helper — get all entries for a key (empty list if absent).
    /// </summary>
    public List<SessionStoreEntry> GetEntries(SessionKey key)
    {
        lock (_lock)
        {
            return _store.TryGetValue(KeyToString(key), out var entries)
                ? new List<SessionStoreEntry>(entries)
                : [];
        }
    }

    /// <summary>
    /// Test helper — number of stored sessions (main transcripts only).
    /// </summary>
    public int Size
    {
        get
        {
            lock (_lock)
            {
                return _store.Keys.Count(k =>
                {
                    var firstSlash = k.IndexOf('/');
                    return firstSlash != -1 && !k[(firstSlash + 1)..].Contains('/');
                });
            }
        }
    }

    /// <summary>
    /// Test helper — clear all stored data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _store.Clear();
            _mtimes.Clear();
            _summaries.Clear();
            _lastMtime = 0;
        }
    }
}
