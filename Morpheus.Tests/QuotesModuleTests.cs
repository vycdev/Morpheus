using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Modules;
using Morpheus.Services;
using System.Runtime.CompilerServices;

namespace Morpheus.Tests;

public class QuotesModuleTests
{
    [Fact]
    public void FormatApprovalMessage_KeepsLongRequestsWithinDiscordLimit()
    {
        string message = QuotesModule.FormatApprovalMessage(
            "ADD REQUEST - Quote #1\nSubmitted by: <@123456789012345678>",
            new string('x', 1988),
            "\nApprovals required: 2");

        Assert.Equal(2000, message.Length);
        Assert.Contains("ADD REQUEST - Quote #1", message);
        Assert.EndsWith("…```\nApprovals required: 2", message);
    }

    [Fact]
    public void FormatApprovalMessage_DoesNotSplitSurrogatePairsWhenTruncating()
    {
        const string header = "APPROVED";
        const string footer = "\nDone";
        int availableContentLength = 2000 - $"{header}\n\n```".Length - $"```{footer}".Length;
        string quote = new string('x', availableContentLength - 2) + "😀tail";

        string message = QuotesModule.FormatApprovalMessage(header, quote, footer);

        Assert.Equal(1999, message.Length);
        Assert.EndsWith("x…```\nDone", message);
        AssertContainsOnlyPairedSurrogates(message);
    }

    [Fact]
    public void FormatApprovalMessage_PreservesShortRequests()
    {
        string message = QuotesModule.FormatApprovalMessage(
            "ADD REQUEST - Quote #42\nSubmitted by: <@123>",
            "A short quote",
            "\nApprovals required: 2");

        Assert.Equal(
            "ADD REQUEST - Quote #42\nSubmitted by: <@123>\n\n```A short quote```\nApprovals required: 2",
            message);
    }

    [Fact]
    public async Task ListQuotes_DoesNotSplitSurrogatePairsWhenTruncating()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        Guild guild = new() { DiscordId = 1, Name = "Test guild" };
        User user = new() { DiscordId = 2, Username = "author" };
        db.AddRange(guild, user);
        await db.SaveChangesAsync();
        db.Quotes.Add(new Quote
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = new string('x', 298) + "😀" + new string('x', 10),
            Approved = true
        });
        await db.SaveChangesAsync();

        LogsService logs = new(new LogQueue());
        TestQuotesModule module = new(
            new UsersService(db, logs),
            logs,
            new InteractionsHandler(new DiscordSocketClient()),
            new QuoteService(db));
        SocketCommandContextExtended context =
            (SocketCommandContextExtended)RuntimeHelpers.GetUninitializedObject(
                typeof(SocketCommandContextExtended));
        context.DbGuild = guild;
        ((IModuleBase)module).SetContext(context);

        await module.ListQuotes();

        Embed embed = Assert.IsType<Embed>(module.LastEmbed);
        string fieldValue = Assert.Single(embed.Fields).Value;
        AssertContainsOnlyPairedSurrogates(fieldValue);
    }

    private static void AssertContainsOnlyPairedSurrogates(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                Assert.True(index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]));
                index++;
            }
            else
            {
                Assert.False(char.IsLowSurrogate(value[index]));
            }
        }
    }

    private sealed class TestQuotesModule(
        UsersService usersService,
        LogsService logsService,
        InteractionsHandler interactionHandler,
        QuoteService quoteService) : QuotesModule(usersService, logsService, interactionHandler, quoteService)
    {
        public Embed? LastEmbed { get; private set; }

        protected override Task<IUserMessage> ReplyAsync(
            string? message = null,
            bool isTTS = false,
            Embed? embed = null,
            RequestOptions? options = null,
            AllowedMentions? allowedMentions = null,
            MessageReference? messageReference = null,
            MessageComponent? components = null,
            ISticker[]? stickers = null,
            Embed[]? embeds = null,
            MessageFlags flags = MessageFlags.None)
        {
            LastEmbed = embed;
            return Task.FromResult<IUserMessage>(null!);
        }
    }
}
