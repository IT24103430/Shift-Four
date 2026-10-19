"""Verify current MainLab in a disposable Unity 6000.6.4f1 project; never save source assets."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
UNITY = '/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', default=UNITY)
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--focused', action='store_true')
    parser.add_argument('--demo-only', action='store_true')
    args = parser.parse_args()
    project = Path(tempfile.mkdtemp(prefix='shiftfour-integration-verify-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    print('Disposable project:', project, flush=True)
    if args.prepare_only:
        return
    results = project / 'VerificationResults'
    results.mkdir()
    env = dict(os.environ, SHIFTFOUR_INTEGRATION_EVIDENCE=str(results),
               SHIFTFOUR_ANIMATION_EVIDENCE=str(results / 'awareness'),
               SHIFTFOUR_EVIDENCE=str(results / 'movement'),
               SHIFTFOUR_DEMO_OUTPUT=str(results / 'destruction'))
    summary = {}

    def run(mode, selected=None):
        command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests',
                   '-testPlatform', mode, '-testResults', str(results / (mode + '.xml')),
                   '-logFile', str(results / (mode + '.log'))]
        if mode == 'EditMode':
            command.append('-nographics')
        if selected:
            command.extend(['-testFilter', selected])
        subprocess.run(command, env=env, check=True)
        report = ET.parse(results / (mode + '.xml')).getroot()
        summary[mode] = dict(report.attrib)
        if report.get('result') != 'Passed' or int(report.get('failed', '0')):
            raise RuntimeError('Unity test failures: ' + str(results / (mode + '.xml')))

    if args.demo_only:
        run('PlayMode', ';'.join([
            'ScriptedMission_AllFourRealConsoles_Gates_Exit_WinRestart_LossRestart',
            'MainLab_RealSoundAndVisionConnectAwarenessToSpeedAndPresentationForAllFour',
            'RenderedAllFour_ShowDistinctAwarenessCuesWithOriginalModelColors',
            'RenderedMainLab_AllFourAcceptedCombatAndDestructionSequences']))
    else:
        run('EditMode', 'ShiftFour.Tests.FourRoomIntegrationEditModeTests' if args.focused else None)
        run('PlayMode', 'ShiftFour.Tests.FourRoomIntegrationPlayModeTests' if args.focused else None)
    (results / 'test-summary.json').write_text(json.dumps(summary, indent=2))
    print('Results, audit and rendered captures:', results)


if __name__ == '__main__':
    main()
