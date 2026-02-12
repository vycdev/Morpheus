# Morpheus
[![Join the chat at https://discord.gg/nU63sFMcnX](https://img.shields.io/discord/1165553796223602708?style=flat-square&logo=discord&logoColor=white&label=Discord&color=%237289DA&link=https%3A%2F%2Fdiscord.gg%2FnU63sFMcnX)](https://discord.gg/nU63sFMcnX) 

Morpheus is a feature-rich Discord bot written in C# using Discord.NET and Entity Framework Core. It provides moderation helpers, a quotes subsystem with approval workflows, activity tracking, interactions (buttons/menus), and utility commands — all driven by a PostgreSQL database.

Key features include: quote submission & approval, per-quote voting/rating, guild-configurable approval channels, user activity jobs, and extensible command modules.

## Commands

A full, auto-generated list of bot commands and metadata is available in the repository: [COMMANDS.md](./COMMANDS.md).

Click the link to view command summaries, aliases, parameters, and rate limits.

## Notable characteristics

### A deeply nuanced XP system (arguably the most advanced of any Discord bot)
Morpheus evaluates messages through multiple, complementary signals designed to reward thoughtful contributions and de‑incentivize spam:

- Diminishing returns for length: XP grows with message length but tapers logarithmically, so very long messages don’t blow out the scale.
- Quality and anti‑spam signals:
	- Similarity suppression via 64‑bit SimHash on normalized trigram shingles to penalize near‑duplicates and copypasta.
	- Time‑gap smoothing with a logarithmic 0–5s recovery curve to discourage burst spam while keeping normal chat flow rewarding.
	- Typing‑speed cap (WPM) to reduce XP for unrealistically fast, low‑effort sequences.
- Context‑aware baselines: Each guild maintains an exponential moving average (EMA) of message length (N≈500), so “long” and “short” are relative to that server’s culture.
- Per‑user analytics: Per‑guild message count, average message length, and EMA are tracked and surfaced in leaderboards and graphs.

The result is balanced progression that feels fair across servers with different norms, while being robust against obvious gaming tactics.

### Leaderboards you’ll actually use
- XP leaderboards (guild/global) for all‑time and past N days.
- Messages‑sent leaderboards (guild/global) for all‑time and past N days.
- Average message length leaderboards (guild all‑time and global all‑time, globally weighted by message count).
- Unlimited pagination and “your rank” shown in every leaderboard.

Example commands: `leaderboard`, `leaderboardpast`, `globalleaderboard`, `globalleaderboardpast`,
`leaderboardmessages`, `leaderboardmessagespast`, `globalleaderboardmessages`, `globalleaderboardmessagespast`,
`leaderboardavglength`, `globalleaderboardavglength`.

### Quotes and level-up messages

Quotes
- Morpheus includes a full quotes subsystem: users can submit text quotes that are stored per-guild (or globally when a guild opts into global quotes).
- Submissions may require approval: a configurable approvals channel can be set per guild and add/remove requests post an approval message with interactive buttons so members can vote. Approval thresholds (add/remove required approvals) are configurable.
- Administrators may bypass approvals or force-approve/remove when permitted. Quotes support upvote/downvote and numeric rating commands; aggregated scores are used by commands like `quoteoftheday`, `quoteoftheweek`, `quoteofthemonth`, and the `listquotes` / `listquotesglobal` listings.

Level-up messages
- Level-up announcements are guild-configurable. Admins can set a channel to receive automatic level-up notifications and an optional separate channel for level-up quote posts.
- All channel targets and related toggles are stored in the guild configuration and can be changed via the guild administration commands (see `GuildModule`).
- There is also an administrative command to invalidate the XP of a specific message (reply to the message to run it); this will zero the recorded XP for that message and adjust the affected user totals.

### Activity graphs that scale
- Per‑day and cumulative XP charts for top users; also 7‑day rolling averages for smoother trends.
- Guild and global variants, with optional explicit date ranges and a configurable maximum window (ACTIVITY_GRAPHS_MAX_DAYS).
- Fast aggregation and PNG output suitable for quick sharing.
- The possibility to mention any list of users for a given graph.

### Automatic activity roles (daily)
- A daily job assigns activity‑based roles (e.g., top 1%, 5%, 10%, 20%, 30%) per guild.
- Roles are created automatically if missing, existing holders are cleared, and fresh recipients are assigned based on up‑to‑date activity.
- Gentle pacing and robust logging keep it reliable across large servers.

### A dynamic, XP-driven economy
Morpheus features a fully integrated economic system where value is directly tied to server activity:

- **Universal Basic Income (UBI):** A central pool of wealth is periodically distributed to all users. This pool is funded by a 0.01% daily wealth tax on all liquid balances, stock trading fees, profit taxes, and generous user donations.
- **Social-Stock Market:** Buy and sell "shares" of users, channels, or entire guilds. Stock prices are algorithmically determined by XP production: if an entity is more active today than their 7-day average, their stock price rises. Daily fluctuations are capped at ±10% to maintain stability.
- **High-Stakes Interaction:**
    - **Stealing:** Risk your own capital to attempt to pickpocket or heist other users. Success depends on the victim's "paranoia" (whether they've been robbed recently). Failure results in a settlement payment to the victim.
    - **Slots:** Gamble against the server's "Slots Vault" — a central bank that grows with losses and pays out from its own reserves.
- **Wealth Transfers:** Securely send money to other users, with a 5% fee (contributed to the UBI pool) to discourage circular loops and encourage a healthy velocity of money.

## Contributing


Contributions welcome! If you'd like to contribute bug fixes, new features, or improvements, please read the contributing guide for setup, workflow, and expectations: [CONTRIBUTING.md](./CONTRIBUTING.md).

Any help is appreciated — small PRs and clear descriptions make reviews faster.

