#!/usr/bin/env python3
"""Check owned class summaries and obvious commented-out code; do not impose public-member doc quotas."""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
CLASS = re.compile(r'^([ \t]*)(?:\[[^\n]+?\][ \t]*)*(?:(?:public|internal|private|protected|static|sealed|abstract|partial)\s+)*class\s+(\w+)', re.M)
ATTRIBUTE = re.compile(r'^\s*(?:\[[^\]\n]+\][ \t]*)+$')
COMMENTED_CODE = re.compile(r'^\s*//\s*(?:[{}]\s*$|#(?:if|endif|else)\b|(?:if|for|foreach|while)\s*\(|(?:public|private|protected|internal)\s+|(?:yield\s+return|return)\s+|(?:[A-Za-z_]\w*\s+)?[A-Za-z_][\w.\[\]]*\s*=.*;|[A-Za-z_][\w.\[\]<>]*\(.*\);\s*$)')


def audit():
    errors = []
    count = 0
    for folder in ('Assets/Scripts', 'Assets/Tests'):
        for path in (ROOT / folder).rglob('*.cs'):
            source = path.read_text(encoding='utf-8-sig')
            for match in CLASS.finditer(source):
                count += 1
                start = match.start()
                while start > 0:
                    previous = source.rfind('\n', 0, start - 1) + 1
                    if not ATTRIBUTE.match(source[previous:start].rstrip('\n')):
                        break
                    start = previous
                prefix = source[:start].rstrip().splitlines()
                docs = []
                for line in reversed(prefix):
                    if not line.lstrip().startswith('///'):
                        break
                    docs.append(line.split('///', 1)[1])
                if not any('<summary>' in line for line in docs):
                    number = source.count('\n', 0, match.start()) + 1
                    errors.append(f'{path.relative_to(ROOT)}:{number}: class {match[2]} needs a meaningful XML summary')
            # Only flag empty Unity frame messages, not legitimate catch/fallback blocks.
            if re.search(r'class\s+\w+[^\n]*:\s*[^\n]*\b(?:MonoBehaviour|NetworkBehaviour)\b', source):
                without_comments = re.sub(r'//[^\n]*|/\*[\s\S]*?\*/', '', source)
                for empty in re.finditer(r'\bvoid\s+(Awake|Start|Update|FixedUpdate|LateUpdate)\([^)]*\)\s*\{\s*\}', without_comments):
                    errors.append(f'{path.relative_to(ROOT)}: empty Unity callback {empty[1]} should be removed')
            for number, line in enumerate(source.splitlines(), 1):
                if not line.lstrip().startswith('///') and COMMENTED_CODE.match(line):
                    errors.append(f'{path.relative_to(ROOT)}:{number}: remove commented-out code: {line.strip()}')
    return count, errors


if __name__ == '__main__':
    count, errors = audit()
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        sys.exit(1)
    print(f'Checked {count} owned classes: XML summaries present; no obvious commented-out statements.')
