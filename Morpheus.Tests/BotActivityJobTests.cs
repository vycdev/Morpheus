using Discord.WebSocket;
using Morpheus.Jobs;
using Morpheus.Services;
using Quartz;
using System.Reflection;

namespace Morpheus.Tests;

public class BotActivityJobTests
{
    [Theory]
    [InlineData(1, "counting down to the end of time 🎉")]
    [InlineData(3, "fireworks everywhere 🎆")]
    public void Schedule_NewYearsEveTakesPriorityUntilTwoAm(int hour, string expected)
    {
        // Inspect the same ordered schedule and IsActive check used by Execute,
        // without connecting to Discord or introducing a new production API.
        Array schedule = Assert.IsAssignableFrom<Array>(typeof(BotActivityJob)
            .GetField("Schedule", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null));
        DateTime now = new(2027, 1, 1, hour, 0, 0);
        object? active = schedule.Cast<object>().FirstOrDefault(activity =>
            (bool)activity.GetType().GetMethod("IsActive")!.Invoke(activity, [now])!);

        Assert.NotNull(active);
        Assert.Equal(expected, active.GetType().GetProperty("Description")!.GetValue(active));
    }

    [Fact]
    public async Task Execute_WhenCanceledBeforeActivityUpdate_PropagatesCancellation()
    {
        using DiscordSocketClient discordClient = new();
        BotActivityJob job = new(new LogsService(new LogQueue()), discordClient);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        IJobExecutionContext context = CreateContext(cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job.Execute(context));
    }

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken)
    {
        JobExecutionContextProxy.CurrentCancellationToken = cancellationToken;
        return DispatchProxy.Create<IJobExecutionContext, JobExecutionContextProxy>();
    }

    private class JobExecutionContextProxy : DispatchProxy
    {
        public static CancellationToken CurrentCancellationToken { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.ReturnType == typeof(CancellationToken))
                return CurrentCancellationToken;

            Type returnType = targetMethod?.ReturnType ?? typeof(void);
            return returnType == typeof(void) || !returnType.IsValueType
                ? null
                : Activator.CreateInstance(returnType);
        }
    }
}
