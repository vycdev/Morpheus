using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Morpheus.Utilities;

namespace Morpheus.Services;

public sealed record DiscordGatewayWatchdogOptions(
    bool Enabled,
    TimeSpan DisconnectedTimeout,
    TimeSpan CheckInterval)
{
    public const int DefaultDisconnectedTimeoutSeconds = 300;
    public const int DefaultCheckIntervalSeconds = 15;

    public static DiscordGatewayWatchdogOptions FromEnvironment() =>
        new(
            Env.Get("DISCORD_GATEWAY_WATCHDOG_ENABLED", true),
            TimeSpan.FromSeconds(Env.Get(
                "DISCORD_GATEWAY_WATCHDOG_TIMEOUT_SECONDS",
                DefaultDisconnectedTimeoutSeconds)),
            TimeSpan.FromSeconds(Env.Get(
                "DISCORD_GATEWAY_WATCHDOG_CHECK_INTERVAL_SECONDS",
                DefaultCheckIntervalSeconds)));

    public void Validate()
    {
        if (DisconnectedTimeout < TimeSpan.FromSeconds(30) ||
            DisconnectedTimeout > TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException(
                "DISCORD_GATEWAY_WATCHDOG_TIMEOUT_SECONDS must be between 30 and 3600.");
        }

        if (CheckInterval < TimeSpan.FromSeconds(1) ||
            CheckInterval > TimeSpan.FromMinutes(1))
        {
            throw new InvalidOperationException(
                "DISCORD_GATEWAY_WATCHDOG_CHECK_INTERVAL_SECONDS must be between 1 and 60.");
        }

        if (CheckInterval >= DisconnectedTimeout)
        {
            throw new InvalidOperationException(
                "DISCORD_GATEWAY_WATCHDOG_CHECK_INTERVAL_SECONDS must be shorter than " +
                "DISCORD_GATEWAY_WATCHDOG_TIMEOUT_SECONDS.");
        }
    }
}

internal interface IDiscordGatewayStateProvider
{
    ConnectionState ConnectionState { get; }
}

internal sealed class DiscordGatewayStateProvider(DiscordSocketClient client)
    : IDiscordGatewayStateProvider
{
    public ConnectionState ConnectionState => client.ConnectionState;
}

internal sealed class DiscordGatewayDisconnectTracker(TimeSpan timeout)
{
    public DateTimeOffset? DisconnectedSince { get; private set; }

    public bool Observe(ConnectionState state, DateTimeOffset observedAt)
    {
        if (state == ConnectionState.Connected)
        {
            DisconnectedSince = null;
            return false;
        }

        DisconnectedSince ??= observedAt;
        return observedAt - DisconnectedSince.Value >= timeout;
    }
}

internal interface IProcessTerminator
{
    void Exit(int exitCode);
}

internal sealed class EnvironmentProcessTerminator : IProcessTerminator
{
    public void Exit(int exitCode) => Environment.Exit(exitCode);
}

internal sealed class DiscordGatewayWatchdogService(
    IDiscordGatewayStateProvider gateway,
    DiscordGatewayWatchdogOptions options,
    IProcessTerminator processTerminator,
    LogsService logsService) : BackgroundService
{
    internal const int WatchdogExitCode = 75;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logsService.Log("Discord gateway watchdog is disabled.", LogSeverity.Warning);
            return;
        }

        DiscordGatewayDisconnectTracker tracker = new(options.DisconnectedTimeout);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConnectionState state = gateway.ConnectionState;
                bool wasDisconnected = tracker.DisconnectedSince.HasValue;
                bool timedOut = tracker.Observe(state, DateTimeOffset.UtcNow);

                if (!wasDisconnected && tracker.DisconnectedSince.HasValue)
                {
                    logsService.Log(
                        $"Discord gateway entered {state}; the process will restart if it does not " +
                        $"recover within {options.DisconnectedTimeout.TotalSeconds:0} seconds.",
                        LogSeverity.Warning);
                }
                else if (wasDisconnected && !tracker.DisconnectedSince.HasValue)
                {
                    logsService.Log("Discord gateway recovered before the watchdog timeout.");
                }

                if (timedOut)
                {
                    logsService.Log(
                        $"Discord gateway remained non-connected ({state}) for " +
                        $"{options.DisconnectedTimeout.TotalSeconds:0} seconds; stopping the process " +
                        "so the container restart policy can recover it.",
                        LogSeverity.Critical);

                    // A graceful host stop can wait indefinitely for an in-flight Quartz job
                    // because the scheduler is configured with WaitForJobsToComplete. This is
                    // the emergency recovery path, so terminate directly after writing the
                    // diagnostic to stdout; Docker's restart policy starts a fresh process.
                    processTerminator.Exit(WatchdogExitCode);
                    return;
                }

                await Task.Delay(options.CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
