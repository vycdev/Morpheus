using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;

namespace Morpheus.Services;

public class QuoteService(DB dbContext)
{
    internal const int PageSize = 10;
    private const int MaxListContentLength = 300;

    public async Task<QuotePage> GetQuotePageAsync(int page, string sort, bool approvedOnly, int? guildId)
    {
        IQueryable<Quote> quotesQuery = dbContext.Quotes
            .AsNoTracking()
            .Where(quote => !quote.Removed);

        if (guildId.HasValue)
            quotesQuery = quotesQuery.Where(quote => quote.GuildId == guildId.Value);

        if (approvedOnly)
            quotesQuery = quotesQuery.Where(quote => quote.Approved);

        int total = await quotesQuery.CountAsync();
        (int currentPage, int totalPages) = NormalizePage(page, total);

        quotesQuery = ApplySort(quotesQuery, sort);

        List<Quote> quotes = await quotesQuery
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        if (quotes.Count == 0)
            return new QuotePage(currentPage, totalPages, total, []);

        List<int> quoteIds = [.. quotes.Select(quote => quote.Id)];
        Dictionary<int, int> scoreMap = await GetScoresByQuoteIdAsync(quoteIds);

        List<int> userIds = [.. quotes.Select(quote => quote.UserId).Distinct()];
        Dictionary<int, string> userMap = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Username);

        List<QuoteListItem> items = [.. quotes.Select(quote => new QuoteListItem(
            quote.Id,
            quote.GuildId,
            quote.UserId,
            quote.Content ?? string.Empty,
            quote.InsertDate,
            quote.Approved,
            quote.Removed,
            scoreMap.GetValueOrDefault(quote.Id),
            userMap.GetValueOrDefault(quote.UserId, "Unknown")))];

        return new QuotePage(currentPage, totalPages, total, items);
    }

    public async Task<QuoteDetails?> GetQuoteDetailsAsync(int quoteId)
    {
        Quote? quote = await dbContext.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(quote => quote.Id == quoteId && !quote.Removed);

        if (quote == null)
            return null;

        int totalScore = await GetTotalScoreAsync(quote.Id);
        string author = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == quote.UserId)
            .Select(user => user.Username)
            .FirstOrDefaultAsync() ?? "Unknown";

        return new QuoteDetails(
            quote.Id,
            quote.GuildId,
            quote.Content ?? string.Empty,
            quote.InsertDate,
            quote.Approved,
            quote.Removed,
            totalScore,
            author);
    }

    public async Task<QuoteScoreResult> ScoreQuoteByContentAsync(
        string quoteContent,
        int guildId,
        bool useGlobalQuotes,
        int userId,
        int score,
        DateTime? utcNow = null)
    {
        Quote? quote = await FindApprovedQuoteByContentAsync(quoteContent, guildId, useGlobalQuotes);
        if (quote == null)
            return QuoteScoreResult.NotFound("Couldn't find a matching quote.");

        DateTime now = utcNow ?? DateTime.UtcNow;
        QuoteScore? existing = await dbContext.QuoteScores
            .FirstOrDefaultAsync(existingScore => existingScore.QuoteId == quote.Id && existingScore.UserId == userId);

        if (existing == null)
        {
            QuoteScore quoteScore = new()
            {
                QuoteId = quote.Id,
                UserId = userId,
                Score = score,
                InsertDate = now
            };

            await dbContext.QuoteScores.AddAsync(quoteScore);
        }
        else
        {
            existing.Score = score;
            existing.UpdateDate = now;
        }

        await dbContext.SaveChangesAsync();

        int totalScore = await GetTotalScoreAsync(quote.Id);
        return QuoteScoreResult.Success(quote.Id, score, totalScore);
    }

    public async Task<QuotePeriodResult> GetTopQuoteSinceAsync(DateTime since, DateTime until, int? guildId)
    {
        IQueryable<QuoteScore> baseScores = dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => (score.UpdateDate ?? score.InsertDate) >= since && (score.UpdateDate ?? score.InsertDate) < until);

        var joined = baseScores.Join(
            dbContext.Quotes.AsNoTracking().Where(quote => !quote.Removed),
            score => score.QuoteId,
            quote => quote.Id,
            (score, quote) => new { Score = score, Quote = quote });

        if (guildId.HasValue)
            joined = joined.Where(item => item.Quote.GuildId == guildId.Value);

        var grouped = await joined
            .GroupBy(item => item.Quote.Id)
            .Select(group => new { QuoteId = group.Key, Score = group.Sum(item => item.Score.Score) })
            .OrderByDescending(item => item.Score)
            .FirstOrDefaultAsync();

        if (grouped == null)
            return QuotePeriodResult.Empty;

        Quote? quote = await dbContext.Quotes
            .AsNoTracking()
            .FirstOrDefaultAsync(quote => quote.Id == grouped.QuoteId && !quote.Removed);

        return quote == null
            ? QuotePeriodResult.Empty
            : new QuotePeriodResult(quote.Id, quote.Content ?? string.Empty, grouped.Score);
    }

    public static (DateTime Since, DateTime Until) GetPreviousPeriodBounds(string period, DateTime? utcNow = null)
    {
        DateTime now = utcNow ?? DateTime.UtcNow;
        DateTime currentStart = period.ToLowerInvariant() switch
        {
            "day" => now.Date,
            "week" => now.Date.AddDays(-(int)now.DayOfWeek),
            "month" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => now.Date
        };

        return period.ToLowerInvariant() switch
        {
            "day" => (currentStart.AddDays(-1), currentStart),
            "week" => (currentStart.AddDays(-7), currentStart),
            "month" => (currentStart.AddMonths(-1), currentStart),
            _ => (currentStart.AddDays(-1), currentStart)
        };
    }

    public static int MapRatingToScore(int rating)
    {
        if (rating is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 10.");

        double mapped = -5 + (rating - 1) * (10.0 / 9.0);
        return Math.Clamp((int)Math.Round(mapped), -5, 5);
    }

    internal static (int Page, int TotalPages) NormalizePage(int page, int total)
    {
        int totalPages = (int)Math.Ceiling(total / (double)PageSize);
        if (totalPages == 0)
            totalPages = 1;

        if (page < 1)
            page = 1;
        if (page > totalPages)
            page = totalPages;

        return (page, totalPages);
    }

    internal static string FormatQuoteListTitle(bool global, QuotePage page) =>
        $"{(global ? "Global Quotes" : "Quotes")} - Page {page.Page}/{page.TotalPages} ({page.Total} total)";

    internal static string FormatQuoteListFieldName(QuoteListItem item, bool global)
    {
        string fieldName = $"#{item.Id} - Score: {item.Score} - {FormatStatus(item.Approved, item.Removed)} - {item.Author}";
        return global
            ? $"{fieldName} - Guild: {item.GuildId}"
            : fieldName;
    }

    internal static string FormatQuoteListFieldValue(QuoteListItem item) =>
        $"{TruncateContent(item.Content, MaxListContentLength)}\nInserted: {item.InsertDate:u}";

    internal static string FormatStatus(bool approved, bool removed)
    {
        string status = approved ? "Approved" : "Pending";
        if (removed)
            status += " (Removed)";

        return status;
    }

    internal static string FormatSignedScore(int score) =>
        score >= 0 ? $"+{score}" : score.ToString();

    private static string TruncateContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;

        return content[..(maxLength - 3)] + "...";
    }

    private async Task<Quote?> FindApprovedQuoteByContentAsync(string quoteContent, int guildId, bool useGlobalQuotes)
    {
        IQueryable<Quote> query = dbContext.Quotes
            .Where(quote => quote.Approved && !quote.Removed && (quote.Content ?? string.Empty) == quoteContent);

        if (!useGlobalQuotes)
            query = query.Where(quote => quote.GuildId == guildId);

        return await query.FirstOrDefaultAsync();
    }

    private async Task<Dictionary<int, int>> GetScoresByQuoteIdAsync(IReadOnlyCollection<int> quoteIds)
    {
        var scores = await dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => quoteIds.Contains(score.QuoteId))
            .GroupBy(score => score.QuoteId)
            .Select(group => new { Id = group.Key, Score = group.Sum(score => score.Score) })
            .ToListAsync();

        return scores.ToDictionary(score => score.Id, score => score.Score);
    }

    private async Task<int> GetTotalScoreAsync(int quoteId) =>
        await dbContext.QuoteScores
            .AsNoTracking()
            .Where(score => score.QuoteId == quoteId)
            .SumAsync(score => (int?)score.Score) ?? 0;

    private IQueryable<Quote> ApplySort(IQueryable<Quote> quotesQuery, string sort) =>
        sort.ToLowerInvariant() switch
        {
            "top" or "top-rated" or "toprated" => quotesQuery
                .OrderByDescending(quote => dbContext.QuoteScores
                    .Where(score => score.QuoteId == quote.Id)
                    .Sum(score => score.Score)),
            "newest" => quotesQuery.OrderByDescending(quote => quote.InsertDate),
            _ => quotesQuery.OrderBy(quote => quote.InsertDate)
        };
}

public sealed record QuotePage(int Page, int TotalPages, int Total, IReadOnlyList<QuoteListItem> Items)
{
    public bool HasItems => Items.Count > 0;
}

public sealed record QuoteListItem(
    int Id,
    int GuildId,
    int UserId,
    string Content,
    DateTime InsertDate,
    bool Approved,
    bool Removed,
    int Score,
    string Author);

public sealed record QuoteDetails(
    int Id,
    int GuildId,
    string Content,
    DateTime InsertDate,
    bool Approved,
    bool Removed,
    int TotalScore,
    string Author);

public sealed record QuoteScoreResult(bool Found, int QuoteId, int AppliedScore, int TotalScore, string? ErrorMessage)
{
    public static QuoteScoreResult Success(int quoteId, int appliedScore, int totalScore) =>
        new(true, quoteId, appliedScore, totalScore, null);

    public static QuoteScoreResult NotFound(string errorMessage) =>
        new(false, 0, 0, 0, errorMessage);
}

public sealed record QuotePeriodResult(int QuoteId, string Content, int TotalScore)
{
    public static QuotePeriodResult Empty { get; } = new(0, string.Empty, 0);

    public bool HasQuote => QuoteId != 0;
}
