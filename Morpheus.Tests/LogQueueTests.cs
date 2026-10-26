using Morpheus.Services;

namespace Morpheus.Tests;

public class LogQueueTests
{
    [Fact]
    public void TryEnqueue_WhenFullRejectsNewEntryWithoutReplacingQueuedEntries()
    {
        LogQueue queue = new(capacity: 2);
        QueuedLog first = LogsService.CreateGeneralLog("first", Discord.LogSeverity.Info);
        QueuedLog second = LogsService.CreateGeneralLog("second", Discord.LogSeverity.Warning);
        QueuedLog rejected = LogsService.CreateGeneralLog("rejected", Discord.LogSeverity.Error);

        Assert.True(queue.TryEnqueue(first));
        Assert.True(queue.TryEnqueue(second));
        Assert.False(queue.TryEnqueue(rejected));

        Assert.True(queue.TryDequeue(out QueuedLog actualFirst));
        Assert.Same(first, actualFirst);
        Assert.True(queue.TryDequeue(out QueuedLog actualSecond));
        Assert.Same(second, actualSecond);
        Assert.False(queue.TryDequeue(out _));

        // Draining a full queue must allow the next log through.
        Assert.True(queue.TryEnqueue(rejected));
        Assert.True(queue.TryDequeue(out QueuedLog actualNext));
        Assert.Same(rejected, actualNext);
    }
}
