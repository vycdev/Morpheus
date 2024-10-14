using Microsoft.EntityFrameworkCore;
using Morpheus.Utilities;
using System;

namespace Morpheus.Database;
public class DbContext : Microsoft.EntityFrameworkCore.DbContext
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

    public DbContext(DbContextOptions<DbContext> options) : base(options) { }

    public DbSet<Models.User> Users { get; set; }
}
