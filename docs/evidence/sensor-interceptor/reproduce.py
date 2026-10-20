"""Verify the active Interceptor in a disposable Unity project, with optional paired sound evidence."""
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
    parser.add_argument('--unity', default=UNITY)
    parser.add_argument('--prepare-only', action='store_true')
    parser.add_argument('--scenarios-only', action='store_true')
    args = parser.parse_args()
    project = Path(tempfile.mkdtemp(prefix='shiftfour-interceptor-verify-'))
    for folder in ['Assets', 'Packages', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, project / folder)
    print('Verification project:', project, flush=True)
    if args.prepare_only:
        return
    output = project / 'VerificationResults'
    output.mkdir()

    def run(mode, name, *, variant='after', test_filter=None):
        directory = output / name
        directory.mkdir()
        command = [args.unity, '-batchmode', '-projectPath', str(project), '-runTests', '-testPlatform', mode,
                   '-testResults', str(directory / 'Tests.xml'), '-logFile', str(directory / 'Unity.log')]
        if mode == 'EditMode':
            command.append('-nographics')
        if test_filter:
            command.extend(['-testFilter', test_filter])
        env = dict(os.environ, SHIFTFOUR_EVIDENCE=str(directory), SHIFTFOUR_INTERCEPTOR_VARIANT=variant,
                   SHIFTFOUR_DEMO_OUTPUT=str(output / 'RegressionFrames'))
        subprocess.run(command, env=env, check=True)

    if args.scenarios_only:
        agents = project / 'Assets/Scripts/AI/Agents'
        current = {name: (agents / name).read_bytes() for name in ['InterceptorBrain.cs', 'InterceptorPolicy.cs']}
        try:
            for name in current:
                (agents / name).write_bytes((HERE / (name + '.before.txt')).read_bytes())
            run('PlayMode', 'before-sound', variant='before', test_filter='MainLab_SoundResponse_BeforeAfterEvidence')
        finally:
            for name, content in current.items():
                (agents / name).write_bytes(content)
        run('PlayMode', 'after-sound', test_filter='MainLab_SoundResponse_BeforeAfterEvidence')
    else:
        run('EditMode', 'edit')
        run('PlayMode', 'play')
    print('Results and rendered evidence:', output)


if __name__ == '__main__':
    main()
