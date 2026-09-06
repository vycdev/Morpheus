# Changelog

## Unreleased

- Coalesce concurrent Twitch access-token refreshes after authentication failures.
- Preserve emoji when truncating long MCP command output.
- Preserve emoji when truncating Urban Dictionary definitions and examples.
- Trim surrounding whitespace from YouTube feed links before posting them.
- Preserve emoji when truncating feed names in the subscription browser.
- Keep overlong RSS entry content within Discord's message limit.
- Keep long feed identities within Discord's webhook username limit.
- Avoid splitting emoji surrogate pairs when truncating quote lists.
- Ignore indented comments when loading `.env` configuration files.
- Trim surrounding whitespace from xkcd RSS links before posting them.
- Allow welcome and goodbye messages to work when custom emote settings are omitted.
- Keep reviewed MCP command registry fingerprints stable across operating systems.
- Trim surrounding whitespace from RSS entry links before posting them.
- Add an authenticated, origin-restricted, rate-limited MCP Streamable HTTP
  server for read-only aggregate activity, guild statistics, leaderboards, and
  approved quotes.
