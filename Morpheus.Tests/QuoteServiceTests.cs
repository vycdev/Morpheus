using Microsoft.EntityFrameworkCore;
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
}
