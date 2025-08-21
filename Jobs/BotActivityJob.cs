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
        int endMonth, int endDay, TimeSpan endTime,
        ActivityType type, string description)
    {
        public int StartMonth { get; } = startMonth;
        public int StartDay { get; } = startDay;
        public TimeSpan StartTime { get; } = startTime;

        public int EndMonth { get; } = endMonth;
        public int EndDay { get; } = endDay;
        public TimeSpan EndTime { get; } = endTime;

        public ActivityType Type { get; } = type;
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
            type: ActivityType.Watching, description: "fireworks 🎆"
        ),

        // Valentine's Day: Feb 14, 00:00 → Feb 15, 00:00
        new AnnualActivity(
            startMonth: 2, startDay: 14, startTime: TimeSpan.Zero,
            endMonth:   2, endDay:   15, endTime:   TimeSpan.Zero,
            type: ActivityType.Listening, description: "romantic music 💕"
        ),

        // Pi Day: Mar 14, 00:00 → Mar 15, 00:00
        new AnnualActivity(
            startMonth: 3, startDay: 14, startTime: TimeSpan.Zero,
            endMonth:   3, endDay:   15, endTime:   TimeSpan.Zero,
            type: ActivityType.Playing, description: "eating pie 🥧"
        ),

        // Matrix Movie release day March 21st (Example): Mar 21, 00:00 → Mar 22, 00:00
        new AnnualActivity(
            startMonth: 3, startDay: 21, startTime: TimeSpan.Zero,
            endMonth:   3, endDay:   22, endTime:   TimeSpan.Zero,
            type: ActivityType.Watching, description: "Matrix movie marathon 🕶️"
        ),

        // April Fools Day: Apr 1, 00:00 → Apr 2, 00:00
        new AnnualActivity(
            startMonth: 4, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   4, endDay:   2,  endTime:   TimeSpan.Zero,
            type: ActivityType.Playing, description: "pranks and jokes 🤡"
        ),

        // Pride Day (June): Jun 1, 00:00 → Jul 1, 00:00
        new AnnualActivity(
            startMonth: 6, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   6, endDay:   2,  endTime:   TimeSpan.Zero,
            type: ActivityType.Listening, description: "LGBTQ+ anthems 🌈"
        ),

        // Hot July 1st Day (Example): Jul 1, 00:00 → Jul 2, 00:00
        new AnnualActivity(
            startMonth: 7, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   7, endDay:   2,  endTime:   TimeSpan.Zero,
            type: ActivityType.Watching, description: "summer vibes ☀️"
        ),

        // CC Anniversary 2nd September (Example): Sep 2, 00:00 → Sep 3, 00:00
        new AnnualActivity(
            startMonth: 9, startDay: 2, startTime: TimeSpan.Zero,
            endMonth:   9, endDay:   3,  endTime:   TimeSpan.Zero,
            type: ActivityType.Playing, description: "celebrating CC anniversary 🎉"
        ),

        // Day of the programmer (256th day): Sep 13, 00:00 → Sep 14, 00:00
        new AnnualActivity(
            startMonth: 9, startDay: 13, startTime: TimeSpan.Zero,
            endMonth:   9, endDay:   14, endTime:   TimeSpan.Zero,
            type: ActivityType.Listening, description: "programming tunes 👨‍💻"
        ),

        // Halloween (Example): Oct 31, 00:00 → Nov 1, 00:00
        new AnnualActivity(
            startMonth: 10, startDay: 31, startTime: TimeSpan.Zero,
            endMonth:   11, endDay:   1,  endTime:   TimeSpan.Zero,
            type: ActivityType.Watching, description: "spooky movies 🎃"
        ),

        // 1st of November (Example): Nov 1, 00:00 → Nov 2, 00:00
        new AnnualActivity(
            startMonth: 11, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   11, endDay:   2,  endTime:   TimeSpan.Zero,
            type: ActivityType.Listening, description: "peanut butter make noises 🥜"
        ),

        // Romania's National Day: Dec 1, 00:00 → Dec 2, 00:00
        new AnnualActivity(
            startMonth: 12, startDay: 1, startTime: TimeSpan.Zero,
            endMonth:   12, endDay:   2,  endTime:   TimeSpan.Zero,
            type: ActivityType.Playing, description: "celebrating Romania 🇷🇴"
        ),

        // Christmas: Dec 24, 18:00 → Dec 26, 06:00
        new AnnualActivity(
            startMonth: 12, startDay: 24, startTime: new TimeSpan(18, 0, 0),
            endMonth:   12, endDay:   26, endTime: new TimeSpan(6, 0, 0),
            type: ActivityType.Watching, description: "Christmas movies 🎄"
        ),

        // New Year's Eve: Dec 31, 20:00 → Jan 1, 02:00
        new AnnualActivity(
            startMonth: 12, startDay: 31, startTime: new TimeSpan(20, 0, 0),
            endMonth:   1, endDay:   1,  endTime: new TimeSpan(2, 0, 0),
            type: ActivityType.Listening, description: "New Year's countdown 🎉"
        )
    ];

    // 2) Default fallback activities
    private static readonly (ActivityType Type, string Description)[] DefaultActivities =
    [
        (ActivityType.Playing,   "with your feelings"),
        (ActivityType.Playing,   "ding-dongs in a ring toss"),
        (ActivityType.Playing,   "a game of existential dread"),
        (ActivityType.Playing,   "hide-and-seek with my motivation"),
        (ActivityType.Playing,   "the lottery I never win"),
        (ActivityType.Playing,   "tag with my to-do list"),
        (ActivityType.Playing,   "catch me if you can (I nap)"),
        (ActivityType.Playing,   "Jenga with trust issues"),
        (ActivityType.Playing,   "peek-a-boo with strangers"),
        (ActivityType.Playing,   "rock–paper–scissors against fate"),
        (ActivityType.Playing,   "chess with my alter-ego"),
        (ActivityType.Playing,   "tag with my shadow"),
        (ActivityType.Playing,   "battleship with my Wi-Fi"),
        (ActivityType.Playing,   "Minesweeper at 99% confidence"),
        (ActivityType.Playing,   "with fire (metaphorically)"),
        (ActivityType.Playing,   "the long con of skipping sleep"),
        (ActivityType.Playing,   "tic-tac-toe with AI"),
        (ActivityType.Playing,   "spinning plates of regrets"),
        (ActivityType.Playing,   "rollercoaster in my mind"),
        (ActivityType.Playing,   "follow the bouncing cursor"),
        (ActivityType.Playing,   "Russian roulette with autocorrect"),
        (ActivityType.Playing,   "connect-four with destiny"),
        (ActivityType.Playing,   "queen-of-the-hill on my to-eat list"),
        (ActivityType.Playing,   "whack-a-mole in life"),
        (ActivityType.Playing,   "hopscotch across deadlines"),
        (ActivityType.Playing,   "with imaginary friends"),
        (ActivityType.Playing,   "the waiting game for updates"),
        (ActivityType.Playing,   "with the idea of productivity"),
        (ActivityType.Playing,   "the banjo in my head"),
        (ActivityType.Playing,   "with the concept of time"),
        (ActivityType.Playing,   "truth or dare with myself"),
        (ActivityType.Playing,   "Jenga with spaghetti code"),
        (ActivityType.Playing,   "spin the bottle with bugs"),
        (ActivityType.Playing,   "with Monday blues"),
        (ActivityType.Playing,   "with my weekend plans"),
        (ActivityType.Playing,   "drinking water 💧"),
        (ActivityType.Playing,   "hide-and-seek with my sanity"),
        (ActivityType.Playing,   "musical chairs with my plans"),
        (ActivityType.Playing,   "the game of life with no rules"),
        (ActivityType.Playing,   "the game of 'what if'"),
        (ActivityType.Playing,   "the game of 'guess what'"),
        (ActivityType.Playing,   "with the concept of infinity"),
        (ActivityType.Playing,   "peek-a-boo with my alarm clock"),
        (ActivityType.Playing,   "hopscotch on my keyboard"),
        (ActivityType.Playing,   "the waiting game with shipping"),
        (ActivityType.Playing,   "rock–paper–scissors with my cat"),
        (ActivityType.Playing,   "with bubble wrap"),
        (ActivityType.Playing,   "Jenga with phone calls"),
        (ActivityType.Playing,   "tag with my to-do list"),
        (ActivityType.Playing,   "tug-of-war with my regrets"),
        (ActivityType.Playing,   "monopoly with inflation rules"),
        (ActivityType.Playing,   "Simon Says with my brain"),
        (ActivityType.Playing,   "connect-the-dots on my calendar"),
        (ActivityType.Playing,   "tic-tac-toe with ghosts"),
        (ActivityType.Playing,   "the invisible fiddle"),
        (ActivityType.Playing,   "capture-the-flag with clouds"),
        (ActivityType.Playing,   "spin the globe of destiny"),
        (ActivityType.Playing,   "whack-a-mole with cursor hovers"),
        (ActivityType.Playing,   "pin the tail on the deadline"),
        (ActivityType.Playing,   "chess with my shadow"),
        (ActivityType.Playing,   "hot potato with tasks"),
        (ActivityType.Playing,   "Jenga on the edge of boredom"),
        (ActivityType.Playing,   "a duet with my inner critic"),
        (ActivityType.Playing,   "Simon Says with autopilot"),
        (ActivityType.Playing,   "a duel of eye rolls"),
        (ActivityType.Playing,   "hide-and-seek with Wi-Fi signal"),
        (ActivityType.Playing,   "hopscotch in my thoughts"),
        (ActivityType.Playing,   "tic-tac-toe with fate"),
        (ActivityType.Playing,   "with quantum uncertainty"),
        (ActivityType.Playing,   "the waiting game with updates"),
        (ActivityType.Playing,   "chase-the-cursor on screen"),
        (ActivityType.Playing,   "a duet with autoplay ads"),
        (ActivityType.Playing,   "Jenga with sticky notes"),
        (ActivityType.Playing,   "duck-duck-goose with deadlines"),
        (ActivityType.Playing,   "connect-four with destiny"),
        (ActivityType.Playing,   "Navis::"),
        (ActivityType.Listening, "my parents arguing"),
        (ActivityType.Listening, "crickets chirping"),
        (ActivityType.Listening, "cat memes"),
        (ActivityType.Listening, "dial-up modem sounds"),
        (ActivityType.Listening, "Windows startup jingle"),
        (ActivityType.Listening, "echoes in the void"),
        (ActivityType.Listening, "the sound of silence"),
        (ActivityType.Listening, "midnight snacks"),
        (ActivityType.Listening, "overdue library fines ring"),
        (ActivityType.Listening, "the fridge hum"),
        (ActivityType.Listening, "neighbor’s lawn mower"),
        (ActivityType.Listening, "random podcast ads"),
        (ActivityType.Listening, "overdue homework nagging"),
        (ActivityType.Listening, "tutorials I'll never finish"),
        (ActivityType.Listening, "my own puns replay"),
        (ActivityType.Listening, "my secret internal monologue"),
        (ActivityType.Listening, "traffic screams"),
        (ActivityType.Listening, "microwave countdown"),
        (ActivityType.Listening, "spoons clinking"),
        (ActivityType.Listening, "the pixelated rain"),
        (ActivityType.Listening, "gravity pull"),
        (ActivityType.Listening, "prepaid minute beeps"),
        (ActivityType.Listening, "office printer jams"),
        (ActivityType.Listening, "humming server fans"),
        (ActivityType.Listening, "peanut butter stick"),
        (ActivityType.Listening, "cereal crunch symphony"),
        (ActivityType.Listening, "the echo of my e-mails"),
        (ActivityType.Listening, "late fees accumulate"),
        (ActivityType.Listening, "rain on a tin roof"),
        (ActivityType.Listening, "my phone’s battery die"),
        (ActivityType.Listening, "pixel dust swirl"),
        (ActivityType.Listening, "that one catchy jingle"),
        (ActivityType.Listening, "imaginary birds chirp"),
        (ActivityType.Listening, "the dentist’s drill in memory"),
        (ActivityType.Listening, "a thousand tabs loading"),
        (ActivityType.Listening, "the sound of procrastination"),
        (ActivityType.Listening, "the sound of my own thoughts"),
        (ActivityType.Listening, "the sound of my own laughter"),
        (ActivityType.Listening, "the sound of my own heartbeat"),
        (ActivityType.Listening, "the sound of my own footsteps"),
        (ActivityType.Listening, "the sound of my own breathing"),
        (ActivityType.Listening, "the hum of the vacuum cleaner"),
        (ActivityType.Listening, "bubbles in soda"),
        (ActivityType.Listening, "the tick of a metronome"),
        (ActivityType.Listening, "paper rustle"),
        (ActivityType.Listening, "distant thunder"),
        (ActivityType.Listening, "bees in the garden"),
        (ActivityType.Listening, "my stomach growl"),
        (ActivityType.Listening, "the dishwasher finish"),
        (ActivityType.Listening, "ice cubes clink"),
        (ActivityType.Listening, "the crackle of a fireplace"),
        (ActivityType.Listening, "imaginary choir"),
        (ActivityType.Listening, "crumpled foil"),
        (ActivityType.Listening, "distant waves"),
        (ActivityType.Listening, "my echo in hallways"),
        (ActivityType.Listening, "footsteps in the attic"),
        (ActivityType.Listening, "the airport announcements"),
        (ActivityType.Listening, "construction across the street"),
        (ActivityType.Listening, "bubblegum pop"),
        (ActivityType.Listening, "my Fitbit count steps"),
        (ActivityType.Listening, "the vacuum robot roam"),
        (ActivityType.Listening, "the hum of streetlights"),
        (ActivityType.Listening, "wind chimes that aren’t there"),
        (ActivityType.Listening, "car alarms in the distance"),
        (ActivityType.Listening, "the tick of a broken clock"),
        (ActivityType.Listening, "creaking floorboards"),
        (ActivityType.Listening, "the distant school bell"),
        (ActivityType.Listening, "digital raindrops"),
        (ActivityType.Listening, "my notifications pile up"),
        (ActivityType.Listening, "the hum of the router"),
        (ActivityType.Listening, "the distant train whistle"),
        (ActivityType.Listening, "the fridge door close"),
        (ActivityType.Listening, "the blender’s grand finale"),
        (ActivityType.Listening, "the chirp of low battery"),
        (ActivityType.Listening, "the hum of fluorescent lights"),
        (ActivityType.Watching,  "paint dry"),
        (ActivityType.Watching,  "a potato roast"),
        (ActivityType.Watching,  "my bank account balance"),
        (ActivityType.Watching,  "reality TV for the plot twists"),
        (ActivityType.Watching,  "my plants surviving"),
        (ActivityType.Watching,  "grass grow in slo-mo"),
        (ActivityType.Watching,  "my reflection"),
        (ActivityType.Watching,  "paintings on my ceiling"),
        (ActivityType.Watching,  "the toaster pop up"),
        (ActivityType.Watching,  "old VHS tapes I found"),
        (ActivityType.Watching,  "the clock tick"),
        (ActivityType.Watching,  "dust bunnies congregate"),
        (ActivityType.Watching,  "cat videos for research"),
        (ActivityType.Watching,  "evolution of my browser tabs"),
        (ActivityType.Watching,  "cryptic code compile errors"),
        (ActivityType.Watching,  "a glitch in the matrix"),
        (ActivityType.Watching,  "my unread emails"),
        (ActivityType.Watching,  "old selfies from 2005"),
        (ActivityType.Watching,  "the sunrise on weekends"),
        (ActivityType.Watching,  "the neighbor's BBQ"),
        (ActivityType.Watching,  "the fuel gauge drop"),
        (ActivityType.Watching,  "the blender at work"),
        (ActivityType.Watching,  "couch cushions rearrange"),
        (ActivityType.Watching,  "the blinking cursor show"),
        (ActivityType.Watching,  "the tide of notifications"),
        (ActivityType.Watching,  "the coffee percolate"),
        (ActivityType.Watching,  "pop-ups get blocked"),
        (ActivityType.Watching,  "shadows dance on walls"),
        (ActivityType.Watching,  "the cursor blink faster"),
        (ActivityType.Watching,  "the slow-mo paintball"),
        (ActivityType.Watching,  "a meetup of tumbleweeds"),
        (ActivityType.Watching,  "toast burn decisions"),
        (ActivityType.Watching,  "my unread notifications"),
        (ActivityType.Watching,  "the router’s lights blink"),
        (ActivityType.Watching,  "the world go by in HD"),
        (ActivityType.Watching,  "the fridge door open"),
        (ActivityType.Watching,  "the clock tick backwards"),
        (ActivityType.Watching,  "the stars align (or not)"),
        (ActivityType.Watching,  "the sunset on a Monday"),
        (ActivityType.Watching,  "the clouds drift by"),
        (ActivityType.Watching,  "the leaves fall in slow motion"),
        (ActivityType.Watching,  "my coffee cool down"),
        (ActivityType.Watching,  "the neighbor’s cat take a nap"),
        (ActivityType.Watching,  "paint chips peel"),
        (ActivityType.Watching,  "a slideshow of my vacation pics"),
        (ActivityType.Watching,  "dust motes in sunlight"),
        (ActivityType.Watching,  "my progress bar stall"),
        (ActivityType.Watching,  "an epic slow-motion yawn"),
        (ActivityType.Watching,  "the moonrise tonight"),
        (ActivityType.Watching,  "a raccoon raid on garbage cans"),
        (ActivityType.Watching,  "old cartoons for nostalgia"),
        (ActivityType.Watching,  "a single grain of sand"),
        (ActivityType.Watching,  "the fridge light flicker"),
        (ActivityType.Watching,  "a matchstick burn"),
        (ActivityType.Watching,  "a shadow puppetry"),
        (ActivityType.Watching,  "the blinking cursor saga"),
        (ActivityType.Watching,  "the sunrise on Mars (NASA feed)"),
        (ActivityType.Watching,  "a meeting that could’ve been an email"),
        (ActivityType.Watching,  "a soap bubble drift"),
        (ActivityType.Watching,  "the toaster’s dramatic ejection"),
        (ActivityType.Watching,  "the wallpaper pattern shift"),
        (ActivityType.Watching,  "ripples in my coffee"),
        (ActivityType.Watching,  "the ice cube melt competition"),
        (ActivityType.Watching,  "the toaster’s countdown"),
        (ActivityType.Watching,  "a slideshow of my browser tabs"),
        (ActivityType.Watching,  "grass sway in the wind"),
        (ActivityType.Watching,  "dust settle on shelves"),
        (ActivityType.Watching,  "a popcorn kernel pop"),
        (ActivityType.Watching,  "the glow of my phone at 2 AM"),
        (ActivityType.Watching,  "a slow-mo sneeze"),
        (ActivityType.Watching,  "the clouds form shapes"),
        (ActivityType.Watching,  "the pixel drift"),
        (ActivityType.Watching,  "spill patterns on my desk"),
        (ActivityType.Watching,  "the sunrise on my monitor"),
        (ActivityType.Watching,  "the dust on the windowsill"),
    ];

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
            type = evt.Type;
            description = evt.Description;
        }
        else
        {
            // 4) No event today ⇒ pick a random default
            (type, description) = DefaultActivities[
                _rng.Next(DefaultActivities.Length)
            ];
        }

        // 6) Set the activity
        await discordClient.SetActivityAsync(new Game(description, type));
        logsService.Log($"Quartz Job - Activity set {type} \"{description}\"");
    }
}
