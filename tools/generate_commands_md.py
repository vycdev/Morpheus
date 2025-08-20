#!/usr/bin/env python3
r"""
Generate a Markdown file listing bot commands by scanning C# source in Modules/.

Usage:
    python tools\generate_commands_md.py            # writes COMMANDS.md in repo root
    python tools\generate_commands_md.py out.md    # write to custom path

Notes:
 - This script uses simple parsing (regex) of C# source files in Modules/.
 - It extracts attributes: [Command("name")], [Alias(...)] and [Summary("...")]
 - It extracts the method signature line to list parameters (marks optional if default present).
 - It's intentionally tolerant but not a full C# parser; it should work on the project's typical formatting.
"""
import sys
import re
from pathlib import Path


MODULES_DIR = Path(__file__).resolve().parents[1] / 'Modules'
OUT_DEFAULT = Path(__file__).resolve().parents[1] / 'COMMANDS.md'


def parse_attributes(attr_block: str):
    # return dict of attribute name -> list of raw contents (None if no params)
    attrs = {}
    # Match attributes like: [Name("x")], [Command], [RateLimit(3, 30)]
    for m in re.finditer(r"\[\s*(\w+)(?:\s*\((.*?)\))?\s*\]", attr_block, flags=re.S):
        name = m.group(1)
        raw = m.group(2).strip() if m.group(2) is not None else None
        attrs.setdefault(name, []).append(raw)
    return attrs


def clean_string_literal(s: str):
    if s is None:
        return None
    s = s.strip()
    # remove trailing commas
    s = s.rstrip(',').strip()
    # remove leading @ and surrounding quotes if present
    m = re.match(r'^@?"(.*)"$', s, flags=re.S)
    if m:
        return m.group(1).strip()
    m = re.match(r"^@?'(.*)'$", s, flags=re.S)
    if m:
        return m.group(1).strip()
    return s


def extract_methods_from_file(path: Path):
    text = path.read_text(encoding='utf-8')
    results = []

    # Find class name
    class_match = re.search(r"class\s+(\w+)", text)
    class_name = class_match.group(1) if class_match else path.stem

    # iterate attribute blocks followed by a method signature
    pattern = re.compile(r"((?:\s*\[[^\]]+\]\s*)+)\s*(public|protected|private)\s+[\w<>,\s]+\s+(\w+)\s*\(([^)]*)\)", re.S)
    for m in pattern.finditer(text):
        attr_block = m.group(1)
        method_name = m.group(3)
        params_raw = m.group(4).strip()

        attrs = parse_attributes(attr_block)
        if 'Command' not in attrs:
            continue

        # command name from Command(...) attribute if present
        cmd_name = None
        if attrs.get('Command'):
            # take first occurrence and first argument
            cmd_name = clean_string_literal(attrs['Command'][0])
        if not cmd_name:
            cmd_name = method_name

        # aliases
        aliases = []
        if attrs.get('Alias'):
            # Alias(...) may contain comma separated strings
            raw = attrs['Alias'][0]
            if raw:
                # split by commas that are outside quotes
                parts = re.findall(r"@?\".*?\"|'.*?'|[^,]+", raw)
                for p in parts:
                    p = p.strip()
                    if not p:
                        continue
                    p = clean_string_literal(p)
                    if p:
                        aliases.append(p)

        # summary
        summary = None
        if attrs.get('Summary'):
            summary = clean_string_literal(attrs['Summary'][0])

        # rate limit: attributes like RateLimit(3, 30)
        rate_limit = None
        if attrs.get('RateLimit') and attrs['RateLimit'][0]:
            raw = attrs['RateLimit'][0]
            # find integers
            nums = re.findall(r"\d+", raw)
            if len(nums) >= 2:
                rate_limit = f"{nums[0]} use(s) per {nums[1]} second(s)"
            elif len(nums) == 1:
                rate_limit = f"{nums[0]} use(s)"

        # RequireUserPermission: extract enum value (e.g. Discord.GuildPermission.Administrator)
        required_permission = None
        if attrs.get('RequireUserPermission') and attrs['RequireUserPermission'][0]:
            rawp = attrs['RequireUserPermission'][0]
            # try to extract the trailing identifier
            mperm = re.search(r"([A-Za-z_][\w\.]*\.)?([A-Za-z_][\w]*)", rawp)
            if mperm:
                required_permission = mperm.group(2)

        # RequireBotPermission: extract enum value (e.g. GuildPermission.AddReactions)
        required_bot_permission = None
        if attrs.get('RequireBotPermission') and attrs['RequireBotPermission'][0]:
            rawb = attrs['RequireBotPermission'][0]
            mbot = re.search(r"([A-Za-z_][\w\.]*\.)?([A-Za-z_][\w]*)", rawb)
            if mbot:
                required_bot_permission = mbot.group(2)

        # RequireContext: detect guild-only commands (e.g. RequireContext(ContextType.Guild))
        requires_guild_context = False
        if attrs.get('RequireContext') and attrs['RequireContext'][0]:
            rawc = attrs['RequireContext'][0]
            if 'ContextType.Guild' in rawc or 'Guild' in rawc:
                requires_guild_context = True

        # Back-compat: if RequireDbGuild attribute is present, treat as guild-only
        if attrs.get('RequireDbGuild'):
            requires_guild_context = True

    # RequireDbGuild: no longer collected (remove from output)

        # parameters: split by commas outside brackets
        params = []
        if params_raw:
            parts = re.split(r",(?![^()]*\))", params_raw)
            for p in parts:
                p = p.strip()
                if not p:
                    continue
                # remove attributes like [Remainder]
                p_clean = re.sub(r"\[[^\]]+\]", "", p).strip()
                # detect default
                optional = '=' in p_clean
                # split type and name (take last token as name)
                toks = p_clean.split()
                if len(toks) >= 2:
                    ptype = ' '.join(toks[:-1])
                    pname = toks[-1]
                else:
                    ptype = toks[0]
                    pname = ''
                params.append({'raw': p.strip(), 'type': ptype, 'name': pname, 'optional': optional})

        results.append({
            'class': class_name,
            'method': method_name,
            'command': cmd_name,
            'aliases': aliases,
            'summary': summary,
            'requires_guild_context': requires_guild_context,
            'params': params,
            'rate_limit': rate_limit,
            'required_permission': required_permission,
            'required_bot_permission': required_bot_permission,
            # 'requires_db_guild' removed per request
            'source': str(path.relative_to(Path.cwd()))
        })

    return results


def generate_markdown(commands_info):
    out = []
    out.append('# Commands')
    out.append('')
    out.append('This file is auto-generated by `tools/generate_commands_md.py`.')
    out.append('')

    # Group by module/class and show totals
    by_module = {}
    for c in commands_info:
        by_module.setdefault(c['class'], []).append(c)
    total_commands = len(commands_info)
    total_modules = len(by_module)
    out.append(f'- Total modules: {total_modules}')
    out.append(f'- Total commands: {total_commands}')
    out.append('')

    for module in sorted(by_module.keys()):
        module_display = module.replace("Module", "")
        module_count = len(by_module[module])
        module_label = 'command' if module_count == 1 else 'commands'
        out.append(f'## {module_display} ({module_count} {module_label})')
        out.append('')
        for info in sorted(by_module[module], key=lambda x: x['command'] or x['method']):
            cmd = info['command'] or info['method']
            aliases = ', '.join(info['aliases']) if info['aliases'] else '(none)'
            summary = info['summary'] or 'No description available.'
            out.append(f'### `{cmd}`')
            out.append('')
            out.append(f'- Source: `{info["source"]}`')
            out.append(f'- Aliases: {aliases}')
            out.append(f'- Summary: {summary}')
            if info.get('rate_limit'):
                out.append(f'- Rate limit: {info.get("rate_limit")}')
            if info.get('required_permission'):
                out.append(f'- Required permission: {info.get("required_permission")}')
            if info.get('required_bot_permission'):
                out.append(f'- Required bot permission: {info.get("required_bot_permission")}')
            if info.get('requires_guild_context'):
                out.append(f'- Requires guild context: Yes')
            # Requires DB guild output removed
            if info['params']:
                out.append('- Parameters:')
                for p in info['params']:
                    opt = 'Optional' if p['optional'] else 'Required'
                    pname = p['name'] or p['raw']
                    ptype = p['type']
                    out.append(f'  - `{pname}` — {ptype} — {opt}')
            out.append('')

    return '\n'.join(out)


def main():
    out_path = Path(sys.argv[1]) if len(sys.argv) > 1 else OUT_DEFAULT

    if not MODULES_DIR.exists():
        print(f"Modules directory not found at {MODULES_DIR}")
        sys.exit(1)

    commands = []
    for cs in sorted(MODULES_DIR.rglob('*.cs')):
        commands.extend(extract_methods_from_file(cs))

    md = generate_markdown(commands)
    out_path.write_text(md, encoding='utf-8')
    print(f'Wrote {out_path}')


if __name__ == '__main__':
    main()
