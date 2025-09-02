#!/usr/bin/env python3
r"""
Import Discord Chat Exporter JSON into the Morpheus database, computing XP exactly
like ActivityHandler.cs (hashing, SimHash similarity, WPM/time penalties, and
guild average length), and updating UserLevels.

Usage (Windows PowerShell):
  python Tools\import_dc_json.py --file Tools\example.json
  python Tools\import_dc_json.py --dir C:\path\to\exports --pattern *.json
  python Tools\import_dc_json.py --file Tools\example.json --dry-run

Environment:
  Reads DB_CONNECTION_STRING from .env in repo root or process env.
  The connection string should be in Npgsql format (e.g., "Host=...;Username=...;Password=...;Database=...").

Dependencies:
  pip install -r Tools/requirements.txt
"""
from __future__ import annotations

import argparse
import base64
import datetime as dt
import json
import math
import os
import sys
import time
import unicodedata
from dataclasses import dataclass
from collections import deque, defaultdict
import heapq
from pathlib import Path
import fnmatch
from typing import Dict, Iterable, List, Optional, Tuple

try:
    # psycopg 3
    import psycopg
    from psycopg import sql
except Exception as e:  # pragma: no cover
    print("psycopg is required. Install with: pip install psycopg[binary]", file=sys.stderr)
    raise

try:
    from dotenv import load_dotenv
except Exception:
    load_dotenv = None  # optional fallback

try:
    import xxhash
except Exception as e:  # pragma: no cover
    print("xxhash is required. Install with: pip install xxhash", file=sys.stderr)
    raise


# ===================== JSON models (loose) =====================

def _get(d: dict, key: str, default=None):
    v = d.get(key, default)
    return v if v is not None else default


@dataclass
class JsonAuthor:
    id: str
    name: str
    is_bot: bool

    @staticmethod
    def from_json(d: dict) -> "JsonAuthor":
        return JsonAuthor(
            id=str(_get(d, "id", "0")),
            name=str(_get(d, "name", "")),
            is_bot=bool(_get(d, "isBot", False)),
        )


@dataclass
class JsonMessage:
    id: str
    content: str
    timestamp: dt.datetime
    author: JsonAuthor

    @staticmethod
    def from_json(d: dict) -> "JsonMessage":
        ts = _get(d, "timestamp")
        # RFC 3339/ISO with offset
        when = dt.datetime.fromisoformat(ts.replace("Z", "+00:00")) if isinstance(ts, str) else dt.datetime.utcnow()
        return JsonMessage(
            id=str(_get(d, "id", "0")),
            content=str(_get(d, "content", "")),
            timestamp=when.astimezone(dt.timezone.utc),
            author=JsonAuthor.from_json(_get(d, "author", {})),
        )


@dataclass
class JsonExport:
    guild_id: str
    guild_name: str
    channel_id: str
    messages: List[JsonMessage]

    @staticmethod
    def from_json(d: dict) -> "JsonExport":
        guild = _get(d, "guild", {})
        channel = _get(d, "channel", {})
        msgs = [JsonMessage.from_json(m) for m in _get(d, "messages", [])]
        return JsonExport(
            guild_id=str(_get(guild, "id", "0")),
            guild_name=str(_get(guild, "name", "Imported Guild")),
            channel_id=str(_get(channel, "id", "0")),
            messages=msgs,
        )


# ===================== SimHasher parity with C# =====================

def normalize_text(s: str) -> str:
    """Mirror Morpheus.Utilities.Text.SimHasher.Normalize.

    Steps:
      - NFKD
      - lowercase
      - collapse whitespace to single spaces
      - strip combining marks
      - remove punctuation/symbol/control/surrogate/format
      - map digits -> '0'
      - drop VS16 (FE0F), ZWJ (200D), ZWSP (200B)
    """
    if not s:
        return ""

    nfkd = unicodedata.normalize("NFKD", s).lower()
    out: List[str] = []
    last_space = False

    for ch in nfkd:
        # whitespace collapse
        if ch.isspace():
            if not last_space:
                out.append(" ")
                last_space = True
            continue
        last_space = False

        # drop combining marks (diacritics)
        cat = unicodedata.category(ch)
        if cat in ("Mn", "Mc"):
            continue

        # remove punctuation/symbols/control/surrogate/format
        if cat[0] in ("P", "S", "C"):
            # Keep space handled above; C covers Cc/Cf/Cs
            # We'll special-case a few below
            pass

        # special zero-width & variation selectors
        code = ord(ch)
        if code in (0xFE0F, 0x200D, 0x200B):
            continue

        # skip most punctuation/symbol/control
        if cat[0] in ("P", "S"):
            continue
        if cat[0] == "C":
            # control/format/surrogate
            continue

        # map digits -> '0'
        if ch.isdigit():
            out.append("0")
            continue

        out.append(ch)

    return "".join(out).strip()


def fnv1a64_over_utf16_units(s: str) -> int:
    """FNV-1a 64 over UTF-16 code units like the C# implementation.

    For each char, process low byte then high byte.
    """
    offset = 14695981039346656037
    prime = 1099511628211
    h = offset
    for ch in s:
        c = ord(ch)
        low = c & 0xFF
        high = (c >> 8) & 0xFF
        h ^= low
        h = (h * prime) & 0xFFFFFFFFFFFFFFFF
        h ^= high
        h = (h * prime) & 0xFFFFFFFFFFFFFFFF
    return h


def compute_simhash(text: str) -> Tuple[int, int]:
    norm = normalize_text(text)
    n = len(norm)
    if n < 3:
        return (0, n)
    weights = [0] * 64
    for i in range(0, n - 2):
        tri = norm[i : i + 3]
        h = fnv1a64_over_utf16_units(tri)
        for b in range(64):
            weights[b] += 1 if ((h >> b) & 1) else -1
    sim = 0
    for b in range(64):
        if weights[b] >= 0:
            sim |= (1 << b)
    return (sim, n)


def hamming_distance(a: int, b: int) -> int:
    x = (a ^ b) & ((1 << 64) - 1)
    try:
        return x.bit_count()  # py3.8+
    except AttributeError:  # pragma: no cover
        return bin(x).count("1")


def xxh64_base64(data: str) -> str:
    d = xxhash.xxh64(data.encode("utf-8")).digest()
    return base64.b64encode(d).decode("ascii")


# ===================== XP logic (mirror ActivityHandler.cs) =====================

def smoothstep_0_1(s: float) -> float:
    if s < 0.0:
        s = 0.0
    elif s > 1.0:
        s = 1.0
    return s * s * (3.0 - 2.0 * s)


def calculate_level(total_xp: int) -> int:
    # return (int)Math.Pow(Math.Log10((xp + 111) / 111), 5.0243);
    v = (total_xp + 111.0) / 111.0
    if v <= 0:
        return 0
    return int(math.pow(math.log10(v), 5.0243))


# ===================== DB helpers =====================

def parse_npgsql_to_libpq(npgsql_cs: str) -> str:
    """Convert Npgsql-style conn string to libpq/psycopg dsn."""
    if not npgsql_cs:
        return ""
    pairs = [p.strip() for p in npgsql_cs.split(";") if p.strip()]
    kv: Dict[str, str] = {}
    for p in pairs:
        if "=" not in p:
            continue
        k, v = p.split("=", 1)
        k = k.strip().lower()
        v = v.strip()
        kv[k] = v

    mapping = {
        "host": "host",
        "server": "host",
        "hostname": "host",
        "port": "port",
        "username": "user",
        "user id": "user",
        "userid": "user",
        "user": "user",
        "password": "password",
        "pwd": "password",
        "database": "dbname",
        "initial catalog": "dbname",
        "ssl mode": "sslmode",
    }
    out: Dict[str, str] = {}
    for k, v in kv.items():
        dst = mapping.get(k)
        if not dst:
            continue
        if k == "ssl mode":
            v = v.lower()
            if v in ("disable", "disabled"):
                v = "disable"
            elif v in ("require", "required"):
                v = "require"
            elif v in ("prefer",):
                v = "prefer"
        out[dst] = v

    # Build DSN
    parts = []
    for key in ("host", "port", "user", "password", "dbname", "sslmode"):
        if key in out and out[key] != "":
            parts.append(f"{key}={out[key]}")
    return " ".join(parts)


def load_connection_string() -> str:
    # Load .env if available
    root = Path(__file__).resolve().parents[1]
    dotenv_path = root / ".env"
    if load_dotenv and dotenv_path.exists():
        load_dotenv(dotenv_path)
    # Fallback to default.env if .env missing
    if os.getenv("DB_CONNECTION_STRING") in (None, ""):
        default_env = root / "default.env"
        if default_env.exists():
            for line in default_env.read_text(encoding="utf-8").splitlines():
                if line.strip().startswith("DB_CONNECTION_STRING="):
                    os.environ.setdefault("DB_CONNECTION_STRING", line.split("=", 1)[1].strip())
                    break
    raw = os.getenv("DB_CONNECTION_STRING", "").strip()
    if raw.startswith("postgres://") or raw.startswith("postgresql://"):
        return raw
    return parse_npgsql_to_libpq(raw)


# ===================== Importer =====================

class Importer:
    def __init__(self, conn: psycopg.Connection, dry_run: bool = False):
        self.conn = conn
        self.dry = dry_run
        # Similarity window in minutes (match ActivityHandler default/env)
        try:
            self.similarity_window_minutes = int(os.getenv("ACTIVITY_SIMILARITY_WINDOW_MINUTES", "10"))
        except Exception:
            self.similarity_window_minutes = 10
        # Cache for UserLevels to minimize per-message round-trips
        # key: (user_id, guild_id) -> (starting_total_xp, starting_level, start_msg_count, start_avg_len, start_ema_len)
        self._ul_start: Dict[Tuple[int, int], Tuple[int, int, int, float, float]] = {}
        # key: (user_id, guild_id) -> accumulated deltas (xp, msg_count, sum_len) and rolling EMA
        # We store sum_len to recompute running average upon flush; EMA updated per message
        self._ul_delta: Dict[Tuple[int, int], Tuple[int, int, int, float]] = {}

    def ensure_guild(self, discord_id: int, name: str) -> int:
        with self.conn.cursor() as cur:
            cur.execute("SELECT \"Id\" FROM \"Guilds\" WHERE \"DiscordId\" = %s", (discord_id,))
            row = cur.fetchone()
            if row:
                return row[0]
            # Insert minimal guild matching non-null constraints
            now = dt.datetime.now(dt.timezone.utc)
            cur.execute(
                """
                INSERT INTO "Guilds" (
                    "DiscordId", "Name", "Prefix",
                    "WelcomeChannelId", "PinsChannelId",
                    "LevelUpMessagesChannelId", "LevelUpQuotesChannelId",
                    "LevelUpMessages", "LevelUpQuotes", "UseGlobalQuotes",
                    "QuotesApprovalChannelId", "QuoteAddRequiredApprovals", "QuoteRemoveRequiredApprovals",
                    "WelcomeMessages", "UseActivityRoles", "InsertDate"
                ) VALUES (
                    %s, %s, %s,
                    0, 0,
                    0, 0,
                    false, false, false,
                    0, 5, 5,
                    false, false, %s
                ) RETURNING "Id"
                """,
                (discord_id, name or "Imported Guild", "m!", now),
            )
            gid = cur.fetchone()[0]
            return gid

    def ensure_user(self, discord_id: int, username: str) -> int:
        with self.conn.cursor() as cur:
            cur.execute("SELECT \"Id\", \"Username\" FROM \"Users\" WHERE \"DiscordId\" = %s", (discord_id,))
            row = cur.fetchone()
            if row:
                uid = row[0]
                cur_name = row[1] or ""
                if username and username != cur_name:
                    cur.execute(
                        "UPDATE \"Users\" SET \"Username\" = %s, \"LastUsernameCheck\" = %s WHERE \"Id\" = %s",
                        (username, dt.datetime.now(dt.timezone.utc), uid),
                    )
                return uid
            now = dt.datetime.now(dt.timezone.utc)
            cur.execute(
                """
                INSERT INTO "Users" (
                    "DiscordId", "Username", "InsertDate", "LastUsernameCheck",
                    "LevelUpMessages", "LevelUpQuotes"
                ) VALUES (%s, %s, %s, %s, %s, %s) RETURNING "Id"
                """,
                (discord_id, username or "", now, now, True, True),
            )
            return cur.fetchone()[0]

    def ensure_userlevels(self, user_id: int, guild_id: int) -> Tuple[int, int, int, float, float]:
        with self.conn.cursor() as cur:
            cur.execute(
                "SELECT \"Id\", \"Level\", \"TotalXp\", \"UserMessageCount\", \"UserAverageMessageLength\", \"UserAverageMessageLengthEma\" FROM \"UserLevels\" WHERE \"UserId\" = %s AND \"GuildId\" = %s",
                (user_id, guild_id),
            )
            row = cur.fetchone()
            if row:
                total_xp = int(row[2])
                level = int(row[1])
                msg_count = int(row[3] or 0)
                avg_len = float(row[4] or 0.0)
                ema_len = float(row[5] or 0.0)
                return total_xp, level, msg_count, avg_len, ema_len
            cur.execute(
                "INSERT INTO \"UserLevels\" (\"UserId\", \"GuildId\", \"Level\", \"TotalXp\", \"UserMessageCount\", \"UserAverageMessageLength\", \"UserAverageMessageLengthEma\") VALUES (%s, %s, %s, %s, %s, %s, %s)",
                (user_id, guild_id, 0, 0, 0, 0.0, 0.0),
            )
            return 0, 0, 0, 0.0, 0.0

    def get_prev_user_activity(self, user_id: int, guild_id: int, before_ts: dt.datetime):
        with self.conn.cursor() as cur:
            cur.execute(
                """
                SELECT "Id", "InsertDate", "MessageHash" FROM "UserActivity"
                WHERE "UserId"=%s AND "GuildId"=%s AND "InsertDate" < %s
                ORDER BY "InsertDate" DESC LIMIT 1
                """,
                (user_id, guild_id, before_ts),
            )
            return cur.fetchone()  # (id, insertdate, messagehash)

    def get_recent_simhashes_in_window(self, user_id: int, guild_id: int, before_ts: dt.datetime) -> List[Tuple[int, int, dt.datetime]]:
        with self.conn.cursor() as cur:
            window_start = before_ts - dt.timedelta(minutes=self.similarity_window_minutes)
            cur.execute(
                """
                SELECT "MessageSimHash", "NormalizedLength", "InsertDate"
                FROM "UserActivity"
                WHERE "UserId"=%s AND "GuildId"=%s AND "InsertDate" >= %s AND "InsertDate" < %s
                ORDER BY "InsertDate" DESC LIMIT 200
                """,
                (user_id, guild_id, window_start, before_ts),
            )
            return [(int(r[0]), int(r[1]), r[2]) for r in cur.fetchall()]

    def get_prev_guild_activity(self, guild_id: int, before_ts: dt.datetime):
        with self.conn.cursor() as cur:
            cur.execute(
                """
                SELECT "GuildAverageMessageLength", "GuildMessageCount"
                FROM "UserActivity"
                WHERE "GuildId"=%s AND "InsertDate" < %s
                ORDER BY "InsertDate" DESC LIMIT 1
                """,
                (guild_id, before_ts),
            )
            return cur.fetchone()  # (avgLen, count)

    def insert_user_activity(
        self,
        discord_channel_id: int,
        guild_id: int,
        user_id: int,
        insert_date: dt.datetime,
        message_hash: str,
        message_length: int,
        simhash: int,
        norm_len: int,
        xp: int,
        guild_avg_len: float,
        guild_msg_count: int,
    ):
        with self.conn.cursor() as cur:
            cur.execute(
                """
                INSERT INTO "UserActivity" (
                    "DiscordChannelId", "GuildId", "UserId", "InsertDate",
                    "MessageHash", "MessageLength", "MessageSimHash", "NormalizedLength",
                    "XpGained", "GuildAverageMessageLength", "GuildMessageCount"
                ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """,
                (
                    discord_channel_id,
                    guild_id,
                    user_id,
                    insert_date,
                    message_hash,
                    message_length,
                    simhash,
                    norm_len,
                    xp,
                    guild_avg_len,
                    guild_msg_count,
                ),
            )

    def update_userlevels(self, user_id: int, guild_id: int, delta_xp: int, msg_len: int):
        """Deprecated per-message updater retained for API compatibility; now we batch updates.

        Accumulate delta locally; ensure a starting value exists in cache via ensure_userlevels().
        """
        key = (user_id, guild_id)
        if key not in self._ul_start:
            total, level, msg_count, avg_len, ema_len = self.ensure_userlevels(user_id, guild_id)
            self._ul_start[key] = (int(total), int(level), int(msg_count), float(avg_len), float(ema_len))
        # deltas: xp_delta, msg_count_delta, sum_len_delta, ema_current (we will overwrite ema_current each call)
        xp_d, cnt_d, sum_d, ema_cur = self._ul_delta.get(key, (0, 0, 0, self._ul_start[key][4]))
        xp_d += int(delta_xp)
        cnt_d += 1
        sum_d += int(msg_len)
        # EMA update per message with N=500
        alpha = 2.0 / (500.0 + 1.0)
        prev_ema = ema_cur if ema_cur > 0.0 else self._ul_start[key][4]
        new_ema = float(msg_len) if prev_ema <= 0.0 else ((1.0 - alpha) * prev_ema + alpha * float(msg_len))
        self._ul_delta[key] = (xp_d, cnt_d, sum_d, new_ema)

    def flush_userlevels_updates(self):
        """Apply all accumulated UserLevels updates in one pass."""
        if not self._ul_delta:
            return
        with self.conn.cursor() as cur:
            for (user_id, guild_id), (xp_delta, cnt_delta, sum_len_delta, ema_cur) in self._ul_delta.items():
                start_total, _start_level, start_cnt, start_avg, start_ema = self._ul_start.get((user_id, guild_id), (0, 0, 0, 0.0, 0.0))
                total_new = int(start_total) + int(xp_delta)
                level_new = calculate_level(total_new)
                # Recompute average from starting count/avg and delta sum
                new_cnt = int(start_cnt) + int(cnt_delta)
                if new_cnt > 0:
                    # starting sum = start_avg * start_cnt
                    start_sum = float(start_avg) * float(start_cnt)
                    new_sum = start_sum + float(sum_len_delta)
                    new_avg = new_sum / float(new_cnt)
                else:
                    new_avg = 0.0
                new_ema = float(ema_cur) if float(ema_cur) > 0.0 else float(start_ema)
                cur.execute(
                    "UPDATE \"UserLevels\" SET \"TotalXp\"=%s, \"Level\"=%s, \"UserMessageCount\"=%s, \"UserAverageMessageLength\"=%s, \"UserAverageMessageLengthEma\"=%s WHERE \"UserId\"=%s AND \"GuildId\"=%s",
                    (total_new, level_new, new_cnt, new_avg, new_ema, user_id, guild_id),
                )
        # Clear caches after flush
        self._ul_delta.clear()
        self._ul_start.clear()

    # ------------- XP parity -------------
    def compute_xp_for_message(
        self,
        content: str,
        now_utc: dt.datetime,
        prev_user_activity: Optional[Tuple[int, dt.datetime, str]],
        recent: List[Tuple[int, int, dt.datetime]],
        prev_guild_activity: Optional[Tuple[float, int]],
    ) -> Tuple[int, int, int]:
        # Hashes
        msg_hash = xxh64_base64(content)
        sim_hash, norm_len = compute_simhash(content)

        # Base XP (match ActivityHandler)
        base_xp = 1

        # Length-based XP (logarithmic taper relative to guild average)
        # r = L / A, clamped to [0, 100]; bonus = B * log(1 + k*r) / log(1 + k)
        B_len = 4.0
        k_len = 0.025
        if prev_guild_activity is not None and prev_guild_activity[0] > 0:
            guild_avg = float(prev_guild_activity[0])
            r = len(content) / guild_avg if guild_avg > 0 else 1.0
        else:
            r = 1.0
        if r < 0.0:
            r = 0.0
        elif r > 100.0:
            r = 100.0
        denom_len = math.log(1.0 + k_len)
        message_length_xp = (B_len * math.log(1.0 + (k_len * r)) / denom_len) if denom_len > 0 else (B_len * r)

        # similarityPenaltySimple (same hash within 60s)
        similarity_penalty_simple = 1.0
        if prev_user_activity is not None:
            _, prev_ts, prev_hash = prev_user_activity
            if prev_hash == msg_hash and abs((now_utc - prev_ts).total_seconds()) < 60:
                similarity_penalty_simple = 0.0

        # speedPenaltySimple (logarithmic over 0..5s)
        speed_penalty_simple = 1.0
        if prev_user_activity is not None:
            _, prev_ts, _ = prev_user_activity
            dt_sec = (now_utc - prev_ts).total_seconds()
            if dt_sec < 0:
                dt_sec = 0.0
            if dt_sec > 5.0:
                dt_sec = 5.0
            k = 9.0
            denom = math.log(1.0 + k * 5.0)
            speed_penalty_simple = math.log(1.0 + k * dt_sec) / denom if denom > 0 else 1.0

        # similarityPenaltyComplex via SimHash against recent messages within window
        similarity_penalty_complex = 1.0
        if norm_len >= 12 and sim_hash != 0 and recent:
            max_similarity = 0.0
            for prev_sim, prev_norm_len, _ in recent:
                if prev_sim == 0 or prev_norm_len < 12:
                    continue
                hd = hamming_distance(sim_hash, prev_sim)
                sim = 1.0 - (hd / 64.0)
                if sim > max_similarity:
                    max_similarity = sim
            if max_similarity >= 0.92:
                similarity_penalty_complex = 0.0
            elif max_similarity >= 0.85:
                similarity_penalty_complex = 0.25

        # speedPenaltyComplex (WPM for long messages)
        speed_penalty_complex = 1.0
        if prev_user_activity is not None and len(content) >= 50:
            _, prev_ts, _ = prev_user_activity
            minutes_since_prev = max((now_utc - prev_ts).total_seconds() / 60.0, 1e-6)
            cpm = len(content) / minutes_since_prev
            wpm = cpm / 5.0
            if wpm > 200.0:
                if wpm >= 300.0:
                    speed_penalty_complex = 0.0
                else:
                    x = (wpm - 200.0) / 100.0
                    dec = math.log(1.0 + 9.0 * x, 10)
                    speed_penalty_complex = 1.0 - dec

        xp = int(math.floor((base_xp + message_length_xp) * similarity_penalty_simple * similarity_penalty_complex * speed_penalty_simple * speed_penalty_complex))
        return xp, sim_hash, norm_len

    # ------------- Import one export -------------
    def import_export(self, export: JsonExport, only_guild_id: Optional[int] = None) -> int:
        gid_discord = int(export.guild_id)
        if only_guild_id is not None and gid_discord != only_guild_id:
            return 0

        # Ensure guild row
        guild_id = self.ensure_guild(gid_discord, export.guild_name)

        # Process messages in chronological order
        msgs = sorted(export.messages, key=lambda m: m.timestamp)

        total = len(msgs)
        if total == 0:
            print(f"Nothing to import for guild={gid_discord} channel={export.channel_id}")
            return 0

        print(
            f"Importing guild={gid_discord} ('{export.guild_name}') channel={export.channel_id} messages={total} | window={self.similarity_window_minutes}m"
        )

        inserted = 0
        xp_positive = 0
        t0 = time.time()
        last_draw = t0
        bar_width = 30

        def draw_progress(i: int):
            now = time.time()
            nonlocal last_draw
            # throttle updates to ~4Hz
            if i + 1 < total and (now - last_draw) < 0.25:
                return
            last_draw = now
            done = i + 1
            frac = done / total
            filled = int(frac * bar_width)
            bar = "#" * filled + "-" * (bar_width - filled)
            rate = done / max(now - t0, 1e-6)
            eta = (total - done) / max(rate, 1e-6)
            # format ETA
            eta_i = int(max(0, eta))
            h, rem = divmod(eta_i, 3600)
            m, s = divmod(rem, 60)
            eta_str = f"{h:d}:{m:02d}:{s:02d}" if h else f"{m:02d}:{s:02d}"
            sys.stdout.write(
                f"\r[{bar}] {frac*100:5.1f}% {done}/{total} | {rate:6.1f} msg/s | ETA {eta_str}"
            )
            sys.stdout.flush()

        with self.conn.transaction():
            for i, m in enumerate(msgs):
                if m.author.is_bot:
                    # Skip bots but still advance progress
                    draw_progress(i)
                    continue

                user_id = self.ensure_user(int(m.author.id), m.author.name)
                # Ensure a UserLevels row exists like the C# handler does (even if XP ends up 0),
                # but only once per (user,guild). Also seed the cache with current totals.
                key = (user_id, guild_id)
                if key not in self._ul_start:
                    total_xp, level, msg_count, avg_len, ema_len = self.ensure_userlevels(user_id, guild_id)
                    self._ul_start[key] = (int(total_xp), int(level), int(msg_count), float(avg_len), float(ema_len))

                prev_user = self.get_prev_user_activity(user_id, guild_id, m.timestamp)
                recent = self.get_recent_simhashes_in_window(user_id, guild_id, m.timestamp)
                prev_guild = self.get_prev_guild_activity(guild_id, m.timestamp)

                xp, sim_hash, norm_len = self.compute_xp_for_message(
                    m.content, m.timestamp, prev_user, recent, prev_guild
                )

                # Compute new guild averages (EMA, N=500)
                ema_alpha = 2.0 / (500.0 + 1.0)
                if prev_guild is not None:
                    prev_avg, prev_count = prev_guild
                    msg_count = int(prev_count) + 1
                    if float(prev_avg) <= 0.0:
                        avg_len = float(len(m.content))
                    else:
                        avg_len = (1.0 - ema_alpha) * float(prev_avg) + ema_alpha * float(len(m.content))
                else:
                    msg_count = 1
                    avg_len = float(len(m.content))

                if not self.dry:
                    self.insert_user_activity(
                        int(export.channel_id),
                        guild_id,
                        user_id,
                        m.timestamp,
                        xxh64_base64(m.content),
                        len(m.content),
                        sim_hash,
                        norm_len,
                        xp,
                        avg_len,
                        msg_count,
                    )
                    if xp > 0:
                        # Accumulate updates; we'll flush once at the end of the file
                        self.update_userlevels(user_id, guild_id, xp, len(m.content))
                        xp_positive += 1

                inserted += 1
                # draw progress periodically
                draw_progress(i)

            # finalize progress line
            sys.stdout.write("\n")

            # Apply all pending UserLevels updates once per file to reduce locking/round trips
            if not self.dry:
                before = len(self._ul_delta)
                self.flush_userlevels_updates()
                print(f"Flushed {before} UserLevels updates")

        elapsed = time.time() - t0
        print(
            f"Done guild={gid_discord} channel={export.channel_id}: inserted={inserted}, xp>0={xp_positive}, in {elapsed:.1f}s"
        )
        return inserted

    # ------------- FAST PATH (bulk, minimal queries) -------------
    def _seed_guild_baseline(self, guild_id: int, first_ts: dt.datetime) -> Tuple[float, int]:
        prev_guild = self.get_prev_guild_activity(guild_id, first_ts)
        if prev_guild is not None:
            return float(prev_guild[0]), int(prev_guild[1])
        return 0.0, 0

    def _seed_prev_user_map(self, guild_id: int, first_ts: dt.datetime) -> Dict[int, Tuple[dt.datetime, str]]:
        """Get last activity before first_ts for all users in guild, in one query.

        Returns: user_id -> (insert_date, message_hash)
        """
        prev_map: Dict[int, Tuple[dt.datetime, str]] = {}
        with self.conn.cursor() as cur:
            cur.execute(
                """
                SELECT DISTINCT ON ("UserId") "UserId", "InsertDate", "MessageHash"
                FROM "UserActivity"
                WHERE "GuildId"=%s AND "InsertDate" < %s
                ORDER BY "UserId", "InsertDate" DESC
                """,
                (guild_id, first_ts),
            )
            for uid, ts, h in cur.fetchall():
                prev_map[int(uid)] = (ts, str(h))
        return prev_map

    def _seed_recent_simhashes(self, guild_id: int, first_ts: dt.datetime) -> Dict[int, deque]:
        """Load recent simhashes for all users in window before first_ts.

        Returns: user_id -> deque[(simhash:int, norm_len:int, ts:datetime)] (newest first)
        """
        per_user: Dict[int, deque] = defaultdict(deque)
        window_start = first_ts - dt.timedelta(minutes=self.similarity_window_minutes)
        with self.conn.cursor() as cur:
            cur.execute(
                """
                SELECT "UserId", "MessageSimHash", "NormalizedLength", "InsertDate"
                FROM "UserActivity"
                WHERE "GuildId"=%s AND "InsertDate" >= %s AND "InsertDate" < %s
                ORDER BY "UserId", "InsertDate" DESC
                """,
                (guild_id, window_start, first_ts),
            )
            for uid, simv, normv, ts in cur.fetchall():
                dq = per_user[int(uid)]
                # keep newest-first: appendleft newest, cap at 200 from the right (oldest)
                dq.appendleft((int(simv), int(normv), ts))
                if len(dq) > 200:
                    dq.pop()
        return per_user

    def import_fast(self, exports: List[JsonExport], only_guild_id: Optional[int] = None) -> int:
        """High-throughput importer: merges messages across files per guild, computes XP with
        in-memory rolling state, and bulk-inserts via COPY. Greatly reduces DB round-trips.

        Notes:
        - To preserve exact parity across channels within a guild, messages are processed in
          strict chronological order across all provided files for that guild.
        - For existing DB content before the earliest provided message, we seed guild averages,
          per-user last message, and per-user similarity window using one-time queries.
        """
        # Group exports by guild
        exports_by_guild: Dict[int, List[JsonExport]] = defaultdict(list)
        for ex in exports:
            gid = int(ex.guild_id)
            if only_guild_id is not None and gid != only_guild_id:
                continue
            # sort messages per file to enable k-way merge
            ex.messages.sort(key=lambda m: m.timestamp)
            exports_by_guild[gid].append(ex)

        total_inserted_all = 0

        for gid_discord, exs in exports_by_guild.items():
            # Ensure guild once
            guild_name = exs[0].guild_name if exs else "Imported Guild"
            guild_id = self.ensure_guild(gid_discord, guild_name)

            # Collect all distinct authors (discord ids and names) to pre-ensure Users
            authors: Dict[str, str] = {}
            first_ts: Optional[dt.datetime] = None
            msg_count_total = 0
            for ex in exs:
                msg_count_total += len(ex.messages)
                for m in ex.messages:
                    if first_ts is None or m.timestamp < first_ts:
                        first_ts = m.timestamp
                    if not m.author.is_bot:
                        authors[m.author.id] = m.author.name

            if first_ts is None or msg_count_total == 0:
                continue

            print(
                f"FAST import guild={gid_discord} ('{guild_name}') files={len(exs)} messages={msg_count_total} | window={self.similarity_window_minutes}m"
            )

            # Ensure Users and map discordId->userId
            user_map: Dict[str, int] = {}
            for did, name in authors.items():
                uid = self.ensure_user(int(did), name)
                user_map[did] = uid

            # Ensure UserLevels rows for all (user,guild)
            for did in user_map.keys():
                uid = user_map[did]
                key = (uid, guild_id)
                if key not in self._ul_start:
                    total_xp, level, msg_count, avg_len, ema_len = self.ensure_userlevels(uid, guild_id)
                    self._ul_start[key] = (int(total_xp), int(level), int(msg_count), float(avg_len), float(ema_len))

            # Seed baselines from DB before first_ts
            guild_avg, guild_count = self._seed_guild_baseline(guild_id, first_ts)

            prev_user_map = self._seed_prev_user_map(guild_id, first_ts)  # user_id -> (ts, hash)
            recent_sim_by_user = self._seed_recent_simhashes(guild_id, first_ts)  # user_id -> deque

            # Build k-way merge of messages across files for this guild
            # Heap entries: (timestamp, idx, channel_id, JsonMessage)
            heap = []
            iters = []
            for idx, ex in enumerate(exs):
                it = iter(ex.messages)
                iters.append((ex.channel_id, it))
                try:
                    first = next(it)
                    heap.append((first.timestamp, idx, ex.channel_id, first))
                except StopIteration:
                    pass
            if heap:
                heapq.heapify(heap)

            inserted = 0
            xp_positive = 0
            t0 = time.time()
            last_draw = t0
            bar_width = 30

            def draw_progress(done: int):
                now = time.time()
                nonlocal last_draw
                # throttle updates to ~4Hz
                if done < msg_count_total and (now - last_draw) < 0.25:
                    return
                last_draw = now
                frac = done / msg_count_total
                filled = int(frac * bar_width)
                bar = "#" * filled + "-" * (bar_width - filled)
                rate = done / max(now - t0, 1e-6)
                eta = (msg_count_total - done) / max(rate, 1e-6)
                eta_i = int(max(0, eta))
                h, rem = divmod(eta_i, 3600)
                m, s = divmod(rem, 60)
                eta_str = f"{h:d}:{m:02d}:{s:02d}" if h else f"{m:02d}:{s:02d}"
                sys.stdout.write(
                    f"\r[{bar}] {frac*100:5.1f}% {done}/{msg_count_total} | {rate:6.1f} msg/s | ETA {eta_str}"
                )
                sys.stdout.flush()

            with self.conn.transaction():
                with self.conn.cursor() as cur:
                    # Speed up commit for this transaction
                    try:
                        cur.execute("SET LOCAL synchronous_commit = OFF")
                    except Exception:
                        pass
                # COPY bulk insert
                copy_sql = (
                    """
                    COPY "UserActivity" (
                        "DiscordChannelId", "GuildId", "UserId", "InsertDate",
                        "MessageHash", "MessageLength", "MessageSimHash", "NormalizedLength",
                        "XpGained", "GuildAverageMessageLength", "GuildMessageCount"
                    ) FROM STDIN
                    """
                )
                with self.conn.cursor() as cur:
                    with cur.copy(copy_sql) as cp:
                        processed = 0
                        while heap:
                            ts, idx, channel_id, msg = heapq.heappop(heap)
                            if msg.author.is_bot:
                                # advance iterator and continue
                                try:
                                    nxt = next(iters[idx][1])
                                    heapq.heappush(heap, (nxt.timestamp, idx, channel_id, nxt))
                                except StopIteration:
                                    pass
                                processed += 1
                                draw_progress(processed)
                                continue

                            uid = user_map[msg.author.id]
                            # Build prev_user tuple as in classic path
                            prev_entry = prev_user_map.get(uid)
                            prev_user = None
                            if prev_entry is not None:
                                prev_user = (-1, prev_entry[0], prev_entry[1])  # id unused

                            # Recent simhashes deque for this user
                            dq = recent_sim_by_user.get(uid)
                            recent_list: List[Tuple[int, int, dt.datetime]] = []
                            if dq:
                                # drop any outside window
                                cutoff = ts - dt.timedelta(minutes=self.similarity_window_minutes)
                                # dq is newest-first; iterate and keep those >= cutoff
                                kept = []
                                for simv, normv, tprev in dq:
                                    if tprev >= cutoff and tprev < ts:
                                        kept.append((int(simv), int(normv), tprev))
                                recent_list = kept[:200]

                            xp, sim_hash, norm_len = self.compute_xp_for_message(
                                msg.content, ts, prev_user, recent_list, (guild_avg, guild_count)
                            )

                            # Compute new guild averages for next iterations (EMA, N=500)
                            ema_alpha = 2.0 / (500.0 + 1.0)
                            guild_count_next = guild_count + 1
                            if guild_avg <= 0.0:
                                guild_avg_next = float(len(msg.content))
                            else:
                                guild_avg_next = (1.0 - ema_alpha) * float(guild_avg) + ema_alpha * float(len(msg.content))

                            # Prepare row
                            row = (
                                int(channel_id),
                                guild_id,
                                uid,
                                ts,
                                xxh64_base64(msg.content),
                                len(msg.content),
                                sim_hash,
                                norm_len,
                                xp,
                                guild_avg_next,  # matches C# storing values at insert time
                                guild_count_next,
                            )
                            cp.write_row(row)

                            # Update rolling state
                            if xp > 0:
                                self.update_userlevels(uid, guild_id, xp, len(msg.content))
                                xp_positive += 1

                            # prev_user_map -> now
                            prev_user_map[uid] = (ts, xxh64_base64(msg.content))
                            # recent simhashes
                            if dq is None:
                                dq = deque()
                                recent_sim_by_user[uid] = dq
                            dq.appendleft((sim_hash, norm_len, ts))
                            # trim by window time and cap 200
                            cutoff2 = ts - dt.timedelta(minutes=self.similarity_window_minutes)
                            while dq and dq[-1][2] < cutoff2:
                                dq.pop()
                            while len(dq) > 200:
                                dq.pop()

                            guild_avg, guild_count = guild_avg_next, guild_count_next

                            inserted += 1
                            processed += 1

                            # advance the iterator for this file
                            try:
                                nxt = next(iters[idx][1])
                                heapq.heappush(heap, (nxt.timestamp, idx, channel_id, nxt))
                            except StopIteration:
                                pass

                            draw_progress(processed)

                # finalize progress line
                sys.stdout.write("\n")

                # Apply all pending UserLevels updates once per guild
                if not self.dry:
                    before = len(self._ul_delta)
                    self.flush_userlevels_updates()
                    print(f"Flushed {before} UserLevels updates")

            elapsed = time.time() - t0
            print(
                f"Done FAST guild={gid_discord}: inserted={inserted}, xp>0={xp_positive}, in {elapsed:.1f}s"
            )

            total_inserted_all += inserted

        return total_inserted_all


def load_json_file(path: Path) -> JsonExport:
    text = path.read_text(encoding="utf-8")
    try:
        data = json.loads(text)
    except json.JSONDecodeError as e:
        # Build a helpful error with file, line, column and a caret marker
        try:
            lines = text.splitlines()
            ln = getattr(e, "lineno", None) or 0
            col = getattr(e, "colno", None) or 0
            src_line = lines[ln - 1] if 1 <= ln <= len(lines) else ""
            marker = (" " * max(0, col - 1)) + "^"
            msg = f"JSON parse error in {path} at line {ln}, column {col}: {e.msg}\n{src_line}\n{marker}"
        except Exception:
            msg = f"JSON parse error in {path}: {e}"
        raise ValueError(msg) from e
    return JsonExport.from_json(data)


def iter_json_files(root: Path, pattern: str = "*.json") -> Iterable[Path]:
    """Yield files in root matching pattern (non-recursive)."""
    entries = []
    try:
        for p in root.iterdir():
            if p.is_file() and fnmatch.fnmatch(p.name, pattern):
                entries.append(p)
    except FileNotFoundError:
        return
    for p in sorted(entries):
        yield p


def main(argv: Optional[List[str]] = None) -> int:
    ap = argparse.ArgumentParser(description="Import Discord Chat Exporter JSON into Morpheus DB")
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--file", type=str, help="Path to a single export JSON file")
    g.add_argument("--dir", type=str, help="Directory containing JSON files (non-recursive)")
    ap.add_argument("--pattern", type=str, default="*.json", help="Filename pattern for --dir (non-recursive, default: *.json)")
    ap.add_argument("--only-guild", type=str, default=None, help="Only import for this Discord guild id")
    ap.add_argument("--dry-run", action="store_true", help="Parse and compute, but do not write to DB")
    ap.add_argument("--fast", action="store_true", help="High-throughput mode: bulk process all files with COPY per guild")
    ap.add_argument("--skip-bad-files", action="store_true", help="Skip files that fail to parse with JSON errors")

    args = ap.parse_args(argv)

    dsn = load_connection_string()
    if not dsn and not args.dry_run:
        print("DB_CONNECTION_STRING not set; provide .env or environment", file=sys.stderr)
        return 2

    only_guild_id = int(args.only_guild) if args.only_guild else None

    files: List[Path]
    if args.file:
        files = [Path(args.file).resolve()]
    else:
        files = list(iter_json_files(Path(args.dir).resolve(), args.pattern))
    if not files:
        print("No JSON files found.")
        return 0

    total_inserted = 0
    if args.dry_run:
        # Parse and show progress to validate logic without DB writes
        total = len(files)
        print(f"Loading {total} JSON file(s) for dry run...")
        t0 = time.time()
        last_draw = t0
        bar_width = 30

        def draw_progress_files(done: int, current: Optional[str] = None):
            now = time.time()
            nonlocal last_draw
            if done < total and (now - last_draw) < 0.25:
                return
            last_draw = now
            frac = (done / total) if total else 1.0
            filled = int(frac * bar_width)
            bar = "#" * filled + "-" * (bar_width - filled)
            rate = done / max(now - t0, 1e-6)
            eta = (total - done) / max(rate, 1e-6)
            eta_i = int(max(0, eta))
            h, rem = divmod(eta_i, 3600)
            m, s = divmod(rem, 60)
            eta_str = f"{h:d}:{m:02d}:{s:02d}" if h else f"{m:02d}:{s:02d}"
            suffix = f" | {current}" if current else ""
            sys.stdout.write(f"\r[{bar}] {frac*100:5.1f}% {done}/{total} | {rate:6.1f} files/s | ETA {eta_str}{suffix}")
            sys.stdout.flush()

        loaded = 0
        for f in files:
            try:
                export = load_json_file(f)
            except Exception as e:
                if args.skip_bad_files:
                    sys.stdout.write(f"\nWARNING: Skipping {f} due to error: {e}\n")
                    loaded += 1
                    draw_progress_files(loaded, f.name)
                    continue
                raise
            print(f"\nLoaded {f.name}: guild={export.guild_id} channel={export.channel_id} messages={len(export.messages)}")
            loaded += 1
            draw_progress_files(loaded, f.name)
        sys.stdout.write("\n")
        return 0

    with psycopg.connect(dsn) as conn:
        imp = Importer(conn, dry_run=False)
        if args.fast:
            total = len(files)
            print(f"Loading {total} JSON file(s) before FAST import...")
            t0 = time.time()
            last_draw = t0
            bar_width = 30

            def draw_progress_files(done: int, current: Optional[str] = None):
                now = time.time()
                nonlocal last_draw
                if done < total and (now - last_draw) < 0.25:
                    return
                last_draw = now
                frac = (done / total) if total else 1.0
                filled = int(frac * bar_width)
                bar = "#" * filled + "-" * (bar_width - filled)
                rate = done / max(now - t0, 1e-6)
                eta = (total - done) / max(rate, 1e-6)
                eta_i = int(max(0, eta))
                h, rem = divmod(eta_i, 3600)
                m, s = divmod(rem, 60)
                eta_str = f"{h:d}:{m:02d}:{s:02d}" if h else f"{m:02d}:{s:02d}"
                suffix = f" | {current}" if current else ""
                sys.stdout.write(f"\r[{bar}] {frac*100:5.1f}% {done}/{total} | {rate:6.1f} files/s | ETA {eta_str}{suffix}")
                sys.stdout.flush()

            exports: List[JsonExport] = []
            loaded = 0
            for f in files:
                try:
                    exports.append(load_json_file(f))
                except Exception as e:
                    if args.skip_bad_files:
                        sys.stdout.write(f"\nWARNING: Skipping {f} due to error: {e}\n")
                        loaded += 1
                        draw_progress_files(loaded, f.name)
                        continue
                    raise
                loaded += 1
                draw_progress_files(loaded, f.name)
            sys.stdout.write("\n")

            n = imp.import_fast(exports, only_guild_id=only_guild_id)
            total_inserted += n
        else:
            total = len(files)
            print(f"Processing {total} file(s) with classic mode...")
            processed = 0
            t0 = time.time()
            last_draw = t0
            bar_width = 30

            def draw_progress_files(done: int, current: Optional[str] = None):
                now = time.time()
                nonlocal last_draw
                if done < total and (now - last_draw) < 0.25:
                    return
                last_draw = now
                frac = (done / total) if total else 1.0
                filled = int(frac * bar_width)
                bar = "#" * filled + "-" * (bar_width - filled)
                rate = done / max(now - t0, 1e-6)
                eta = (total - done) / max(rate, 1e-6)
                eta_i = int(max(0, eta))
                h, rem = divmod(eta_i, 3600)
                m, s = divmod(rem, 60)
                eta_str = f"{h:d}:{m:02d}:{s:02d}" if h else f"{m:02d}:{s:02d}"
                suffix = f" | {current}" if current else ""
                sys.stdout.write(f"\r[{bar}] {frac*100:5.1f}% {done}/{total} file(s) done | {rate:6.1f} files/s | ETA {eta_str}{suffix}")
                sys.stdout.flush()

            for f in files:
                try:
                    export = load_json_file(f)
                except Exception as e:
                    if args.skip_bad_files:
                        sys.stdout.write(f"\nWARNING: Skipping {f} due to error: {e}\n")
                        processed += 1
                        draw_progress_files(processed, f.name)
                        continue
                    raise
                n = imp.import_export(export, only_guild_id=only_guild_id)
                print(f"Imported {n} messages from {f}")
                total_inserted += n
                processed += 1
                draw_progress_files(processed, f.name)
            sys.stdout.write("\n")

    print(f"Done. Inserted {total_inserted} messages.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
