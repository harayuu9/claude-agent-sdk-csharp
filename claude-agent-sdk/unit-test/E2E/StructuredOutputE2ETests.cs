using System.Text.Json;
using ClaudeAgentSdk;

namespace unit_test.E2E;

/// <summary>
/// End-to-end tests for structured output with real Claude API calls.
/// These tests verify that the output_schema feature works correctly.
/// Equivalent to Python e2e-tests/test_structured_output.py
/// </summary>
[Trait("Category", "E2E")]
public class StructuredOutputE2ETests : E2ETestBase
{
    /// <summary>
    /// Test structured output with file counting requiring tool use.
    /// Equivalent to Python test_simple_structured_output.
    /// </summary>
    [Fact]
    public async Task SimpleStructuredOutput()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        // Define schema for file analysis
        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["file_count"] = new Dictionary<string, object?> { ["type"] = "number" },
                ["has_tests"] = new Dictionary<string, object?> { ["type"] = "boolean" },
                ["test_file_count"] = new Dictionary<string, object?> { ["type"] = "number" }
            },
            ["required"] = new[] { "file_count", "has_tests" }
        };

        var options = new ClaudeAgentOptions
        {
            OutputFormat = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["schema"] = schema
            },
            PermissionMode = PermissionMode.AcceptEdits,
            Cwd = "."  // Use current directory
        };

        ResultMessage? resultMessage = null;
        await foreach (var message in ClaudeAgent.QueryAsync(
            "Count how many C# files are in the claude-agent-sdk/ directory and check if there are any test files. Use tools to explore the filesystem.",
            options))
        {
            if (message is ResultMessage result)
            {
                resultMessage = result;
            }
        }

        // Verify result
        Assert.NotNull(resultMessage);
        Assert.False(resultMessage.IsError, $"Query failed: {resultMessage.Result}");
        Assert.Equal("success", resultMessage.Subtype);

        // Verify structured output is present and valid
        Assert.NotNull(resultMessage.StructuredOutput);

        var output = GetStructuredOutputAsDictionary(resultMessage.StructuredOutput);
        Assert.True(output.ContainsKey("file_count"));
        Assert.True(output.ContainsKey("has_tests"));

        // Should find C# files
        var fileCount = Convert.ToDouble(output["file_count"]);
        Assert.True(fileCount > 0);
    }

    /// <summary>
    /// Test structured output with nested objects and arrays.
    /// Equivalent to Python test_nested_structured_output.
    /// </summary>
    [Fact]
    public async Task NestedStructuredOutput()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        // Define a schema with nested structure
        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["analysis"] = new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["word_count"] = new Dictionary<string, object?> { ["type"] = "number" },
                        ["character_count"] = new Dictionary<string, object?> { ["type"] = "number" }
                    },
                    ["required"] = new[] { "word_count", "character_count" }
                },
                ["words"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "string" }
                }
            },
            ["required"] = new[] { "analysis", "words" }
        };

        var options = new ClaudeAgentOptions
        {
            OutputFormat = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["schema"] = schema
            },
            PermissionMode = PermissionMode.AcceptEdits
        };

        ResultMessage? resultMessage = null;
        await foreach (var message in ClaudeAgent.QueryAsync(
            "Analyze this text: 'Hello world'. Provide word count, character count, and list of words.",
            options))
        {
            if (message is ResultMessage result)
            {
                resultMessage = result;
            }
        }

        // Verify result
        Assert.NotNull(resultMessage);
        Assert.False(resultMessage.IsError);
        Assert.NotNull(resultMessage.StructuredOutput);

        // Check nested structure
        var output = GetStructuredOutputAsDictionary(resultMessage.StructuredOutput);
        Assert.True(output.ContainsKey("analysis"));
        Assert.True(output.ContainsKey("words"));

        var analysis = GetNestedDictionary(output["analysis"]);
        Assert.Equal(2, Convert.ToInt32(analysis["word_count"]));
        Assert.Equal(11, Convert.ToInt32(analysis["character_count"]));  // "Hello world"
    }

    /// <summary>
    /// Test structured output with enum constraints.
    /// Equivalent to Python test_structured_output_with_enum.
    /// </summary>
    [Fact]
    public async Task StructuredOutputWithEnum()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["has_tests"] = new Dictionary<string, object?> { ["type"] = "boolean" },
                ["test_framework"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { "xunit", "nunit", "mstest", "unknown" }
                },
                ["test_count"] = new Dictionary<string, object?> { ["type"] = "number" }
            },
            ["required"] = new[] { "has_tests", "test_framework" }
        };

        var options = new ClaudeAgentOptions
        {
            OutputFormat = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["schema"] = schema
            },
            PermissionMode = PermissionMode.AcceptEdits,
            Cwd = "."
        };

        ResultMessage? resultMessage = null;
        await foreach (var message in ClaudeAgent.QueryAsync(
            "Search for test files in the unit-test/ directory. Determine which test framework is being used (xunit/nunit/mstest) and count how many test files exist. Use Grep to search for framework imports.",
            options))
        {
            if (message is ResultMessage result)
            {
                resultMessage = result;
            }
        }

        // Verify result
        Assert.NotNull(resultMessage);
        Assert.False(resultMessage.IsError);
        Assert.NotNull(resultMessage.StructuredOutput);

        // Check enum values are valid
        var output = GetStructuredOutputAsDictionary(resultMessage.StructuredOutput);
        var testFramework = output["test_framework"]?.ToString();
        Assert.Contains(testFramework, new[] { "xunit", "nunit", "mstest", "unknown" });

        var hasTests = Convert.ToBoolean(output["has_tests"]);
        Assert.True(hasTests);

        // This repo uses xunit
        Assert.Equal("xunit", testFramework);
    }

    /// <summary>
    /// Test structured output when agent uses tools.
    /// Equivalent to Python test_structured_output_with_tools.
    /// </summary>
    [Fact]
    public async Task StructuredOutputWithTools()
    {
        if (ShouldSkipE2E(out var reason)) { return; }

        // Schema for file analysis
        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["file_count"] = new Dictionary<string, object?> { ["type"] = "number" },
                ["has_readme"] = new Dictionary<string, object?> { ["type"] = "boolean" }
            },
            ["required"] = new[] { "file_count", "has_readme" }
        };

        var options = new ClaudeAgentOptions
        {
            OutputFormat = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["schema"] = schema
            },
            PermissionMode = PermissionMode.AcceptEdits,
            Cwd = Path.GetTempPath()  // Cross-platform temp directory
        };

        ResultMessage? resultMessage = null;
        await foreach (var message in ClaudeAgent.QueryAsync(
            "Count how many files are in the current directory and check if there's a README file. Use tools as needed.",
            options))
        {
            if (message is ResultMessage result)
            {
                resultMessage = result;
            }
        }

        // Verify result
        Assert.NotNull(resultMessage);
        Assert.False(resultMessage.IsError);
        Assert.NotNull(resultMessage.StructuredOutput);

        // Check structure
        var output = GetStructuredOutputAsDictionary(resultMessage.StructuredOutput);
        Assert.True(output.ContainsKey("file_count"));
        Assert.True(output.ContainsKey("has_readme"));

        var fileCount = Convert.ToDouble(output["file_count"]);
        Assert.True(fileCount >= 0);  // Should be non-negative
    }

    #region Helper Methods

    private static Dictionary<string, object?> GetStructuredOutputAsDictionary(object? structuredOutput)
    {
        if (structuredOutput is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (structuredOutput is JsonElement jsonElement)
        {
            return JsonElementToDictionary(jsonElement);
        }

        throw new InvalidOperationException($"Unexpected structured output type: {structuredOutput?.GetType()}");
    }

    private static Dictionary<string, object?> GetNestedDictionary(object? value)
    {
        if (value is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (value is JsonElement jsonElement)
        {
            return JsonElementToDictionary(jsonElement);
        }

        throw new InvalidOperationException($"Unexpected nested value type: {value?.GetType()}");
    }

    private static Dictionary<string, object?> JsonElementToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>();

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = JsonElementToObject(property.Value);
            }
        }

        return result;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => JsonElementToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            _ => element.ToString()
        };
    }

    #endregion
}
