# Task 7 movement verification evidence

Active implementation: `/Users/dev/Desktop/game/Shift-Four`, branch `feature/progressive-drone-intelligence-workspace`.
Starting HEAD: `a4ff4fdebcd3a7c461d416dfcc2c565926b37b05`, with existing Task 6 working changes preserved.
Unity: 6000.6.4f1.

Tests run against the matching source in `/Users/dev/Desktop/game/Shift-Four-Progressive` because the active project remains open in Unity. Task 7 changes are present in the active project, not only in the verification worktree.

## Commands

```sh
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/dev/Desktop/game/Shift-Four-Progressive -runTests -testPlatform EditMode -testResults /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/EditMode.xml -logFile /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/EditMode.log

/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/dev/Desktop/game/Shift-Four-Progressive -runTests -testPlatform PlayMode -testFilter ShiftFour.Tests.DroneMovementSpeedsPlayModeTests -testResults /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/FocusedPlayMode.xml -logFile /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/FocusedPlayMode.log

SHIFTFOUR_EVIDENCE=/Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/generated /Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/dev/Desktop/game/Shift-Four-Progressive -runTests -testPlatform PlayMode -testResults /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/PlayMode.xml -logFile /Users/dev/Desktop/game/Shift-Four/docs/evidence/drone-speeds/PlayMode.log
```

## Evidence layout

- `test-summary.json`: final Unity totals, durations and timestamps.
- `TEST-MATRIX.csv`: individual final Edit/Play test results.
- `EditMode.xml`, `FocusedPlayMode.xml`, `PlayMode.xml`: actual NUnit output; corresponding `.log` files retain Unity logs.
- `PlayMode.second.xml`/`.log`: second full run, 134/136; transient feedback and per-frame delta fixture assumptions corrected with explicit simulation cadence.
- `TimingFixtures.xml`/`.log`: focused animation/proximity fixture rerun.
- `PlayMode.initial.xml`/`.log`: first full run, 130/136; `InterceptorFixtures.initial.xml`/`.log`: diagnostics confirming the five staging failures after the chase-mode setup correction.
- `InterceptorFixtures.xml`/`.log`: focused rerun of MainLab Interceptor plus Control junction regression.
- `cadence-results.json`: measured accepted displacement peaks at each simulation step.
- `FocusedPlayMode.initial.xml`/`.log`: initial 4/8 result; new fixture assumptions corrected as explained in `docs/DRONE_MOVEMENT_SPEEDS.md`.
- `task7-before.json`: initial active checkout file fingerprints, HEAD and index entries.
- `verification-before.json`: prior matching verification-worktree source fingerprints and Git state.
- `validated-source-sha256.json`: all 79 C# files used for final verification.
- `changed-files.json`: exact Task 7 delta, excluding preexisting Task 6 work.
- `*.before.txt`, `*.task7.patch`, `task7-source.zip`: preimages, isolated patches and final source package.
- `integrity.json`: active/verification source agreement, 131 protected assets, 8 profile files, unchanged tactics/body/A*/gates/health/fire/death, and preserved HEAD/index.
- `verification-settings-before.txt` and any `verification-settings-generated.*`: Unity-generated verification-only settings changes, archived before restoring the captured bytes.
- `generated/`: inherited regression traces, performance reports and lossless screenshot archive.

Run `python3 docs/evidence/drone-speeds/finalize-evidence.py` only after test processes exit. It asserts passing XML and exact source/asset preservation, restores only verification settings rewritten by Unity, archives final source and losslessly compresses generated screenshots.

See `docs/DRONE_MOVEMENT_SPEEDS.md` for configuration, runtime API, speed/awareness mapping, test interpretation and limitations.
