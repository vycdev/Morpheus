using Microsoft.EntityFrameworkCore;
using Morpheus.Database.Models;

namespace Morpheus.Database;

public class DB(DbContextOptions<DB> options) : Microsoft.EntityFrameworkCore.DbContext(options)
{
    // =============== Migrations instructions =============== 
    // ================== Create migrations ==================
    //
    // dotnet ef migrations add <name>
    //
    // =================== Update database ===================
    //
    // dotnet ef database update
    //
    // ============= If dotnet can't find ef =================
    // 
    // dotnet tool install --global dotnet-ef --version 8.*
    // 


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Create unique index on DiscordId
        modelBuilder.Entity<User>().HasIndex(u => u.DiscordId).IsUnique();
        modelBuilder.Entity<Guild>().HasIndex(g => g.DiscordId).IsUnique();
        modelBuilder.Entity<QuoteApproval>().HasIndex(a => new { a.QuoteApprovalMessageId, a.UserId }).IsUnique();
        // Speeds up recent per-user, per-guild activity queries
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.UserId, ua.GuildId, ua.InsertDate });

        // Indexes for temporary bans processing
        modelBuilder.Entity<TemporaryBan>().HasIndex(tb => new { tb.GuildId, tb.UserId });
        modelBuilder.Entity<TemporaryBan>().HasIndex(tb => new { tb.ExpiresAt, tb.UnbannedAt });

        modelBuilder.Entity<ReactionRoleMessage>().HasIndex(m => m.MessageId).IsUnique();
        modelBuilder.Entity<ReactionRoleItem>().HasIndex(i => new { i.ReactionRoleMessageId, i.RoleId }).IsUnique();
        modelBuilder.Entity<ReactionRoleItem>().HasIndex(i => new { i.ReactionRoleMessageId, i.Emoji }).IsUnique();
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserActivity> UserActivity { get; set; }
    public DbSet<UserLevels> UserLevels { get; set; }
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<QuoteApprovalMessage> QuoteApprovalMessages { get; set; }
    public DbSet<QuoteApproval> QuoteApprovals { get; set; }
    public DbSet<QuoteScore> QuoteScores { get; set; }
    public DbSet<Log> Logs { get; set; }
    public DbSet<ButtonGamePress> ButtonGamePresses { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<BotSetting> BotSettings { get; set; }
    public DbSet<TemporaryBan> TemporaryBans { get; set; }
    public DbSet<ReactionRoleMessage> ReactionRoleMessages { get; set; }
    public DbSet<ReactionRoleItem> ReactionRoleItems { get; set; }
}
