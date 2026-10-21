"""Finalize Task 4 evidence only after the Unity batch processes have exited."""
import csv
import difflib
import hashlib
import json
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / 'docs/evidence/player-noise'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(name, value): (EVIDENCE / name).write_text(json.dumps(value, indent=2) + '\n')
def git(*args, cwd=ROOT): return subprocess.check_output(['git', *args], cwd=cwd).decode()

summary = {}
rows = []
for name in ['EditMode', 'FocusedPlayMode', 'PlayMode']:
    run = ET.parse(EVIDENCE / (name + '.xml')).getroot()
    assert run.get('result') == 'Passed', (name, run.attrib)
    summary[name] = {key: run.get(key) for key in ['result', 'total', 'passed', 'failed', 'skipped', 'inconclusive', 'duration', 'start-time', 'end-time']}
    if name != 'FocusedPlayMode':
        rows.extend((name, t.get('fullname'), t.get('result'), t.get('duration')) for t in run.iter('test-case'))
write('test-summary.json', summary)
with (EVIDENCE / 'TEST-MATRIX.csv').open('w', newline='') as file:
    out = csv.writer(file); out.writerow(['mode', 'test', 'result', 'duration_seconds']); out.writerows(rows)

before = json.loads((EVIDENCE / 'task4-before.json').read_text())
settings = 'ProjectSettings/ProjectSettings.asset'
original_settings = ROOT.parent / 'Shift-Four' / settings
assert sha(original_settings) == before['files'][settings]
if sha(ROOT / settings) != before['files'][settings]:
    generated = (ROOT / settings).read_bytes()
    (EVIDENCE / 'ProjectSettings.unity-generated.txt').write_bytes(generated)
    (EVIDENCE / 'ProjectSettings.unity-generated.patch').write_text(''.join(difflib.unified_diff(
        original_settings.read_text().splitlines(True), generated.decode().splitlines(True), fromfile='settings-before', tofile='settings-unity-generated')))
    (ROOT / settings).write_bytes(original_settings.read_bytes())

files = [f for f in git('ls-files', '-z', '--cached', '--others', '--exclude-standard').split('\0')
         if f and (ROOT / f).is_file() and not f.startswith('docs/evidence/player-noise/')]
changed = [f for f, h in before['files'].items() if not (ROOT / f).is_file() or sha(ROOT / f) != h]
expected = ['Assets/Scripts/Gameplay/ConsoleSwitch.cs', 'Assets/Scripts/Gameplay/PlayerController.cs']
assert sorted(changed) == expected, changed
created = sorted(set(files) - set(before['files']))
assert created == sorted([
    'Assets/Scripts/Gameplay/MovementNoiseCadence.cs', 'Assets/Scripts/Gameplay/MovementNoiseCadence.cs.meta',
    'Assets/Tests/EditMode/Movement/MovementNoiseCadenceEditModeTests.cs', 'Assets/Tests/EditMode/Movement/MovementNoiseCadenceEditModeTests.cs.meta',
    'Assets/Tests/PlayMode/PlayerNoisePlayModeTests.cs', 'Assets/Tests/PlayMode/PlayerNoisePlayModeTests.cs.meta',
    'docs/PLAYER_ACTION_NOISE.md']), created
write('changed-files.json', {'modified': changed, 'created': created})
(EVIDENCE / 'changed-files.txt').write_text('\n'.join(changed + created) + '\n')
for name in ['PlayerController', 'ConsoleSwitch']:
    prior = (EVIDENCE / (name + '.task3.txt')).read_text()
    current = (ROOT / 'Assets/Scripts/Gameplay' / (name + '.cs')).read_text()
    (EVIDENCE / (name + '.task4.patch')).write_text(''.join(difflib.unified_diff(prior.splitlines(True), current.splitlines(True), fromfile=name + '.task3.cs', tofile=name + '.task4.cs')))
    if name == 'PlayerController':
        for start, end in [('private void Shoot', 'private static void LockCursor'), ('public void ApplyCameraPose', 'private void OnValidate')]:
            assert prior[prior.index(start):prior.index(end)] == current[current.index(start):current.index(end)]
with zipfile.ZipFile(EVIDENCE / 'task4-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for f in changed + created: archive.write(ROOT / f, f)

sources = json.loads((EVIDENCE / 'validated-source-sha256.json').read_text())
source_drift = [f for f, h in sources.items() if sha(ROOT / f) != h]
assert not source_drift, source_drift
protected = json.loads((ROOT / 'docs/evidence/integration-baseline/protected-assets-before.json').read_text())
protected_changes = [f for f, h in protected.items() if sha(ROOT / f) != h]
assert not protected_changes, protected_changes
profiles = json.loads((ROOT / 'docs/evidence/difficulty-profiles/profile-asset-sha256.json').read_text())
profile_changes = [f for f, h in profiles.items() if sha(ROOT / f) != h]
assert not profile_changes, profile_changes
originals = json.loads((ROOT / 'docs/evidence/integration-baseline/original-worktrees-before.json').read_text())
original_results = {}
for name, snapshot in originals.items():
    worktree = ROOT.parent / name
    drift = [f for f, h in snapshot['sha256'].items() if not (worktree / f).is_file() or sha(worktree / f) != h]
    original_results[name] = {'head_unchanged': git('rev-parse', 'HEAD', cwd=worktree).strip() == snapshot['head'],
                              'status_unchanged': git('status', '--short', cwd=worktree) == snapshot['status'],
                              'files_checked': len(snapshot['sha256']), 'changed_files': drift}
    assert not drift and original_results[name]['head_unchanged'] and original_results[name]['status_unchanged'], original_results[name]
write('original-worktrees-integrity.json', original_results)
assert git('rev-parse', 'HEAD').strip() == before['head']
scenes = [str(p.relative_to(ROOT)) for p in ROOT.glob('Assets/InitTestScene*')]
assert not scenes, scenes
policies = sum(len(re.findall(r'\bclass\s+InterceptorPolicy\b', p.read_text())) for p in ROOT.glob('Assets/Scripts/**/*.cs'))
assert policies == 1
write('integrity.json', {'passed': True, 'protected_assets_checked': len(protected), 'protected_asset_changes': protected_changes,
                       'source_files_checked': len(sources), 'source_drift': source_drift, 'difficulty_profile_changes': profile_changes,
                       'existing_files_modified_from_task3': changed, 'new_unity_files': len([f for f in created if f.startswith('Assets/')]),
                       'original_worktrees_preserved': True, 'temporary_test_scenes_remaining': scenes,
                       'interceptor_policy_definitions': policies, 'gunshot_and_camera_methods_unchanged': True,
                       'settings_cleanup': 'Archived Unity-generated defaults and restored verified original content after tests.'})

screenshots = sorted((EVIDENCE / 'generated').glob('*.tga'))
if screenshots:
    hashes = {p.name: sha(p) for p in screenshots}
    with zipfile.ZipFile(EVIDENCE / 'generated/screenshots.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for p in screenshots: archive.write(p, p.name)
    with zipfile.ZipFile(EVIDENCE / 'generated/screenshots.zip') as archive:
        assert all(hashlib.sha256(archive.read(name)).hexdigest() == h for name, h in hashes.items())
    write('generated/screenshots-sha256.json', hashes)
    for p in screenshots: p.unlink()
print(json.dumps(summary, indent=2))
print('Integrity passed; exact delta:', changed, created)
