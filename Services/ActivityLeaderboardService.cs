using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using System.Text;

namespace Morpheus.Services;

public class ActivityLeaderboardService(DB dbContext)
{
    internal const int PageSize = 10;

    public async Task<ActivityLeaderboardQueryResult> GetGuildXpLeaderboardAsync(
        int guildId,
        string guildName,
        int? viewerUserId,
        int page)
    {
        IQueryable<UserLevels> query = dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.GuildId == guildId)
            .OrderByDescending(ul => ul.TotalXp);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, "No level data found for this guild.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ul => new { ul.UserId, Value = (long)ul.TotalXp })
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatXpLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetGuildXpRankLineAsync(guildId, viewerUserId);

        return CreatePage($"**Leaderboard for {guildName}**", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGuildPastXpLeaderboardAsync(
        int guildId,
        string guildName,
        int? viewerUserId,
        int days,
        int page)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);
        IQueryable<UserActivity> baseQuery = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.GuildId == guildId && ua.InsertDate >= cutoff);

        var query = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Value = (long)g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.Value);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, $"No activity data found for the past {days} days.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatXpLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetPastXpRankLineAsync(baseQuery, viewerUserId);

        return CreatePage($"**Leaderboard for {guildName}** for the past **{days}** days", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGlobalXpLeaderboardAsync(int? viewerUserId, int page)
    {
        var query = dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(ul => ul.UserId)
            .Select(g => new { UserId = g.Key, Value = (long)g.Sum(ul => ul.TotalXp) })
            .OrderByDescending(x => x.Value);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, "No level data found globally.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatXpLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetGlobalXpRankLineAsync(viewerUserId);

        return CreatePage("**Global Leaderboard**", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGlobalPastXpLeaderboardAsync(
        int? viewerUserId,
        int days,
        int page)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);
        IQueryable<UserActivity> baseQuery = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate >= cutoff);

        var query = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Value = (long)g.Sum(x => x.XpGained) })
            .OrderByDescending(x => x.Value);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, $"No activity data found globally for the past {days} days.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatXpLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetPastXpRankLineAsync(baseQuery, viewerUserId);

        return CreatePage($"**Global Leaderboard** for the past **{days}** days", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGuildMessageLeaderboardAsync(
        int guildId,
        string guildName,
        int? viewerUserId,
        int page)
    {
        IQueryable<UserLevels> query = dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.GuildId == guildId && ul.UserMessageCount > 0)
            .OrderByDescending(ul => ul.UserMessageCount);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, "No message data found for this guild.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ul => new { ul.UserId, Value = ul.UserMessageCount })
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatMessageLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetGuildMessageRankLineAsync(guildId, viewerUserId);

        return CreatePage($"**Messages Leaderboard for {guildName}** (all time)", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGuildPastMessageLeaderboardAsync(
        int guildId,
        string guildName,
        int? viewerUserId,
        int days,
        int page)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);
        IQueryable<UserActivity> baseQuery = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.GuildId == guildId && ua.InsertDate >= cutoff);

        var query = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, $"No message data found for the past {days} days.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatMessageLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetPastMessageRankLineAsync(baseQuery, viewerUserId);

        return CreatePage($"**Messages Leaderboard for {guildName}** for the past **{days}** days", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGlobalMessageLeaderboardAsync(int? viewerUserId, int page)
    {
        var query = dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(ul => ul.UserId)
            .Select(g => new { UserId = g.Key, Value = g.Sum(ul => ul.UserMessageCount) })
            .OrderByDescending(x => x.Value);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, "No message data found globally.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatMessageLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetGlobalMessageRankLineAsync(viewerUserId);

        return CreatePage("**Global Messages Leaderboard** (all time)", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGlobalPastMessageLeaderboardAsync(
        int? viewerUserId,
        int days,
        int page)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-days);
        IQueryable<UserActivity> baseQuery = dbContext.UserActivity
            .AsNoTracking()
            .Where(ua => ua.InsertDate >= cutoff);

        var query = baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, $"No message data found globally for the past {days} days.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatMessageLine(item.UserId, names, item.Value, page, index))];
        string rankLine = await GetPastMessageRankLineAsync(baseQuery, viewerUserId);

        return CreatePage($"**Global Messages Leaderboard** for the past **{days}** days", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGuildAverageLengthLeaderboardAsync(
        int guildId,
        string guildName,
        int? viewerUserId,
        int page)
    {
        IQueryable<UserLevels> query = dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.GuildId == guildId && ul.UserMessageCount > 0)
            .OrderByDescending(ul => ul.UserAverageMessageLength);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, "No message data found for this guild.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ul => new
            {
                ul.UserId,
                AverageLength = ul.UserAverageMessageLength,
                MessageCount = ul.UserMessageCount
            })
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines =
        [
            .. pageItems.Select((item, index) =>
                FormatAverageLengthLine(item.UserId, names, item.AverageLength, item.MessageCount, page, index))
        ];
        string rankLine = await GetGuildAverageLengthRankLineAsync(guildId, viewerUserId);

        return CreatePage($"**Average Message Length Leaderboard for {guildName}** (all time)", lines, page, totalUsers, rankLine);
    }

    public async Task<ActivityLeaderboardQueryResult> GetGlobalAverageLengthLeaderboardAsync(int? viewerUserId, int page)
    {
        var query = dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(ul => ul.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                SumLen = g.Sum(ul => ul.UserAverageMessageLength * ul.UserMessageCount),
                SumCount = g.Sum(ul => ul.UserMessageCount)
            })
            .Where(x => x.SumCount > 0)
            .Select(x => new { x.UserId, AverageLength = x.SumLen / x.SumCount })
            .OrderByDescending(x => x.AverageLength);

        int totalUsers = await query.CountAsync();
        ActivityLeaderboardQueryResult? invalidPage = ValidatePage(page, totalUsers, "No message data found globally.");
        if (invalidPage != null)
            return invalidPage;

        var pageItems = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        Dictionary<int, string> names = await GetUserNamesAsync(pageItems.Select(item => item.UserId));
        List<string> lines = [.. pageItems.Select((item, index) => FormatAverageLengthLine(item.UserId, names, item.AverageLength, null, page, index))];
        string rankLine = await GetGlobalAverageLengthRankLineAsync(viewerUserId);

        return CreatePage("**Global Average Message Length Leaderboard** (all time)", lines, page, totalUsers, rankLine);
    }

    internal static ActivityLeaderboardQueryResult? ValidatePage(int page, int totalUsers, string emptyMessage)
    {
        if (totalUsers == 0)
            return ActivityLeaderboardQueryResult.Error(emptyMessage);

        int totalPages = GetTotalPages(totalUsers);
        if (page < 1 || page > totalPages)
            return ActivityLeaderboardQueryResult.Error($"Invalid page number. Please choose a page between 1 and {totalPages}.");

        return null;
    }

    internal static ActivityLeaderboardQueryResult CreatePage(
        string title,
        IReadOnlyList<string> lines,
        int page,
        int totalUsers,
        string rankLine)
    {
        return ActivityLeaderboardQueryResult.WithPage(new ActivityLeaderboardPage(
            title,
            lines,
            page,
            GetTotalPages(totalUsers),
            rankLine));
    }

    internal static string FormatLeaderboardMessage(ActivityLeaderboardPage page)
    {
        StringBuilder sb = new();

        sb.AppendLine(page.Title);
        sb.AppendLine("```js");
        sb.AppendLine(string.Join("\n", page.Lines));
        sb.AppendLine($"\n(Page {page.CurrentPage}/{page.TotalPages})");
        sb.AppendLine("```");
        sb.AppendLine(page.RankLine);

        return sb.ToString();
    }

    private async Task<Dictionary<int, string>> GetUserNamesAsync(IEnumerable<int> userIds)
    {
        List<int> ids = [.. userIds.Distinct()];

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new { user.Id, user.Username })
            .ToDictionaryAsync(user => user.Id, user => user.Username);
    }

    private static string FormatXpLine(int userId, IReadOnlyDictionary<int, string> names, long xp, int page, int index)
    {
        string name = names.TryGetValue(userId, out string? username) ? username : userId.ToString();
        return $"[{GetRankNumber(page, index)}] | {name}: Level {ActivityLevelService.CalculateLevel(xp)} with {xp} XP";
    }

    private static string FormatMessageLine(int userId, IReadOnlyDictionary<int, string> names, int count, int page, int index)
    {
        string name = names.TryGetValue(userId, out string? username) ? username : userId.ToString();
        return $"[{GetRankNumber(page, index)}] | {name}: Messages {count}";
    }

    private static string FormatAverageLengthLine(
        int userId,
        IReadOnlyDictionary<int, string> names,
        double averageLength,
        int? messageCount,
        int page,
        int index)
    {
        string name = names.TryGetValue(userId, out string? username) ? username : userId.ToString();
        string avg = averageLength.ToString("0.0");
        string suffix = messageCount.HasValue ? $" ({messageCount.Value} msgs)" : string.Empty;

        return $"[{GetRankNumber(page, index)}] | {name}: Avg length {avg} chars{suffix}";
    }

    private async Task<string> GetGuildXpRankLineAsync(int guildId, int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        UserLevels? userLevel = await dbContext.UserLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(ul => ul.GuildId == guildId && ul.UserId == viewerUserId.Value);

        if (userLevel == null)
            return "Your rank: N/A";

        int better = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.GuildId == guildId && ul.TotalXp > userLevel.TotalXp)
            .CountAsync();

        return $"Your rank: #{better + 1}";
    }

    private async Task<string> GetGlobalXpRankLineAsync(int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        bool hasRows = await dbContext.UserLevels.AsNoTracking().AnyAsync(ul => ul.UserId == viewerUserId.Value);
        if (!hasRows)
            return "Your rank: N/A";

        long myTotal = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.UserId == viewerUserId.Value)
            .Select(ul => (long)ul.TotalXp)
            .SumAsync();

        int better = await dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(ul => ul.UserId)
            .Select(g => new { Total = g.Sum(ul => ul.TotalXp) })
            .CountAsync(x => x.Total > myTotal);

        return $"Your rank: #{better + 1}";
    }

    private static async Task<string> GetPastXpRankLineAsync(IQueryable<UserActivity> baseQuery, int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        bool hasUser = await baseQuery.AnyAsync(ua => ua.UserId == viewerUserId.Value);
        if (!hasUser)
            return "Your rank: N/A";

        int mySum = await baseQuery.Where(ua => ua.UserId == viewerUserId.Value).SumAsync(ua => ua.XpGained);
        int better = await baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { Sum = g.Sum(ua => ua.XpGained) })
            .CountAsync(x => x.Sum > mySum);

        return $"Your rank: #{better + 1}";
    }

    private async Task<string> GetGuildMessageRankLineAsync(int guildId, int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        UserLevels? userLevel = await dbContext.UserLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(ul => ul.GuildId == guildId && ul.UserId == viewerUserId.Value);

        if (userLevel == null)
            return "Your rank: N/A";

        int better = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.GuildId == guildId && ul.UserMessageCount > userLevel.UserMessageCount)
            .CountAsync();

        return $"Your rank: #{better + 1}";
    }

    private async Task<string> GetGlobalMessageRankLineAsync(int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        bool hasRows = await dbContext.UserLevels.AsNoTracking().AnyAsync(ul => ul.UserId == viewerUserId.Value);
        if (!hasRows)
            return "Your rank: N/A";

        long myCount = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.UserId == viewerUserId.Value)
            .Select(ul => (long)ul.UserMessageCount)
            .SumAsync();

        int better = await dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(ul => ul.UserId)
            .Select(g => new { Count = g.Sum(ul => ul.UserMessageCount) })
            .CountAsync(x => x.Count > myCount);

        return $"Your rank: #{better + 1}";
    }

    private static async Task<string> GetPastMessageRankLineAsync(IQueryable<UserActivity> baseQuery, int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        bool hasUser = await baseQuery.AnyAsync(ua => ua.UserId == viewerUserId.Value);
        if (!hasUser)
            return "Your rank: N/A";

        int myCount = await baseQuery.CountAsync(ua => ua.UserId == viewerUserId.Value);
        int better = await baseQuery
            .GroupBy(ua => ua.UserId)
            .Select(g => new { Count = g.Count() })
            .CountAsync(x => x.Count > myCount);

        return $"Your rank: #{better + 1}";
    }

    private async Task<string> GetGuildAverageLengthRankLineAsync(int guildId, int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        UserLevels? userLevel = await dbContext.UserLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(ul => ul.GuildId == guildId && ul.UserId == viewerUserId.Value && ul.UserMessageCount > 0);

        if (userLevel == null)
            return "Your rank: N/A";

        int better = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul =>
                ul.GuildId == guildId &&
                ul.UserMessageCount > 0 &&
                ul.UserAverageMessageLength > userLevel.UserAverageMessageLength)
            .CountAsync();

        return $"Your rank: #{better + 1}";
    }

    private async Task<string> GetGlobalAverageLengthRankLineAsync(int? viewerUserId)
    {
        if (viewerUserId == null)
            return "Your rank: N/A";

        var viewerAverage = await dbContext.UserLevels
            .AsNoTracking()
            .Where(ul => ul.UserId == viewerUserId.Value)
            .GroupBy(ul => ul.UserId)
            .Select(g => new
            {
                SumLen = g.Sum(ul => ul.UserAverageMessageLength * ul.UserMessageCount),
                SumCount = g.Sum(ul => ul.UserMessageCount)
            })
            .FirstOrDefaultAsync();

        if (viewerAverage == null || viewerAverage.SumCount <= 0)
            return "Your rank: N/A";

        double myAverage = viewerAverage.SumLen / viewerAverage.SumCount;
        int better = await dbContext.UserLevels
            .AsNoTracking()
            .GroupBy(ul => ul.UserId)
            .Select(g => new
            {
                SumLen = g.Sum(ul => ul.UserAverageMessageLength * ul.UserMessageCount),
                SumCount = g.Sum(ul => ul.UserMessageCount)
            })
            .Where(x => x.SumCount > 0)
            .CountAsync(x => (x.SumLen / x.SumCount) > myAverage);

        return $"Your rank: #{better + 1}";
    }

    private static int GetTotalPages(int totalUsers) => (int)Math.Ceiling(totalUsers / (double)PageSize);

    private static int GetRankNumber(int page, int index) => ((page - 1) * PageSize) + index + 1;
}

public sealed record ActivityLeaderboardQueryResult(ActivityLeaderboardPage? Page, string? ErrorMessage)
{
    public bool Success => Page != null;

    public static ActivityLeaderboardQueryResult WithPage(ActivityLeaderboardPage page) => new(page, null);

    public static ActivityLeaderboardQueryResult Error(string message) => new(null, message);
}

public sealed record ActivityLeaderboardPage(
    string Title,
    IReadOnlyList<string> Lines,
    int CurrentPage,
    int TotalPages,
    string RankLine);
