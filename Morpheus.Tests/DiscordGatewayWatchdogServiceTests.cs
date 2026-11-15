using Discord;
using Morpheus.Services;

namespace Morpheus.Tests;

public class DiscordGatewayWatchdogServiceTests
{
    [Fact]
    public void Observe_ExpiresAfterContinuousNonConnectedPeriod()
    {
        DiscordGatewayDisconnectTracker tracker = new(TimeSpan.FromMinutes(5));
        DateTimeOffset start = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

        Assert.False(tracker.Observe(ConnectionState.Disconnected, start));
        Assert.False(tracker.Observe(ConnectionState.Connecting, start.AddMinutes(4)));
        Assert.True(tracker.Observe(ConnectionState.Disconnecting, start.AddMinutes(5)));
        Assert.Equal(start, tracker.DisconnectedSince);
    }

    [Fact]
    public void Observe_ConnectedStateResetsDisconnectTimer()
    {
        DiscordGatewayDisconnectTracker tracker = new(TimeSpan.FromMinutes(5));
        DateTimeOffset start = new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

        Assert.False(tracker.Observe(ConnectionState.Connecting, start));
        Assert.False(tracker.Observe(ConnectionState.Connected, start.AddMinutes(4)));
        Assert.Null(tracker.DisconnectedSince);
        Assert.False(tracker.Observe(ConnectionState.Disconnected, start.AddMinutes(8)));
        Assert.False(tracker.Observe(ConnectionState.Disconnected, start.AddMinutes(12)));
        Assert.True(tracker.Observe(ConnectionState.Disconnected, start.AddMinutes(13)));
    }

    [Fact]
    public async Task ExecuteAsync_StopsApplicationAfterTimeout()
    {
        FakeGatewayStateProvider gateway = new(ConnectionState.Connecting);
        FakeProcessTerminator terminator = new();
        LogsService logs = new(new LogQueue(capacity: 10));
        DiscordGatewayWatchdogOptions options = new(
            Enabled: true,
            DisconnectedTimeout: TimeSpan.FromMilliseconds(30),
            CheckInterval: TimeSpan.FromMilliseconds(5));
        DiscordGatewayWatchdogService service = new(gateway, options, terminator, logs);

        await service.StartAsync(CancellationToken.None);

        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (terminator.ExitCode is null && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        await service.StopAsync(CancellationToken.None);
        Assert.Equal(DiscordGatewayWatchdogService.WatchdogExitCode, terminator.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledWatchdogDoesNotPollGatewayOrExit()
    {
        FakeGatewayStateProvider gateway = new(ConnectionState.Disconnected);
        FakeProcessTerminator terminator = new();
        LogQueue queue = new(capacity: 10);
        DiscordGatewayWatchdogService service = new(
            gateway,
            new DiscordGatewayWatchdogOptions(false, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(15)),
            terminator,
            new LogsService(queue));

        await service.StartAsync(CancellationToken.None);
        await WaitForLogAsync(queue, "watchdog is disabled", TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, gateway.ReadCount);
        Assert.Null(terminator.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_RecoveryClearsTimeoutWhileServiceIsRunning()
    {
        FakeGatewayStateProvider gateway = new(ConnectionState.Disconnected);
        FakeProcessTerminator terminator = new();
        LogQueue queue = new(capacity: 10);
        DiscordGatewayWatchdogService service = new(
            gateway,
            new DiscordGatewayWatchdogOptions(true, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(10)),
            terminator,
            new LogsService(queue));

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForLogAsync(queue, "gateway entered", TimeSpan.FromSeconds(2));
            gateway.ConnectionState = ConnectionState.Connected;
            await WaitForLogAsync(queue, "gateway recovered", TimeSpan.FromSeconds(2));
            await Task.Delay(550);

            Assert.Null(terminator.ExitCode);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForLogAsync(LogQueue queue, string fragment, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (queue.TryDequeue(out QueuedLog? log) && log.Message.Contains(fragment))
                return;
            await Task.Delay(5);
        }

        Assert.Fail($"Watchdog did not log '{fragment}' before the deadline.");
    }

    [Theory]
    [InlineData(29, 15)]
    [InlineData(3601, 15)]
    [InlineData(300, 0)]
    [InlineData(300, 61)]
    [InlineData(30, 30)]
    public void Validate_RejectsUnsafeValues(int timeoutSeconds, int intervalSeconds)
    {
        DiscordGatewayWatchdogOptions options = new(
            Enabled: true,
            DisconnectedTimeout: TimeSpan.FromSeconds(timeoutSeconds),
            CheckInterval: TimeSpan.FromSeconds(intervalSeconds));

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_AcceptsDefaults()
    {
        DiscordGatewayWatchdogOptions options = new(
            Enabled: true,
            DisconnectedTimeout: TimeSpan.FromSeconds(
                DiscordGatewayWatchdogOptions.DefaultDisconnectedTimeoutSeconds),
            CheckInterval: TimeSpan.FromSeconds(
                DiscordGatewayWatchdogOptions.DefaultCheckIntervalSeconds));

        options.Validate();
    }

    private sealed class FakeGatewayStateProvider(ConnectionState state)
        : IDiscordGatewayStateProvider
    {
        private ConnectionState connectionState = state;

        public int ReadCount { get; private set; }

        public ConnectionState ConnectionState
        {
            get
            {
                ReadCount++;
                return connectionState;
            }
            set => connectionState = value;
        }
    }

    private sealed class FakeProcessTerminator : IProcessTerminator
    {
        public int? ExitCode { get; private set; }

        public void Exit(int exitCode) => ExitCode = exitCode;
    }
}
