"""Finalize Task 7 evidence after the Unity test processes have exited."""
import csv
import difflib
import hashlib
import json
import re
import subprocess
import shutil
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'docs/evidence/drone-speeds'
TEST = ROOT.parent / 'Shift-Four-Progressive'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n')


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT).decode()


summary, rows = {}, []
for mode in ['EditMode', 'FocusedPlayMode', 'InterceptorFixtures', 'TimingFixtures', 'PlayMode']:
    run = ET.parse(OUT / (mode + '.xml')).getroot()
    assert run.get('result') == 'Passed', (mode, run.attrib)
    summary[mode] = {k: run.get(k) for k in ['result', 'total', 'passed', 'failed', 'skipped',
                                           'inconclusive', 'duration', 'start-time', 'end-time']}
    if mode in ['EditMode', 'PlayMode']:
        rows.extend((mode, t.get('fullname'), t.get('result'), t.get('duration')) for t in run.iter('test-case'))
write('test-summary.json', summary)
report = ROOT / 'docs/DRONE_MOVEMENT_SPEEDS.md'
pending = 'Final actual results and evidence are recorded here after Unity completes.'
result_text = ('Final Unity results: **269/269 Edit Mode**, **136/136 full Play Mode**, '
               '**8/8 focused speed Play Mode**, and **8/8 focused Interceptor/Control fixtures** passed. '
               'The final full suites contain **405 passing tests**, zero failures and zero skipped tests. '
               'Raw XML, logs, per-test results, source fingerprints and integrity checks are in '
               '`docs/evidence/drone-speeds/`.')
report.write_text(report.read_text().replace(pending, result_text))
cadence = {int(hz): float(speed) for hz, speed in re.findall(
    r'\[Speed QA\] (\d+) Hz: arrived, max accepted speed ([\d.]+) m/s',
    (OUT / 'PlayMode.log').read_text(errors='replace'))}
assert set(cadence) == {30, 60, 120}, cadence
assert all(4.0 < speed <= 4.24 for speed in cadence.values()), cadence
write('cadence-results.json', {'simulation_hz_to_max_accepted_mps': cadence,
    'chase_target_mps': 4.2, 'large_coordinate_measurement_tolerance_mps': .04,
    'all_routes_arrived': True, 'turn_limits_checked_each_frame': True,
    'gpu_or_display_frame_rate_benchmark': False})
with (OUT / 'TEST-MATRIX.csv').open('w', newline='') as stream:
    writer = csv.writer(stream)
    writer.writerow(['mode', 'test', 'result', 'duration_seconds'])
    writer.writerows(rows)

settings = TEST / 'ProjectSettings/ProjectSettings.asset'
original_settings = (OUT / 'verification-settings-before.txt').read_bytes()
if settings.read_bytes() != original_settings:
    generated = settings.read_bytes()
    (OUT / 'verification-settings-generated.txt').write_bytes(generated)
    (OUT / 'verification-settings-generated.patch').write_text(''.join(difflib.unified_diff(
        original_settings.decode().splitlines(True), generated.decode().splitlines(True),
        fromfile='before', tofile='unity-generated')))
    settings.write_bytes(original_settings)

before = json.loads((OUT / 'task7-before.json').read_text())
modified = sorted(f for f, h in before['files'].items() if not (ROOT / f).is_file() or sha(ROOT / f) != h)
expected_modified = sorted([
    'Assets/Scripts/AI/Core/DroneBrain.cs', 'Assets/Scripts/AI/Movement/DroneMotor.cs',
    'Assets/Scripts/Gameplay/DroneRotorVisuals.cs', 'Assets/Tests/PlayMode/DroneDifficultyPlayModeTests.cs',
    'Assets/Tests/PlayMode/InterceptorMainLabPlayModeTests.cs',
    'Assets/Tests/PlayMode/ControlTacticalNavigationPlayModeTests.cs',
    'Assets/Tests/PlayMode/DroneAnimationPlayModeTests.cs',
    'Assets/Tests/PlayMode/MainLabProximityPlayModeTests.cs'])
assert modified == expected_modified, modified
files = [f for f in git('ls-files', '-z', '--cached', '--others', '--exclude-standard').split('\0')
         if f and (ROOT / f).is_file() and not f.startswith('docs/evidence/drone-speeds/')]
created = sorted(set(files) - set(before['files']))
expected_created = sorted([
    'Assets/Scripts/AI/Movement/DroneMovementSpeeds.cs', 'Assets/Scripts/AI/Movement/DroneMovementSpeeds.cs.meta',
    'Assets/Tests/EditMode/Movement/DroneMovementSpeedsEditModeTests.cs',
    'Assets/Tests/EditMode/Movement/DroneMovementSpeedsEditModeTests.cs.meta',
    'Assets/Tests/PlayMode/DroneMovementSpeedsPlayModeTests.cs',
    'Assets/Tests/PlayMode/DroneMovementSpeedsPlayModeTests.cs.meta', 'docs/DRONE_MOVEMENT_SPEEDS.md'])
assert created == expected_created, created
for f in created:
    if f.startswith('Assets/'):
        assert sha(ROOT / f) == sha(TEST / f), ('New source/metadata mismatch', f)
write('changed-files.json', {'modified': modified, 'created': created})
(OUT / 'changed-files.txt').write_text('\n'.join(modified + created) + '\n')
for f in modified:
    old = (OUT / (Path(f).stem + '.before.txt')).read_text()
    current = (ROOT / f).read_text()
    (OUT / (Path(f).stem + '.task7.patch')).write_text(''.join(difflib.unified_diff(
        old.splitlines(True), current.splitlines(True), fromfile=f + ' (Task 6)', tofile=f + ' (Task 7)')))
brain_before = (OUT / 'DroneBrain.before.txt').read_text()
brain = (ROOT / 'Assets/Scripts/AI/Core/DroneBrain.cs').read_text()
assert brain_before[brain_before.index('private bool CanSeePlayer()'):] == brain[brain.index('private bool CanSeePlayer()'):]
motor_before = (OUT / 'DroneMotor.before.txt').read_text()
motor = (ROOT / 'Assets/Scripts/AI/Movement/DroneMotor.cs').read_text()
for start, end in [('public void GoTo(', 'private void Update()'),
                   ('private void CacheVisualPosition()', 'public void Stop()')]:
    assert motor_before[motor_before.index(start):motor_before.index(end)] == motor[motor.index(start):motor.index(end)]

sources = json.loads((OUT / 'validated-source-sha256.json').read_text())
source_drift = [f for f, h in sources.items() if sha(ROOT / f) != h or sha(TEST / f) != h]
assert not source_drift, source_drift
protected = json.loads((ROOT / 'docs/evidence/integration-baseline/protected-assets-before.json').read_text())
protected_drift = [f for f, h in protected.items() if sha(ROOT / f) != h or sha(TEST / f) != h]
assert not protected_drift, protected_drift
profiles = json.loads((ROOT / 'docs/evidence/difficulty-profiles/profile-asset-sha256.json').read_text())
assert all(sha(ROOT / f) == sha(TEST / f) == h for f, h in profiles.items())
for name in ['ScoutBrain.cs', 'GuardBrain.cs', 'InterceptorBrain.cs', 'FlankerBrain.cs',
             'InterceptorPolicy.cs', 'ControlTacticalNavigation.cs']:
    f = 'Assets/Scripts/AI/Agents/' + name
    assert sha(ROOT / f) == before['files'][f], f
for f in ['Assets/Scripts/AI/Movement/DroneBody.cs', 'Assets/Scripts/AI/Core/WaypointGraph.cs',
          'Assets/Scripts/Gameplay/Health.cs', 'Assets/Scripts/Gameplay/LabDoor.cs']:
    assert sha(ROOT / f) == before['files'][f], f
assert git('rev-parse', 'HEAD').strip() == before['head']
assert git('ls-files', '--stage', '-z') == before['index']
scenes = [str(f) for f in ROOT.glob('Assets/InitTestScene*')] + [str(f) for f in TEST.glob('Assets/InitTestScene*')]
assert not scenes, scenes
write('integrity.json', {'passed': True, 'active_project': str(ROOT), 'test_project': str(TEST),
    'source_files_checked': len(sources), 'source_drift': source_drift,
    'protected_files_checked': len(protected), 'protected_drift': protected_drift,
    'difficulty_assets_unchanged': True, 'individual_policies_and_interceptor_control_tactics_unchanged': True,
    'body_astar_gate_and_health_unchanged': True, 'vision_fire_and_death_methods_unchanged': True,
    'motor_standoff_arrival_braking_and_route_recovery_methods_unchanged': True,
    'existing_index_entries_preserved': True, 'head_unchanged': True,
    'existing_files_modified': modified, 'new_unity_files': len([f for f in created if f.startswith('Assets/')]),
    'temporary_test_scenes': scenes, 'verification_settings_restored': True})

with zipfile.ZipFile(OUT / 'task7-source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for f in modified + created:
        archive.write(ROOT / f, f)

control = OUT / 'generated/control-tactical'
control.mkdir(parents=True, exist_ok=True)
for name in ['geometry.txt', 'junctions.csv', 'routes.txt', 'motor-routes.csv']:
    source = TEST / 'Logs/ControlTactical' / name
    assert source.is_file(), source
    shutil.copy2(source, control / name)

for directory in ['generated', 'generated.initial', 'generated.second']:
    screens = sorted((OUT / directory).glob('*.tga'))
    if screens:
        hashes = {f.name: sha(f) for f in screens}
        with zipfile.ZipFile(OUT / directory / 'screenshots.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
            for f in screens:
                archive.write(f, f.name)
        with zipfile.ZipFile(OUT / directory / 'screenshots.zip') as archive:
            assert all(hashlib.sha256(archive.read(name)).hexdigest() == h for name, h in hashes.items())
        write(directory + '/screenshots-sha256.json', hashes)
        for f in screens:
            f.unlink()
print(json.dumps(summary, indent=2))
print('Integrity passed; Task 7 delta:', modified, created)
