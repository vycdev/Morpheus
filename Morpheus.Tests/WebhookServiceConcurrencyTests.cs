using Discord;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using System.Reflection;

namespace Morpheus.Tests;

public class WebhookServiceConcurrencyTests
{
    [Fact]
    public async Task GetOrCreateWebhookAsync_WhenAnotherHandlerCreatesWebhook_ReturnsPersistedWebhook()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using (DB setup = new(options))
            await setup.Database.EnsureCreatedAsync();

        await using RacingDb db = new(options);
        db.InsertCompetingWebhookOnNextSave = true;
        LogsService logs = new(new LogQueue());
        WebhookService service = new(db, new DiscordSocketClient(), new DiscordWebhookService(logs), logs);
        ITextChannel channel = DispatchProxy.Create<ITextChannel, TextChannelProxy>();

        Webhook? result = await service.GetOrCreateWebhookAsync(channel);

        Assert.NotNull(result);
        Assert.Equal((ulong)123, result.ChannelDiscordId);
        Assert.Equal((ulong)456, result.WebhookId);
        Assert.Equal("token", result.Token);
        Assert.Equal(1, await db.Webhooks.CountAsync());
    }

    private sealed class RacingDb(DbContextOptions<DB> options) : DB(options)
    {
        public bool InsertCompetingWebhookOnNextSave { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (InsertCompetingWebhookOnNextSave)
            {
                InsertCompetingWebhookOnNextSave = false;
                ChangeTracker.Clear();
                Webhooks.Add(new Webhook
                {
                    GuildDiscordId = 789,
                    ChannelDiscordId = 123,
                    WebhookId = 456,
                    Token = "token"
                });
                await base.SaveChangesAsync(cancellationToken);
                throw new DbUpdateException("Simulated concurrent unique-key conflict.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private class TextChannelProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(ITextChannel.GetWebhooksAsync))
                return Task.FromResult<IReadOnlyCollection<IWebhook>>(Array.Empty<IWebhook>());

            if (targetMethod?.Name == nameof(ITextChannel.CreateWebhookAsync))
                return Task.FromResult<IWebhook>(DispatchProxy.Create<IWebhook, WebhookProxy>());

            return targetMethod?.ReturnType == typeof(ulong)
                ? targetMethod.Name == "get_Id" ? (ulong)123 : (ulong)789
                : targetMethod?.ReturnType == typeof(string) ? string.Empty
                : targetMethod?.ReturnType is { IsValueType: true } type ? Activator.CreateInstance(type)
                : null;
        }
    }

    private class WebhookProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name switch
            {
                "get_Id" => (ulong)456,
                "get_Token" => "token",
                _ => targetMethod?.ReturnType is { IsValueType: true } type ? Activator.CreateInstance(type) : null
            };
    }
}
