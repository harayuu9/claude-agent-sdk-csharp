using System.Runtime.CompilerServices;
using ClaudeAgentSdk.Internal.Transport;

namespace UnitTest.Helpers;

/// <summary>
/// Mock transport for testing.
/// </summary>
internal class MockTransport : Transport
{
    public List<string> WrittenMessages { get; } = new();
    public List<Dictionary<string, object?>> MessagesToRead { get; } = new();
    private bool _connected;

    public override Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _connected = true;
        return Task.CompletedTask;
    }

    public override Task CloseAsync()
    {
        _connected = false;
        return Task.CompletedTask;
    }

    public override Task WriteAsync(string data, CancellationToken cancellationToken = default)
    {
        WrittenMessages.Add(data);
        return Task.CompletedTask;
    }

    public override Task EndInputAsync() => Task.CompletedTask;

    public override async IAsyncEnumerable<Dictionary<string, object?>> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var msg in MessagesToRead)
        {
            yield return msg;
        }
        await Task.CompletedTask;
    }

    public override bool IsReady => _connected;
}
