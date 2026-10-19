"""Run complete existing suites from the exact Task 15 commit without changing a checkout."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[3]
BASELINE = '5a5cf7ad9c389576db0f2de1407c936289bc32ab'
UNITY = '/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', default=UNITY)
    parser.add_argument('--prepare-only', action='store_true')
    args = parser.parse_args()
    project = Path(tempfile.mkdtemp(prefix='shiftfour-task15-reproduce-'))
    # Read the committed tree, independent of later edits or branch/worktree changes.
    with tempfile.TemporaryFile() as archive:
        subprocess.run(['git', 'archive', BASELINE, 'Assets', 'Packages', 'ProjectSettings'],
                       cwd=ROOT, stdout=archive, check=True)
        archive.seek(0)
        with tarfile.open(fileobj=archive) as contents:
            contents.extractall(project, filter='data')
    print('Exact baseline:', BASELINE, flush=True)
    print('Disposable Unity project:', project, flush=True)
    if args.prepare_only:
        return
    results = project / 'VerificationResults'
    results.mkdir()
    env = dict(os.environ, SHIFTFOUR_INTEGRATION_EVIDENCE=str(results / 'generated'),
               SHIFTFOUR_FINAL_EVIDENCE=str(results / 'generated'),
               SHIFTFOUR_EVIDENCE=str(results / 'regression'),
               SHIFTFOUR_ANIMATION_EVIDENCE=str(results / 'awareness'),
               SHIFTFOUR_DEMO_OUTPUT=str(results / 'destruction'))
    summary = {}
    for mode in ['EditMode', 'PlayMode']:
        command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests',
                   '-testPlatform', mode, '-testResults', str(results / (mode + '.xml')),
                   '-logFile', str(results / (mode + '.log'))]
        if mode == 'EditMode':
            command.append('-nographics')
        subprocess.run(command, env=env, check=True)
        report = ET.parse(results / (mode + '.xml')).getroot()
        summary[mode] = report.attrib
        (results / 'test-summary.json').write_text(json.dumps(summary, indent=2))
        if report.get('result') != 'Passed' or int(report.get('failed', '0')) or int(report.get('skipped', '0')):
            raise RuntimeError('Inspect failed/skipped test results: ' + str(results))
    print('Complete Edit/Play results and actual fixture output:', results)


if __name__ == '__main__':
    main()
