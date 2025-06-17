using Discord.WebSocket;
using Discord;
using Quartz;
using Morpheus.Services;

namespace Morpheus.Jobs;

public class ActivityJob(LogsService logsService, DiscordSocketClient discordClient) : IJob
{
    // A time‐boxed activity that recurs every year
    private class AnnualActivity
    {
        public int StartMonth { get; }
        public int StartDay { get; }
        public TimeSpan StartTime { get; }

        public int EndMonth { get; }
        public int EndDay { get; }
        public TimeSpan EndTime { get; }

        public ActivityType Type { get; }
        public string Description { get; }

        public AnnualActivity(
            int startMonth, int startDay, TimeSpan startTime,
            int endMonth, int endDay, TimeSpan endTime,
            ActivityType type, string description)
        {
            StartMonth = startMonth;
            StartDay = startDay;
            StartTime = startTime;
            EndMonth = endMonth;
            EndDay = endDay;
            EndTime = endTime;
            Type = type;
            Description = description;
        }

        public bool IsActive(DateTime now)
        {
            // Build this year's start/end DateTimes
            var year = now.Year;
            var start = new DateTime(year, StartMonth, StartDay)
                            .Add(StartTime);
            var end = new DateTime(year, EndMonth, EndDay)
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
    private static readonly AnnualActivity[] Schedule = new[]
    {
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
    };

    // 2) Default fallback activities
    private static readonly (ActivityType Type, string Description)[] DefaultActivities =
    {
        (ActivityType.Playing,   "with your feelings"),
        (ActivityType.Watching,  "paint dry"),
        (ActivityType.Listening, "to my parents arguing"),
        (ActivityType.Playing,   "ding-dongs in a ring toss"),
        (ActivityType.Watching,  "a potato roast"),
        (ActivityType.Listening, "to crickets chirping"),
        (ActivityType.Playing,   "a game of existential dread"),
        (ActivityType.Watching,  "reality TV for the plot twists"),
        (ActivityType.Listening, "to cat memes"),
        (ActivityType.Playing,   "hide-and-seek with my motivation"),
        (ActivityType.Watching,  "my bank account balance"),
        (ActivityType.Listening, "to dial-up modem sounds"),
        (ActivityType.Playing,   "the lottery I never win"),
        (ActivityType.Watching,  "grass grow in slo-mo"),
        (ActivityType.Listening, "to Windows startup jingle"),
        (ActivityType.Playing,   "tag with my to-do list"),
        (ActivityType.Watching,  "my plants surviving"),
        (ActivityType.Listening, "to echoes in the void"),
        (ActivityType.Playing,   "catch me if you can (I nap)"),
        (ActivityType.Watching,  "old VHS tapes I found"),
        (ActivityType.Listening, "to the sound of silence"),
        (ActivityType.Playing,   "Jenga with trust issues"),
        (ActivityType.Watching,  "my reflection"),
        (ActivityType.Listening, "to midnight snacks"),
        (ActivityType.Playing,   "peek-a-boo with strangers"),
        (ActivityType.Watching,  "paintings on my ceiling"),
        (ActivityType.Listening, "to overdue library fines ring"),
        (ActivityType.Playing,   "rock–paper–scissors against fate"),
        (ActivityType.Watching,  "the toaster pop up"),
        (ActivityType.Listening, "the fridge hum"),
        (ActivityType.Playing,   "chess with my alter-ego"),
        (ActivityType.Watching,  "the clock tick"),
        (ActivityType.Listening, "to neighbor’s lawn mower"),
        (ActivityType.Playing,   "tag with my shadow"),
        (ActivityType.Watching,  "dust bunnies congregate"),
        (ActivityType.Listening, "to random podcast ads"),
        (ActivityType.Playing,   "battleship with my Wi-Fi"),
        (ActivityType.Watching,  "cat videos for research"),
        (ActivityType.Listening, "to overdue homework nagging"),
        (ActivityType.Playing,   "Minesweeper at 99% confidence"),
        (ActivityType.Watching,  "evolution of my browser tabs"),
        (ActivityType.Listening, "to tutorials I'll never finish"),
        (ActivityType.Playing,   "with fire (metaphorically)"),
        (ActivityType.Watching,  "cryptic code compile errors"),
        (ActivityType.Listening, "to my own puns replay"),
        (ActivityType.Playing,   "the long con of skipping sleep"),
        (ActivityType.Watching,  "a glitch in the matrix"),
        (ActivityType.Listening, "to my secret internal monologue"),
        (ActivityType.Playing,   "tic-tac-toe with AI"),
        (ActivityType.Watching,  "my unread emails"),
        (ActivityType.Listening, "to traffic screams"),
        (ActivityType.Playing,   "spinning plates of regrets"),
        (ActivityType.Watching,  "old selfies from 2005"),
        (ActivityType.Listening, "to microwave countdown"),
        (ActivityType.Playing,   "rollercoaster in my mind"),
        (ActivityType.Watching,  "the sunrise on weekends"),
        (ActivityType.Listening, "to spoons clinking"),
        (ActivityType.Playing,   "follow the bouncing cursor"),
        (ActivityType.Watching,  "the neighbor's BBQ"),
        (ActivityType.Listening, "to the pixelated rain"),
        (ActivityType.Playing,   "Russian roulette with autocorrect"),
        (ActivityType.Watching,  "the fuel gauge drop"),
        (ActivityType.Listening, "to gravity pull"),
        (ActivityType.Playing,   "connect-four with destiny"),
        (ActivityType.Watching,  "the blender at work"),
        (ActivityType.Listening, "to prepaid minute beeps"),
        (ActivityType.Playing,   "queen-of-the-hill on my to-eat list"),
        (ActivityType.Watching,  "couch cushions rearrange"),
        (ActivityType.Listening, "to office printer jams"),
        (ActivityType.Playing,   "whack-a-mole in life"),
        (ActivityType.Watching,  "the blinking cursor show"),
        (ActivityType.Listening, "to humming server fans"),
        (ActivityType.Playing,   "hopscotch across deadlines"),
        (ActivityType.Watching,  "the tide of notifications"),
        (ActivityType.Listening, "to peanut butter stick"),
        (ActivityType.Playing,   "with imaginary friends"),
        (ActivityType.Watching,  "the coffee percolate"),
        (ActivityType.Listening, "to cereal crunch symphony"),
        (ActivityType.Playing,   "the waiting game for updates"),
        (ActivityType.Watching,  "pop-ups get blocked"),
        (ActivityType.Listening, "to the echo of my e-mails"),
        (ActivityType.Playing,   "with the idea of productivity"),
        (ActivityType.Watching,  "shadows dance on walls"),
        (ActivityType.Listening, "to late fees accumulate"),
        (ActivityType.Playing,   "the banjo in my head"),
        (ActivityType.Watching,  "the cursor blink faster"),
        (ActivityType.Listening, "to rain on a tin roof"),
        (ActivityType.Playing,   "with the concept of time"),
        (ActivityType.Watching,  "the slow-mo paintball"),
        (ActivityType.Listening, "to my phone’s battery die"),
        (ActivityType.Playing,   "truth or dare with myself"),
        (ActivityType.Watching,  "a meetup of tumbleweeds"),
        (ActivityType.Listening, "to pixel dust swirl"),
        (ActivityType.Playing,   "Jenga with spaghetti code"),
        (ActivityType.Watching,  "toast burn decisions"),
        (ActivityType.Listening, "to that one catchy jingle"),
        (ActivityType.Playing,   "spin the bottle with bugs"),
        (ActivityType.Watching,  "my unread notifications"),
        (ActivityType.Listening, "to imaginary birds chirp"),
        (ActivityType.Playing,   "with Monday blues"),
        (ActivityType.Watching,  "the router’s lights blink"),
        (ActivityType.Listening, "to the dentist’s drill in memory"),
        (ActivityType.Playing,   "with my weekend plans"),
        (ActivityType.Watching,  "the world go by in HD"),
        (ActivityType.Listening, "to a thousand tabs loading"),
        (ActivityType.Playing,   "drinking water 💧"),
        (ActivityType.Watching,  "the fridge door open"),
        (ActivityType.Listening, "to the sound of procrastination"),
        (ActivityType.Playing,   "hide-and-seek with my sanity"),
        (ActivityType.Watching,  "the clock tick backwards"),
        (ActivityType.Listening, "to the sound of my own thoughts"),
        (ActivityType.Playing,   "musical chairs with my plans"),
        (ActivityType.Watching,  "the stars align (or not)"),
        (ActivityType.Listening, "to the sound of my own laughter"),
        (ActivityType.Playing,   "the game of life with no rules"),
        (ActivityType.Watching,  "the sunset on a Monday"),
        (ActivityType.Listening, "to the sound of my own heartbeat"),
        (ActivityType.Playing,   "the game of 'what if'"),
        (ActivityType.Watching,  "the clouds drift by"),
        (ActivityType.Listening, "to the sound of my own footsteps"),
        (ActivityType.Playing,   "the game of 'guess what'"),
        (ActivityType.Watching,  "the leaves fall in slow motion"),
        (ActivityType.Listening, "to the sound of my own breathing"),
        (ActivityType.Playing,   "with the concept of infinity"),
        (ActivityType.Watching,  "my coffee cool down"),
        (ActivityType.Listening, "to the hum of the vacuum cleaner"),
        (ActivityType.Playing,   "peek-a-boo with my alarm clock"),
        (ActivityType.Watching,  "the neighbor’s cat take a nap"),
        (ActivityType.Listening, "to bubbles in soda"),
        (ActivityType.Playing,   "hopscotch on my keyboard"),
        (ActivityType.Watching,  "paint chips peel"),
        (ActivityType.Listening, "to the tick of a metronome"),
        (ActivityType.Playing,   "the waiting game with shipping"),
        (ActivityType.Watching,  "a slideshow of my vacation pics"),
        (ActivityType.Listening, "to paper rustle"),
        (ActivityType.Playing,   "rock–paper–scissors with my cat"),
        (ActivityType.Watching,  "dust motes in sunlight"),
        (ActivityType.Listening, "to distant thunder"),
        (ActivityType.Playing,   "with bubble wrap"),
        (ActivityType.Watching,  "my progress bar stall"),
        (ActivityType.Listening, "to bees in the garden"),
        (ActivityType.Playing,   "Jenga with phone calls"),
        (ActivityType.Watching,  "an epic slow-motion yawn"),
        (ActivityType.Listening, "to my stomach growl"),
        (ActivityType.Playing,   "tag with my to-do list"),
        (ActivityType.Watching,  "the moonrise tonight"),
        (ActivityType.Listening, "to the dishwasher finish"),
        (ActivityType.Playing,   "tug-of-war with my regrets"),
        (ActivityType.Watching,  "a raccoon raid on garbage cans"),
        (ActivityType.Listening, "to ice cubes clink"),
        (ActivityType.Playing,   "monopoly with inflation rules"),
        (ActivityType.Watching,  "old cartoons for nostalgia"),
        (ActivityType.Listening, "to the crackle of a fireplace"),
        (ActivityType.Playing,   "Simon Says with my brain"),
        (ActivityType.Watching,  "a single grain of sand"),
        (ActivityType.Listening, "to imaginary choir"),
        (ActivityType.Playing,   "connect-the-dots on my calendar"),
        (ActivityType.Watching,  "the fridge light flicker"),
        (ActivityType.Listening, "to crumpled foil"),
        (ActivityType.Playing,   "tic-tac-toe with ghosts"),
        (ActivityType.Watching,  "a matchstick burn"),
        (ActivityType.Listening, "to distant waves"),
        (ActivityType.Playing,   "the invisible fiddle"),
        (ActivityType.Watching,  "a shadow puppetry"),
        (ActivityType.Listening, "to my echo in hallways"),
        (ActivityType.Playing,   "capture-the-flag with clouds"),
        (ActivityType.Watching,  "the blinking cursor saga"),
        (ActivityType.Listening, "to footsteps in the attic"),
        (ActivityType.Playing,   "spin the globe of destiny"),
        (ActivityType.Watching,  "the sunrise on Mars (NASA feed)"),
        (ActivityType.Listening, "to the airport announcements"),
        (ActivityType.Playing,   "whack-a-mole with cursor hovers"),
        (ActivityType.Watching,  "a meeting that could’ve been an email"),
        (ActivityType.Listening, "to construction across the street"),
        (ActivityType.Playing,   "pin the tail on the deadline"),
        (ActivityType.Watching,  "a soap bubble drift"),
        (ActivityType.Listening, "to bubblegum pop"),
        (ActivityType.Playing,   "chess with my shadow"),
        (ActivityType.Watching,  "the toaster’s dramatic ejection"),
        (ActivityType.Listening, "to my Fitbit count steps"),
        (ActivityType.Playing,   "hot potato with tasks"),
        (ActivityType.Watching,  "the wallpaper pattern shift"),
        (ActivityType.Listening, "to the vacuum robot roam"),
        (ActivityType.Playing,   "Jenga on the edge of boredom"),
        (ActivityType.Watching,  "ripples in my coffee"),
        (ActivityType.Listening, "to the hum of streetlights"),
        (ActivityType.Playing,   "a duet with my inner critic"),
        (ActivityType.Watching,  "the ice cube melt competition"),
        (ActivityType.Listening, "to wind chimes that aren’t there"),
        (ActivityType.Playing,   "Simon Says with autopilot"),
        (ActivityType.Watching,  "the toaster’s countdown"),
        (ActivityType.Listening, "to car alarms in the distance"),
        (ActivityType.Playing,   "a duel of eye rolls"),
        (ActivityType.Watching,  "a slideshow of my browser tabs"),
        (ActivityType.Listening, "to the tick of a broken clock"),
        (ActivityType.Playing,   "hide-and-seek with Wi-Fi signal"),
        (ActivityType.Watching,  "grass sway in the wind"),
        (ActivityType.Listening, "to creaking floorboards"),
        (ActivityType.Playing,   "hopscotch in my thoughts"),
        (ActivityType.Watching,  "dust settle on shelves"),
        (ActivityType.Listening, "to the distant school bell"),
        (ActivityType.Playing,   "tic-tac-toe with fate"),
        (ActivityType.Watching,  "a popcorn kernel pop"),
        (ActivityType.Listening, "to digital raindrops"),
        (ActivityType.Playing,   "with quantum uncertainty"),
        (ActivityType.Watching,  "the glow of my phone at 2 AM"),
        (ActivityType.Listening, "to my notifications pile up"),
        (ActivityType.Playing,   "the waiting game with updates"),
        (ActivityType.Watching,  "a slow-mo sneeze"),
        (ActivityType.Listening, "to the hum of the router"),
        (ActivityType.Playing,   "chase-the-cursor on screen"),
        (ActivityType.Watching,  "the clouds form shapes"),
        (ActivityType.Listening, "to the distant train whistle"),
        (ActivityType.Playing,   "a duet with autoplay ads"),
        (ActivityType.Watching,  "the pixel drift"),
        (ActivityType.Listening, "to the fridge door close"),
        (ActivityType.Playing,   "Jenga with sticky notes"),
        (ActivityType.Watching,  "spill patterns on my desk"),
        (ActivityType.Listening, "to the blender’s grand finale"),
        (ActivityType.Playing,   "duck-duck-goose with deadlines"),
        (ActivityType.Watching,  "the sunrise on my monitor"),
        (ActivityType.Listening, "to the chirp of low battery"),
        (ActivityType.Playing,   "connect-four with destiny"),
        (ActivityType.Watching,  "the dust on the windowsill"),
        (ActivityType.Listening, "to the hum of fluorescent lights")
    };

    private static readonly Random _rng = new();

    public async Task Execute(IJobExecutionContext context)
    {
        var now = DateTime.Now;

        // 3) Find an annual event matching today
        var evt = Schedule.FirstOrDefault(e => e.IsActive(now));

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
