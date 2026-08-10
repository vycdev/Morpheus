using Morpheus.Modules;

namespace Morpheus.Tests;

public class EmojisModuleTests
{
    [Fact]
    public void TryGetReferencedMessageId_ReturnsFalseWhenReferenceIsMissing()
    {
        bool found = EmojisModule.TryGetReferencedMessageId(null, out ulong messageId);

        Assert.False(found);
        Assert.Equal(0UL, messageId);
    }

    [Fact]
    public void TryGetReferencedMessageId_ReturnsReferenceIdWhenPresent()
    {
        bool found = EmojisModule.TryGetReferencedMessageId(42UL, out ulong messageId);

        Assert.True(found);
        Assert.Equal(42UL, messageId);
    }

    [Theory]
    [InlineData(0, 0, false, 0, 0)]
    [InlineData(26, -1, true, 0, 2)]
    [InlineData(26, 10, true, 1, 2)]
    public void TryGetEmojiPage_HandlesEmptyAndOutOfRangePages(
        int emojiCount,
        int requestedPage,
        bool expectedResult,
        int expectedPage,
        int expectedTotalPages)
    {
        bool result = EmojisModule.TryGetEmojiPage(
            emojiCount,
            requestedPage,
            out int emojiPage,
            out int totalPages);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedPage, emojiPage);
        Assert.Equal(expectedTotalPages, totalPages);
    }

    [Fact]
    public async Task ImportSessionStore_AppliesConcurrentPageUpdatesWithoutLosingChanges()
    {
        var store = new EmojiImportSessionStore();
        store.Set(42UL, new EmojiImportSession
        {
            UserId = 1UL,
            TargetGuildId = 2UL
        });

        const int updateCount = 1_000;
        Task[] updates = Enumerable.Range(0, updateCount)
            .Select(index => Task.Run(() =>
            {
                bool updated = store.TryUpdate(
                    42UL,
                    session => session with { EmojiPage = session.EmojiPage + 1 },
                    out _);
                Assert.True(updated);
            }))
            .ToArray();

        await Task.WhenAll(updates);

        Assert.True(store.TryGetValue(42UL, out EmojiImportSession session));
        Assert.Equal(updateCount, session.EmojiPage);
    }

    [Fact]
    public void ImportSessionStore_RemovesOnlyExpiredSessions()
    {
        var store = new EmojiImportSessionStore();
        var now = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        var timeout = TimeSpan.FromMinutes(5);

        store.Set(1UL, new EmojiImportSession
        {
            UserId = 1UL,
            TargetGuildId = 2UL,
            CreatedAt = now - timeout - TimeSpan.FromSeconds(1)
        });
        store.Set(2UL, new EmojiImportSession
        {
            UserId = 1UL,
            TargetGuildId = 2UL,
            CreatedAt = now - timeout
        });

        int removed = store.RemoveExpired(now, timeout);

        Assert.Equal(1, removed);
        Assert.False(store.TryGetValue(1UL, out _));
        Assert.True(store.TryGetValue(2UL, out _));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void ImportSessionStore_AllowsOnlyOneInteractionPerSession()
    {
        var store = new EmojiImportSessionStore();
        store.Set(42UL, new EmojiImportSession
        {
            UserId = 1UL,
            TargetGuildId = 2UL
        });

        Assert.True(store.TryAcquire(42UL, 1UL, out _, out EmojiImportSessionLease firstLease));
        Assert.False(store.TryAcquire(42UL, 1UL, out _, out _));

        firstLease.Dispose();

        Assert.True(store.TryAcquire(42UL, 1UL, out _, out EmojiImportSessionLease secondLease));
        secondLease.Dispose();
    }

    [Fact]
    public void ImportSessionStore_DoesNotCleanUpAnActiveSession()
    {
        var store = new EmojiImportSessionStore();
        var now = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
        store.Set(42UL, new EmojiImportSession
        {
            UserId = 1UL,
            TargetGuildId = 2UL,
            CreatedAt = now - TimeSpan.FromMinutes(10)
        });

        Assert.True(store.TryAcquire(42UL, 1UL, out _, out EmojiImportSessionLease lease));

        Assert.Equal(0, store.RemoveExpired(now, TimeSpan.FromMinutes(5)));
        Assert.True(store.TryGetValue(42UL, out _));

        lease.Dispose();
        Assert.Equal(1, store.RemoveExpired(now, TimeSpan.FromMinutes(5)));
        Assert.False(store.TryGetValue(42UL, out _));
    }
}
