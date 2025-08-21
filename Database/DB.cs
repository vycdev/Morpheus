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
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserActivity> UserActivity { get; set; }
    public DbSet<UserLevels> UserLevels { get; set; }
    public DbSet<Guild> Guilds { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<QuoteApproval> QuoteApprovals { get; set; }
    public DbSet<QuoteScore> QuoteScores { get; set; }
    public DbSet<Log> Logs { get; set; }
    public DbSet<ButtonGamePress> ButtonGamePresses { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<BotSetting> BotSettings { get; set; }
}
