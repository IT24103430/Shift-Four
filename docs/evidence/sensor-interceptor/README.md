# Task 10 — Sensor-aware Interceptor evidence

Implementation is applied in the active checkout. The only modified production sources are `InterceptorBrain.cs` and `InterceptorPolicy.cs`. Existing other-agent policies, shared movement/body/perception, difficulty assets, scenes, prefabs, gates and navigation data remain unchanged. The pending Task 8/9 proposal assets were not applied.

Unity: **6000.6.4f1**. Tests run in `/tmp/shiftfour-task10-pch6738y`, a disposable copy of the active Assets/Packages/ProjectSettings. `before.json` records the starting source and index; saved Interceptor preimages are used only in the disposable copy for baseline experiments. Concurrent workspace Git activity is recorded separately from source-byte checks. This task issues no commit, staging or push commands.

## Validation scope

The final full Edit Mode and Play Mode totals are recorded from actual XML in `test-summary.json` and `TEST-MATRIX.csv`. The complete project suites include all existing Interceptor, Control tactical, prediction evidence and unified movement tests, plus the new motion-timing/sensor cases.

New coverage:

- Acceleration/braking ETA, short and long routes, stopping at corners, opposing/lateral own motion, invalid motion input, configured speed and acceleration changes.
- Motion-aware straight prediction, unchanged normalized score terms, stationary observations and genuine reversal.
- Hidden-input isolation, unchanged last visual observation/velocity/deadline, bounded history, cooldown, gate invalidation and repeated-tick stability.
- Actual MainLab vision → prediction; fresh noise cannot replace a justified committed interception.
- Sound-only investigation with zero player sightings, zero predictive confidence and no combat authorization.
- Sound expiry, moved source, return/home fallback, locked Control gate, cover occlusion and hidden movement.
- Runtime 125 HP, 2.5/3.1/3.7 m/s mode targets and terminal death.
- The same rendered sound scenario executed against the saved original implementation and the updated implementation.

The original `InterceptorPolicyEditModeTests`, `InterceptorComparisonEditModeTests`, `ControlTacticalNavigationEditModeTests`, `InterceptorMainLabPlayModeTests`, `ControlPredictionMainLabPlayModeTests` (including its rendered partial), `ControlTacticalNavigationPlayModeTests` and Student 4 unified regressions are all included in the full suites. No existing assertions are removed or relaxed.

## Baseline and iterations

- `BeforePlayMode.xml/.log`: **22/22 passed** on the unchanged source: existing Interceptor MainLab, Control prediction and Control tactical fixtures. `before/` retains their traces and actual rendered prediction frames.
- `EditMode.initial.xml/.log`: **280/280 passed**, including 11 new motion-timing cases, on the initial candidate.
- `FocusedPlayMode.initial.xml/.log`: **28/29 passed**. All seven initial sensor cases passed. The original both-junction fixture failed because a disabled/warped motor still exposed its last accepted velocity sample; the adapter treated that cached sample as active motion and over-penalized ETA. The adapter now supplies zero current velocity when the motor is disabled. No shared motor or existing fixture assertion was changed.
- `MotionAndSensors.xml/.log`: **8/8 passed**, rerunning that exact existing fixture plus all seven initial sensor cases after the correction.
- `BeforeSound.xml/.log`: **1/1 passed** against saved original Interceptor sources, with the new reflection-only evidence fixture and explicit `SHIFTFOUR_INTERCEPTOR_VARIANT=before`. The sensor receives a gunshot and awareness advances, but the original policy stays on its home destination. No fabricated visual observations are added.
- `PlayMode.before-route-fix.xml/.log`: **144/144 passed**, but inspection of its actual sound trace exposed a continuity gap: the sound action lasted one decision, then a raised obstacle invalidated the changing native approach. Passing assertions did not establish sustained investigation. The adapter now retains validated intermediate stops and reconnects through the existing Control ring; the evidence fixture requires repeated sound actions, sound-search and actual home arrival. `after-before-route-fix/` preserves the problematic trace.
- `SoundRoutes.xml/.log`: **15/15 passed**, covering the strengthened sensor fixture and all existing Control tactical Play Mode tests after routing changes.
- Final full `EditMode.xml/.log` and `PlayMode.xml/.log`: actual final results, including the additional paired-evidence test and finite-cap validation.

Baseline source substitution never touches active assets. It restores only the two disposable Interceptor files from `*.before.txt`, runs the one explicit baseline fixture, then restores the active implementation before candidate tests. A test asserts that baseline mode really has the original adapter and candidate mode really has the new one.

## Before/after scenarios

See [SCENARIOS.md](SCENARIOS.md) for rendered views, measured behavior and limitations. Evidence is divided deliberately:

- `before/`: existing tests running against the original source.
- `before-sound/`: the actual original adapter under the new sound-only scenario.
- `after/`: final candidate prediction/sound frames and traces.
- `after/motion-before-after.csv`: identical observation sequence and routes through the same advanced policy, comparing its preserved explicit cruise-timing API with the new motion-aware API. This isolates timing effects; it is not a player capture benchmark.

Timing comparisons hold candidate points, route delegates, observation positions/timestamps and settings fixed. Per-instance hearing uncertainty may vary between scene instances; sound comparisons use the same scripted scene, emitter distance/category/intensity and production hearing defaults, with actual roots/goals recorded in CSV. Rendered route overlays come from validated route data, not invented trajectories. Raw TGA frames and converted PNGs are retained or losslessly archived.

## Reproduce

```sh
python3 docs/evidence/sensor-interceptor/reproduce.py
```

This creates a fresh disposable project and runs the complete Edit/Play suites. Use `--scenarios-only` for the explicit original/current sound comparison, or `--prepare-only` to inspect the current source in Unity. The script prints the project and result paths, never rewrites active sources, and restores the current disposable sources in `finally` after the baseline experiment. A fresh Unity import may take several minutes.

Exact final commands:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
VERIFY_PROJECT=/tmp/shiftfour-task10-pch6738y
INTERCEPTOR_RESULTS=/Users/dev/Desktop/game/Shift-Four/docs/evidence/sensor-interceptor

SHIFTFOUR_EVIDENCE="$INTERCEPTOR_RESULTS/after" SHIFTFOUR_DEMO_OUTPUT="$VERIFY_PROJECT/RegressionFrames" "$UNITY" -batchmode -projectPath "$VERIFY_PROJECT" -runTests -testPlatform PlayMode -testResults "$INTERCEPTOR_RESULTS/PlayMode.xml" -logFile "$INTERCEPTOR_RESULTS/PlayMode.log"

SHIFTFOUR_EVIDENCE="$INTERCEPTOR_RESULTS/after" "$UNITY" -batchmode -nographics -projectPath "$VERIFY_PROJECT" -runTests -testPlatform EditMode -testResults "$INTERCEPTOR_RESULTS/EditMode.xml" -logFile "$INTERCEPTOR_RESULTS/EditMode.log"
```

Before the candidate commands, `BeforePlayMode` used `-testFilter 'ShiftFour.Tests.InterceptorMainLabPlayModeTests;ShiftFour.Tests.ControlPredictionMainLabPlayModeTests;ShiftFour.Tests.ControlTacticalNavigationPlayModeTests'` against original sources. `BeforeSound` used `-testFilter MainLab_SoundResponse_BeforeAfterEvidence` with `SHIFTFOUR_INTERCEPTOR_VARIANT=before` and output directory `before-sound`.

## Integrity and limits

`validated-source-sha256.json` fingerprints the actual final C# sources used in verification. `integrity.json` compares the active checkout with the captured source, distinguishes intended Interceptor/test additions, and verifies scenes, prefabs, models, materials, health/combat, other policies, graph, motor/body and sensing services. Any platform defaults serialized only in the disposable ProjectSettings copy are recorded separately and excluded from active changes. The index is compared with captured entries and never reset to overwrite concurrent work.

The implementation keeps the advanced predictor; it does not assert that every situation should produce an intercept. Conservative timing can reject an attractive but late junction. Confidence remains heuristic, avoidance can delay arrival, and current caps are refreshed per decision rather than assuming future acceleration to a higher cap. No capture-rate, general speedup, hardware-frame-rate or full human gameplay acceptance claim is made.

See [implementation and APIs](../../SENSOR_INTERCEPTOR.md) for the exact behavior, configuration, diagnostics and remaining limitations.
