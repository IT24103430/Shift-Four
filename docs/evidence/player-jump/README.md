# Player jump verification

Unity 6000.6.4f1; baseline `2aece7603222c2fc3964779cf0da75e9a9456dd5`; date 2026-10-20.

See [PLAYER_JUMPING.md](../../PLAYER_JUMPING.md) for behavior, test coverage, gate-approach limits and the known imported-sample compile blocker.

## Runs

Exact argument arrays are retained in `commands.json`. All runs use the Task 1 disposable Unity project named in `source-state.json`, with Samples excluded only there. No package installation or source sample removal occurred in either real checkout.

The test processes use:

```text
SHIFTFOUR_JUMP_EVIDENCE=<this directory>/generated
SHIFTFOUR_EVIDENCE=<this directory>/regression-generated
SHIFTFOUR_DEMO_OUTPUT=<disposable project>/RegressionFrames
```

Run one Unity process at a time for a project. Edit Mode selects the two ShiftFour test assemblies. Full Play Mode selects `ShiftFour.Movement.PlayModeTests`. Focused Play Mode selects `ShiftFour.Tests.PlayerJump`, covering both the collision fixture and actual MainLab fixture. Results and exit codes are written after each process exits; no `-quit` flag prematurely ends a test run.

## Evidence

- `EditMode.xml` / log / exit code: existing and new Edit Mode tests.
- `JumpPlayMode.xml` / log / exit code: first passing fourteen-case jump fixture.
- `PlayMode.xml` / log / exit code: full existing Play Mode suite plus those fourteen new cases: **171/173 passed**, two existing hearing/navigation failures. This failed run is retained without being replaced by a passing rerun.
- `FinalJumpPlayMode.xml` / log / exit code: final focused fixture, adding a fifteenth case that measures a differently configured jump height and gravity, plus the two full-run failures selected explicitly: **17/17 passed**. The production controller is identical to the full regression run.
- `BaselineFailureControl.xml` / log / exit code and `baseline-control-controller.cs.txt`: the same two isolated cases using the exact baseline controller in the disposable project: **2/2 passed**. Candidate source was restored afterward. Isolated passes do not establish the cause of full-suite failures or turn the full run into a pass.
- `PlayerJumpPlayModeTests.full-run.cs.txt`: exact jump fixture compiled during the full run, before adding the final tuning-arc case. All fifteen final fixture cases run after that addition; existing regression cases do not need another run for this test-only addition.
- `JumpPlayMode.attempt1.*`, `JumpPlayMode.attempt2.*`: earlier fixture failures. A camera assertion needed floating-point tolerance; exterior final-exit trajectories were invalid approaches to a locked gate, as explained below. The controller was not changed to hide those failures.
- `generated/frame-rates.csv`, `standing-jump.csv`: measured height, travel and grounded timing with actual CharacterController collisions.
- `generated/mainlab-gate-jumps.csv`, `mainlab-gate-trace.csv`: final thirty-five reachable gate approaches. Each trajectory contains 150 movement samples with repeated jump requests and diagonal pressure.
- `invalid-exterior-mainlab-gate-*.csv`: retained diagnosis of teleported trajectories outside the final exit's authored floor, falling below the level before crossing its infinite gate plane. These do not represent a reachable bypass from inside the locked lab.
- `regression-generated/`: measurements/captures emitted by existing tests.
- `source-state.json`: baseline, paths, initial candidate source hashes and isolation details.
- `integrity.json`: existing-source and GUID checks relative to preparation, with the previously documented binary sample checkout-normalization exceptions.
- `test-summary.json`, `tested-source-sha256.json`, `evidence-sha256.json`: final result summary and source/evidence hashes.

The tests call the same movement/launch methods used by Update through reflection, following the existing assembly layout. They test real colliders, actual MainLab gate barriers, actual Health/GameSession death and scene reload; they do not synthesize native keyboard input. Space is verified in the existing InputManager bindings. Temporary test objects and transforms are never saved into scenes/prefabs.

The two inspection-test source files and their new GUIDs are the only added Assets. No scene, imported model/material, drone, weapon, input configuration or package file is edited.
