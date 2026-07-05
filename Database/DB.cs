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
        modelBuilder.Entity<BotSetting>().HasIndex(s => s.Key).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.DiscordId).IsUnique();
        modelBuilder.Entity<Guild>().HasIndex(g => g.DiscordId).IsUnique();
        modelBuilder.Entity<QuoteApproval>().HasIndex(a => new { a.QuoteApprovalMessageId, a.UserId }).IsUnique();
        modelBuilder.Entity<QuoteScore>().HasIndex(s => new { s.QuoteId, s.UserId }).IsUnique();
        modelBuilder.Entity<Quote>().HasIndex(q => new { q.InsertDate, q.Approved, q.Removed });
        modelBuilder.Entity<QuoteApprovalMessage>().HasIndex(qam => new { qam.Approved, qam.InsertDate });
        modelBuilder.Entity<Log>().HasIndex(log => new { log.InsertDate, log.Severity });
        modelBuilder.Entity<ButtonGamePress>().HasIndex(press => press.InsertDate);
        modelBuilder.Entity<ButtonGamePress>().HasIndex(press => new { press.GuildId, press.InsertDate });
        modelBuilder.Entity<ButtonGamePress>().HasIndex(press => new { press.UserId, press.InsertDate });
        // Speeds up recent per-user, per-guild activity queries
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.UserId, ua.GuildId, ua.InsertDate });
        modelBuilder.Entity<UserActivity>().HasIndex(ua => ua.InsertDate);
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.GuildId, ua.InsertDate });
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.DiscordChannelId, ua.InsertDate });
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.InsertDate, ua.GuildId });
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.InsertDate, ua.UserId });
        modelBuilder.Entity<UserActivity>().HasIndex(ua => new { ua.InsertDate, ua.DiscordChannelId });
        modelBuilder.Entity<UserLevels>().HasIndex(ul => new { ul.UserId, ul.GuildId }).IsUnique();

        // Indexes for temporary bans processing
        modelBuilder.Entity<TemporaryBan>().HasIndex(tb => new { tb.GuildId, tb.UserId });
        modelBuilder.Entity<TemporaryBan>().HasIndex(tb => new { tb.ExpiresAt, tb.UnbannedAt });

        modelBuilder.Entity<ReactionRoleMessage>().HasIndex(m => m.MessageId).IsUnique();
        modelBuilder.Entity<ReactionRoleItem>().HasIndex(i => new { i.ReactionRoleMessageId, i.RoleId }).IsUnique();
        modelBuilder.Entity<ReactionRoleItem>().HasIndex(i => new { i.ReactionRoleMessageId, i.Emoji }).IsUnique();

        // Stocks
        modelBuilder.Entity<Channel>().HasIndex(c => c.DiscordId).IsUnique();
        modelBuilder.Entity<Stock>().HasIndex(s => new { s.EntityType, s.EntityId }).IsUnique();
        modelBuilder.Entity<Stock>().HasIndex(s => new { s.LastUpdatedDate, s.UpdateTimeMinutes });
        modelBuilder.Entity<StockHolding>().HasIndex(sh => new { sh.UserId, sh.StockId }).IsUnique();
        modelBuilder.Entity<StockTransaction>().HasIndex(st => new { st.UserId, st.InsertDate });
        modelBuilder.Entity<StockTransaction>().HasIndex(st => st.InsertDate);
        modelBuilder.Entity<StockTransaction>().HasIndex(st => new { st.StockId, st.InsertDate });

        // Stock Price uses decimal(18,4) for precision
        modelBuilder.Entity<Stock>().Property(s => s.Price).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<Stock>().Property(s => s.PreviousPrice).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<Stock>().Property(s => s.DailyChangePercent).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<StockHolding>().Property(sh => sh.Shares).HasColumnType("decimal(18,6)");
        modelBuilder.Entity<StockHolding>().Property(sh => sh.TotalInvested).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<StockTransaction>().Property(st => st.Amount).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<StockTransaction>().Property(st => st.Fee).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<StockTransaction>().Property(st => st.Shares).HasColumnType("decimal(18,6)");
        modelBuilder.Entity<StockTransaction>().Property(st => st.PriceAtTransaction).HasColumnType("decimal(18,4)");
        modelBuilder.Entity<User>().Property(u => u.Balance).HasColumnType("decimal(18,4)").HasDefaultValue(1000.00m);

        // Prevent cascade-delete from TargetUser on StockTransaction
        modelBuilder.Entity<StockTransaction>()
            .HasOne(st => st.TargetUser)
            .WithMany()
            .HasForeignKey(st => st.TargetUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Webhooks & feed subscriptions
        // One reusable webhook per Discord channel
        modelBuilder.Entity<Webhook>().HasIndex(w => w.ChannelDiscordId).IsUnique();

        // xkcd: one subscription per channel, global "seen" set keyed by comic link
        modelBuilder.Entity<XkcdSubscription>().HasIndex(s => s.ChannelDiscordId).IsUnique();
        modelBuilder.Entity<XkcdSeen>().HasIndex(s => s.Link).IsUnique();
        modelBuilder.Entity<XkcdSubscription>()
            .HasOne(s => s.Webhook)
            .WithMany()
            .HasForeignKey(s => s.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // YouTube: one subscription per (channel, youtuber), global "seen" set keyed by video id
        modelBuilder.Entity<YoutubeSubscription>().HasIndex(s => new { s.ChannelDiscordId, s.YoutubeChannelId }).IsUnique();
        modelBuilder.Entity<YoutubeSubscription>().HasIndex(s => s.YoutubeChannelId);
        modelBuilder.Entity<YoutubeSeenVideo>().HasIndex(s => s.VideoId).IsUnique();
        modelBuilder.Entity<YoutubeSubscription>()
            .HasOne(s => s.Webhook)
            .WithMany()
            .HasForeignKey(s => s.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Generic RSS/Atom feeds: one subscription per (channel, feed), global "seen" set per feed
        modelBuilder.Entity<RssSubscription>().HasIndex(s => new { s.ChannelDiscordId, s.FeedUrl }).IsUnique();
        modelBuilder.Entity<RssSubscription>().HasIndex(s => s.FeedUrl);
        modelBuilder.Entity<RssSeenEntry>().HasIndex(s => new { s.FeedUrl, s.EntryId }).IsUnique();
        modelBuilder.Entity<RssSubscription>()
            .HasOne(s => s.Webhook)
            .WithMany()
            .HasForeignKey(s => s.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Twitch go-live: one subscription per (channel, streamer)
        modelBuilder.Entity<TwitchSubscription>().HasIndex(s => new { s.ChannelDiscordId, s.TwitchUserId }).IsUnique();
        modelBuilder.Entity<TwitchSubscription>().HasIndex(s => s.TwitchUserId);
        modelBuilder.Entity<TwitchSubscription>()
            .HasOne(s => s.Webhook)
            .WithMany()
            .HasForeignKey(s => s.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
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
    public DbSet<Channel> Channels { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<StockHolding> StockHoldings { get; set; }
    public DbSet<StockTransaction> StockTransactions { get; set; }
    public DbSet<Webhook> Webhooks { get; set; }
    public DbSet<XkcdSubscription> XkcdSubscriptions { get; set; }
    public DbSet<XkcdSeen> XkcdSeen { get; set; }
    public DbSet<YoutubeSubscription> YoutubeSubscriptions { get; set; }
    public DbSet<YoutubeSeenVideo> YoutubeSeenVideos { get; set; }
    public DbSet<RssSubscription> RssSubscriptions { get; set; }
    public DbSet<RssSeenEntry> RssSeenEntries { get; set; }
    public DbSet<TwitchSubscription> TwitchSubscriptions { get; set; }
}
