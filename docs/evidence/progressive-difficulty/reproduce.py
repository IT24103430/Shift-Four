"""Run final unified verification in a fresh project; source checkout and saved assets stay untouched."""
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
    parser.add_argument('--scenarios-only', action='store_true')
    args = parser.parse_args()
    project = Path(tempfile.mkdtemp(prefix='shiftfour-final-validation-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    print('Disposable project:', project, flush=True)
    if args.prepare_only:
        return
    results = project / 'VerificationResults'
    results.mkdir()
    env = dict(os.environ, SHIFTFOUR_FINAL_EVIDENCE=str(results / 'generated'),
               SHIFTFOUR_INTEGRATION_EVIDENCE=str(results / 'integration'),
               SHIFTFOUR_EVIDENCE=str(results / 'regression'),
               SHIFTFOUR_ANIMATION_EVIDENCE=str(results / 'awareness'),
               SHIFTFOUR_DEMO_OUTPUT=str(results / 'destruction'))
    summary = {}
    for mode in (['PlayMode'] if args.scenarios_only else ['EditMode', 'PlayMode']):
        command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests',
                   '-testPlatform', mode, '-testResults', str(results / (mode + '.xml')),
                   '-logFile', str(results / (mode + '.log'))]
        if mode == 'EditMode':
            command.append('-nographics')
        if args.scenarios_only:
            command.extend(['-testFilter', ';'.join([
                'ShiftFour.Tests.ProgressiveDifficultyValidationPlayModeTests',
                'ShiftFour.Tests.FourRoomIntegrationPlayModeTests',
                'ScriptedMission_AllFourRealConsoles_Gates_Exit_WinRestart_LossRestart',
                'RenderedPrediction_VisibleMotion_Reversal_CoverAndMemorySearch',
                'MainLab_SoundResponse_BeforeAfterEvidence',
                'RenderedAllFour_ShowDistinctAwarenessCuesWithOriginalModelColors',
                'RenderedMainLab_AllFourAcceptedCombatAndDestructionSequences'])])
        subprocess.run(command, env=env, check=True)
        report = ET.parse(results / (mode + '.xml')).getroot()
        summary[mode] = report.attrib
        if report.get('result') != 'Passed' or int(report.get('failed', '0')) or int(report.get('skipped', '0')):
            raise RuntimeError('Inspect failed/skipped Unity results: ' + str(results / (mode + '.xml')))
    (results / 'test-summary.json').write_text(json.dumps(summary, indent=2))
    print('Raw results, runtime metrics and rendered captures:', results)


if __name__ == '__main__':
    main()
