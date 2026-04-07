using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeAgentSdk;

/// <summary>
/// Static methods for modifying Claude sessions (rename, tag, delete, fork).
/// Mutations are append-only JSONL operations safe for concurrent CLI access.
/// </summary>
public static partial class SessionMutations
{
    /// <summary>
    /// Rename a session by appending a custom-title entry.
    /// </summary>
    /// <param name="sessionId">UUID of the session.</param>
    /// <param name="title">New title (must be non-empty after trimming).</param>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <exception cref="ArgumentException">If UUID is invalid or title is empty.</exception>
    /// <exception cref="FileNotFoundException">If session not found.</exception>
    public static void RenameSession(string sessionId, string title, string? directory = null)
    {
        ValidateUuid(sessionId);

        title = title.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("Title must be non-empty", nameof(title));
        }

        var sessionFile = Sessions.FindSessionFile(sessionId, directory)
            ?? throw new FileNotFoundException($"Session not found: {sessionId}");

        var entry = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "custom-title",
            ["customTitle"] = title,
            ["sessionId"] = sessionId
        });

        AppendToFile(sessionFile, entry + "\n");
    }

    /// <summary>
    /// Tag a session. Pass null to clear the tag.
    /// </summary>
    /// <param name="sessionId">UUID of the session.</param>
    /// <param name="tag">Tag string or null to clear.</param>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <exception cref="ArgumentException">If UUID is invalid or tag is empty after sanitization.</exception>
    /// <exception cref="FileNotFoundException">If session not found.</exception>
    public static void TagSession(string sessionId, string? tag, string? directory = null)
    {
        ValidateUuid(sessionId);

        var sessionFile = Sessions.FindSessionFile(sessionId, directory)
            ?? throw new FileNotFoundException($"Session not found: {sessionId}");

        var sanitizedTag = tag != null ? SanitizeUnicode(tag.Trim()) : "";

        if (tag != null && string.IsNullOrEmpty(sanitizedTag))
        {
            throw new ArgumentException("Tag is empty after sanitization", nameof(tag));
        }

        var entry = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "tag",
            ["tag"] = sanitizedTag,
            ["sessionId"] = sessionId
        });

        AppendToFile(sessionFile, entry + "\n");
    }

    /// <summary>
    /// Delete a session by removing its JSONL file.
    /// </summary>
    /// <param name="sessionId">UUID of the session.</param>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <exception cref="ArgumentException">If UUID is invalid.</exception>
    /// <exception cref="FileNotFoundException">If session not found.</exception>
    public static void DeleteSession(string sessionId, string? directory = null)
    {
        ValidateUuid(sessionId);

        var sessionFile = Sessions.FindSessionFile(sessionId, directory)
            ?? throw new FileNotFoundException($"Session not found: {sessionId}");

        File.Delete(sessionFile);
    }

    /// <summary>
    /// Fork a session into a new branch with fresh UUIDs.
    /// </summary>
    /// <param name="sessionId">UUID of the source session.</param>
    /// <param name="directory">Project directory path. If null, uses current directory.</param>
    /// <param name="upToMessageId">Slice transcript up to this message UUID (inclusive).</param>
    /// <param name="title">Custom title for the fork. Derived from original if null.</param>
    /// <returns>Result containing the new session UUID.</returns>
    /// <exception cref="ArgumentException">If UUIDs are invalid or no messages to fork.</exception>
    /// <exception cref="FileNotFoundException">If session not found.</exception>
    public static ForkSessionResult ForkSession(
        string sessionId,
        string? directory = null,
        string? upToMessageId = null,
        string? title = null)
    {
        ValidateUuid(sessionId);
        if (upToMessageId != null)
            ValidateUuid(upToMessageId);

        var sessionFile = Sessions.FindSessionFile(sessionId, directory)
            ?? throw new FileNotFoundException($"Session not found: {sessionId}");

        // Parse all entries
        var entries = new List<(Dictionary<string, object?> data, string line)>();
        foreach (var line in File.ReadLines(sessionFile))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            try
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(line);
                if (data != null)
                {
                    entries.Add((data, line));
                }
            }
            catch (JsonException)
            {
                // Skip corrupt lines
            }
        }

        if (entries.Count == 0)
        {
            throw new ArgumentException("Session has no entries to fork");
        }

        // Filter to transcript entries (user/assistant)
        var transcriptEntries = entries
            .Where(e =>
            {
                var type = GetStringValue(e.data, "type");
                return type is "user" or "assistant";
            })
            .ToList();

        if (transcriptEntries.Count == 0)
        {
            throw new ArgumentException("Session has no messages to fork");
        }

        // Slice if upToMessageId provided
        if (upToMessageId != null)
        {
            var idx = transcriptEntries.FindIndex(e =>
                GetStringValue(e.data, "uuid") == upToMessageId);
            if (idx < 0)
            {
                throw new ArgumentException($"Message {upToMessageId} not found in session");
            }
            transcriptEntries = transcriptEntries[..(idx + 1)];
        }

        // Generate new session ID and UUID mapping
        var newSessionId = Guid.NewGuid().ToString();
        var uuidMap = new Dictionary<string, string>();

        foreach (var (data, _) in transcriptEntries)
        {
            var uuid = GetStringValue(data, "uuid");
            if (uuid != null && !uuidMap.ContainsKey(uuid))
            {
                uuidMap[uuid] = Guid.NewGuid().ToString();
            }
        }

        // Build forked entries
        var forkedLines = new List<string>();
        var isLast = false;

        for (var i = 0; i < transcriptEntries.Count; i++)
        {
            isLast = i == transcriptEntries.Count - 1;
            var (data, originalLine) = transcriptEntries[i];

            // Parse as mutable doc
            using var doc = JsonDocument.Parse(originalLine);
            var mutableDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(originalLine)!;

            // Remap UUIDs
            RemapField(mutableDict, "uuid", uuidMap);
            RemapField(mutableDict, "parentUuid", uuidMap);
            RemapField(mutableDict, "logicalParentUuid", uuidMap);

            // Update session ID
            mutableDict["sessionId"] = newSessionId;

            // Remove session-specific fields
            mutableDict.Remove("teamName");
            mutableDict.Remove("agentName");
            mutableDict.Remove("slug");
            mutableDict.Remove("sourceToolAssistantUUID");

            // Set isSidechain to false
            mutableDict["isSidechain"] = false;

            // Add forkedFrom backpointer
            var originalUuid = GetStringValue(data, "uuid");
            mutableDict["forkedFrom"] = new Dictionary<string, string>
            {
                ["sessionId"] = sessionId,
                ["messageUuid"] = originalUuid ?? ""
            };

            // Update timestamp on last message
            if (isLast)
            {
                mutableDict["timestamp"] = DateTimeOffset.UtcNow.ToString("O");
            }

            forkedLines.Add(JsonSerializer.Serialize(mutableDict));
        }

        // Determine title
        var forkTitle = title;
        if (string.IsNullOrEmpty(forkTitle))
        {
            // Read original file to get title
            var (head, tail) = ReadHeadTail(sessionFile);
            forkTitle = ExtractLastJsonStringField(tail, "customTitle")
                ?? ExtractJsonStringField(head, "customTitle")
                ?? ExtractLastJsonStringField(tail, "aiTitle")
                ?? ExtractJsonStringField(head, "aiTitle")
                ?? "Forked session";
            forkTitle += " (fork)";
        }

        // Append custom-title entry
        var titleEntry = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "custom-title",
            ["customTitle"] = forkTitle,
            ["sessionId"] = newSessionId
        });
        forkedLines.Add(titleEntry);

        // Write new session file
        var projectDir = Path.GetDirectoryName(sessionFile)!;
        var newFilePath = Path.Combine(projectDir, $"{newSessionId}.jsonl");

        File.WriteAllText(newFilePath, string.Join("\n", forkedLines) + "\n");

        return new ForkSessionResult { SessionId = newSessionId };
    }

    #region Internal Helpers

    private static void AppendToFile(string filePath, string data)
    {
        using var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var bytes = Encoding.UTF8.GetBytes(data);
        fs.Write(bytes, 0, bytes.Length);
    }

    private static void RemapField(Dictionary<string, object?> dict, string key, Dictionary<string, string> uuidMap)
    {
        if (!dict.TryGetValue(key, out var value))
            return;

        string? strValue = null;
        if (value is string s)
            strValue = s;
        else if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
            strValue = je.GetString();

        if (strValue != null && uuidMap.TryGetValue(strValue, out var mapped))
        {
            dict[key] = mapped;
        }
    }

    private static string? GetStringValue(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value))
            return null;
        if (value is string s)
            return s;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
            return je.GetString();
        return value?.ToString();
    }

    private static (string head, string tail) ReadHeadTail(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var headBuf = new byte[Math.Min(fileInfo.Length, 65536)];
        var headRead = fs.Read(headBuf, 0, headBuf.Length);
        var head = Encoding.UTF8.GetString(headBuf, 0, headRead);

        if (fileInfo.Length <= 65536)
            return (head, head);

        fs.Seek(Math.Max(0, fileInfo.Length - 65536), SeekOrigin.Begin);
        var tailBuf = new byte[65536];
        var tailRead = fs.Read(tailBuf, 0, tailBuf.Length);
        var tail = Encoding.UTF8.GetString(tailBuf, 0, tailRead);

        return (head, tail);
    }

    private static string? ExtractJsonStringField(string text, string key)
    {
        var idx = text.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        if (idx < 0)
            idx = text.IndexOf($"\"{key}\" :", StringComparison.Ordinal);
        if (idx < 0)
            return null;

        var colonIdx = text.IndexOf(':', idx + key.Length + 2);
        if (colonIdx < 0)
            return null;

        var quoteIdx = text.IndexOf('"', colonIdx + 1);
        if (quoteIdx < 0)
            return null;

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
        string? lastValue = null;
        var searchKey = $"\"{key}\":";
        var startIdx = 0;

        while (true)
        {
            var idx = text.IndexOf(searchKey, startIdx, StringComparison.Ordinal);
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

    internal static string SanitizeUnicode(string input)
    {
        var result = input;
        for (var iter = 0; iter < 10; iter++)
        {
            var normalized = result.Normalize(NormalizationForm.FormKC);

            // Remove format, private use, and unassigned characters
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category is UnicodeCategory.Format or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
                    continue;

                // Remove specific zero-width and directional characters
                if (c is >= '\u200B' and <= '\u200F')
                    continue;
                if (c is >= '\u202A' and <= '\u202E')
                    continue;
                if (c is >= '\u2066' and <= '\u2069')
                    continue;
                if (c == '\uFEFF')
                    continue;
                if (c is >= '\uE000' and <= '\uF8FF')
                    continue;

                sb.Append(c);
            }

            var cleaned = sb.ToString();
            if (cleaned == result)
                break;
            result = cleaned;
        }

        return result;
    }

    private static void ValidateUuid(string value)
    {
        if (!Guid.TryParse(value, out _))
        {
            throw new ArgumentException($"Invalid UUID: {value}", nameof(value));
        }
    }

    #endregion
}
