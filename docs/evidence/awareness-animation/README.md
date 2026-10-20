# Task 12 — Awareness and movement animation evidence

Applied implementation, verified in a disposable Unity **6000.6.4f1** project: `/tmp/shiftfour-task12-edh9b9vv`. Only `DroneAnimationPose.cs` and `DroneRotorVisuals.cs` change in production. Two new test files and their stable meta GUIDs provide the additional coverage. No existing test is removed, rewritten or relaxed.

The active scene, FBX imports, prefabs, materials, colliders, navigation, Health, firing presentation events, weapon effects, wreck physics, motor, awareness/difficulty services and individual policies remain unchanged. The pending Scout/Guard/Flanker review proposals are not applied. The starting files/index/status are in `before.json`; concurrent user Git activity is recorded separately. This task does not stage, commit, push or reset Git state.

## Results

**297/297 Edit Mode and 153/153 Play Mode tests passed: 450 total, zero failures or skips.** Final complete suite totals are recorded from XML in `test-summary.json` and the per-case `TEST-MATRIX.csv`. These include every existing Interceptor test and all other original project tests alongside the new cases.

New coverage: **17 Edit Mode** cases and **8 Play Mode** cases.

- Six awareness states, distinct amber/red intensity and pulse cadence, slow bounded scanning, movement suppression and natural return to neutral hover.
- Positive acceleration adds modest pitch/rotor effort at equal movement speed, then releases while braking. Turning bank is unchanged.
- Firing/hit priorities and fatal latching; alert state alone cannot fabricate a shot.
- Actual `DroneMotor` motion on all four models, accepted velocity equality, turning, acceleration, braking, arrival and 4° combined pose cap.
- Actual imported mesh vertices inside each existing `DroneBody` envelope during scanning at 30/60/120 Hz; unchanged root position/rotation, local scale and authored rest height.
- Exact property-block restoration on disable, no duplicate pivots/writers, shared material references and original alpha preserved.
- Disabled awareness and teleports cannot manufacture acceleration or revive alert feedback.
- Calibrated zero managed allocation events through all six awareness states after binding.
- Sixteen actual rendered cue captures: four states × four existing models.

## Executed stages

- `EditMode.initial.xml/.log`: **297/297 passed**, including all 17 new pure animation cases. Production animation code has not changed since this run.
- `FocusedPlayMode.initial.xml/.log`: **31/31 passed**, including all eight new MainLab cases and existing animation, destruction and progressive-speed fixtures. Both old and new allocation tests pass; old body envelope, accepted-fire and physics wreck assertions are unchanged.
- Visual inspection found the initial Scout camera obscured by foreground cover. The capture helper now selects a camera angle using read-only clearance queries instead of moving room objects. No production behavior changed.
- `RenderedCues.xml/.log`: **1/1 passed** after the camera correction. All four models have readable cue comparisons.
- `EditMode.xml/.log`: **297/297 passed**. `PlayMode.xml/.log`: **153/153 passed** against the final production code and clear-angle capture helper; all original regressions remain enabled.

## Measurements

`actual-movement.csv` records actual motor travel, peak normalized forward effort, rotor-speed factor, turning bank and observed braking for each model. Root motion comes from the existing motor/body pipeline, not an animated transform. Raw pose bank can include the established small hit/hover contributions; the final model pose is still capped at 4°.

The final measurement reports **zero managed allocation events across 24,000 warmed ticks**, with a positive calibration. CPU time ranges from **10.771 to 44.977 ms per 4,000 ticks**, depending on cue activity.

`allocations.csv` measures six warmed blocks of 4,000 visual ticks (24,000 total). The existing `MeasuredAllocation` helper first proves that Unity's GC.Alloc profiler counter detects a known allocation. It then records allocation events on the current thread. A zero result is not inferred from the unsupported Mono byte counter. Active property-block cues, scan math and reused VFX updates are included. The measured timing is a CPU microbenchmark, not a full frame or GPU result; native rendering/physics allocation and initial binding/death-rig creation are outside that claim.

`rendered-cues.csv` records actual cue strength, scan yaw, rotor factor and pitch/bank for every image. [Open the capture viewer](index.html). PNGs are losslessly converted from the raw Unity TGA files; `captures.json` records both hashes. The test uses controlled awareness snapshots via `DroneAwareness.Step`, with brains/input frozen, to isolate presentation. It does not claim to demonstrate new policy decisions or sensing. The unchanged project sensing/combat suites validate those interfaces separately.

## Reproduce

```sh
python3 docs/evidence/awareness-animation/reproduce.py
```

Default: new Edit/Play tests plus existing animation/destruction/speed fixtures. `--full` runs the complete project suites; `--demo-only` runs the sixteen-capture scenario; `--prepare-only` prepares a current-source copy for interactive Unity inspection. The script only copies Assets/Packages/ProjectSettings into a new temporary project and never substitutes old source into active files.

Exact complete verification commands:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
ANIMATION_PROJECT=/tmp/shiftfour-task12-edh9b9vv
ANIMATION_RESULTS=/Users/dev/Desktop/game/Shift-Four/docs/evidence/awareness-animation

"$UNITY" -batchmode -nographics -projectPath "$ANIMATION_PROJECT" -runTests -testPlatform EditMode -testResults "$ANIMATION_RESULTS/EditMode.xml" -logFile "$ANIMATION_RESULTS/EditMode.log"

SHIFTFOUR_ANIMATION_EVIDENCE="$ANIMATION_RESULTS" SHIFTFOUR_EVIDENCE="$ANIMATION_PROJECT/RegressionEvidence" SHIFTFOUR_DEMO_OUTPUT="$ANIMATION_PROJECT/RegressionFrames" "$UNITY" -batchmode -projectPath "$ANIMATION_PROJECT" -runTests -testPlatform PlayMode -testResults "$ANIMATION_RESULTS/PlayMode.xml" -logFile "$ANIMATION_RESULTS/PlayMode.log"
```

## Integrity and limits

`integrity.json` compares every original active Assets/Packages/ProjectSettings file with the starting snapshot and identifies only the two intended production edits plus four new test/meta files. `validated-source-sha256.json` matches actual verification sources to the active and reproduced sources. If Unity serializes platform defaults only in disposable ProjectSettings, that delta is recorded separately and excluded from the active change.

No MainLab generation, NavMesh bake, FBX edit, material/prefab migration, other-policy edit, staging, commit or push is performed. Actual rendering is inspected, but subjective visual quality/human gameplay balance and standalone-player GPU profiling are not claimed. See [implementation and practical limits](../../AWARENESS_ANIMATION_INTEGRATION.md).
