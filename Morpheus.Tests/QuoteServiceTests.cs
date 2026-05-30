using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class QuoteServiceTests
{
    [Theory]
    [InlineData(1, -5)]
    [InlineData(5, -1)]
    [InlineData(6, 1)]
    [InlineData(10, 5)]
    public void MapRatingToScore_MapsOneToTenOntoMinusFiveToPlusFive(int rating, int expectedScore)
    {
        Assert.Equal(expectedScore, QuoteService.MapRatingToScore(rating));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void MapRatingToScore_RejectsOutOfRangeRatings(int rating)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => QuoteService.MapRatingToScore(rating));
    }

    [Theory]
    [InlineData(-2, 0, 1, 1)]
    [InlineData(0, 25, 1, 3)]
    [InlineData(4, 25, 3, 3)]
    public void NormalizePage_ClampsToAvailablePages(int requestedPage, int total, int expectedPage, int expectedTotalPages)
    {
        (int page, int totalPages) = QuoteService.NormalizePage(requestedPage, total);

        Assert.Equal(expectedPage, page);
        Assert.Equal(expectedTotalPages, totalPages);
    }

    [Fact]
    public void FormatQuoteListFieldName_IncludesScoreStatusAuthorAndGuildForGlobalLists()
    {
        QuoteListItem item = new(
            Id: 42,
            GuildId: 7,
            UserId: 3,
            Content: "hello",
            InsertDate: new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc),
            Approved: false,
            Removed: true,
            Score: -2,
            Author: "vycto");

        string fieldName = QuoteService.FormatQuoteListFieldName(item, global: true);

        Assert.Equal("#42 - Score: -2 - Pending (Removed) - vycto - Guild: 7", fieldName);
    }

    [Fact]
    public void FormatQuoteListFieldValue_TruncatesLongContent()
    {
        QuoteListItem item = new(
            Id: 1,
            GuildId: 1,
            UserId: 1,
            Content: new string('x', 305),
            InsertDate: new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc),
            Approved: true,
            Removed: false,
            Score: 0,
            Author: "author");

        string fieldValue = QuoteService.FormatQuoteListFieldValue(item);

        string firstLine = fieldValue.Split('\n')[0];
        Assert.Equal(300, firstLine.Length);
        Assert.EndsWith("...", firstLine);
        Assert.Contains("Inserted: 2026-05-30 12:00:00Z", fieldValue);
    }

    [Fact]
    public void GetPreviousPeriodBounds_ReturnsPreviousMonthWindow()
    {
        DateTime now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

        (DateTime since, DateTime until) = QuoteService.GetPreviousPeriodBounds("month", now);

        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), since);
        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), until);
    }

    [Theory]
    [InlineData(-5, "-5")]
    [InlineData(0, "+0")]
    [InlineData(5, "+5")]
    public void FormatSignedScore_AddsPlusForNonNegativeScores(int score, string expected)
    {
        Assert.Equal(expected, QuoteService.FormatSignedScore(score));
    }

    [Fact]
    public void DbModel_ConfiguresOneScorePerQuoteAndUser()
    {
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseNpgsql("Host=localhost;Database=morpheus_test;Username=test;Password=test")
            .Options;
        using DB db = new(options);

        var quoteScore = db.Model.FindEntityType(typeof(QuoteScore))
            ?? throw new InvalidOperationException("QuoteScore is missing from the EF model.");
        var index = Assert.Single(quoteScore.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["QuoteId", "UserId"]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void ApplyScore_ForNewScore_SetsScoreAndInsertDate()
    {
        DateTime now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        QuoteScore quoteScore = new();

        QuoteService.ApplyScore(quoteScore, 4, now, isNew: true);

        Assert.Equal(4, quoteScore.Score);
        Assert.Equal(now, quoteScore.InsertDate);
        Assert.Null(quoteScore.UpdateDate);
    }

    [Fact]
    public void ApplyScore_ForExistingScore_SetsScoreAndUpdateDate()
    {
        DateTime insertDate = new(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        DateTime updateDate = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);
        QuoteScore quoteScore = new()
        {
            Score = -2,
            InsertDate = insertDate
        };

        QuoteService.ApplyScore(quoteScore, 5, updateDate, isNew: false);

        Assert.Equal(5, quoteScore.Score);
        Assert.Equal(insertDate, quoteScore.InsertDate);
        Assert.Equal(updateDate, quoteScore.UpdateDate);
    }

    [Fact]
    public void IsApprovalExpired_ExpiresAfterConfiguredWindow()
    {
        DateTime insertDate = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(QuoteService.IsApprovalExpired(insertDate, 5, insertDate.AddDays(5)));
        Assert.True(QuoteService.IsApprovalExpired(insertDate, 5, insertDate.AddDays(5).AddTicks(1)));
    }

    [Fact]
    public void GetRequiredApprovals_UsesApprovalType()
    {
        Guild guild = new()
        {
            QuoteAddRequiredApprovals = 3,
            QuoteRemoveRequiredApprovals = 7
        };

        Assert.Equal(3, QuoteService.GetRequiredApprovals(QuoteApprovalType.AddRequest, guild));
        Assert.Equal(7, QuoteService.GetRequiredApprovals(QuoteApprovalType.RemoveRequest, guild));
    }

    [Fact]
    public void ApplyApprovalResolution_ForAddRequest_ApprovesQuote()
    {
        Quote quote = new()
        {
            Approved = false,
            Removed = false
        };
        QuoteApprovalMessage approval = new()
        {
            Type = QuoteApprovalType.AddRequest,
            Approved = false
        };

        QuoteService.ApplyApprovalResolution(approval, quote);

        Assert.True(approval.Approved);
        Assert.True(quote.Approved);
        Assert.False(quote.Removed);
    }

    [Fact]
    public void ApplyApprovalResolution_ForRemoveRequest_RemovesQuote()
    {
        Quote quote = new()
        {
            Approved = true,
            Removed = false
        };
        QuoteApprovalMessage approval = new()
        {
            Type = QuoteApprovalType.RemoveRequest,
            Approved = false
        };

        QuoteService.ApplyApprovalResolution(approval, quote);

        Assert.True(approval.Approved);
        Assert.True(quote.Approved);
        Assert.True(quote.Removed);
    }

    [Fact]
    public void QuoteApprovalResult_FinalizedMarksVoteRecorded()
    {
        QuoteApprovalResult result = QuoteApprovalResult.Finalized(
            currentApprovals: 5,
            requiredApprovals: 5,
            type: QuoteApprovalType.AddRequest,
            quoteId: 10,
            quoteContent: "hello",
            approvalMessageId: 123,
            quotesApprovalChannelId: 456);

        Assert.True(result.VoteRecorded);
        Assert.True(result.IsFinalized);
        Assert.Equal(QuoteApprovalResultStatus.Finalized, result.Status);
    }

    [Fact]
    public void QuoteAddRequestResult_PendingApprovalRequiresApprovalMessage()
    {
        QuoteAddRequestResult result = QuoteAddRequestResult.PendingApproval(
            quoteId: 10,
            quoteContent: "hello",
            approvalId: 20,
            requiredApprovals: 3,
            approvalChannelId: 30);

        Assert.True(result.RequiresApprovalMessage);
        Assert.Equal(QuoteAddRequestStatus.PendingApproval, result.Status);
        Assert.Equal(20, result.ApprovalId);
    }

    [Fact]
    public void QuoteRemoveRequestResult_NonPendingStatusesDoNotRequireApprovalMessage()
    {
        QuoteRemoveRequestResult removed = QuoteRemoveRequestResult.Removed(10, "hello");
        QuoteRemoveRequestResult notFound = QuoteRemoveRequestResult.NotFound();

        Assert.False(removed.RequiresApprovalMessage);
        Assert.False(notFound.RequiresApprovalMessage);
        Assert.Equal(QuoteRemoveRequestStatus.Removed, removed.Status);
        Assert.Equal(QuoteRemoveRequestStatus.NotFound, notFound.Status);
    }

    [Fact]
    public async Task CreateAddRequestAsync_WithApprovalChannel_CreatesPendingQuoteAndApproval()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db, approvalChannelId: 123, addRequiredApprovals: 4);
        QuoteService service = new(testDb.Db);

        QuoteAddRequestResult result = await service.CreateAddRequestAsync(
            guild.Id,
            user.Id,
            "pending quote",
            isAdmin: false,
            forceFlag: false,
            guild.QuotesApprovalChannelId,
            guild.QuoteAddRequiredApprovals);

        Assert.Equal(QuoteAddRequestStatus.PendingApproval, result.Status);
        Assert.Equal(4, result.RequiredApprovals);
        Assert.Equal(123UL, result.ApprovalChannelId);

        Quote quote = await testDb.Db.Quotes.AsNoTracking().SingleAsync();
        Assert.Equal(result.QuoteId, quote.Id);
        Assert.Equal("pending quote", quote.Content);
        Assert.False(quote.Approved);
        Assert.False(quote.Removed);

        QuoteApprovalMessage approval = await testDb.Db.QuoteApprovalMessages.AsNoTracking().SingleAsync();
        Assert.Equal(result.ApprovalId, approval.Id);
        Assert.Equal(quote.Id, approval.QuoteId);
        Assert.Equal(QuoteApprovalType.AddRequest, approval.Type);
        Assert.Equal(0UL, approval.ApprovalMessageId);
    }

    [Fact]
    public async Task CreateAddRequestAsync_AdminWithoutApprovalChannel_ApprovesImmediately()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db);
        QuoteService service = new(testDb.Db);

        QuoteAddRequestResult result = await service.CreateAddRequestAsync(
            guild.Id,
            user.Id,
            "admin quote",
            isAdmin: true,
            forceFlag: false,
            guild.QuotesApprovalChannelId,
            guild.QuoteAddRequiredApprovals);

        Assert.Equal(QuoteAddRequestStatus.Approved, result.Status);

        Quote quote = await testDb.Db.Quotes.AsNoTracking().SingleAsync();
        Assert.True(quote.Approved);
        Assert.False(quote.Removed);
        Assert.Empty(await testDb.Db.QuoteApprovalMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateAddRequestAsync_NonAdminWithoutApprovalChannel_PersistsPendingQuoteOnly()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db);
        QuoteService service = new(testDb.Db);

        QuoteAddRequestResult result = await service.CreateAddRequestAsync(
            guild.Id,
            user.Id,
            "manual approval quote",
            isAdmin: false,
            forceFlag: false,
            guild.QuotesApprovalChannelId,
            guild.QuoteAddRequiredApprovals);

        Assert.Equal(QuoteAddRequestStatus.PendingWithoutApprovalChannel, result.Status);

        Quote quote = await testDb.Db.Quotes.AsNoTracking().SingleAsync();
        Assert.False(quote.Approved);
        Assert.False(quote.Removed);
        Assert.Empty(await testDb.Db.QuoteApprovalMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RecordApprovalMessageIdAsync_PersistsDiscordMessageId()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db, approvalChannelId: 123);
        QuoteService service = new(testDb.Db);
        QuoteAddRequestResult request = await service.CreateAddRequestAsync(
            guild.Id,
            user.Id,
            "needs message id",
            isAdmin: false,
            forceFlag: false,
            guild.QuotesApprovalChannelId,
            guild.QuoteAddRequiredApprovals);

        bool recorded = await service.RecordApprovalMessageIdAsync(request.ApprovalId, 999UL);

        Assert.True(recorded);
        QuoteApprovalMessage approval = await testDb.Db.QuoteApprovalMessages.AsNoTracking().SingleAsync();
        Assert.Equal(999UL, approval.ApprovalMessageId);
    }

    [Fact]
    public async Task AbandonApprovalRequestAsync_ForUnpostedAddRequest_RemovesQuoteAndApproval()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db, approvalChannelId: 123);
        QuoteService service = new(testDb.Db);
        QuoteAddRequestResult request = await service.CreateAddRequestAsync(
            guild.Id,
            user.Id,
            "abandoned quote",
            isAdmin: false,
            forceFlag: false,
            guild.QuotesApprovalChannelId,
            guild.QuoteAddRequiredApprovals);

        bool abandoned = await service.AbandonApprovalRequestAsync(request.ApprovalId);

        Assert.True(abandoned);
        Assert.Empty(await testDb.Db.Quotes.AsNoTracking().ToListAsync());
        Assert.Empty(await testDb.Db.QuoteApprovalMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateRemoveRequestAsync_WithApprovalChannel_CreatesPendingRemoveApproval()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db, approvalChannelId: 456, removeRequiredApprovals: 2);
        Quote quote = await SeedQuoteAsync(testDb.Db, guild, user, approved: true);
        QuoteService service = new(testDb.Db);

        QuoteRemoveRequestResult result = await service.CreateRemoveRequestAsync(
            guild.Id,
            quote.Id,
            isAdmin: false,
            forceFlag: false,
            guild.QuotesApprovalChannelId,
            guild.QuoteRemoveRequiredApprovals);

        Assert.Equal(QuoteRemoveRequestStatus.PendingApproval, result.Status);
        Assert.Equal(2, result.RequiredApprovals);
        Assert.Equal(456UL, result.ApprovalChannelId);

        Quote updatedQuote = await testDb.Db.Quotes.AsNoTracking().SingleAsync();
        Assert.False(updatedQuote.Removed);

        QuoteApprovalMessage approval = await testDb.Db.QuoteApprovalMessages.AsNoTracking().SingleAsync();
        Assert.Equal(quote.Id, approval.QuoteId);
        Assert.Equal(QuoteApprovalType.RemoveRequest, approval.Type);
    }

    [Fact]
    public async Task CreateRemoveRequestAsync_AdminForce_RemovesUnapprovedQuoteImmediately()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db, approvalChannelId: 456);
        Quote quote = await SeedQuoteAsync(testDb.Db, guild, user, approved: false);
        QuoteService service = new(testDb.Db);

        QuoteRemoveRequestResult result = await service.CreateRemoveRequestAsync(
            guild.Id,
            quote.Id,
            isAdmin: true,
            forceFlag: true,
            guild.QuotesApprovalChannelId,
            guild.QuoteRemoveRequiredApprovals);

        Assert.Equal(QuoteRemoveRequestStatus.Removed, result.Status);
        Quote removedQuote = await testDb.Db.Quotes.AsNoTracking().SingleAsync();
        Assert.True(removedQuote.Removed);
        Assert.Empty(await testDb.Db.QuoteApprovalMessages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateRemoveRequestAsync_ReturnsValidationStatuses()
    {
        await using SqliteTestDb testDb = await CreateSqliteDbAsync();
        (User user, Guild guild) = await SeedUserAndGuildAsync(testDb.Db, approvalChannelId: 456);
        Guild otherGuild = new() { DiscordId = 789, Name = "Other guild" };
        testDb.Db.Guilds.Add(otherGuild);
        await testDb.Db.SaveChangesAsync();
        Quote wrongGuildQuote = await SeedQuoteAsync(testDb.Db, otherGuild, user, approved: true, content: "wrong guild");
        Quote removedQuote = await SeedQuoteAsync(testDb.Db, guild, user, approved: true, removed: true, content: "removed");
        Quote pendingQuote = await SeedQuoteAsync(testDb.Db, guild, user, approved: false, content: "pending");
        QuoteService service = new(testDb.Db);

        QuoteRemoveRequestResult notFound = await service.CreateRemoveRequestAsync(guild.Id, 999, false, false, 456, 2);
        QuoteRemoveRequestResult wrongGuild = await service.CreateRemoveRequestAsync(guild.Id, wrongGuildQuote.Id, false, false, 456, 2);
        QuoteRemoveRequestResult alreadyRemoved = await service.CreateRemoveRequestAsync(guild.Id, removedQuote.Id, false, false, 456, 2);
        QuoteRemoveRequestResult notApproved = await service.CreateRemoveRequestAsync(guild.Id, pendingQuote.Id, false, false, 456, 2);

        Assert.Equal(QuoteRemoveRequestStatus.NotFound, notFound.Status);
        Assert.Equal(QuoteRemoveRequestStatus.WrongGuild, wrongGuild.Status);
        Assert.Equal(QuoteRemoveRequestStatus.AlreadyRemoved, alreadyRemoved.Status);
        Assert.Equal(QuoteRemoveRequestStatus.NotApproved, notApproved.Status);
    }

    private static async Task<SqliteTestDb> CreateSqliteDbAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        return new SqliteTestDb(connection, db);
    }

    private static async Task<(User User, Guild Guild)> SeedUserAndGuildAsync(
        DB db,
        ulong approvalChannelId = 0,
        int addRequiredApprovals = 3,
        int removeRequiredApprovals = 3)
    {
        User user = new()
        {
            DiscordId = 123,
            Username = "tester"
        };
        Guild guild = new()
        {
            DiscordId = 456,
            Name = "Test guild",
            QuotesApprovalChannelId = approvalChannelId,
            QuoteAddRequiredApprovals = addRequiredApprovals,
            QuoteRemoveRequiredApprovals = removeRequiredApprovals
        };

        await db.Users.AddAsync(user);
        await db.Guilds.AddAsync(guild);
        await db.SaveChangesAsync();

        return (user, guild);
    }

    private static async Task<Quote> SeedQuoteAsync(
        DB db,
        Guild guild,
        User user,
        bool approved,
        bool removed = false,
        string content = "quote")
    {
        Quote quote = new()
        {
            GuildId = guild.Id,
            UserId = user.Id,
            Content = content,
            Approved = approved,
            Removed = removed
        };

        await db.Quotes.AddAsync(quote);
        await db.SaveChangesAsync();
        return quote;
    }

    private sealed class SqliteTestDb(SqliteConnection connection, DB db) : IAsyncDisposable
    {
        public DB Db { get; } = db;

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
