#!/usr/bin/env python3
"""Regression tests for the command documentation generator."""
import importlib.util
import os
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "tools" / "generate_commands_md.py"
SPEC = importlib.util.spec_from_file_location("generate_commands_md", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
GENERATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GENERATOR)


class ParameterParsingTests(unittest.TestCase):
    def test_optional_default_is_not_reported_as_parameter_name(self):
        parsed = GENERATOR.parse_parameter("int page = 1")

        self.assertEqual({"raw": "int page = 1", "type": "int", "name": "page", "optional": True}, parsed)

    def test_parameter_attributes_and_quoted_defaults_are_supported(self):
        parsed = GENERATOR.parse_parameter('[Remainder] string? text = "hello world"')

        self.assertEqual("string?", parsed["type"])
        self.assertEqual("text", parsed["name"])
        self.assertTrue(parsed["optional"])

    def test_generated_markdown_uses_parameter_name_and_type(self):
        source = '''
using System.Threading.Tasks;

internal sealed class ExampleGuildScore
{
}

public class ExampleModule
{
    [Command("example")]
    [Summary("Shows an example.")]
    public Task Example(int page = 1, string sort = "oldest") => Task.CompletedTask;
}
'''
        with tempfile.TemporaryDirectory(dir=ROOT) as temporary:
            path = Path(temporary) / "Nested" / "ExampleModule.cs"
            path.parent.mkdir()
            path.write_text(source, encoding="utf-8")
            old_cwd = Path.cwd()
            try:
                os.chdir(ROOT)
                commands = GENERATOR.extract_methods_from_file(path)
            finally:
                os.chdir(old_cwd)

        markdown = GENERATOR.generate_markdown(commands)
        self.assertIn("## Example (1 command)", markdown)
        self.assertNotIn("## ExampleGuildScore", markdown)
        self.assertIn("`page` — int — Optional", markdown)
        self.assertIn("`sort` — string — Optional", markdown)
        self.assertNotIn("`1` — int page", markdown)
        expected_source = str(path.relative_to(ROOT)).replace("/", chr(92))
        self.assertIn(f"- Source: `{expected_source}`", markdown)


if __name__ == "__main__":
    unittest.main()
