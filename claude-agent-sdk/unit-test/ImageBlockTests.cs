using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Internal;
using UnitTest.Helpers;

namespace UnitTest;

/// <summary>
/// Tests for ImageBlock content type support.
/// </summary>
public class ImageBlockTests
{
    private static JsonElement ParseJson(string json) =>
        JsonDocument.Parse(json).RootElement;

    #region ImageBlock Creation Tests

    [Fact]
    public void CreateImageBlockFromBase64()
    {
        var block = ImageBlock.FromBase64("iVBORw0KGgo=", "image/png");

        Assert.IsType<Base64ImageSource>(block.Source);
        var source = (Base64ImageSource)block.Source;
        Assert.Equal("iVBORw0KGgo=", source.Data);
        Assert.Equal("image/png", source.MediaType);
    }

    [Fact]
    public void CreateImageBlockFromUrl()
    {
        var block = ImageBlock.FromUrl("https://example.com/image.png");

        Assert.IsType<UrlImageSource>(block.Source);
        var source = (UrlImageSource)block.Source;
        Assert.Equal("https://example.com/image.png", source.Url);
    }

    [Fact]
    public void CreateImageBlockFromFile()
    {
        // Create a temporary PNG file (minimal valid PNG header)
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.png");
        try
        {
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
            File.WriteAllBytes(tempFile, pngBytes);

            var block = ImageBlock.FromFile(tempFile);

            Assert.IsType<Base64ImageSource>(block.Source);
            var source = (Base64ImageSource)block.Source;
            Assert.Equal("image/png", source.MediaType);
            Assert.Equal(Convert.ToBase64String(pngBytes), source.Data);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CreateImageBlockFromFileAsync()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.jpeg");
        try
        {
            var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic bytes
            await File.WriteAllBytesAsync(tempFile, jpegBytes, TestContext.Current.CancellationToken);

            var block = await ImageBlock.FromFileAsync(tempFile, ct: TestContext.Current.CancellationToken);

            Assert.IsType<Base64ImageSource>(block.Source);
            var source = (Base64ImageSource)block.Source;
            Assert.Equal("image/jpeg", source.MediaType);
            Assert.Equal(Convert.ToBase64String(jpegBytes), source.Data);
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
            ImageBlock.FromFile("/nonexistent/image.png"));
    }

    [Fact]
    public void FromFile_ThrowsOnUnknownExtension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.bmp");
        try
        {
            File.WriteAllBytes(tempFile, [0x42, 0x4D]);

            Assert.Throws<ArgumentException>(() => ImageBlock.FromFile(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_AcceptsExplicitMediaType()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.bmp");
        try
        {
            File.WriteAllBytes(tempFile, [0x42, 0x4D]);

            var block = ImageBlock.FromFile(tempFile, "image/bmp");

            Assert.IsType<Base64ImageSource>(block.Source);
            Assert.Equal("image/bmp", ((Base64ImageSource)block.Source).MediaType);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".jpeg", "image/jpeg")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".webp", "image/webp")]
    public void FromFile_InfersCorrectMediaType(string extension, string expectedMediaType)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}{extension}");
        try
        {
            File.WriteAllBytes(tempFile, [0x00]);

            var block = ImageBlock.FromFile(tempFile);
            Assert.Equal(expectedMediaType, ((Base64ImageSource)block.Source).MediaType);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region UserMessage with Image Tests

    [Fact]
    public void UserMessageWithImageBlock()
    {
        var msg = new UserMessage
        {
            Content =
            [
                new TextBlock { Text = "What is in this image?" },
                ImageBlock.FromBase64("iVBORw0KGgo=", "image/png")
            ]
        };

        Assert.Equal(2, msg.Content.Count);
        Assert.IsType<TextBlock>(msg.Content[0]);
        Assert.IsType<ImageBlock>(msg.Content[1]);
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void SerializeBase64ImageBlock()
    {
        var block = ImageBlock.FromBase64("iVBORw0KGgo=", "image/png");

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("image", root.GetProperty("type").GetString());
        var source = root.GetProperty("source");
        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("image/png", source.GetProperty("media_type").GetString());
        Assert.Equal("iVBORw0KGgo=", source.GetProperty("data").GetString());
    }

    [Fact]
    public void SerializeUrlImageBlock()
    {
        var block = ImageBlock.FromUrl("https://example.com/img.png");

        var json = JsonSerializer.Serialize<ContentBlock>(block);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("image", root.GetProperty("type").GetString());
        var source = root.GetProperty("source");
        Assert.Equal("url", source.GetProperty("type").GetString());
        Assert.Equal("https://example.com/img.png", source.GetProperty("url").GetString());
    }

    [Fact]
    public void DeserializeBase64ImageBlock()
    {
        var json = """{"type":"image","source":{"type":"base64","media_type":"image/jpeg","data":"AQID"}}""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json);

        var imageBlock = Assert.IsType<ImageBlock>(block);
        var source = Assert.IsType<Base64ImageSource>(imageBlock.Source);
        Assert.Equal("image/jpeg", source.MediaType);
        Assert.Equal("AQID", source.Data);
    }

    [Fact]
    public void DeserializeUrlImageBlock()
    {
        var json = """{"type":"image","source":{"type":"url","url":"https://example.com/img.webp"}}""";
        var block = JsonSerializer.Deserialize<ContentBlock>(json);

        var imageBlock = Assert.IsType<ImageBlock>(block);
        var source = Assert.IsType<UrlImageSource>(imageBlock.Source);
        Assert.Equal("https://example.com/img.webp", source.Url);
    }

    #endregion

    #region MessageParser Tests

    [Fact]
    public void ParseUserMessageWithBase64Image()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {"type": "text", "text": "Describe this image"},
                    {
                        "type": "image",
                        "source": {
                            "type": "base64",
                            "media_type": "image/png",
                            "data": "iVBORw0KGgo="
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
        Assert.Equal("Describe this image", textBlock.Text);

        var imageBlock = Assert.IsType<ImageBlock>(userMessage.Content[1]);
        var source = Assert.IsType<Base64ImageSource>(imageBlock.Source);
        Assert.Equal("image/png", source.MediaType);
        Assert.Equal("iVBORw0KGgo=", source.Data);
    }

    [Fact]
    public void ParseUserMessageWithUrlImage()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "image",
                        "source": {
                            "type": "url",
                            "url": "https://example.com/photo.jpg"
                        }
                    }
                ]
            }
        }
        """;

        var message = MessageParser.ParseMessage(ParseJson(json));
        var userMessage = Assert.IsType<UserMessage>(message);
        Assert.Single(userMessage.Content);

        var imageBlock = Assert.IsType<ImageBlock>(userMessage.Content[0]);
        var source = Assert.IsType<UrlImageSource>(imageBlock.Source);
        Assert.Equal("https://example.com/photo.jpg", source.Url);
    }

    [Fact]
    public void ParseImageBlockWithUnknownSourceType_IsIgnored()
    {
        var json = """
        {
            "type": "user",
            "message": {
                "content": [
                    {
                        "type": "image",
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
    public async Task QueryAsyncWithContentBlocks_SendsCorrectFormat()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync(ct: TestContext.Current.CancellationToken);

        var contentBlocks = new ContentBlock[]
        {
            new TextBlock { Text = "What is in this image?" },
            ImageBlock.FromBase64("iVBORw0KGgo=", "image/png")
        };

        await client.QueryAsync(contentBlocks, ct: TestContext.Current.CancellationToken);

        // Find the user message in written messages
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

        // First block: text
        var textBlock = content[0];
        Assert.Equal("text", textBlock.GetProperty("type").GetString());
        Assert.Equal("What is in this image?", textBlock.GetProperty("text").GetString());

        // Second block: image
        var imageBlock = content[1];
        Assert.Equal("image", imageBlock.GetProperty("type").GetString());
        var source = imageBlock.GetProperty("source");
        Assert.Equal("base64", source.GetProperty("type").GetString());
        Assert.Equal("image/png", source.GetProperty("media_type").GetString());
        Assert.Equal("iVBORw0KGgo=", source.GetProperty("data").GetString());

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task QueryAsyncWithContentBlocks_IncludesSessionId()
    {
        var transport = new StreamingMockTransport();
        var client = new ClaudeSDKClient(transport: transport);
        await client.ConnectAsync(ct: TestContext.Current.CancellationToken);

        var contentBlocks = new ContentBlock[]
        {
            new TextBlock { Text = "Hello" },
            ImageBlock.FromUrl("https://example.com/img.png")
        };

        await client.QueryAsync(contentBlocks, sessionId: "test-session-123", ct: TestContext.Current.CancellationToken);

        var userMsg = transport.FindWrittenMessage(msg =>
        {
            if (msg.GetValueOrDefault("type")?.ToString() != "user")
                return false;
            if (msg.GetValueOrDefault("session_id") is not JsonElement sessionElem)
                return false;
            return sessionElem.GetString() == "test-session-123";
        });

        Assert.NotNull(userMsg);

        await client.DisconnectAsync();
    }

    #endregion
}
