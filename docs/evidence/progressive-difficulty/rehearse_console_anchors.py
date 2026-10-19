"""Rehearse the unapproved five-path console anchor patch in an isolated Unity copy."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import uuid
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
UNITY = '/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', default=UNITY)
    parser.add_argument('--cache-project', type=Path, help='Optional inactive disposable Library cache, cloned on macOS.')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    project = Path(tempfile.mkdtemp(prefix='shiftfour-console-review-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    if args.cache_project:
        subprocess.run(['cp', '-cR', str(args.cache_project / 'Library'), str(project / 'Library')], check=True)
    subprocess.run(['git', 'apply', '--check', str(HERE / 'ConsoleAnchors.task14.patch')], cwd=project, check=True)
    subprocess.run(['git', 'apply', str(HERE / 'ConsoleAnchors.task14.patch')], cwd=project, check=True)
    target = project / 'Assets/Tests/PlayMode/ConsoleAnchorReviewPlayModeTests.cs'
    shutil.copy2(HERE / 'harness' / target.name, target)
    target.with_suffix('.cs.meta').write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n')
    output = (args.output or project / 'VerificationResults').resolve()
    output.mkdir(parents=True, exist_ok=True)
    command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests', '-testPlatform', 'PlayMode',
               '-testFilter', 'ShiftFour.Tests.ConsoleAnchorReviewPlayModeTests', '-testResults', str(output / 'ProposalPlayMode.xml'),
               '-logFile', str(output / 'ProposalPlayMode.log')]
    (output / 'command.json').write_text(json.dumps(command, indent=2))
    (output / 'review-project.txt').write_text(str(project) + '\n')
    print('Pending console anchor proposal project:', project, flush=True)
    subprocess.run(command, env=dict(os.environ, SHIFTFOUR_ANCHOR_EVIDENCE=str(output)), check=True)
    report = ET.parse(output / 'ProposalPlayMode.xml').getroot()
    if report.get('result') != 'Passed':
        raise RuntimeError('Inspect proposal results: ' + str(output))
    preimages = json.loads((HERE / 'console-anchor-preimages.json').read_text())
    (output / 'integrity.json').write_text(json.dumps(dict(active_owned_files_unchanged=all(
        hashlib.sha256((ROOT / p).read_bytes()).hexdigest() == digest for p, digest in preimages.items()),
        proposal_paths=list(preimages), test_counts=report.attrib), indent=2))
    print('Proposal-only validation:', output)


if __name__ == '__main__':
    main()
