"""Prepare the pending Flanker proposal only in a new disposable Unity project."""
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
    'Assets/Scripts/AI/Agents/FlankerBrain.cs': 'FlankerBrain.cs.txt',
    'Assets/Tests/EditMode/Movement/TacticalFlankerEditModeTests.cs': 'harness/TacticalFlankerEditModeTests.cs',
    'Assets/Tests/EditMode/Movement/TacticalFlankerEditModeTests.cs.meta': 'harness/TacticalFlankerEditModeTests.cs.meta',
    'Assets/Tests/PlayMode/TacticalFlankerPlayModeTests.cs': 'harness/TacticalFlankerPlayModeTests.cs',
    'Assets/Tests/PlayMode/TacticalFlankerPlayModeTests.cs.meta': 'harness/TacticalFlankerPlayModeTests.cs.meta',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--demo-only', action='store_true')
    parser.add_argument('--full', action='store_true')
    parser.add_argument('--unity', default=UNITY)
    args = parser.parse_args()
    before = json.loads((HERE / 'before.json').read_text())['files']
    for target in SOURCES:
        if target in before:
            assert hashlib.sha256((ROOT / target).read_bytes()).hexdigest() == before[target], target + ' changed; rebase and review first'
        else:
            assert not (ROOT / target).exists(), target + ' already exists; review the collision first'
    project = Path(tempfile.mkdtemp(prefix='shiftfour-flanker-review-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    for target, source in SOURCES.items():
        (project / target).write_bytes((HERE / source).read_bytes())
    print('Review project:', project, flush=True)
    if args.prepare_only:
        return
    results = project / 'ReviewResults'
    results.mkdir()
    env = dict(os.environ, SHIFTFOUR_FLANKER_EVIDENCE=str(results),
               SHIFTFOUR_EVIDENCE=str(results / 'Regression'), SHIFTFOUR_DEMO_OUTPUT=str(results / 'RegressionFrames'))

    def run(mode, test_filter=None):
        command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests', '-testPlatform', mode,
                   '-testResults', str(results / (mode + '.xml')), '-logFile', str(results / (mode + '.log'))]
        if mode == 'EditMode':
            command.append('-nographics')
        if test_filter:
            command.extend(['-testFilter', test_filter])
        subprocess.run(command, env=env, check=True)

    if args.demo_only:
        run('PlayMode', ';'.join([
            'MainLab_LeftAndRightAreReachable_UseNativeAStarAndCombatStandoff',
            'MainLab_OneSideSolid_OverridesCommitAndSelectsOtherSide',
            'MainLab_BothSidesSolid_RejectsFlanksAndUsesSafeFallback',
            'MainLab_SoundOnly_DemonstratesInvestigationSearchAndReturn']))
    else:
        run('EditMode', None if args.full else 'ShiftFour.Tests.TacticalFlankerEditModeTests')
        run('PlayMode', None if args.full else 'ShiftFour.Tests.TacticalFlankerPlayModeTests')
    print('Results and actual rendered frames:', results)


if __name__ == '__main__':
    main()
