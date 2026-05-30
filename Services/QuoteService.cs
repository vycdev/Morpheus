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
        await UpsertQuoteScoreAsync(quote.Id, userId, score, now);

        int totalScore = await GetTotalScoreAsync(quote.Id);
        return QuoteScoreResult.Success(quote.Id, score, totalScore);
    }

    public async Task<QuoteApprovalResult> ApproveQuoteRequestAsync(
        int approvalId,
        int userId,
        int approvalExpiryDays,
        DateTime? utcNow = null)
    {
        DateTime now = utcNow ?? DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        QuoteApprovalMessage? approval = await dbContext.QuoteApprovalMessages
            .FromSqlInterpolated($"""SELECT * FROM "QuoteApprovalMessages" WHERE "Id" = {approvalId} FOR UPDATE""")
            .FirstOrDefaultAsync();

        if (approval == null)
            return QuoteApprovalResult.NotFound();

        if (IsApprovalExpired(approval.InsertDate, approvalExpiryDays, now))
            return QuoteApprovalResult.Expired();

        if (approval.Approved)
            return QuoteApprovalResult.AlreadyFinalized();

        Quote? quote = await dbContext.Quotes.FirstOrDefaultAsync(quote => quote.Id == approval.QuoteId);
        if (quote == null)
            return QuoteApprovalResult.QuoteNotFound();

        Guild? guild = await dbContext.Guilds
            .AsNoTracking()
            .FirstOrDefaultAsync(guild => guild.Id == quote.GuildId);
        if (guild == null)
            return QuoteApprovalResult.GuildNotFound();

        int requiredApprovals = GetRequiredApprovals(approval.Type, guild);
        int currentApprovals = await dbContext.QuoteApprovals
            .CountAsync(existingApproval => existingApproval.QuoteApprovalMessageId == approval.Id);

        bool alreadyApproved = await dbContext.QuoteApprovals.AnyAsync(existingApproval =>
            existingApproval.QuoteApprovalMessageId == approval.Id && existingApproval.UserId == (ulong)userId);
        if (alreadyApproved)
            return QuoteApprovalResult.Duplicate(currentApprovals, requiredApprovals);

        QuoteApproval vote = new()
        {
            QuoteApprovalMessageId = approval.Id,
            UserId = (ulong)userId,
            InsertDate = now
        };

        await dbContext.QuoteApprovals.AddAsync(vote);
        currentApprovals++;

        bool finalized = currentApprovals >= requiredApprovals;
        if (finalized)
            ApplyApprovalResolution(approval, quote);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return finalized
            ? QuoteApprovalResult.Finalized(
                currentApprovals,
                requiredApprovals,
                approval.Type,
                quote.Id,
                quote.Content ?? string.Empty,
                approval.ApprovalMessageId,
                guild.QuotesApprovalChannelId)
            : QuoteApprovalResult.Recorded(currentApprovals, requiredApprovals);
    }

    private async Task UpsertQuoteScoreAsync(int quoteId, int userId, int score, DateTime now)
    {
        QuoteScore? existing = await dbContext.QuoteScores
            .FirstOrDefaultAsync(existingScore => existingScore.QuoteId == quoteId && existingScore.UserId == userId);

        if (existing == null)
        {
            QuoteScore quoteScore = new()
            {
                QuoteId = quoteId,
                UserId = userId,
            };

            ApplyScore(quoteScore, score, now, isNew: true);
            await dbContext.QuoteScores.AddAsync(quoteScore);
            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                dbContext.Entry(quoteScore).State = EntityState.Detached;
                QuoteScore? racedExisting = await dbContext.QuoteScores
                    .FirstOrDefaultAsync(existingScore => existingScore.QuoteId == quoteId && existingScore.UserId == userId);

                if (racedExisting == null)
                    throw;

                ApplyScore(racedExisting, score, now, isNew: false);
                await dbContext.SaveChangesAsync();
            }

            return;
        }

        ApplyScore(existing, score, now, isNew: false);
        await dbContext.SaveChangesAsync();
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

    internal static bool IsApprovalExpired(DateTime insertDate, int approvalExpiryDays, DateTime now) =>
        insertDate.AddDays(approvalExpiryDays) < now;

    internal static int GetRequiredApprovals(QuoteApprovalType approvalType, Guild guild) =>
        approvalType == QuoteApprovalType.AddRequest
            ? guild.QuoteAddRequiredApprovals
            : guild.QuoteRemoveRequiredApprovals;

    internal static void ApplyApprovalResolution(QuoteApprovalMessage approval, Quote quote)
    {
        approval.Approved = true;
        if (approval.Type == QuoteApprovalType.AddRequest)
            quote.Approved = true;
        else
            quote.Removed = true;
    }

    internal static void ApplyScore(QuoteScore quoteScore, int score, DateTime now, bool isNew)
    {
        quoteScore.Score = score;
        if (isNew)
            quoteScore.InsertDate = now;
        else
            quoteScore.UpdateDate = now;
    }

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

public enum QuoteApprovalResultStatus
{
    Recorded,
    Finalized,
    Duplicate,
    NotFound,
    Expired,
    AlreadyFinalized,
    QuoteNotFound,
    GuildNotFound
}

public sealed record QuoteApprovalResult(
    QuoteApprovalResultStatus Status,
    int CurrentApprovals,
    int RequiredApprovals,
    QuoteApprovalType Type,
    int QuoteId,
    string QuoteContent,
    ulong ApprovalMessageId,
    ulong QuotesApprovalChannelId)
{
    public bool VoteRecorded => Status is QuoteApprovalResultStatus.Recorded or QuoteApprovalResultStatus.Finalized;
    public bool IsFinalized => Status == QuoteApprovalResultStatus.Finalized;

    public static QuoteApprovalResult Recorded(int currentApprovals, int requiredApprovals) =>
        new(QuoteApprovalResultStatus.Recorded, currentApprovals, requiredApprovals, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);

    public static QuoteApprovalResult Finalized(
        int currentApprovals,
        int requiredApprovals,
        QuoteApprovalType type,
        int quoteId,
        string quoteContent,
        ulong approvalMessageId,
        ulong quotesApprovalChannelId) =>
        new(QuoteApprovalResultStatus.Finalized, currentApprovals, requiredApprovals, type, quoteId, quoteContent, approvalMessageId, quotesApprovalChannelId);

    public static QuoteApprovalResult Duplicate(int currentApprovals, int requiredApprovals) =>
        new(QuoteApprovalResultStatus.Duplicate, currentApprovals, requiredApprovals, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);

    public static QuoteApprovalResult NotFound() =>
        new(QuoteApprovalResultStatus.NotFound, 0, 0, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);

    public static QuoteApprovalResult Expired() =>
        new(QuoteApprovalResultStatus.Expired, 0, 0, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);

    public static QuoteApprovalResult AlreadyFinalized() =>
        new(QuoteApprovalResultStatus.AlreadyFinalized, 0, 0, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);

    public static QuoteApprovalResult QuoteNotFound() =>
        new(QuoteApprovalResultStatus.QuoteNotFound, 0, 0, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);

    public static QuoteApprovalResult GuildNotFound() =>
        new(QuoteApprovalResultStatus.GuildNotFound, 0, 0, QuoteApprovalType.AddRequest, 0, string.Empty, 0, 0);
}
