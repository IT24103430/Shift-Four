"""Rehearse the pending Scout patch in a disposable project; never patch the source project."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SCOUT = Path('Assets/Scripts/AI/Agents/ScoutBrain.cs')
TEST = Path('Assets/Tests/PlayMode/EnhancedScoutPlayModeTests.cs')
UNITY = '/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--demo-only', action='store_true')
    parser.add_argument('--unity', default=UNITY)
    args = parser.parse_args()
    before = json.loads((HERE / 'before.json').read_text())
    assert hashlib.sha256((ROOT / SCOUT).read_bytes()).hexdigest() == before['files'][str(SCOUT)], 'Scout preimage changed; rebase the proposal first'
    project = Path(tempfile.mkdtemp(prefix='shiftfour-scout-review-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    # Only the disposable project receives the proposed policy and test.
    (project / SCOUT).write_bytes((HERE / 'ScoutBrain.cs.txt').read_bytes())
    (project / TEST).write_bytes((HERE / 'harness' / TEST.name).read_bytes())
    (project / (str(TEST) + '.meta')).write_bytes((HERE / 'harness' / (TEST.name + '.meta')).read_bytes())
    print('Review project:', project, flush=True)
    if args.prepare_only:
        return
    result_dir = project / 'ReviewResults'
    result_dir.mkdir()
    name = 'MainLab_Demonstration' if args.demo_only else 'ShiftFour.Tests.EnhancedScoutPlayModeTests'
    import os
    env = dict(os.environ, SHIFTFOUR_SCOUT_EVIDENCE=str(result_dir))
    subprocess.run([args.unity, '-batchmode', '-projectPath', str(project), '-runTests',
                    '-testPlatform', 'PlayMode', '-testFilter', name,
                    '-testResults', str(result_dir / 'PlayMode.xml'), '-logFile', str(result_dir / 'PlayMode.log')],
                   env=env, check=True)
    print('Results:', result_dir)


if __name__ == '__main__':
    main()
