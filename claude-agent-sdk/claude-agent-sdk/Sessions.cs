using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeAgentSdk;

/// <summary>
/// Static methods for querying Claude session history.
/// Sessions are stored as JSONL files in ~/.claude/projects/&lt;sanitized-cwd&gt;/.
/// </summary>
public static partial class Sessions
{
    private const int LiteReadBufSize = 65536;
    private const int MaxSanitizedLength = 200;

    /// <summary>
    /// List sessions with metadata extracted from stat + head/tail reads.
    /// </summary>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <param name="limit">Maximum number of sessions to return.</param>
    /// <param name="offset">Number of sessions to skip.</param>
    /// <returns>List of session info sorted by last_modified descending.</returns>
    public static List<SDKSessionInfo> ListSessions(
        string? directory = null,
        int? limit = null,
        int offset = 0)
    {
        var projectDir = FindProjectDir(directory ?? Environment.CurrentDirectory);
        if (projectDir == null || !Directory.Exists(projectDir))
        {
            return [];
        }

        var sessions = new List<SDKSessionInfo>();

        foreach (var file in Directory.GetFiles(projectDir, "*.jsonl"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (!IsValidUuid(fileName))
                continue;

            var info = ParseSessionInfoFromFile(file, fileName);
            if (info != null)
            {
                sessions.Add(info);
            }
        }

        // Sort by last_modified descending
        sessions.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

        // Apply offset and limit
        if (offset > 0)
        {
            sessions = sessions.Skip(offset).ToList();
        }
        if (limit.HasValue)
        {
            sessions = sessions.Take(limit.Value).ToList();
        }

        return sessions;
    }

    /// <summary>
    /// Get metadata for a single session by ID.
    /// </summary>
    /// <param name="sessionId">UUID of the session.</param>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <returns>Session info or null if not found.</returns>
    public static SDKSessionInfo? GetSessionInfo(string sessionId, string? directory = null)
    {
        ValidateUuid(sessionId);

        var sessionFile = FindSessionFile(sessionId, directory);
        if (sessionFile == null)
            return null;

        return ParseSessionInfoFromFile(sessionFile, sessionId);
    }

    /// <summary>
    /// Read a session's conversation messages from JSONL transcript.
    /// </summary>
    /// <param name="sessionId">UUID of the session.</param>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <param name="limit">Maximum number of messages to return.</param>
    /// <param name="offset">Number of messages to skip.</param>
    /// <returns>List of session messages in chronological order.</returns>
    public static List<SessionMessage> GetSessionMessages(
        string sessionId,
        string? directory = null,
        int? limit = null,
        int offset = 0)
    {
        ValidateUuid(sessionId);

        var sessionFile = FindSessionFile(sessionId, directory);
        if (sessionFile == null)
        {
            throw new FileNotFoundException($"Session not found: {sessionId}");
        }

        var messages = new List<SessionMessage>();

        foreach (var line in File.ReadLines(sessionFile))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeElem))
                    continue;

                var type = typeElem.GetString();
                if (type is not ("user" or "assistant"))
                    continue;

                if (!root.TryGetProperty("uuid", out var uuidElem))
                    continue;

                var uuid = uuidElem.GetString();
                if (string.IsNullOrEmpty(uuid))
                    continue;

                // Skip sidechain messages
                if (root.TryGetProperty("isSidechain", out var sc) &&
                    sc.ValueKind == JsonValueKind.True)
                    continue;

                var sid = root.TryGetProperty("sessionId", out var sidElem)
                    ? sidElem.GetString() ?? sessionId
                    : sessionId;

                var parentToolUseId = root.TryGetProperty("parentToolUseId", out var ptui) &&
                    ptui.ValueKind == JsonValueKind.String
                    ? ptui.GetString()
                    : null;

                messages.Add(new SessionMessage
                {
                    Type = type,
                    Uuid = uuid,
                    SessionId = sid,
                    Message = JsonSerializer.Deserialize<object>(line)!,
                    ParentToolUseId = parentToolUseId
                });
            }
            catch (JsonException)
            {
                // Skip corrupt lines
            }
        }

        // Apply offset and limit
        if (offset > 0)
        {
            messages = messages.Skip(offset).ToList();
        }
        if (limit.HasValue)
        {
            messages = messages.Take(limit.Value).ToList();
        }

        return messages;
    }

    #region Internal Helpers

    internal static string? FindSessionFile(string sessionId, string? directory)
    {
        var projectDir = FindProjectDir(directory ?? Environment.CurrentDirectory);
        if (projectDir == null)
            return null;

        var filePath = Path.Combine(projectDir, $"{sessionId}.jsonl");
        return File.Exists(filePath) ? filePath : null;
    }

    internal static string? FindProjectDir(string directory)
    {
        var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

        var projectsDir = Path.Combine(configDir, "projects");
        if (!Directory.Exists(projectsDir))
            return null;

        var canonPath = CanonicalizePath(directory);
        var sanitized = SanitizePath(canonPath);

        var projectDir = Path.Combine(projectsDir, sanitized);
        if (Directory.Exists(projectDir))
            return projectDir;

        // Hash mismatch tolerance: try prefix-based scanning for long paths
        if (canonPath.Length > MaxSanitizedLength)
        {
            var prefix = sanitized[..Math.Min(sanitized.Length, MaxSanitizedLength)];
            try
            {
                foreach (var dir in Directory.GetDirectories(projectsDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith(prefix[..Math.Min(prefix.Length, 50)]))
                    {
                        return dir;
                    }
                }
            }
            catch
            {
                // Ignore directory scanning errors
            }
        }

        return null;
    }

    private static SDKSessionInfo? ParseSessionInfoFromFile(string filePath, string sessionId)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
                return null;

            var lastModified = new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeMilliseconds();
            var fileSize = fileInfo.Length;

            // Read head and tail buffers
            var (head, tail) = ReadHeadTail(filePath, fileSize);

            if (string.IsNullOrEmpty(head))
                return null;

            // Skip sidechain sessions
            if (head.Contains("\"isSidechain\":true") || head.Contains("\"isSidechain\": true"))
                return null;

            var customTitle = ExtractLastJsonStringField(tail, "customTitle")
                ?? ExtractJsonStringField(head, "customTitle");
            var aiTitle = ExtractLastJsonStringField(tail, "aiTitle")
                ?? ExtractJsonStringField(head, "aiTitle");
            var firstPrompt = ExtractJsonStringField(head, "content");
            var gitBranch = ExtractJsonStringField(head, "gitBranch");
            var cwd = ExtractJsonStringField(head, "cwd");
            var tag = ExtractLastTagValue(tail);

            // Summary priority: customTitle > lastPrompt > aiTitle > firstPrompt
            var summary = customTitle ?? aiTitle ?? firstPrompt ?? "Untitled session";

            // Extract created_at from first entry
            long? createdAt = null;
            var timestamp = ExtractJsonStringField(head, "timestamp");
            if (timestamp != null && DateTimeOffset.TryParse(timestamp, out var dto))
            {
                createdAt = dto.ToUnixTimeMilliseconds();
            }

            return new SDKSessionInfo
            {
                SessionId = sessionId,
                Summary = summary,
                LastModified = lastModified,
                FileSize = fileSize,
                CustomTitle = customTitle,
                FirstPrompt = firstPrompt,
                GitBranch = gitBranch,
                Cwd = cwd,
                Tag = tag,
                CreatedAt = createdAt
            };
        }
        catch
        {
            return null;
        }
    }

    private static (string head, string tail) ReadHeadTail(string filePath, long fileSize)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var headBuf = new byte[Math.Min(fileSize, LiteReadBufSize)];
        var headRead = fs.Read(headBuf, 0, headBuf.Length);
        var head = Encoding.UTF8.GetString(headBuf, 0, headRead);

        if (fileSize <= LiteReadBufSize)
            return (head, head);

        var tailOffset = Math.Max(0, fileSize - LiteReadBufSize);
        fs.Seek(tailOffset, SeekOrigin.Begin);
        var tailBuf = new byte[Math.Min(fileSize - tailOffset, LiteReadBufSize)];
        var tailRead = fs.Read(tailBuf, 0, tailBuf.Length);
        var tail = Encoding.UTF8.GetString(tailBuf, 0, tailRead);

        return (head, tail);
    }

    private static string? ExtractJsonStringField(string text, string key)
    {
        var pattern = $"\"{key}\"\\s*:\\s*\"";
        var idx = text.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        if (idx < 0)
            idx = text.IndexOf($"\"{key}\" :", StringComparison.Ordinal);
        if (idx < 0)
            return null;

        // Find the opening quote of the value
        var colonIdx = text.IndexOf(':', idx + key.Length + 2);
        if (colonIdx < 0)
            return null;

        var quoteIdx = text.IndexOf('"', colonIdx + 1);
        if (quoteIdx < 0)
            return null;

        // Find the closing quote (handling escaped quotes)
        var sb = new StringBuilder();
        for (var i = quoteIdx + 1; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                sb.Append(text[i + 1]);
                i++;
                continue;
            }
            if (text[i] == '"')
                break;
            sb.Append(text[i]);
        }

        var result = sb.ToString();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    private static string? ExtractLastJsonStringField(string text, string key)
    {
        var searchKey = $"\"{key}\":";
        var searchKeySpaced = $"\"{key}\" :";
        string? lastValue = null;
        var startIdx = 0;

        while (true)
        {
            var idx = text.IndexOf(searchKey, startIdx, StringComparison.Ordinal);
            if (idx < 0)
                idx = text.IndexOf(searchKeySpaced, startIdx, StringComparison.Ordinal);
            if (idx < 0)
                break;

            var colonIdx = text.IndexOf(':', idx + key.Length + 2);
            if (colonIdx < 0)
                break;

            var quoteIdx = text.IndexOf('"', colonIdx + 1);
            if (quoteIdx < 0)
                break;

            var sb = new StringBuilder();
            var i = quoteIdx + 1;
            for (; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    sb.Append(text[i + 1]);
                    i++;
                    continue;
                }
                if (text[i] == '"')
                    break;
                sb.Append(text[i]);
            }

            var val = sb.ToString();
            if (!string.IsNullOrEmpty(val))
                lastValue = val;

            startIdx = i + 1;
        }

        return lastValue;
    }

    private static string? ExtractLastTagValue(string text)
    {
        // Look for {"type":"tag","tag":"..."} entries
        string? lastTag = null;
        var idx = 0;
        while (true)
        {
            var typeIdx = text.IndexOf("\"type\":\"tag\"", idx, StringComparison.Ordinal);
            if (typeIdx < 0)
                typeIdx = text.IndexOf("\"type\": \"tag\"", idx, StringComparison.Ordinal);
            if (typeIdx < 0)
                break;

            // Find the tag value near this entry
            var tagValue = ExtractJsonStringField(text[typeIdx..], "tag");
            if (tagValue != null)
                lastTag = tagValue;

            idx = typeIdx + 10;
        }

        return lastTag;
    }

    internal static string SanitizePath(string path)
    {
        var sanitized = SanitizePathRegex().Replace(path, "-");
        if (sanitized.Length <= MaxSanitizedLength)
            return sanitized;

        var hash = SimpleHash(path);
        return sanitized[..MaxSanitizedLength] + "-" + hash;
    }

    private static string SimpleHash(string input)
    {
        var h = 0L;
        foreach (var c in input)
        {
            h = ((h << 5) - h + c) & 0xFFFFFFFF;
        }
        if (h >= 0x80000000)
            h -= 0x100000000;
        return ConvertToBase36(Math.Abs(h));
    }

    private static string ConvertToBase36(long value)
    {
        if (value == 0)
            return "0";

        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        while (value > 0)
        {
            sb.Insert(0, chars[(int)(value % 36)]);
            value /= 36;
        }
        return sb.ToString();
    }

    internal static string CanonicalizePath(string path)
    {
        try
        {
            var resolved = Path.GetFullPath(path);
            return resolved.Normalize(NormalizationForm.FormC);
        }
        catch
        {
            return path.Normalize(NormalizationForm.FormC);
        }
    }

    private static bool IsValidUuid(string value)
    {
        return Guid.TryParse(value, out _);
    }

    private static void ValidateUuid(string value)
    {
        if (!IsValidUuid(value))
        {
            throw new ArgumentException($"Invalid UUID: {value}", nameof(value));
        }
    }

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex SanitizePathRegex();

    #endregion

    #region Project Key

    /// <summary>
    /// Derive the SessionStore project_key for a directory.
    /// Defaults to the current working directory.
    /// </summary>
    public static string ProjectKeyForDirectory(string? directory = null)
    {
        var absPath = CanonicalizePath(directory ?? Environment.CurrentDirectory);
        return SanitizePath(absPath);
    }

    #endregion

    #region Subagent Support

    /// <summary>
    /// List subagent IDs for a given session.
    /// </summary>
    public static List<string> ListSubagents(string sessionId, string? directory = null)
    {
        if (!IsValidUuid(sessionId))
            return [];

        var subagentsDir = ResolveSubagentsDir(sessionId, directory);
        if (subagentsDir == null || !Directory.Exists(subagentsDir))
            return [];

        return CollectAgentFiles(subagentsDir).Select(a => a.AgentId).ToList();
    }

    /// <summary>
    /// Read a subagent's conversation messages from its JSONL transcript file.
    /// </summary>
    public static List<SessionMessage> GetSubagentMessages(
        string sessionId,
        string agentId,
        string? directory = null,
        int? limit = null,
        int offset = 0)
    {
        if (!IsValidUuid(sessionId) || string.IsNullOrEmpty(agentId))
            return [];

        var subagentsDir = ResolveSubagentsDir(sessionId, directory);
        if (subagentsDir == null || !Directory.Exists(subagentsDir))
            return [];

        string? matchPath = null;
        foreach (var (id, path) in CollectAgentFiles(subagentsDir))
        {
            if (id == agentId)
            {
                matchPath = path;
                break;
            }
        }

        if (matchPath == null)
            return [];

        try
        {
            var messages = ParseJsonlToMessages(matchPath);
            return ApplyPaging(messages, limit, offset);
        }
        catch
        {
            return [];
        }
    }

    private static string? ResolveSubagentsDir(string sessionId, string? directory)
    {
        var sessionFile = FindSessionFile(sessionId, directory);
        if (sessionFile == null)
            return null;

        var sessionDir = Path.ChangeExtension(sessionFile, null);
        return Path.Combine(sessionDir, "subagents");
    }

    private static List<(string AgentId, string Path)> CollectAgentFiles(string baseDir)
    {
        var results = new List<(string, string)>();
        CollectAgentFilesRecursive(baseDir, results);
        return results;
    }

    private static void CollectAgentFilesRecursive(string dir, List<(string, string)> results)
    {
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir).OrderBy(e => Path.GetFileName(e)))
            {
                var name = Path.GetFileName(entry);
                if (File.Exists(entry) && name.StartsWith("agent-") && name.EndsWith(".jsonl"))
                {
                    var agentId = name["agent-".Length..^".jsonl".Length];
                    results.Add((agentId, entry));
                }
                else if (Directory.Exists(entry))
                {
                    CollectAgentFilesRecursive(entry, results);
                }
            }
        }
        catch
        {
            // Ignore directory scanning errors
        }
    }

    private static List<SessionMessage> ParseJsonlToMessages(string filePath)
    {
        var messages = new List<SessionMessage>();
        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeElem))
                    continue;
                var type = typeElem.GetString();
                if (type is not ("user" or "assistant"))
                    continue;
                if (!root.TryGetProperty("uuid", out var uuidElem))
                    continue;
                var uuid = uuidElem.GetString();
                if (string.IsNullOrEmpty(uuid))
                    continue;
                if (root.TryGetProperty("isSidechain", out var sc) && sc.ValueKind == JsonValueKind.True)
                    continue;
                var sid = root.TryGetProperty("sessionId", out var sidElem) ? sidElem.GetString() ?? "" : "";
                var parentToolUseId = root.TryGetProperty("parentToolUseId", out var ptui) && ptui.ValueKind == JsonValueKind.String
                    ? ptui.GetString() : null;
                messages.Add(new SessionMessage
                {
                    Type = type,
                    Uuid = uuid,
                    SessionId = sid,
                    Message = JsonSerializer.Deserialize<object>(line)!,
                    ParentToolUseId = parentToolUseId
                });
            }
            catch (JsonException) { }
        }
        return messages;
    }

    private static List<SessionMessage> ApplyPaging(List<SessionMessage> messages, int? limit, int offset)
    {
        if (offset > 0)
            messages = messages.Skip(offset).ToList();
        if (limit.HasValue && limit.Value > 0)
            messages = messages.Take(limit.Value).ToList();
        return messages;
    }

    #endregion

    #region Store-backed Session Functions

    /// <summary>
    /// List sessions from an ISessionStore.
    /// </summary>
    public static async Task<List<SDKSessionInfo>> ListSessionsFromStoreAsync(
        ISessionStore sessionStore,
        string? directory = null,
        int? limit = null,
        int offset = 0)
    {
        var projectKey = ProjectKeyForDirectory(directory);
        var listing = await sessionStore.ListSessionsAsync(projectKey);

        var results = new List<SDKSessionInfo>();
        var semaphore = new SemaphoreSlim(16);

        var tasks = listing.Select(async entry =>
        {
            await semaphore.WaitAsync();
            try
            {
                var key = new SessionKey { ProjectKey = projectKey, SessionId = entry.SessionId };
                var entries = await sessionStore.LoadAsync(key);
                if (entries == null || entries.Count == 0)
                    return null;

                return new SDKSessionInfo
                {
                    SessionId = entry.SessionId,
                    Summary = "",
                    LastModified = entry.Mtime
                };
            }
            catch
            {
                return new SDKSessionInfo
                {
                    SessionId = entry.SessionId,
                    Summary = "",
                    LastModified = entry.Mtime
                };
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var settled = await Task.WhenAll(tasks);
        results.AddRange(settled.Where(r => r != null).Select(r => r!));

        results.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

        if (offset > 0)
            results = results.Skip(offset).ToList();
        if (limit.HasValue && limit.Value > 0)
            results = results.Take(limit.Value).ToList();

        return results;
    }

    /// <summary>
    /// Get session info from an ISessionStore.
    /// </summary>
    public static async Task<SDKSessionInfo?> GetSessionInfoFromStoreAsync(
        ISessionStore sessionStore,
        string sessionId,
        string? directory = null)
    {
        if (!IsValidUuid(sessionId))
            return null;

        var projectKey = ProjectKeyForDirectory(directory);
        var key = new SessionKey { ProjectKey = projectKey, SessionId = sessionId };
        var entries = await sessionStore.LoadAsync(key);
        if (entries == null || entries.Count == 0)
            return null;

        return new SDKSessionInfo
        {
            SessionId = sessionId,
            Summary = "",
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// Get session messages from an ISessionStore.
    /// </summary>
    public static async Task<List<SessionMessage>> GetSessionMessagesFromStoreAsync(
        ISessionStore sessionStore,
        string sessionId,
        string? directory = null,
        int? limit = null,
        int offset = 0)
    {
        if (!IsValidUuid(sessionId))
            return [];

        var projectKey = ProjectKeyForDirectory(directory);
        var key = new SessionKey { ProjectKey = projectKey, SessionId = sessionId };
        var entries = await sessionStore.LoadAsync(key);
        if (entries == null || entries.Count == 0)
            return [];

        var messages = StoreEntriesToMessages(entries, sessionId);
        return ApplyPaging(messages, limit, offset);
    }

    /// <summary>
    /// List subagent IDs from an ISessionStore.
    /// </summary>
    public static async Task<List<string>> ListSubagentsFromStoreAsync(
        ISessionStore sessionStore,
        string sessionId,
        string? directory = null)
    {
        if (!IsValidUuid(sessionId))
            return [];

        var projectKey = ProjectKeyForDirectory(directory);
        var subkeys = await sessionStore.ListSubkeysAsync(
            new SessionListSubkeysKey { ProjectKey = projectKey, SessionId = sessionId });

        var seen = new HashSet<string>();
        var ids = new List<string>();
        foreach (var subpath in subkeys)
        {
            if (!subpath.StartsWith("subagents/"))
                continue;
            var last = subpath.Split('/')[^1];
            if (last.StartsWith("agent-"))
            {
                var agentId = last["agent-".Length..];
                if (seen.Add(agentId))
                    ids.Add(agentId);
            }
        }
        return ids;
    }

    /// <summary>
    /// Get subagent messages from an ISessionStore.
    /// </summary>
    public static async Task<List<SessionMessage>> GetSubagentMessagesFromStoreAsync(
        ISessionStore sessionStore,
        string sessionId,
        string agentId,
        string? directory = null,
        int? limit = null,
        int offset = 0)
    {
        if (!IsValidUuid(sessionId) || string.IsNullOrEmpty(agentId))
            return [];

        var projectKey = ProjectKeyForDirectory(directory);

        var subpath = $"subagents/agent-{agentId}";
        try
        {
            var subkeys = await sessionStore.ListSubkeysAsync(
                new SessionListSubkeysKey { ProjectKey = projectKey, SessionId = sessionId });

            var target = $"agent-{agentId}";
            var match = subkeys.FirstOrDefault(sk =>
                sk.StartsWith("subagents/") && sk.Split('/')[^1] == target);
            if (match != null)
                subpath = match;
            else
                return [];
        }
        catch (NotImplementedException)
        {
            // Fall through with default subpath
        }

        var key = new SessionKey
        {
            ProjectKey = projectKey,
            SessionId = sessionId,
            Subpath = subpath
        };
        var entries = await sessionStore.LoadAsync(key);
        if (entries == null || entries.Count == 0)
            return [];

        var filtered = entries.Where(e => e.Type != "agent_metadata").ToList();
        var messages = StoreEntriesToMessages(filtered, sessionId);
        return ApplyPaging(messages, limit, offset);
    }

    private static List<SessionMessage> StoreEntriesToMessages(
        List<SessionStoreEntry> entries, string sessionId)
    {
        var messages = new List<SessionMessage>();
        foreach (var entry in entries)
        {
            if (entry.Type is not ("user" or "assistant"))
                continue;
            if (string.IsNullOrEmpty(entry.Uuid))
                continue;

            messages.Add(new SessionMessage
            {
                Type = entry.Type,
                Uuid = entry.Uuid,
                SessionId = sessionId,
                Message = entry,
                ParentToolUseId = null
            });
        }
        return messages;
    }

    #endregion
}
