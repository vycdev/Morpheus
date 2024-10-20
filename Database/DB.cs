using Microsoft.EntityFrameworkCore;

namespace Morpheus.Database;
public class DB : Microsoft.EntityFrameworkCore.DbContext
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

    public DB(DbContextOptions<DB> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Create unique index on DiscordId
        modelBuilder.Entity<Models.User>().HasIndex(u => u.DiscordId).IsUnique();
        modelBuilder.Entity<Models.Guild>().HasIndex(g => g.DiscordId).IsUnique();
    }

    public DbSet<Models.User> Users { get; set; }
    public DbSet<Models.Guild> Guilds { get; set; }
    public DbSet<Models.Quote> Quotes { get; set; }
    public DbSet<Models.Log> Logs { get; set; }
}
