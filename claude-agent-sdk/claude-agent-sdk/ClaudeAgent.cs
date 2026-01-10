using System.Runtime.CompilerServices;
using ClaudeAgentSdk.Internal;
using ClaudeAgentSdk.Internal.Transport;

namespace ClaudeAgentSdk;

/// <summary>
/// Static entry point for one-shot or unidirectional streaming interactions with Claude Code.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the <see cref="QueryAsync(string, ClaudeAgentOptions?, Transport?, CancellationToken)"/>
/// method for simple, stateless queries where you don't need bidirectional communication or
/// conversation management.
/// </para>
///
/// <para><b>Key characteristics:</b></para>
/// <list type="bullet">
///   <item><b>Unidirectional:</b> Send all messages upfront, receive all responses</item>
///   <item><b>Stateless:</b> Each query is independent, no conversation state</item>
///   <item><b>Simple:</b> Fire-and-forget style, no connection management</item>
///   <item><b>No interrupts:</b> Cannot interrupt or send follow-up messages</item>
/// </list>
///
/// <para><b>When to use QueryAsync:</b></para>
/// <list type="bullet">
///   <item>Simple one-off questions ("What is 2+2?")</item>
///   <item>Batch processing of independent prompts</item>
///   <item>Code generation or analysis tasks</item>
///   <item>Automated scripts and CI/CD pipelines</item>
///   <item>When you know all inputs upfront</item>
/// </list>
/// </remarks>
/// <example>
/// Simple query:
/// <code>
/// await foreach (var message in ClaudeAgent.QueryAsync("What is the capital of France?"))
/// {
///     Console.WriteLine(message);
/// }
/// </code>
/// </example>
/// <example>
/// With options:
/// <code>
/// var options = new ClaudeAgentOptions
/// {
///     SystemPrompt = "You are an expert Python developer",
///     Cwd = "/home/user/project"
/// };
///
/// await foreach (var message in ClaudeAgent.QueryAsync("Create a Python web server", options))
/// {
///     Console.WriteLine(message);
/// }
/// </code>
/// </example>
/// <example>
/// Streaming mode (still unidirectional):
/// <code>
/// async IAsyncEnumerable&lt;Dictionary&lt;string, object?&gt;&gt; GetPrompts()
/// {
///     yield return new Dictionary&lt;string, object?&gt;
///     {
///         ["type"] = "user",
///         ["message"] = new Dictionary&lt;string, object?&gt; { ["role"] = "user", ["content"] = "Hello" }
///     };
///     yield return new Dictionary&lt;string, object?&gt;
///     {
///         ["type"] = "user",
///         ["message"] = new Dictionary&lt;string, object?&gt; { ["role"] = "user", ["content"] = "How are you?" }
///     };
/// }
///
/// // All prompts are sent, then all responses received
/// await foreach (var message in ClaudeAgent.QueryAsync(GetPrompts()))
/// {
///     Console.WriteLine(message);
/// }
/// </code>
/// </example>
public static class ClaudeAgent
{
    /// <summary>
    /// Query Claude Code for one-shot interactions.
    /// </summary>
    /// <param name="prompt">The prompt to send to Claude.</param>
    /// <param name="options">
    /// Optional configuration (defaults to <see cref="ClaudeAgentOptions"/> if null).
    /// Set <see cref="ClaudeAgentOptions.PermissionMode"/> to control tool execution:
    /// <list type="bullet">
    ///   <item><c>Default</c>: CLI prompts for dangerous tools</item>
    ///   <item><c>AcceptEdits</c>: Auto-accept file edits</item>
    ///   <item><c>BypassPermissions</c>: Allow all tools (use with caution)</item>
    /// </list>
    /// Set <see cref="ClaudeAgentOptions.Cwd"/> for working directory.
    /// </param>
    /// <param name="transport">
    /// Optional transport implementation. If provided, this will be used instead of
    /// the default transport selection based on options. The transport will be
    /// automatically configured with the prompt and options.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages from the conversation.</returns>
    public static IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeAgentOptions? options = null,
        Transport? transport = null,
        CancellationToken cancellationToken = default)
    {
        return QueryInternalAsync(prompt, options, transport, cancellationToken);
    }

    /// <summary>
    /// Query Claude Code with streaming mode for continuous interaction.
    /// </summary>
    /// <param name="prompt">
    /// An async enumerable of message dictionaries for streaming mode.
    /// Each dictionary should have the structure:
    /// <code>
    /// {
    ///     "type": "user",
    ///     "message": { "role": "user", "content": "..." },
    ///     "parent_tool_use_id": null,
    ///     "session_id": "..."
    /// }
    /// </code>
    /// </param>
    /// <param name="options">
    /// Optional configuration (defaults to <see cref="ClaudeAgentOptions"/> if null).
    /// </param>
    /// <param name="transport">
    /// Optional transport implementation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of messages from the conversation.</returns>
    public static IAsyncEnumerable<Message> QueryAsync(
        IAsyncEnumerable<Dictionary<string, object?>> prompt,
        ClaudeAgentOptions? options = null,
        Transport? transport = null,
        CancellationToken cancellationToken = default)
    {
        return QueryInternalAsync(prompt, options, transport, cancellationToken);
    }

    private static async IAsyncEnumerable<Message> QueryInternalAsync(
        object prompt,
        ClaudeAgentOptions? options,
        Transport? transport,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        options ??= new ClaudeAgentOptions();

        // Set the entrypoint environment variable
        Environment.SetEnvironmentVariable("CLAUDE_CODE_ENTRYPOINT", "sdk-csharp");

        var client = new InternalClient();

        await foreach (var message in client.ProcessQueryAsync(prompt, options, transport, cancellationToken))
        {
            yield return message;
        }
    }
}
