using System.Threading.Channels;

namespace Morpheus.Services;

public sealed class LogQueue
{
    private const int DefaultCapacity = 1000;
    private readonly Channel<QueuedLog> channel;

    public LogQueue() : this(DefaultCapacity)
    {
    }

    internal LogQueue(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Log queue capacity must be positive.");

        channel = Channel.CreateBounded<QueuedLog>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    internal ChannelReader<QueuedLog> Reader => channel.Reader;

    internal bool TryEnqueue(QueuedLog log) =>
        channel.Writer.TryWrite(log);

    internal bool TryDequeue(out QueuedLog log) =>
        channel.Reader.TryRead(out log!);
}

internal sealed record QueuedLog(string Message, int Severity, string Version, DateTime InsertDate);
