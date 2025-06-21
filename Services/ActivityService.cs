using Morpheus.Database;
using Morpheus.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morpheus.Services;
public class ActivityService(DB dbContext)
{
    public List<UserLevels> GetTopActivity(int dbGuildId, int days = 30)
    {
        DateTime date = DateTime.UtcNow.AddDays(-days);

        List<UserLevels> userLevels = [.. dbContext.UserActivity
            .Where(ua => ua.GuildId == dbGuildId && ua.InsertDate >= date)
            .GroupBy(ua => ua.User)
            .Select(g => new UserLevels {
                User = g.Key,
                TotalXp = g.Sum(a => a.XpGained)
            })
            .OrderByDescending(x => x.TotalXp)];

        return userLevels;
    }

    public List<List<User>> GetUserSlices(List<UserLevels> userLevels, List<double> percentBounds)
    {
        int totalUsers = userLevels.Count;
        var slices = new List<List<User>>();
        int currentIndex = 0;

        foreach (var percent in percentBounds)
        {
            int targetIndex = (int)Math.Round(totalUsers * percent);

            // Clamp to avoid out-of-range and ensure at least one per bucket if available
            targetIndex = Math.Min(targetIndex, totalUsers);
            int count = Math.Max(1, targetIndex - currentIndex);

            var slice = userLevels
                .Skip(currentIndex)
                .Take(count)
                .Select(u => u.User)
                .ToList();

            slices.Add(slice);
            currentIndex += count;
        }

        return slices;
    }
}
