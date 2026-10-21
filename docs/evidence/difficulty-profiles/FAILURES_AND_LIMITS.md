# Task 2 verification history and limits

All evidence in this directory was produced in the dedicated Progressive worktree. Existing Task 1 sources/tests were not edited to obtain passes.

## Corrected attempts

1. `FocusedPlayMode-attempt1.log`: the first new fixture attempted to unload the Test Runner's initial scene during cleanup. The run stalled without XML; the owned Unity process was stopped with SIGTERM (exit 143). No pass/fail case counts are inferred from that aborted run. Standalone health fixtures now use their own transient scene.
2. `FocusedPlayMode-attempt2.xml`: **1 passed / 5 failed**. The new fixture reused an already loaded `After difficulty tests` scene name, causing teardown and subsequent setup exceptions. Cleanup now reuses its empty scene and each fixture receives a unique transient name. No gameplay code or assertion was relaxed to fix this.
3. `FocusedPlayMode-attempt3.xml`: **5 passed / 1 failed**. The natural-recovery test assumed one uncapped Editor frame was enough to recover 1 HP. Actual health was 74.2226257 instead of 75. The test now waits up to one second for full recovery, as its other recovery check already did, while retaining the exact HP and initialization-lock assertions.
4. `InactiveRegression.xml`: **6 passed / 1 failed**. A newly added lifecycle regression exposed a real health issue: configuring and damaging an inactive object to 125 HP was overwritten to 150 by its first `Awake`. The production fix makes Awake preserve health already explicitly initialized or damaged. The same assertion then passed in the focused suite.

These artifacts remain available; final verification results are reported separately in README.md and are not an aggregate that hides the corrected attempts.

## Scope and limits

- Task 2 applies maximum health at fresh initialization/reset points. Patrol/investigation/chase speeds, acceleration, turn rate and sensing/awareness settings are authored and exposed for later consumers. Existing cruise speed and decision rules are intentionally not changed by this task.
- Scene reload is the supported complete game restart. The legacy `Health.Configure` remains an explicit new-life API, but resetting Health alone is not a full reset of a stopped motor, wreck, brain observations or mission counters. This task does not implement drone pooling/resurrection.
- If accepted damage occurs before profile binding, the API refuses to replace maximum/current health for that life. This deliberately preserves the accepted damage rather than retroactively increasing HP. Normal MainLab startup binds the four defaults before damage in the executed fixtures.
- Unity Editor tests include scripted actor placement, calls to accepted damage APIs, Inspector-field overrides and actual GameSession restart. They are not a manual keyboard/mouse playthrough, standalone-build result, balance study or whole-game performance claim.
- Existing serialized drone prefab HP remains 75. Runtime default lookup initializes 100/150/125/150 without prefab or scene edits. Custom/unrecognized brain types without a profile keep legacy behavior.
- No sound system, AI policy, movement-mode transition, sensing behavior, combat damage/cooldown, collision or animation implementation was changed.
