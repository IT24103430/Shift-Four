# Task 11 — Tactical Flanker review evidence

**Pending owner approval.** This is a focused proposal rehearsed in a disposable Unity project. It is not applied to active Assets. Nimsara's ownership and the baseline/historical-implementation discrepancy are explained in [the handoff](../../TACTICAL_FLANKER_HANDOFF.md).

Unity: **6000.6.4f1**. Verification project: `/tmp/shiftfour-task11-kou_gxu0`. It starts from the active project with the completed Task 10 Interceptor, but without applying the pending Scout/Guard proposals. `before.json` records the starting HEAD/index/status and every Assets/Packages/ProjectSettings file hash. Concurrent user Git activity is recorded separately; this task never stages, commits, pushes or resets it.

## Final results

Final actual full-suite XML totals are recorded in `test-summary.json` and the per-case `TEST-MATRIX.csv`. All existing Interceptor tests are included, along with the existing movement, body clearance, gates, firing, death, perception, awareness, difficulty, sound producer, animation and A* tests.

New tests: **13 Edit Mode** and **11 Play Mode** cases.

| Requirement | Evidence |
| --- | --- |
| Both left/right valid | Pure policy candidates/owner formula; real visible MainLab pair with two body-clear A*/native routes |
| One side blocked | Deterministic one-side rejection; real solid collider invalidates commitment while the other physical route remains available |
| Both sides unavailable | Standoff pursuit then safe hold/Storage fallback; both actual side areas obstructed |
| Visible reversal | Genuine successive visual observations trigger replan; no hidden velocity samples |
| Sound without vision | Production ReportShot → hearing → awareness → policy, actual movement, no visual history/confirmation |
| Failure cooldown/side change | Invalid route, no physical progress and ineffective arrival; bounded ledger and eventual retry |
| Limited switching | Commitment plus score margin under repeated small score changes; physical route-failure cooldown |
| Chase speed/clearance | 150 HP, target 4.2 m/s, actual travel above 3.3 m/s, frame speed/cap-change bounds, full raised-body clearance |
| Gate/solid rejection | Locked Control/Exit gate routes unavailable; complete native path and swept-body checks |
| Evidence expiry/return | Six-second serialized visual memory expires; actual return to a reachable nearby Storage post at patrol target 2.8 m/s |
| Combat/death unchanged | Actual occlusion prevents sight refresh/damage; terminal health, motor, hearing and collider state plus existing full regressions |
| Hidden information | Poisoned hidden arguments cannot update observation position/time/velocity or decision; real hidden relocation and cover tests |

No original assertions are removed or relaxed. The candidate uses the existing resource's 3.5 m/s investigation target and shared smooth acceleration/animation. Difficulty “Very Hard” still needs owner gameplay review.

## Iterations retained honestly

- `EditMode.initial.xml/.log`: **292/293 passed**. The new ineffective-arrival case exposed rebuilding the left/right axis from the moved drone after failure, which could relabel a recently failed direction as a new side. The proposal now keeps its tactical reference stable across route failures. Existing 280 tests passed.
- `Flanker.initial.xml/.log`: **3/11 passed**. Seven failures came from an overly sparse scene-fixture search for both flanks; the single-side chase, gate and death cases already passed. The sound fixture also used an incorrect home-distance arrival bound while the drone was still returning via intermediate stops.
- `Flanker.second.xml/.log`: **9/11 passed** after a denser set of real, physically validated test placements. Remaining cases assumed five-second memory despite the unchanged prefab's serialized six seconds, and used a blocker that cut both approaches.
- `Flanker.third.xml/.log`: **10/11 passed** after honoring the serialized deadline and refining committed, cost-aware adjacent-post return and native-clear intermediate-stop skipping. The remaining fixture treated an endpoint obstruction as if it guaranteed the other physical route remained open.
- `Flanker.xml/.log`: **11/11 passed** after the one-blocked-side setup explicitly verifies that the other actual route stays available. The test still requires the opposite side; no fallback assertion replaces that requirement.
- `PlayMode.xml/.log` and `EditMode.xml/.log`: final complete regression runs, with the additional rendered tactical captures. Those totals are summarized above and in JSON/CSV.

The final sound scenario requires at least three active hearing decisions, actual travel, empty visual history/velocity, no combat confirmation, expiry, physical arrival within 1.2 m of the chosen post, and patrol speed. Merely selecting one goal or reaching a route connector cannot pass it.

## Visual scenarios and reproduction

See [VISUAL_SCENARIOS.md](VISUAL_SCENARIOS.md), the tactical CSV files and `MainLab-demo.csv`. Frames come from actual Unity test cameras and validated route overlays; PNGs are lossless conversions of retained raw TGA captures. Temporary test blockers/cameras/lines are never serialized to MainLab.

```sh
python3 docs/evidence/tactical-flanker/rehearse.py --demo-only
```

Omit `--demo-only` for the new Edit/Play cases. Use `--full` for complete suites or `--prepare-only` to open the proposed implementation in Unity. The script validates the active Flanker preimage, copies Assets/Packages/ProjectSettings and substitutes sources only in that new temporary project. It rejects test-path collisions. The preparation path was executed and its output compared with the actual verified sources.

Exact final full-run commands:

```sh
UNITY=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
FLANKER_REVIEW=/tmp/shiftfour-task11-kou_gxu0
FLANKER_RESULTS=/Users/dev/Desktop/game/Shift-Four/docs/evidence/tactical-flanker

SHIFTFOUR_FLANKER_EVIDENCE="$FLANKER_RESULTS" SHIFTFOUR_EVIDENCE="$FLANKER_REVIEW/RegressionEvidence" SHIFTFOUR_DEMO_OUTPUT="$FLANKER_REVIEW/RegressionFrames" "$UNITY" -batchmode -projectPath "$FLANKER_REVIEW" -runTests -testPlatform PlayMode -testResults "$FLANKER_RESULTS/PlayMode.xml" -logFile "$FLANKER_RESULTS/PlayMode.log"

"$UNITY" -batchmode -nographics -projectPath "$FLANKER_REVIEW" -runTests -testPlatform EditMode -testResults "$FLANKER_RESULTS/EditMode.xml" -logFile "$FLANKER_RESULTS/EditMode.log"
```

## Patch and integrity

- `TacticalFlanker.task11.patch`: one owned production file plus four new test/meta files; checked for applicability, not applied to active Assets.
- `FlankerBrain.cs.txt`, `FlankerBrain.cs.before.txt`, `harness/`: exact proposed source, preimage and new tests.
- `before.json`, `integrity.json`: original asset hashes, final active-source audit, and separate concurrent Git state.
- `validated-source-sha256.json`: actual verified production and test source hashes, checked against the proposal and prepared reproduction project.
- `test-summary.json`, `TEST-MATRIX.csv`: final actual per-suite/per-case results.
- `MainLab-demo.csv`, tactical CSVs, raw/PNG frames and XML/logs: actual behavior and retained iterations.

The proposal does not change Scout, Guard, Interceptor, shared core/motor/body, noise, health, player, gates, resources, prefabs, models, scenes or navigation assets. Any platform defaults serialized by Unity only in the disposable ProjectSettings copy are recorded separately and excluded from the review patch. See the handoff for conservative validation, current-layout connectors, bounded sound search and human balance limitations.
