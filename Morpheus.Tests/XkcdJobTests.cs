using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Jobs;
using System.Net;
using System.Text;

namespace Morpheus.Tests;

public class XkcdJobTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void ShouldFetchFeed_RequiresAtLeastOneSubscriber(int subscriptionCount, bool expected)
    {
        Assert.Equal(expected, XkcdJob.ShouldFetchFeed(subscriptionCount));
    }

    [Fact]
    public async Task DispatchAsync_WhenOneDeliveryFails_ReportsFailureAndAttemptsEverySubscriber()
    {
        List<XkcdSubscription> subscriptions =
        [
            new() { ChannelDiscordId = 1 },
            new() { ChannelDiscordId = 2 }
        ];
        List<ulong> attemptedChannels = [];

        bool succeeded = await XkcdJob.DispatchAsync(
            "https://xkcd.com/1/",
            subscriptions,
            (subscription, _) =>
            {
                attemptedChannels.Add(subscription.ChannelDiscordId);
                return Task.FromResult(subscription.ChannelDiscordId != 1);
            });

        Assert.False(succeeded);
        Assert.Equal([1UL, 2UL], attemptedChannels);
    }

    [Fact]
    public async Task FetchItemsAsync_TrimsEntryLinks()
    {
        const string feed = """
            <rss version="2.0">
              <channel>
                <item>
                  <title>Test comic</title>
                  <link>
                    https://xkcd.com/1234/
                  </link>
                </item>
              </channel>
            </rss>
            """;
        using HttpClient httpClient = new(new StaticResponseHandler(feed));

        List<XkcdJob.XkcdItem> items = await XkcdJob.FetchItemsAsync(httpClient, CancellationToken.None);

        XkcdJob.XkcdItem item = Assert.Single(items);
        Assert.Equal("https://xkcd.com/1234/", item.Link);
    }

    [Fact]
    public async Task FetchItemsAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using HttpClient httpClient = new();
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => XkcdJob.FetchItemsAsync(httpClient, cancellation.Token));
    }

    [Fact]
    public async Task RecordFailedDeliveryAsync_PersistsOneWeekOfHourlyAttempts()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<DB> options = new DbContextOptionsBuilder<DB>()
            .UseSqlite(connection)
            .Options;
        await using DB db = new(options);
        await db.Database.EnsureCreatedAsync();

        const string link = "https://xkcd.com/1/";
        DateTime startedAt = new(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        for (int attempt = 1; attempt <= XkcdJob.MaxDeliveryAttempts; attempt++)
        {
            int recordedAttempts = await XkcdJob.RecordFailedDeliveryAsync(
                db,
                link,
                startedAt.AddHours(attempt - 1));

            Assert.Equal(attempt, recordedAttempts);
        }

        XkcdDeliveryRetry retry = await db.XkcdDeliveryRetries.SingleAsync();
        Assert.Equal(168, retry.AttemptCount);
        Assert.Equal(startedAt.AddHours(167), retry.LastAttemptAt);
        Assert.False(XkcdJob.ShouldRetryDelivery(retry.AttemptCount));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(167, true)]
    [InlineData(168, false)]
    [InlineData(169, false)]
    public void ShouldRetryDelivery_StopsAfterOneWeek(int attemptCount, bool expected)
    {
        Assert.Equal(expected, XkcdJob.ShouldRetryDelivery(attemptCount));
    }

    private sealed class StaticResponseHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/rss+xml")
            });
    }
}
