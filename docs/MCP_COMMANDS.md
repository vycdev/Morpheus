# MCP command integration

Morpheus exposes its Discord text-command registry through three MCP tools:

- `list_commands` returns every non-hidden command and alias, parameter shape,
  preconditions, possible effects, and the reviewed registry fingerprint.
- `describe_command` resolves any command alias to the same capability record.
- `run_command` validates or executes a command with Discord-equivalent context.

The manifest is built once from Discord.Net's live `CommandService` registry and
then cached. Tests compare the registry-derived fingerprint—including aliases,
parameters, preconditions, and effect classifications—to an explicitly reviewed
fingerprint. Adding or changing a command therefore fails the MCP parity gate
until the exposure is reviewed. All visible commands use the same general
dispatcher; hidden owner commands (`dumplogs`, `guildcount`, and `sendto`) are
intentionally absent.

## Trust boundary

The bearer key grants access to the complete MCP surface. Keep the listener on
loopback or behind an authenticated private proxy, use a long random key, and
share it only with the intended agent service.

An MCP request supplies Discord ids, not permissions. Morpheus resolves the
channel, guild, and member through its authenticated Discord session and runs
the original Discord.Net preconditions. A caller cannot grant itself
administrator, bot-owner, channel, or bot permissions by adding fields to the
request. Live execution requires a real source message. Its author must match
`userId`, the channel must belong to `guildId`, and bot accounts cannot invoke
commands.

`validate` is the safe default. It performs registry lookup, real preconditions,
and normal argument parsing but does not execute a module method, create missing
database users/guilds, increment a command cooldown, or produce command output.
It still reports an existing cooldown if the user has exhausted it.

`execute` runs the normal command pipeline and is deliberately disabled by
default. Once enabled, it can mutate Morpheus data, change Discord state, call
external services, or produce files according to the selected command. The
result's `sideEffectsMayHaveOccurred` flag must be treated as authoritative:
never retry with a new key when it is true or when a request times out.

## Invocation shape

IDs are decimal strings so JavaScript clients do not lose 64-bit snowflake
precision. `command` omits the guild prefix because the MCP tool already marks
the input as a command. Optional `locale` and `timeZoneId` values are validated
and carried as context metadata for command adapters; they never replace the
source message timestamp or Morpheus's authoritative server clock.

```json
{
  "invocation": {
    "command": "showquote 42",
    "userId": "123456789012345678",
    "guildId": "234567890123456789",
    "channelId": "345678901234567890",
    "sourceMessageId": "456789012345678901",
    "mode": "validate"
  }
}
```

For a validation-only synthetic invocation, omit `sourceMessageId` and optionally provide
`messageContent` and `replyToMessageId`. Morpheus creates only the message shell;
the user, guild, channel, referenced message, and their permissions still come
from Discord. This supports normal scalar arguments, mentions, reply-based
commands, and message-context checks without asking the AI to fabricate Discord
objects.

Validation may include bounded Discord-CDN attachment metadata. Execution
always requires `sourceMessageId`; Morpheus uses that real message's author,
reply, content, and attachments instead of trusting caller-supplied metadata.
This preserves normal reply, image, and bulk-subscription command behavior
without creating an identity-spoofing, SSRF, or oversized-download path.

To execute after a successful validation, set `mode` to `execute` and add a
unique 16–128 character `idempotencyKey`. `responseMode` defaults to `capture`,
which prevents Morpheus from posting a duplicate reply while Claudify interprets
the result. Use `responseMode: "discord"` when output must exist in Discord—for
example reaction-role, slots, emoji-import, help, or subscription browser
components:

```json
{
  "invocation": {
    "command": "showquote 42",
    "userId": "123456789012345678",
    "guildId": "234567890123456789",
    "channelId": "345678901234567890",
    "sourceMessageId": "456789012345678901",
    "mode": "execute",
    "idempotencyKey": "claudify:456789012345678901:showquote:42",
    "responseMode": "capture"
  }
}
```

Reusing the same key with the same invocation returns the cached result; using
it with different input is rejected. Results include the matched capability,
Discord.Net error category/reason, elapsed time, and ordered captured outputs.
Messages and embeds are structured, including embed image/thumbnail URLs,
colors, timestamps, authors, footers, and fields. With Discord response mode,
replies are both
delivered normally and captured, and returned message edits/reactions are
forwarded to the real message. Small files include a SHA-256 digest and base64
data; oversized files are reported as truncated rather than copied into the MCP
response. Interactive components are only descriptive in capture mode; they
become usable Discord-side sessions in Discord response mode.

## Claudify flow

1. Read the triggering Discord message and retain its user, guild, channel, and
   message ids.
2. Use `describe_command` when the agent needs parameter or effect information.
3. Call `run_command` with `mode: validate`.
4. Explain validation errors or ask for missing arguments/context.
5. For an intended action, call the same invocation with `mode: execute` and an
   idempotency key derived from the triggering message plus the chosen command.
6. Interpret `outputs` for the user. If `sideEffectsMayHaveOccurred` is true,
   never issue an automatic retry with a different key.

The tool captures responses addressed to the invoking channel, including
follow-up edits and generated files. Commands that intentionally target another
channel or mutate roles, reactions, webhooks, subscriptions, quotes, economy,
or stock data retain those live effects in execute mode; consult the manifest's
`effects` field before asking for confirmation.

## Operational limits

| Variable | Default | Purpose |
| --- | ---: | --- |
| `MCP_COMMAND_EXECUTION_ENABLED` | `false` | Enables command bodies. |
| `MCP_COMMAND_TIMEOUT_SECONDS` | `45` | MCP response deadline; timed-out work may finish. |
| `MCP_MAX_CONCURRENT_COMMANDS` | `4` | Bounds in-flight command bodies. |
| `MCP_MAX_COMMAND_LENGTH` | `2000` | Bounds command and synthetic message text. |
| `MCP_MAX_ATTACHMENTS` | `10` | Bounds synthetic validation metadata. |
| `MCP_MAX_ATTACHMENT_BYTES` | `8388608` | Maximum declared attachment size. |
| `MCP_MAX_CAPTURED_OUTPUT_BYTES` | `2097152` | Maximum cumulative inline file bytes. |
| `MCP_MAX_CAPTURED_OUTPUTS` | `100` | Maximum captured output events. |
| `MCP_IDEMPOTENCY_MINUTES` | `10` | Completed-result replay window. |

Discord.Net text modules do not accept cancellation tokens. If a command exceeds
the MCP deadline, Morpheus returns `timed-out`, retains its dependency scope and
concurrency slot until the command event completes, and caches that result under
the idempotency key. This fails closed under stuck work and prevents a timeout
from causing unsafe disposal or unbounded parallel command execution.
