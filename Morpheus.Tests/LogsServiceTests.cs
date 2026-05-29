using Discord;
using Morpheus.Services;

namespace Morpheus.Tests;

public class LogsServiceTests
{
    [Fact]
    public void FormatGeneralLog_PadsCategoryAndIncludesMessage()
    {
        string log = LogsService.FormatGeneralLog("hello", LogSeverity.Warning);

        Assert.Equal("[General/Warning]    hello", log);
    }

    [Fact]
    public void CreateGeneralLog_CapturesSeverityVersionAndInsertDate()
    {
        DateTime insertDate = new(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);

        QueuedLog log = LogsService.CreateGeneralLog("hello", LogSeverity.Error, insertDate);

        Assert.Equal("[General/Error]      hello", log.Message);
        Assert.Equal((int)LogSeverity.Error, log.Severity);
        Assert.False(string.IsNullOrWhiteSpace(log.Version));
        Assert.Equal(insertDate, log.InsertDate);
    }

    [Fact]
    public void CreateDiscordLog_FormatsNonCommandLogMessages()
    {
        DateTime insertDate = new(2026, 5, 29, 12, 30, 0, DateTimeKind.Utc);
        LogMessage message = new(LogSeverity.Verbose, source: "Gateway", message: "Connected");

        QueuedLog log = LogsService.CreateDiscordLog(message, insertDate);

        Assert.StartsWith("[General/Verbose]    ", log.Message);
        Assert.Contains("Gateway", log.Message);
        Assert.Contains("Connected", log.Message);
        Assert.Equal((int)LogSeverity.Verbose, log.Severity);
        Assert.Equal(insertDate, log.InsertDate);
    }

    [Fact]
    public void Log_EnqueuesFormattedLogWithoutDatabaseWrite()
    {
        LogQueue queue = new(capacity: 1);
        LogsService logsService = new(queue);

        logsService.Log("queued", LogSeverity.Info);

        Assert.True(queue.TryDequeue(out QueuedLog? log));
        Assert.Equal("[General/Info]       queued", log.Message);
        Assert.Equal((int)LogSeverity.Info, log.Severity);
    }

    [Fact]
    public void Log_DiscordMessage_EnqueuesFormattedLog()
    {
        LogQueue queue = new(capacity: 1);
        LogsService logsService = new(queue);
        LogMessage message = new(LogSeverity.Debug, source: "Discord", message: "Heartbeat");

        logsService.Log(message);

        Assert.True(queue.TryDequeue(out QueuedLog? log));
        Assert.StartsWith("[General/Debug]      ", log.Message);
        Assert.Contains("Discord", log.Message);
        Assert.Contains("Heartbeat", log.Message);
        Assert.Equal((int)LogSeverity.Debug, log.Severity);
    }

    [Fact]
    public void TryEnqueue_ReturnsFalseWhenQueueIsFull()
    {
        LogQueue queue = new(capacity: 1);
        QueuedLog first = LogsService.CreateGeneralLog("first", LogSeverity.Info);
        QueuedLog second = LogsService.CreateGeneralLog("second", LogSeverity.Info);

        Assert.True(queue.TryEnqueue(first));
        Assert.False(queue.TryEnqueue(second));
    }

    [Fact]
    public void Constructor_RejectsInvalidQueueCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogQueue(capacity: 0));
    }

    [Fact]
    public void CreateLogEntity_MapsQueuedLogToDatabaseModel()
    {
        DateTime insertDate = new(2026, 5, 29, 13, 0, 0, DateTimeKind.Utc);
        QueuedLog queuedLog = new("message", (int)LogSeverity.Critical, "1.2.3.4", insertDate);

        Database.Models.Log log = LogsWriterService.CreateLogEntity(queuedLog);

        Assert.Equal("message", log.Message);
        Assert.Equal((int)LogSeverity.Critical, log.Severity);
        Assert.Equal("1.2.3.4", log.Version);
        Assert.Equal(insertDate, log.InsertDate);
    }
}
