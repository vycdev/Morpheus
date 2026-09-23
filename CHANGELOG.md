# Changelog

## Unreleased

- Keep long quote approval requests and finalized status messages within Discord's message limit.
- Reject transfer amounts whose fee would overflow the supported money range.
- Prevent per-user activity XP and message counters from wrapping at the integer limit.
- Fall back to Atom entry links when entry IDs are blank.
- Keep large stock portfolios within Discord's embed description limit.
- Fall back to canonical YouTube video URLs when feed entry links are blank.
- Prevent guild activity message counts from wrapping negative at the integer limit.
- Reject malformed and out-of-range ports on bare URL hosts without matching only a valid-looking prefix.
- Require every supplied MCP Discord ID to contain 1–20 ASCII digits and represent a positive 64-bit value; reject signs, whitespace, and control characters. Omit optional IDs with `null`, not an empty string.
- Match application emoji names consistently across host cultures.
- Preserve emoji when truncating bulk subscription failure summaries.
- Allow activity leaderboards to cover day ranges larger than the representable date history.
- Bound oversized activity similarity windows to the supported `DateTime` range.
- Handle username-check timestamps near the `DateTime` limit without failing message processing.
- Recognize uppercase guild stock targets regardless of the host culture.
- Avoid caching Twitch access tokens when the reported lifetime is negative or below the safety margin.
- Handle quote approval expiry settings beyond the `DateTime` range without failing interactions.
- Preserve emoji when truncating command summaries in help pages.
- Reject reminder durations whose numeric values exceed the supported range.
- Reject activity graph day counts and explicit ranges whose query bounds would exceed the supported date range.
- Preserve emoji when limiting RSS subscription display names.
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
- Trim surrounding whitespace from RSS and Atom entry links before posting them.
- Add an authenticated, origin-restricted, rate-limited MCP Streamable HTTP
  server for read-only aggregate activity, guild statistics, leaderboards, and
  approved quotes.
