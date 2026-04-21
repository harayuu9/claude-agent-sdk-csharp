using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Internal;
using UnitTest.Helpers;

namespace UnitTest;

public class DocumentBlockTests
{
    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement;

    #region DocumentBlock Creation Tests

    [Fact]
    public void CreateDocumentBlockFromBase64()
    {
        var block = DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf");

        Assert.IsType<Base64DocumentSource>(block.Source);
        var source = (Base64DocumentSource)block.Source;
        Assert.Equal("JVBERi0xLjQ=", source.Data);
        Assert.Equal("application/pdf", source.MediaType);
    }

    [Fact]
    public void CreateDocumentBlockFromUrl()
    {
        var block = DocumentBlock.FromUrl("https://example.com/doc.pdf");

        Assert.IsType<UrlDocumentSource>(block.Source);
        var source = (UrlDocumentSource)block.Source;
        Assert.Equal("https://example.com/doc.pdf", source.Url);
    }

    [Fact]
    public void CreateDocumentBlockFromText()
    {
        var block = DocumentBlock.FromText("Hello, world!");

        Assert.IsType<PlainTextDocumentSource>(block.Source);
        var source = (PlainTextDocumentSource)block.Source;
        Assert.Equal("Hello, world!", source.Data);
        Assert.Equal("text/plain", source.MediaType);
    }

    [Fact]
    public void CreateDocumentBlockFromTextWithCustomMediaType()
    {
        var csvContent = "name,age\nAlice,30";
        var block = DocumentBlock.FromText(csvContent, "text/csv");

        Assert.IsType<PlainTextDocumentSource>(block.Source);
        var source = (PlainTextDocumentSource)block.Source;
        Assert.Equal(csvContent, source.Data);
        Assert.Equal("text/csv", source.MediaType);
    }

    [Fact]
    public void CreateDocumentBlockFromFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.pdf");
        try
        {
            var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes
            File.WriteAllBytes(tempFile, pdfBytes);

            var block = DocumentBlock.FromFile(tempFile);

            Assert.IsType<Base64DocumentSource>(block.Source);
            var source = (Base64DocumentSource)block.Source;
            Assert.Equal("application/pdf", source.MediaType);
            Assert.Equal(Convert.ToBase64String(pdfBytes), source.Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateDocumentBlockFromFileAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
        try
        {
            var textBytes = "Hello, world!"u8.ToArray();
            await File.WriteAllBytesAsync(tempFile, textBytes, TestContext.Current.CancellationToken);

            var block = await DocumentBlock.FromFileAsync(tempFile, ct: TestContext.Current.CancellationToken);

            Assert.IsType<Base64DocumentSource>(block.Source);
            var source = (Base64DocumentSource)block.Source;
            Assert.Equal("text/plain", source.MediaType);
            Assert.Equal(Convert.ToBase64String(textBytes), source.Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_ThrowsOnMissingFile()
    {
        Assert.Throws<FileNotFoundException>(() =>
            DocumentBlock.FromFile("/nonexistent/doc.pdf"));
    }

    [Fact]
    public void FromFile_ThrowsOnUnknownExtension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xyz");
        try
        {
            File.WriteAllBytes(tempFile, [0x00]);

            Assert.Throws<ArgumentException>(() => DocumentBlock.FromFile(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_AcceptsExplicitMediaType()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xyz");
        try
        {
            File.WriteAllBytes(tempFile, [0x00]);

            var block = DocumentBlock.FromFile(tempFile, "application/octet-stream");

            Assert.IsType<Base64DocumentSource>(block.Source);
            Assert.Equal("application/octet-stream", ((Base64DocumentSource)block.Source).MediaType);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".txt", "text/plain")]
    [InlineData(".csv", "text/csv")]
    [InlineData(".html", "text/html")]
    [InlineData(".htm", "text/html")]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public void FromFile_InfersCorrectMediaType(string extension, string expectedMediaType)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}{extension}");
        try
        {
            File.WriteAllBytes(tempFile, [0x00]);

            var block = DocumentBlock.FromFile(tempFile);
            Assert.Equal(expectedMediaType, ((Base64DocumentSource)block.Source).MediaType);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region DocumentBlock with Optional Properties

    [Fact]
    public void CreateDocumentBlockWithTitle()
    {
        var block = DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf") with
        {
            Title = "My Document"
        };

        Assert.Equal("My Document", block.Title);
    }

    [Fact]
    public void CreateDocumentBlockWithAllOptions()
    {
        var block = DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf") with
        {
            Title = "My Document",
            Context = "This is a financial report",
            Citations = new CitationsConfig { Enabled = true }
        };

        Assert.Equal("My Document", block.Title);
        Assert.Equal("This is a financial report", block.Context);
        Assert.NotNull(block.Citations);
        Assert.True(block.Citations.Enabled);
    }

    #endregion

    #region UserMessage with Document Tests

    [Fact]
    public void UserMessageWithDocumentBlock()
    {
        var msg = new UserMessage
        {
            Content =
            [
                new TextBlock { Text = "Summarize this document" },
                DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf")
            ]
        };

        Assert.Equal(2, msg.Content.Count);
        Assert.IsType<TextBlock>(msg.Content[0]);
        Assert.IsType<DocumentBlock>(msg.Content[1]);
    }

    [Fact]
    public void UserMessageWithMixedContent()
    {
        var msg = new UserMessage
        {
            Content =
            [
                new TextBlock { Text = "Compare this image and document" },
                ImageBlock.FromBase64("iVBORw0KGgo=", "image/png"),
                DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf")
            ]
        };

        Assert.Equal(3, msg.Content.Count);
        Assert.IsType<TextBlock>(msg.Content[0]);
        Assert.IsType<ImageBlock>(msg.Content[1]);
        Assert.IsType<DocumentBlock>(msg.Content[2]);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void SerializeBase64DocumentBlock()
    {
        var block = DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf");

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("document", root.GetProperty("type").GetString());
        var source = root.GetProperty("source");
        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("application/pdf", source.GetProperty("media_type").GetString());
        Assert.Equal("JVBERi0xLjQ=", source.GetProperty("data").GetString());
    }

    [Fact]
    public void SerializeUrlDocumentBlock()
    {
        var block = DocumentBlock.FromUrl("https://example.com/doc.pdf");

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("document", root.GetProperty("type").GetString());
        var source = root.GetProperty("source");
        Assert.Equal("url", source.GetProperty("type").GetString());
        Assert.Equal("https://example.com/doc.pdf", source.GetProperty("url").GetString());
    }

    [Fact]
    public void SerializePlainTextDocumentBlock()
    {
        var block = DocumentBlock.FromText("Hello, world!", "text/plain");

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("document", root.GetProperty("type").GetString());
        var source = root.GetProperty("source");
        Assert.Equal("text", source.GetProperty("type").GetString());
        Assert.Equal("text/plain", source.GetProperty("media_type").GetString());
        Assert.Equal("Hello, world!", source.GetProperty("data").GetString());
    }

    [Fact]
    public void SerializeDocumentBlockWithOptionalFields()
    {
        var block = DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf") with
        {
            Title = "Report",
            Context = "Q4 financial report",
            Citations = new CitationsConfig { Enabled = true }
        };

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Report", root.GetProperty("title").GetString());
        Assert.Equal("Q4 financial report", root.GetProperty("context").GetString());
        Assert.True(root.GetProperty("citations").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void SerializeDocumentBlockWithoutOptionalFields_OmitsThem()
    {
        var block = DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf");

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("title", out _));
        Assert.False(root.TryGetProperty("context", out _));
        Assert.False(root.TryGetProperty("citations", out _));
    }

    [Fact]
    public void DeserializeBase64DocumentBlock()
    {
        var json = """{"type":"document","source":{"type":"base64","media_type":"application/pdf","data":"JVBERi0xLjQ="}}""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json);

        var docBlock = Assert.IsType<DocumentBlock>(block);
        var source = Assert.IsType<Base64DocumentSource>(docBlock.Source);
        Assert.Equal("application/pdf", source.MediaType);
        Assert.Equal("JVBERi0xLjQ=", source.Data);
    }

    [Fact]
    public void DeserializeUrlDocumentBlock()
    {
        var json = """{"type":"document","source":{"type":"url","url":"https://example.com/doc.pdf"}}""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json);

        var docBlock = Assert.IsType<DocumentBlock>(block);
        var source = Assert.IsType<UrlDocumentSource>(docBlock.Source);
        Assert.Equal("https://example.com/doc.pdf", source.Url);
    }

    [Fact]
    public void DeserializePlainTextDocumentBlock()
    {
        var json = """{"type":"document","source":{"type":"text","media_type":"text/csv","data":"name,age\nAlice,30"}}""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json);

        var docBlock = Assert.IsType<DocumentBlock>(block);
        var source = Assert.IsType<PlainTextDocumentSource>(docBlock.Source);
        Assert.Equal("text/csv", source.MediaType);
        Assert.Equal("name,age\nAlice,30", source.Data);
    }

    [Fact]
    public void DeserializeDocumentBlockWithOptionalFields()
    {
        var json = """{"type":"document","source":{"type":"base64","media_type":"application/pdf","data":"JVBERi0xLjQ="},"title":"Report","context":"Q4","citations":{"enabled":true}}""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json);

        var docBlock = Assert.IsType<DocumentBlock>(block);
        Assert.Equal("Report", docBlock.Title);
        Assert.Equal("Q4", docBlock.Context);
        Assert.NotNull(docBlock.Citations);
        Assert.True(docBlock.Citations.Enabled);
    }

    #endregion

    #region MessageParser Tests

    [Fact]
    public void ParseUserMessageWithBase64Document()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {"type": "text", "text": "Summarize this PDF"},
                    {
                        "type": "document",
                        "source": {
                            "type": "base64",
                            "media_type": "application/pdf",
                            "data": "JVBERi0xLjQ="
                        }
                    }
                ]
            }
        }
        """;

        var message = MessageParser.ParseMessage(ParseJson(json));
        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Equal(2, userMessage.Content.Count);

        var textBlock = Assert.IsType<TextBlock>(userMessage.Content[0]);
        Assert.Equal("Summarize this PDF", textBlock.Text);

        var docBlock = Assert.IsType<DocumentBlock>(userMessage.Content[1]);
        var source = Assert.IsType<Base64DocumentSource>(docBlock.Source);
        Assert.Equal("application/pdf", source.MediaType);
        Assert.Equal("JVBERi0xLjQ=", source.Data);
    }

    [Fact]
    public void ParseUserMessageWithUrlDocument()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "document",
                        "source": {
                            "type": "url",
                            "url": "https://example.com/report.pdf"
                        }
                    }
                ]
            }
        }
        """;

        var message = MessageParser.ParseMessage(ParseJson(json));
        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Single(userMessage.Content);

        var docBlock = Assert.IsType<DocumentBlock>(userMessage.Content[0]);
        var source = Assert.IsType<UrlDocumentSource>(docBlock.Source);
        Assert.Equal("https://example.com/report.pdf", source.Url);
    }

    [Fact]
    public void ParseUserMessageWithPlainTextDocument()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "document",
                        "source": {
                            "type": "text",
                            "media_type": "text/plain",
                            "data": "Hello, world!"
                        }
                    }
                ]
            }
        }
        """;

        var message = MessageParser.ParseMessage(ParseJson(json));
        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Single(userMessage.Content);

        var docBlock = Assert.IsType<DocumentBlock>(userMessage.Content[0]);
        var source = Assert.IsType<PlainTextDocumentSource>(docBlock.Source);
        Assert.Equal("text/plain", source.MediaType);
        Assert.Equal("Hello, world!", source.Data);
    }

    [Fact]
    public void ParseDocumentBlockWithTitleAndCitations()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "document",
                        "source": {
                            "type": "base64",
                            "media_type": "application/pdf",
                            "data": "JVBERi0xLjQ="
                        },
                        "title": "Annual Report",
                        "context": "FY2024",
                        "citations": {"enabled": true}
                    }
                ]
            }
        }
        """;

        var message = MessageParser.ParseMessage(ParseJson(json));
        var userMessage = Assert.IsType<UserMessage>(message);
        var docBlock = Assert.IsType<DocumentBlock>(userMessage.Content[0]);

        Assert.Equal("Annual Report", docBlock.Title);
        Assert.Equal("FY2024", docBlock.Context);
        Assert.NotNull(docBlock.Citations);
        Assert.True(docBlock.Citations.Enabled);
    }

    [Fact]
    public void ParseDocumentBlockWithUnknownSourceType_IsIgnored()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "document",
                        "source": {
                            "type": "unknown_source",
                            "data": "abc"
                        }
                    }
                ]
            }
        }
        """;

        var message = MessageParser.ParseMessage(ParseJson(json));
        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Empty(userMessage.Content);
    }

    #endregion

    #region ClaudeSDKClient QueryAsync Tests

    [Fact]
    public async Task QueryAsyncWithDocumentBlock_SendsCorrectFormat()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync(ct: TestContext.Current.CancellationToken);

        var contentBlocks = new ContentBlock[]
        {
            new TextBlock { Text = "Summarize this document" },
            DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf")
        };

        await client.QueryAsync(contentBlocks, ct: TestContext.Current.CancellationToken);

        var userMsg = transport.FindWrittenMessage(msg =>
            msg.GetValueOrDefault("type")?.ToString() == "user" &&
            msg.GetValueOrDefault("message") is JsonElement msgElem &&
            msgElem.TryGetProperty("content", out var c) &&
            c.ValueKind == JsonValueKind.Array);

        Assert.NotNull(userMsg);

        var parsed = JsonDocument.Parse(userMsg.Trim());
        var content = parsed.RootElement
            .GetProperty("message")
            .GetProperty("content");

        Assert.Equal(JsonValueKind.Array, content.ValueKind);
        Assert.Equal(2, content.GetArrayLength());

        var textBlock = content[0];
        Assert.Equal("text", textBlock.GetProperty("type").GetString());
        Assert.Equal("Summarize this document", textBlock.GetProperty("text").GetString());

        var docBlock = content[1];
        Assert.Equal("document", docBlock.GetProperty("type").GetString());
        var source = docBlock.GetProperty("source");
        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("application/pdf", source.GetProperty("media_type").GetString());
        Assert.Equal("JVBERi0xLjQ=", source.GetProperty("data").GetString());

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task QueryAsyncWithDocumentBlockAndOptions_SendsAllFields()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync(ct: TestContext.Current.CancellationToken);

        var contentBlocks = new ContentBlock[]
        {
            new TextBlock { Text = "Summarize" },
            DocumentBlock.FromBase64("JVBERi0xLjQ=", "application/pdf") with
            {
                Title = "Report",
                Context = "Q4 report",
                Citations = new CitationsConfig { Enabled = true }
            }
        };

        await client.QueryAsync(contentBlocks, ct: TestContext.Current.CancellationToken);

        var userMsg = transport.FindWrittenMessage(msg =>
            msg.GetValueOrDefault("type")?.ToString() == "user" &&
            msg.GetValueOrDefault("message") is JsonElement msgElem &&
            msgElem.TryGetProperty("content", out var c) &&
            c.ValueKind == JsonValueKind.Array);

        Assert.NotNull(userMsg);

        var parsed = JsonDocument.Parse(userMsg.Trim());
        var content = parsed.RootElement
            .GetProperty("message")
            .GetProperty("content");

        var docBlock = content[1];
        Assert.Equal("Report", docBlock.GetProperty("title").GetString());
        Assert.Equal("Q4 report", docBlock.GetProperty("context").GetString());
        Assert.True(docBlock.GetProperty("citations").GetProperty("enabled").GetBoolean());

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task QueryAsyncWithPlainTextDocument_SendsCorrectFormat()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync(ct: TestContext.Current.CancellationToken);

        var contentBlocks = new ContentBlock[]
        {
            new TextBlock { Text = "Analyze this CSV" },
            DocumentBlock.FromText("name,age\nAlice,30", "text/csv")
        };

        await client.QueryAsync(contentBlocks, ct: TestContext.Current.CancellationToken);

        var userMsg = transport.FindWrittenMessage(msg =>
            msg.GetValueOrDefault("type")?.ToString() == "user" &&
            msg.GetValueOrDefault("message") is JsonElement msgElem &&
            msgElem.TryGetProperty("content", out var c) &&
            c.ValueKind == JsonValueKind.Array);

        Assert.NotNull(userMsg);

        var parsed = JsonDocument.Parse(userMsg.Trim());
        var content = parsed.RootElement
            .GetProperty("message")
            .GetProperty("content");

        var docBlock = content[1];
        Assert.Equal("document", docBlock.GetProperty("type").GetString());
        var source = docBlock.GetProperty("source");
        Assert.Equal("text", source.GetProperty("type").GetString());
        Assert.Equal("text/csv", source.GetProperty("media_type").GetString());
        Assert.Equal("name,age\nAlice,30", source.GetProperty("data").GetString());

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task QueryAsyncWithUrlDocument_SendsCorrectFormat()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync(ct: TestContext.Current.CancellationToken);

        var contentBlocks = new ContentBlock[]
        {
            new TextBlock { Text = "Read this" },
            DocumentBlock.FromUrl("https://example.com/doc.pdf")
        };

        await client.QueryAsync(contentBlocks, ct: TestContext.Current.CancellationToken);

        var userMsg = transport.FindWrittenMessage(msg =>
            msg.GetValueOrDefault("type")?.ToString() == "user" &&
            msg.GetValueOrDefault("message") is JsonElement msgElem &&
            msgElem.TryGetProperty("content", out var c) &&
            c.ValueKind == JsonValueKind.Array);

        Assert.NotNull(userMsg);

        var parsed = JsonDocument.Parse(userMsg.Trim());
        var content = parsed.RootElement
            .GetProperty("message")
            .GetProperty("content");

        var docBlock = content[1];
        Assert.Equal("document", docBlock.GetProperty("type").GetString());
        var source = docBlock.GetProperty("source");
        Assert.Equal("url", source.GetProperty("type").GetString());
        Assert.Equal("https://example.com/doc.pdf", source.GetProperty("url").GetString());

        await client.DisconnectAsync();
    }

    #endregion
}
