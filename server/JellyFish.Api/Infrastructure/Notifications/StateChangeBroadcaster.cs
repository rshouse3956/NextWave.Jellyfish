using System.Collections.Concurrent;
using System.Threading.Channels;

namespace JellyFish.Api.Infrastructure.Notifications;

public interface IStateChangeBroadcaster
{
    ChannelReader<string> Subscribe(CancellationToken ct);
    void Broadcast(string message);
}

public sealed class StateChangeBroadcaster : IStateChangeBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _subscribers = new();

    public ChannelReader<string> Subscribe(CancellationToken ct)
    {
        var ch = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = true
        });
        var id = Guid.NewGuid();
        _subscribers[id] = ch;

        ct.Register(() =>
        {
            _subscribers.TryRemove(id, out _);
            ch.Writer.TryComplete();
        });

        return ch.Reader;
    }

    public void Broadcast(string message)
    {
        foreach (var kvp in _subscribers)
        {
            try { kvp.Value.Writer.TryWrite(message); } catch { /* ignore */ }
        }
    }
}

