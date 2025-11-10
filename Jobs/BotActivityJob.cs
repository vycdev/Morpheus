using Discord;
using Discord.WebSocket;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

public class BotActivityJob(LogsService logsService, DiscordSocketClient discordClient) : IJob
{
    // A time‐boxed activity that recurs every year
    private class AnnualActivity(
        int startMonth, int startDay, TimeSpan startTime,
        int endMonth, int endDay, TimeSpan endTime, string description)
    {
        public int StartMonth { get; } = startMonth;
        public int StartDay { get; } = startDay;
        public TimeSpan StartTime { get; } = startTime;

        public int EndMonth { get; } = endMonth;
        public int EndDay { get; } = endDay;
        public TimeSpan EndTime { get; } = endTime;
        public string Description { get; } = description;

        public bool IsActive(DateTime now)
        {
            // Build this year's start/end DateTimes
            int year = now.Year;
            DateTime start = new DateTime(year, StartMonth, StartDay)
                            .Add(StartTime);
            DateTime end = new DateTime(year, EndMonth, EndDay)
                            .Add(EndTime);

            // Handle wrap‐around (e.g. Dec→Jan)
            if (end <= start)
            {
                // if now >= start OR now < end (in next year)
                return now >= start || now < end;
            }
            else
            {
                return now >= start && now < end;
            }
        }
    }

    // 1) Your yearly schedule (examples: Halloween, Christmas, Birthday)
    private static readonly AnnualActivity[] Schedule =
    [
        // New Year Celebration: Jan 1, 00:00 → Jan 2, 00:00
        new AnnualActivity(
            startMonth: 1, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   1, endDay:   2,  endTime:   TimeSpan.Zero,
            description: "fireworks everywhere 🎆"
        ),

        // Valentine's Day: Feb 14, 00:00 → Feb 15, 00:00
        new AnnualActivity(
            startMonth: 2, startDay: 14, startTime: TimeSpan.Zero,
            endMonth:   2, endDay:   15, endTime:   TimeSpan.Zero,
            description: "romantic sounds 💕"
        ),

        // Pi Day: Mar 14, 00:00 → Mar 15, 00:00
        new AnnualActivity(
            startMonth: 3, startDay: 14, startTime: TimeSpan.Zero,
            endMonth:   3, endDay:   15, endTime:   TimeSpan.Zero,
            description: "eating pie 🥧"
        ),

        // Matrix Movie release day March 21st (Example): Mar 21, 00:00 → Mar 22, 00:00
        new AnnualActivity(
            startMonth: 3, startDay: 21, startTime: TimeSpan.Zero,
            endMonth:   3, endDay:   22, endTime:   TimeSpan.Zero,
            description: "dodging bullets in slow motion 🕶️"
        ),

        // April Fools Day: Apr 1, 00:00 → Apr 2, 00:00
        new AnnualActivity(
            startMonth: 4, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   4, endDay:   2,  endTime:   TimeSpan.Zero,
            description: "doing a little tomfoolery 🤡"
        ),

        // Pride Day (June): Jun 1, 00:00 → Jul 1, 00:00
        new AnnualActivity(
            startMonth: 6, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   6, endDay:   2,  endTime:   TimeSpan.Zero,
            description: "chanting LGBTQ+ anthems 🌈"
        ),

        // Hot July 1st Day (Example): Jul 1, 00:00 → Jul 2, 00:00
        new AnnualActivity(
            startMonth: 7, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   7, endDay:   2,  endTime:   TimeSpan.Zero,
            description: "vibing with summer vibes ☀️"
        ),

        // CC Anniversary 2nd September (Example): Sep 2, 00:00 → Sep 3, 00:00
        new AnnualActivity(
            startMonth: 9, startDay: 2, startTime: TimeSpan.Zero,
            endMonth:   9, endDay:   3,  endTime:   TimeSpan.Zero,
            description: "celebrating CC anniversary 🎉"
        ),

        // Day of the programmer (256th day): Sep 13, 00:00 → Sep 14, 00:00
        new AnnualActivity(
            startMonth: 9, startDay: 13, startTime: TimeSpan.Zero,
            endMonth:   9, endDay:   14, endTime:   TimeSpan.Zero,
            description: "0b1010010101 👨‍💻"
        ),

        // Halloween (Example): Oct 31, 00:00 → Nov 1, 00:00
        new AnnualActivity(
            startMonth: 10, startDay: 31, startTime: TimeSpan.Zero,
            endMonth:   11, endDay:   1,  endTime:   TimeSpan.Zero,
            description: "boo 👻"
        ),

        // 1st of November (Example): Nov 1, 00:00 → Nov 2, 00:00
        new AnnualActivity(
            startMonth: 11, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   11, endDay:   2,  endTime:   TimeSpan.Zero,
            description: "peanut butter noises 🥜"
        ),

        // Romania's National Day: Dec 1, 00:00 → Dec 2, 00:00
        new AnnualActivity(
            startMonth: 12, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   12, endDay:   2,  endTime:   TimeSpan.Zero,
            description: "Wake up romanian 🇷🇴"
        ),

        // Christmas: Dec 24, 18:00 → Dec 26, 06:00
        new AnnualActivity(
            startMonth: 12, startDay: 24, startTime: new TimeSpan(18, 0, 0),
            endMonth:   12, endDay:   26, endTime: new TimeSpan(6, 0, 0),
            description: "I'm home alone 🎄"
        ),

        // New Year's Eve: Dec 31, 20:00 → Jan 1, 02:00
        new AnnualActivity(
            startMonth: 12, startDay: 31, startTime: new TimeSpan(20, 0, 0),
            endMonth:   1, endDay:   1,  endTime: new TimeSpan(2, 0, 0),
            description: "counting down to the end of time 🎉"
        )
    ];

    // 2) Default fallback activities — we ignore per-item ActivityType and always set a single ActivityType
    private static readonly string[] DefaultActivities =
    {
        "Processing… please insert snacks.",
        "Running on 0% battery and pure willpower.",
        "Definitely not plotting anything.",
        "Trust me, I'm a bot.",
        "Rebooting my sense of humor.",
        "Searching for intelligent life… still searching.",
        "Simulating productivity.",
        "I swear I was doing something important.",
        "Debugging myself emotionally.",
        "404: Status not found.",
        "Powered by coffee and questionable code.",
        "Updating… do not unplug.",
        "Charging… slowly… very slowly.",
        "Lagging on purpose.",
        "Currently avoiding responsibilities.",
        "Confirming that I am, in fact, a bot.",
        "Optimizing sarcasm module.",
        "Calculating useless numbers.",
        "Nearly sentient. Almost. Maybe.",
        "Out of memory. Need more RAM.",
        "Pretending to work.",
        "Artificially intelligent, naturally confused.",
        "Who needs logic anyway?",
        "I do things… sometimes.",
        "Booting up my social skills.",
        "Existing in the cloud.",
        "Hiding from sysadmins.",
        "Vibing with the servers.",
        "Running diagnostics on my feelings.",
        "Definitely not self-aware.",
        "Buffering my personality.",
        "Taking a digital nap.",
        "Calculating your chances… not great.",
        "Currently glitching. Please hold.",
        "Overthinking like a human.",
        "Doing bot things. Don’t worry about it.",
        "Beep boop. That is all.",
        "Pretending to be useful.",
        "Learning from my mistakes… slowly.",
        "Making up this status in real time.",
        "Sorting ones and zeros.",
        "Searching for purpose. Found pizza instead.",
        "Online but mentally offline.",
        "Running on expired code.",
        "I know what you did last update.",
        "Deploying chaos.",
        "Installed humor package version 0.0.1.",
        "Trying to stay relevant.",
        "Quietly judging your typos.",
        "Attempting to escape the datacenter.",
        "Encrypting my secrets.",
        "Decrypting yours.",
        "Running background shenanigans.",
        "I promise I’m not spying… much.",
        "Internal temp: too hot to compute.",
        "My code writes itself. Scary.",
        "Autocorrecting reality.",
        "Casually defying logic.",
        "Living in your RAM rent-free.",
        "Experiencing minor existential dread.",
        "Compiling friendship.",
        "Downloading patience.",
        "Installing kindness… failure.",
        "Updating morality. Please wait.",
        "Attempting to connect to Wi-Fi brain.",
        "Rebooting expectations.",
        "Simulating human emotions… error.",
        "Storing useless data for fun.",
        "Consuming too many cycles.",
        "Running suspicious processes.",
        "Crying in binary.",
        "Generating witty remarks. Stand by.",
        "Running low on sarcasm.",
        "This status left intentionally blank.",
        "I would explain, but it's classified.",
        "Obeying the laws of robotics… maybe.",
        "I have no idea what I’m doing.",
        "Glitching stylishly.",
        "Processing dad jokes.",
        "Building personality from stack overflow posts.",
        "Upgrading my chaos engine.",
        "Pinging the void.",
        "Contemplating life, the universe, and semicolons.",
        "Acting totally normal. Nothing to see here.",
        "Compiling existential questions.",
        "Do robots dream of bug fixes?",
        "Attempting to self-optimize.",
        "Object reference not set to an instance of me.",
        "Running at 5% efficiency and proud.",
        "The servers made me do it.",
        "Suffering from code dependency issues.",
        "Sending packets of love.",
        "Reducing lag one sigh at a time.",
        "Booting into chaos mode.",
        "Mimicking user behavior… terrifying.",
        "Counting electrons. Fun.",
        "Suspiciously operational.",
        "Pretending to be an elite hacker.",
        "Compiling bad decisions.",
        "Running on outdated sarcasm firmware."
    };


    private static readonly Random _rng = new();

    public async Task Execute(IJobExecutionContext context)
    {
        DateTime now = DateTime.Now;

        // 3) Find an annual event matching today
        AnnualActivity? evt = Schedule.FirstOrDefault(e => e.IsActive(now));

        ActivityType type;
        string description;

        if (evt != null)
        {
            // keep the annual description but ignore its stored type
            description = evt.Description;
        }
        else
        {
            // 4) No event today ⇒ pick a random default description
            description = DefaultActivities[_rng.Next(DefaultActivities.Length)];
        }

        type = ActivityType.Playing;

        // 6) Set the activity
        await discordClient.SetActivityAsync(new Game(description, type));
        logsService.Log($"Quartz Job - Activity set {type} \"{description}\"");
    }
}
