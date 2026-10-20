"""Run optional scripted encounter diagnostics without installing tests in the source checkout."""
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


def sources(root):
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in (root / 'Assets').rglob('*.cs')}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity', default=UNITY)
    parser.add_argument('--project', type=Path, help='Optional existing disposable copy with matching current C# source.')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    project = args.project or Path(tempfile.mkdtemp(prefix='shiftfour-encounters-'))
    if project.resolve() == ROOT.resolve():
        raise ValueError('The source checkout cannot be used as a diagnostic project')
    if not args.project:
        for folder in ['Assets', 'Packages', 'ProjectSettings']:
            shutil.copytree(ROOT / folder, project / folder)
    before = sources(ROOT)
    if sources(project) != before:
        raise ValueError('Disposable project C# source differs from the latest unified checkout')
    output = (args.output or project / 'VerificationEncounters').resolve()
    output.mkdir(parents=True, exist_ok=True)
    target = project / 'Assets/Tests/PlayMode/ProgressiveEncounterEvidencePlayModeTests.cs'
    meta = target.with_suffix('.cs.meta')
    if target.exists() or meta.exists():
        raise FileExistsError(target)
    shutil.copy2(HERE / 'harness' / target.name, target)
    meta.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n')
    command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests', '-testPlatform', 'PlayMode',
               '-testFilter', 'ShiftFour.Tests.ProgressiveEncounterEvidencePlayModeTests',
               '-testResults', str(output / 'ScenarioPlayMode.xml'), '-logFile', str(output / 'ScenarioPlayMode.log')]
    (output / 'command.json').write_text(json.dumps(command, indent=2))
    print('Scripted encounter project:', project, flush=True)
    try:
        subprocess.run(command, env=dict(os.environ, SHIFTFOUR_ENCOUNTER_EVIDENCE=str(output)), check=True)
        report = ET.parse(output / 'ScenarioPlayMode.xml').getroot()
        if report.get('result') != 'Passed':
            raise RuntimeError('Inspect diagnostic scenario results: ' + str(output))
    finally:
        target.unlink(missing_ok=True)
        meta.unlink(missing_ok=True)
        (output / 'integrity.json').write_text(json.dumps(dict(active_sources_unchanged=sources(ROOT) == before,
            disposable_sources_restored=sources(project) == before, original_csharp_count=len(before),
            harness_sha256=hashlib.sha256((HERE / 'harness' / target.name).read_bytes()).hexdigest()), indent=2))
    print('Results and actual capture frames:', output)


if __name__ == '__main__':
    main()
