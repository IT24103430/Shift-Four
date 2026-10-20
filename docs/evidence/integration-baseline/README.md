# Task 1 baseline verification — PASS

Executed on **19 October 2026**, Unity **6000.6.4f1**, macOS Editor with **Metal / Apple M1**. These are new runs against the dedicated baseline worktree, not copied historical results.

| Suite | Passed | Failed | Skipped | Inconclusive | Test execution time | Evidence |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Edit Mode | 131 | 0 | 0 | 0 | 0.402 s | [XML](EditMode.xml), [complete log](EditMode.log) |
| Play Mode | 86 | 0 | 0 | 0 | 348.966 s | [XML](PlayMode.xml), [complete log](PlayMode.log) |
| Total | **217** | **0** | **0** | **0** | | [Every case and outcome](TEST-MATRIX.csv) |

Both Unity processes exited with code 0. Times above are NUnit execution durations and exclude Editor startup/import/compilation. Local test window: Edit Mode at 14:31:45; Play Mode from 14:32:15 to 14:38:04, Asia/Colombo. [Machine-readable summary](test-summary.json).

## Baseline identity

Committed foundation: `a523d7d953118cfbb154042b6304723b02ea0e39`. Branch: `feature/progressive-drone-intelligence`. Worktree: `/Users/dev/Desktop/game/Shift-Four-Progressive`.

The candidate includes the **existing uncommitted unified integration overlay**, imported byte-for-byte from `/Users/dev/Desktop/game/Shift-Four-Student4-Integration`. HEAD alone is insufficient to reconstruct it. The [32-file inventory](imported-source-files.txt), [hashes](imported-source-files.json), [source ZIP](imported-source.zip) and [tracked patch](imported-source.patch) preserve the overlay. [All 56 candidate C# source hashes](validated-candidate-source-sha256.json) identify the tested sources, including tests.

No new gameplay implementation or test changes were authored. No branch merge, reset, cherry-pick, commit or push was performed. The destination intentionally remains uncommitted; all other worktrees retain their prior states.

## What the run verifies

- Shared A* and reconstruction/no-path contracts, including the existing independent Dijkstra comparison across 400 gate/endpoint combinations.
- Native route following, arrival/braking, gate opening/replanning, raised obstacle clearance, player separation, and moving-player recovery.
- Accepted combat damage, solid-cover hit validation, camera clearance, four-drone movement/animation, physical falling/impact/settled wrecks and cleanup.
- Single predictive Interceptor policy, visual observation memory, reversal/stop/expiry recovery, hidden-information cases, Control junction validation and both staged arcs.
- Existing console/drone prerequisites, physical four-gate progression, scripted completion, win restart, loss and restart.

The exact test names and outcomes are in the matrix. Runtime fixture outputs are in [generated/](generated/), including [400 graph queries](generated/graph-400-queries.csv), [scripted mission](generated/scripted-mission.csv), [prediction trace](generated/prediction-trace.csv), and [perception-driven prediction trace](generated/rendered-prediction.csv). [Render captures](raw-captures.zip) are archived with a [SHA-256 manifest](raw-captures-sha256.json). Captures are test-generated, not a manual gameplay demonstration.

## Integrity and cleanup

- [Original worktree verification](original-worktrees-integrity.json): all HEADs, statuses and **1,641 files** match the [before inventory](original-worktrees-before.json): 524 animation, 332 Control tactical, 785 unified integration files.
- [Final baseline verification](baseline-integrity.json): **131 protected assets** match the initial snapshot; all **56 C# sources** and **32 imported files** match their hashes; exactly **one** `InterceptorPolicy` definition; no temporary Test Runner scenes remain.
- Scout, Guard, Flanker, Health, SoundEvents, player controls, console, gates, mission/session and WaypointGraph source match the committed foundation. No teammate-owned policy, MainLab, existing prefab/model/material, graph topology or baked navigation was edited.
- Unity automatically serialized platform defaults in `ProjectSettings/ProjectSettings.asset` during the run. The incidental output and [exact delta](unity-generated-settings.patch) were archived; only this worktree's generated settings delta was removed by restoring the original file after testing. No source or gameplay change was needed.
- `git diff --check` passed. [Destination status](destination-status.txt) records the expected uncommitted imported snapshot and documentation.

## Environment observations and limits

Initial cache-copy attempts stalled on macOS cloud placeholders and were stopped before any test run started. Cache preparation then used the available unified-worktree cache data. Three known empty module compilation trigger files were initially skipped; Edit Mode passed with missing immutable-package warnings. Their zero-byte files were restored **only in the destination's ignored Library**, and the Play Mode log confirms normal trigger imports. [Cache preparation record](cache-preparation.json). No package manifest/dependency changes were made.

Unity/Mono shutdown diagnostics and duplicate assembly-hint-path messages are retained in the complete logs. Neither suite reported a failed/skipped/inconclusive case. These results establish the executed Editor baseline; they do not prove future difficulty balance, manual keyboard/mouse playability, standalone compatibility, stable target FPS or zero allocations for the whole game. Existing profiling fixtures ran as part of the suite; no new performance claim is made.

The unmerged Guard alternative has incompatible base-brain APIs and script GUIDs and depends on `CoverPoint` without a tracked declaration on that ref. It was not imported. [Additional branch compatibility evidence](additional-branch-compatibility.txt). The inherited animation-branch MainLab/prefab differences from dev still need normal teammate review before any later merge.

## Reproduce in the dedicated worktree

Keep the tested snapshot unmodified and close any Editor using this destination before running. The following writes fresh results to ignored Logs rather than overwriting this evidence:

```sh
INTEGRATION_ROOT='/Users/dev/Desktop/game/Shift-Four-Progressive'
UNITY_EDITOR_PATH='/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity'
RUN_DIR="$INTEGRATION_ROOT/Logs/BaselineVerification"
mkdir -p "$RUN_DIR/generated"
for PLATFORM in EditMode PlayMode; do
  env SHIFTFOUR_EVIDENCE="$RUN_DIR/generated" "$UNITY_EDITOR_PATH" \
    -batchmode -projectPath "$INTEGRATION_ROOT" -runTests \
    -testPlatform "$PLATFORM" -testResults "$RUN_DIR/$PLATFORM.xml" \
    -logFile "$RUN_DIR/$PLATFORM.log" || break
done
```

The dependency report, confirmed feature gaps and Task 2 file boundaries are in [SAFE_INTEGRATION_BASELINE.md](../../SAFE_INTEGRATION_BASELINE.md). **Task 1 is complete; Task 2 implementation has not started.**
