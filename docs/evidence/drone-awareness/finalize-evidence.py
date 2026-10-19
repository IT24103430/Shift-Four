"""Validate Task 6 evidence after both Unity test processes have exited."""
import csv
import difflib
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'docs/evidence/drone-awareness'
TEST = ROOT.parent / 'Shift-Four-Progressive'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(name, value): (OUT / name).write_text(json.dumps(value, indent=2) + '\n')
def git(*args, cwd=ROOT): return subprocess.check_output(['git', *args], cwd=cwd).decode()

summary = {}; rows = []
for mode in ['EditMode', 'FocusedPlayMode', 'PlayMode']:
    run = ET.parse(OUT / (mode + '.xml')).getroot()
    assert run.get('result') == 'Passed', (mode, run.attrib)
    summary[mode] = {k: run.get(k) for k in ['result', 'total', 'passed', 'failed', 'skipped', 'inconclusive', 'duration', 'start-time', 'end-time']}
    if mode != 'FocusedPlayMode':
        rows.extend((mode, t.get('fullname'), t.get('result'), t.get('duration')) for t in run.iter('test-case'))
write('test-summary.json', summary)
with (OUT / 'TEST-MATRIX.csv').open('w', newline='') as stream:
    writer = csv.writer(stream); writer.writerow(['mode', 'test', 'result', 'duration_seconds']); writer.writerows(rows)

before = json.loads((OUT / 'task6-before.json').read_text())
settings = TEST / 'ProjectSettings/ProjectSettings.asset'
original_settings = (OUT / 'verification-settings-before.txt').read_bytes()
if settings.read_bytes() != original_settings:
    generated = settings.read_bytes()
    (OUT / 'verification-settings-generated.txt').write_bytes(generated)
    (OUT / 'verification-settings-generated.patch').write_text(''.join(difflib.unified_diff(
        original_settings.decode().splitlines(True), generated.decode().splitlines(True), fromfile='before', tofile='unity-generated')))
    settings.write_bytes(original_settings)

existing_delta = [f for f, h in before['files'].items() if not (ROOT / f).is_file() or sha(ROOT / f) != h]
assert existing_delta == ['Assets/Scripts/AI/Core/DroneBrain.cs'], existing_delta
files = [f for f in git('ls-files', '-z', '--cached', '--others', '--exclude-standard').split('\0')
         if f and (ROOT / f).is_file() and not f.startswith('docs/evidence/drone-awareness/')]
created = sorted(set(files) - set(before['files']))
expected = [
    'Assets/Scripts/AI/Perception/AwarenessSettings.cs', 'Assets/Scripts/AI/Perception/AwarenessSettings.cs.meta',
    'Assets/Scripts/AI/Perception/DroneAwareness.cs', 'Assets/Scripts/AI/Perception/DroneAwareness.cs.meta',
    'Assets/Tests/EditMode/Movement/DroneAwarenessEditModeTests.cs', 'Assets/Tests/EditMode/Movement/DroneAwarenessEditModeTests.cs.meta',
    'Assets/Tests/PlayMode/DroneAwarenessPlayModeTests.cs', 'Assets/Tests/PlayMode/DroneAwarenessPlayModeTests.cs.meta',
    'docs/DRONE_AWARENESS.md']
assert created == sorted(expected), created
write('changed-files.json', {'modified': existing_delta, 'created': created})
(OUT / 'changed-files.txt').write_text('\n'.join(existing_delta + created) + '\n')
prior = (OUT / 'DroneBrain.task5.txt').read_text()
current = (ROOT / existing_delta[0]).read_text()
(OUT / 'DroneBrain.task6.patch').write_text(''.join(difflib.unified_diff(prior.splitlines(True), current.splitlines(True), fromfile='DroneBrain.task5.cs', tofile='DroneBrain.task6.cs')))
assert prior[prior.index('private bool CanSeePlayer()'):prior.index('private void OnDied(')] == current[current.index('private bool CanSeePlayer()'):current.index('private void OnDied(')]
with zipfile.ZipFile(OUT / 'task6-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for f in existing_delta + created: archive.write(ROOT / f, f)

sources = json.loads((OUT / 'validated-source-sha256.json').read_text())
source_drift = [f for f, h in sources.items() if sha(ROOT / f) != h or sha(TEST / f) != h]
assert not source_drift, source_drift
protected = json.loads((ROOT / 'docs/evidence/integration-baseline/protected-assets-before.json').read_text())
protected_drift = [f for f, h in protected.items() if sha(ROOT / f) != h or sha(TEST / f) != h]
assert not protected_drift, protected_drift
profiles = json.loads((ROOT / 'docs/evidence/difficulty-profiles/profile-asset-sha256.json').read_text())
assert all(sha(ROOT / f) == sha(TEST / f) == h for f, h in profiles.items())
policy_files = ['ScoutBrain.cs', 'GuardBrain.cs', 'InterceptorBrain.cs', 'FlankerBrain.cs', 'InterceptorPolicy.cs', 'ControlTacticalNavigation.cs']
for name in policy_files:
    f = 'Assets/Scripts/AI/Agents/' + name
    assert sha(ROOT / f) == before['files'][f], f
for name in ['DroneMotor.cs', 'DroneBody.cs']:
    f = 'Assets/Scripts/AI/Movement/' + name
    assert sha(ROOT / f) == before['files'][f], f
assert git('rev-parse', 'HEAD').strip() == before['head']
assert git('ls-files', '--stage', '-z') == before['index_entries']
scenes = [str(f) for f in ROOT.glob('Assets/InitTestScene*')] + [str(f) for f in TEST.glob('Assets/InitTestScene*')]
assert not scenes, scenes
write('integrity.json', {'passed': True, 'active_project': str(ROOT), 'test_project': str(TEST),
    'source_files_checked': len(sources), 'source_drift': source_drift,
    'protected_files_checked': len(protected), 'protected_drift': protected_drift,
    'difficulty_assets_unchanged': True, 'individual_policies_and_interceptor_control_tactics_unchanged': True,
    'motor_and_body_unchanged': True, 'vision_and_fire_methods_unchanged': True,
    'existing_index_entries_preserved': True, 'head_unchanged': True,
    'existing_files_modified': existing_delta, 'new_unity_files': len([f for f in created if f.startswith('Assets/')]),
    'temporary_test_scenes': scenes, 'verification_settings_restored': True})

screens = sorted((OUT / 'generated').glob('*.tga'))
if screens:
    hashes = {f.name: sha(f) for f in screens}
    with zipfile.ZipFile(OUT / 'generated/screenshots.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
        for f in screens: archive.write(f, f.name)
    with zipfile.ZipFile(OUT / 'generated/screenshots.zip') as archive:
        assert all(hashlib.sha256(archive.read(name)).hexdigest() == h for name, h in hashes.items())
    write('generated/screenshots-sha256.json', hashes)
    for f in screens: f.unlink()
print(json.dumps(summary, indent=2))
print('Integrity passed; Task 6 delta:', existing_delta, created)
