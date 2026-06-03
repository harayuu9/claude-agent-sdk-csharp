using ClaudeAgentSdk;

namespace Examples;

/// <summary>
/// Examples demonstrating document input (PDF, TXT, CSV, HTML, DOCX, XLSX) via DocumentBlock.
/// </summary>
public static class DocumentInputExamples
{
    /// <summary>
    /// Send a plain-text CSV inline using <see cref="DocumentBlock.FromText"/>.
    /// This example does not require any file on disk so it always runs.
    /// </summary>
    public static async Task PlainTextCsvExampleAsync()
    {
        Console.WriteLine("=== Document Input: Plain-Text CSV ===");

        const string csv = "name,score\nAlice,90\nBob,82\nCarol,77\n";

        await using var client = new ClaudeSDKClient(new ClaudeAgentOptions
        {
            PermissionMode = PermissionMode.BypassPermissions
        });

        await client.ConnectAsync(new ContentBlock[]
        {
            new TextBlock { Text = "Who has the highest score in this CSV? Reply in one short sentence." },
            DocumentBlock.FromText(csv, "text/csv")
        });

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is AssistantMessage am)
            {
                foreach (var b in am.Content)
                {
                    if (b is TextBlock t)
                        Console.WriteLine($"Claude: {t.Text}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Send a local PDF (or any supported document) using <see cref="DocumentBlock.FromFileAsync"/>.
    /// Set the DOCUMENT_INPUT_PATH env var, or pass a path explicitly when calling this method.
    /// </summary>
    public static async Task LocalFileExampleAsync(string? filePath = null)
    {
        Console.WriteLine("=== Document Input: Local File ===");

        filePath ??= Environment.GetEnvironmentVariable("DOCUMENT_INPUT_PATH");

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine(
                "Skipped: set DOCUMENT_INPUT_PATH to a local PDF/TXT/CSV/HTML/DOCX/XLSX, " +
                "or pass a path to LocalFileExampleAsync(\"...\").");
            Console.WriteLine();
            return;
        }

        var doc = (await DocumentBlock.FromFileAsync(filePath)) with
        {
            Title = Path.GetFileName(filePath),
            Citations = new CitationsConfig { Enabled = true }
        };

        await using var client = new ClaudeSDKClient(new ClaudeAgentOptions
        {
            PermissionMode = PermissionMode.BypassPermissions
        });

        await client.ConnectAsync(new ContentBlock[]
        {
            new TextBlock { Text = "Summarize this document in 2-3 bullet points." },
            doc
        });

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is AssistantMessage am)
            {
                foreach (var b in am.Content)
                {
                    if (b is TextBlock t)
                        Console.WriteLine($"Claude: {t.Text}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Send a remote PDF by URL using <see cref="DocumentBlock.FromUrl"/>.
    /// Pass a URL explicitly or set DOCUMENT_INPUT_URL.
    /// </summary>
    public static async Task UrlExampleAsync(string? url = null)
    {
        Console.WriteLine("=== Document Input: URL ===");

        url ??= Environment.GetEnvironmentVariable("DOCUMENT_INPUT_URL");

        if (string.IsNullOrWhiteSpace(url))
        {
            Console.WriteLine(
                "Skipped: set DOCUMENT_INPUT_URL to a public PDF URL, " +
                "or pass a URL to UrlExampleAsync(\"...\").");
            Console.WriteLine();
            return;
        }

        await using var client = new ClaudeSDKClient(new ClaudeAgentOptions
        {
            PermissionMode = PermissionMode.BypassPermissions
        });

        await client.ConnectAsync(new ContentBlock[]
        {
            new TextBlock { Text = "What is this document about? One short paragraph." },
            DocumentBlock.FromUrl(url)
        });

        await foreach (var msg in client.ReceiveResponseAsync())
        {
            if (msg is AssistantMessage am)
            {
                foreach (var b in am.Content)
                {
                    if (b is TextBlock t)
                        Console.WriteLine($"Claude: {t.Text}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all document input examples.
    /// </summary>
    public static async Task RunAllAsync()
    {
        await PlainTextCsvExampleAsync();
        await LocalFileExampleAsync();
        await UrlExampleAsync();
    }
}
