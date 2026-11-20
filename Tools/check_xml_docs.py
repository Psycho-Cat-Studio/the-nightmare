#!/usr/bin/env python3
"""Validate selective C# XML documentation without requiring documentation on every public member."""
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

root = pathlib.Path(__file__).resolve().parents[1]
blocks = 0
errors = []
for path in list((root / 'Assets/Scripts').rglob('*.cs')) + list((root / 'Assets/Tests').rglob('*.cs')):
    source = path.read_text(encoding='utf-8-sig')
    for match in re.finditer(r'(?:^[ \t]*///[^\n]*\n)+', source, re.M):
        xml = '\n'.join(line.split('///', 1)[1] for line in match[0].splitlines())
        try:
            ET.fromstring('<member>' + xml + '</member>')
        except ET.ParseError as error:
            line = source.count('\n', 0, match.start()) + 1
            errors.append(f'{path.relative_to(root)}:{line}: {error}')
        blocks += 1
for error in errors:
    print(error, file=sys.stderr)
if errors:
    sys.exit(1)
print(f'Validated {blocks} XML documentation blocks.')
