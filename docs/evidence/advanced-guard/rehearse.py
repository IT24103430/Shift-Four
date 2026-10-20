"""Prepare and test the pending Guard proposal only in a new disposable Unity project."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
UNITY = '/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'
SOURCES = {
    'Assets/Scripts/AI/Agents/GuardBrain.cs': 'GuardBrain.cs.txt',
    'Assets/Scripts/AI/Core/DroneBrain.cs': 'DroneBrain.cs.txt',
    'Assets/Scripts/Gameplay/ConsoleSwitch.cs': 'ConsoleSwitch.cs.txt',
    'Assets/Prefabs/Sections/Server.prefab': 'Server.prefab.txt',
    'Assets/Tests/PlayMode/AdvancedGuardPlayModeTests.cs': 'harness/AdvancedGuardPlayModeTests.cs',
    'Assets/Tests/PlayMode/AdvancedGuardPlayModeTests.cs.meta': 'harness/AdvancedGuardPlayModeTests.cs.meta',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--demo-only', action='store_true')
    parser.add_argument('--unity', default=UNITY)
    args = parser.parse_args()
    original = json.loads((HERE / 'before.json').read_text())['files']
    for target in SOURCES:
        if target in original:
            assert hashlib.sha256((ROOT / target).read_bytes()).hexdigest() == original[target], target + ' changed; rebase and review the proposal first'
    project = Path(tempfile.mkdtemp(prefix='shiftfour-guard-review-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    for target, source in SOURCES.items():
        (project / target).write_bytes((HERE / source).read_bytes())
    print('Review project:', project, flush=True)
    if args.prepare_only:
        return
    result_dir = project / 'ReviewResults'
    result_dir.mkdir()
    name = 'MainLab_Demonstration_GunshotInvestigationStaysInServerAndReturnsToPost' if args.demo_only else 'ShiftFour.Tests.AdvancedGuardPlayModeTests'
    env = dict(os.environ, SHIFTFOUR_GUARD_EVIDENCE=str(result_dir))
    subprocess.run([args.unity, '-batchmode', '-projectPath', str(project), '-runTests',
                    '-testPlatform', 'PlayMode', '-testFilter', name,
                    '-testResults', str(result_dir / 'PlayMode.xml'), '-logFile', str(result_dir / 'PlayMode.log')],
                   env=env, check=True)
    print('Results and visual frames:', result_dir)


if __name__ == '__main__':
    main()
