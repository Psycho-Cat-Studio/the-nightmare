#!/usr/bin/env python3
"""Check architecture boundaries without launching Unity or requiring a license."""
import json
import pathlib
import re
import sys

root = pathlib.Path(__file__).resolve().parents[1]
core = root / 'Assets/Scripts/Core'
errors = []
for path in core.rglob('*.cs'):
    text = path.read_text(encoding='utf-8-sig')
    if re.search(r'\b(UnityEngine|UnityEditor|Mirror)\s*[.;]', text):
        errors.append(f'{path.relative_to(root)}: engine/network dependency in Core')
assemblies = {}
for path in (root / 'Assets/Scripts').rglob('*.asmdef'):
    data = json.loads(path.read_text())
    assemblies[data['name']] = data
if not assemblies.get('Nightmare.Core', {}).get('noEngineReferences'):
    errors.append('Nightmare.Core must set noEngineReferences=true')
visited, visiting = set(), set()
def visit(name):
    if name in visiting:
        errors.append('Assembly dependency cycle: ' + name)
        return
    if name in visited or name not in assemblies:
        return
    visiting.add(name)
    for dependency in assemblies[name].get('references', []):
        visit(dependency)
    visiting.remove(name)
    visited.add(name)
for name in assemblies:
    visit(name)
for path in (root / 'Assets/Scripts/Player/FPS').glob('*.cs'):
    if path.stem in {'PlayerController', 'PlayerMotor', 'PlayerShoot', 'PlayerEquipment', 'WeaponPresentation', 'PlayerAuthority'}:
        if re.search(r'^using Mirror\s*;', path.read_text(encoding='utf-8-sig'), re.M):
            errors.append(f'{path.relative_to(root)}: local player behavior depends on Mirror')
runtime = root / 'Assets/Scripts/Runtime'
for path in runtime.rglob('*.cs'):
    if re.search(r'^using Mirror\s*;', path.read_text(encoding='utf-8-sig'), re.M):
        errors.append(f'{path.relative_to(root)}: Runtime depends on Mirror')
for path in (root / 'Assets/Scripts').rglob('*.cs'):
    relative = path.relative_to(root / 'Assets/Scripts').as_posix()
    if relative.startswith(('Networking/Mirror/', 'Editor/')):
        continue
    if re.search(r'^using Mirror\s*;', path.read_text(encoding='utf-8-sig'), re.M):
        errors.append(f'{path.relative_to(root)}: Mirror must stay in its adapter module')
for path in (root / 'Assets/Scripts').rglob('*.cs'):
    text = path.read_text(encoding='utf-8-sig')
    if re.search(r'\bGameManager\.(?:Instance|Players|Enemies|LocalPlayer|ActivePlayers)\b|\b(?:LegacyMatchStateMap|CurrentMachState)\b', text):
        errors.append(f'{path.relative_to(root)}: use injected match services and MatchProgress, not removed global/numbered-room APIs')
for error in errors:
    print('error:', error)
if errors:
    sys.exit(1)
print('Module boundaries and assembly dependency graph are valid.')
