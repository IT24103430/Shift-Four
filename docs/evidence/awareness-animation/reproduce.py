"""Test current awareness/movement animation in a new disposable Unity project."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
UNITY = '/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--full', action='store_true')
    parser.add_argument('--demo-only', action='store_true')
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--unity', default=UNITY)
    args = parser.parse_args()
    project = Path(tempfile.mkdtemp(prefix='shiftfour-animation-verify-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    print('Verification project:', project, flush=True)
    if args.prepare_only:
        return
    results = project / 'VerificationResults'
    results.mkdir()
    env = dict(os.environ, SHIFTFOUR_ANIMATION_EVIDENCE=str(results),
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
        run('PlayMode', 'RenderedAllFour_ShowDistinctAwarenessCuesWithOriginalModelColors')
    else:
        run('EditMode', None if args.full else 'ShiftFour.Tests.AwarenessAnimationEditModeTests;ShiftFour.Tests.DroneAnimationEditModeTests')
        run('PlayMode', None if args.full else ';'.join([
            'ShiftFour.Tests.AwarenessAnimationPlayModeTests', 'ShiftFour.Tests.DroneAnimationPlayModeTests',
            'ShiftFour.Tests.DroneDestructionPlayModeTests', 'ShiftFour.Tests.DroneMovementSpeedsPlayModeTests']))
    print('Results and actual capture frames:', results)


if __name__ == '__main__':
    main()
