using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;

namespace Morpheus.Tests;

public class GuildPrefixServiceTests
{
    [Fact]
    public async Task GetPrefixAsync_DoesNotOverwriteConcurrentPrefixUpdate()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DB> setupOptions = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using (DB setup = new(setupOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Guilds.Add(new Guild
            {
                DiscordId = 123,
                Name = "test-guild",
                Prefix = "old!"
            });
            await setup.SaveChangesAsync();
        }

        BlockingQueryInterceptor interceptor = new();
        DbContextOptions<DB> serviceOptions = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        using ServiceProvider services = new ServiceCollection()
            .AddScoped(_ => new DB(serviceOptions))
            .BuildServiceProvider();
        GuildPrefixService service = new(services.GetRequiredService<IServiceScopeFactory>());

        Task<string> initialRead = service.GetPrefixAsync(123);
        await interceptor.QueryStarted.WaitAsync(TimeSpan.FromSeconds(10));

        service.SetPrefix(123, "new!");
        interceptor.Release();

        Assert.Equal("new!", await initialRead);
        Assert.Equal("new!", await service.GetPrefixAsync(123));
    }

    private sealed class BlockingQueryInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> queryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> releaseQuery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int hasBlocked;

        public Task QueryStarted => queryStarted.Task;

        public void Release() => releaseQuery.TrySetResult(true);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("Guilds", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref hasBlocked, 1, 0) == 0)
            {
                queryStarted.TrySetResult(true);
                await releaseQuery.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
