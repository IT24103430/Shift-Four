# Task 1 — safe integration baseline

Inspection date: 19 October 2026, Asia/Colombo. Scope is baseline integration and verification only; progressive difficulty implementation has not started.

## Selected source and reproducibility

- Committed foundation: `a523d7d953118cfbb154042b6304723b02ea0e39`, `feature/gv-asmadala-drone-animation`.
- Selected overlay: the existing, uncommitted source/test snapshot in `/Users/dev/Desktop/game/Shift-Four-Student4-Integration`, `feature/student4-unified-integration`, whose HEAD is also `a523d7d`.
- Dedicated destination: `/Users/dev/Desktop/game/Shift-Four-Progressive`, branch `feature/progressive-drone-intelligence`, created from `a523d7d`.
- The overlay is copied byte-for-byte; no new gameplay code or test edits are made for Task 1. The destination intentionally has uncommitted changes because committing was prohibited. HEAD alone does **not** reproduce the selected baseline: retain the overlay and its hashes.
- [Imported file inventory](evidence/integration-baseline/imported-source-files.txt), [SHA-256 hashes](evidence/integration-baseline/imported-source-files.json), and [tracked overlay patch](evidence/integration-baseline/imported-source.patch) identify all 32 imported source/test files, including metadata. Untracked file contents are present in the destination and the accompanying source archive; the tracked patch alone is insufficient.
- Original worktrees and their committed/uncommitted content remain in place. [Before inventory](evidence/integration-baseline/original-worktrees-before.json) records their statuses and file hashes. Final integrity verification is recorded alongside the test results.

## Branch dependencies and conflicts

| Input | HEAD | Relationship and decision |
| --- | --- | --- |
| `dev` / `origin/dev` | `271de5106fd3463529ac710de4478a1774155943` | Ancestor of the animation foundation. Retains shared A*, route following, four-gate progression and original models. Too old to use alone for the requested animation/collision/prediction baseline. |
| `feature/gv-asmadala-drone-animation` | `a523d7d953118cfbb154042b6304723b02ea0e39` | Five commits ahead of dev. Adds height/camera/collision protections, accepted combat events, advanced visuals, physical wrecks and predictive Interceptor work. Chosen committed foundation. |
| `feature/is-asmadala-control-tactical-navigation` worktree | `271de5106fd3463529ac710de4478a1774155943` | Its extra tactical/prediction code is uncommitted; branch ancestry alone conceals it. Has separate policy, six private junctions and Control-specific tests, but uses the older scene/collision assumptions. Preserved as an input, not merged over the newer implementation. |
| `feature/student4-unified-integration` worktree | `a523d7d953118cfbb154042b6304723b02ea0e39` | Newest compatible code snapshot. Already reconciles the two Interceptor layouts, raised-body validator, staged arcs, per-decision route caching, query buffers, wreck optimization and regression fixtures. Selected overlay. |
| Dedicated destination | `a523d7d953118cfbb154042b6304723b02ea0e39` | Exact source/test copy of the unified candidate over the animation assets; no branch merge, reset, cherry-pick, commit or push. |

Remote branch heads were checked read-only with `git ls-remote`. The named Control and unified integration branches have no matching remote heads; their local uncommitted contents are essential. [Git inspection](evidence/integration-baseline/git-inspection.txt) records local refs, remotes, worktree status, ancestry and the dev-to-animation asset/code delta.

Additional local refs were checked for conflicting alternatives. `origin/feature/guard-ai-server-room` (`d025dd7`) is unmerged: it changes the Guard and base-brain script GUIDs, replaces the latest combat-origin/accepted-shot APIs with an older renderer-material flash implementation, and requires a `CoverPoint` type with no tracked declaration on that ref. Importing it would break the selected animation bindings and needs a separate teammate handoff. It is **not** part of this baseline. The Flanker-scoring ref (`8ede59d`) is already an ancestor of dev. Route-following ref `b6e0bbf` adds one stronger native-partial-path test beyond the dev merge; it is documented as an available follow-up, not silently substituted into the exact unified snapshot.

| Potential conflict | Selected resolution already present in the copied snapshot |
| --- | --- |
| Animation embeds `InterceptorPolicy` in `InterceptorBrain.cs`; Control separates it. | Use the unified adapter and standalone `InterceptorPolicy.cs` together. Exactly one policy and one settings class remain. Never copy the separate policy alongside the old embedded definition. |
| Older Control validator derives a body shape independently. | Unified validator consumes the real `DroneBody.EnvelopeCenter` and `EnvelopeExtents`, adds the existing tactical margin, and rejects obstructed native route legs. |
| A visible player occupies a semantic engagement endpoint. | Adapter permits the semantic player route, then the shared motor chooses a collision-safe combat standoff. Hidden tactical destinations still require full body clearance. |
| Re-evaluating local connectors could reverse a committed arc. | Retain remaining arc stops and stage the next stop through `DroneMotor`; both left/right alternatives use shared A* and native NavMesh validation. |
| Repeated route/physics queries and buffer saturation. | Cache only within a synchronous decision; requery on the next decision or graph change. Full physics-query buffers use a complete-query fallback. |
| Test assumptions differ across scene revisions. | Import the unified snapshot's existing fixtures without further edits. Its gate test separately asserts graph/native connectivity and rejection of an occupied raised-body endpoint; allocation checks calibrate the GC.Alloc recorder. |

## Confirmed systems

| System | Inspection finding |
| --- | --- |
| A* | Existing `WaypointGraph.cs` retains g/h/f search, ordered parent reconstruction, explicit empty no-path result, native connector checks, four independent gate flags and graph version invalidation. No topology or source changes in Task 1. |
| Shared movement | `DroneMotor.cs` retains route following, pending/blocked/retry states, final braking, angular limits and combat standoff. `Stop()` remains terminal; no temporary-hold misuse is introduced. |
| Collision | `DroneBody.cs` retains raised envelope overlap/sweep clearance, synchronized physics, agent-next-position acceptance, local static avoidance and player/gate exclusion from detours. No serialized envelope changes. |
| Four individual policies | Scout patrol/shot/search, Guard defensive cover scoring, predictive Interceptor and Flanker left/right scoring remain separate. Scout/Guard/Flanker source is unchanged. |
| Prediction | One standalone `InterceptorPolicy` retains bounded visual history, smoothing, confidence decay, timing scores, commitment, reversal/stop invalidation, missed-window cooldown, last-known pursuit, local search and return-home fallback. |
| Control junctions | Six private code-defined junctions plus both staged arcs are present. They supplement rather than replace the shared graph. Existing Control art/cover is used; no scene regeneration or NavMesh rebake. |
| Animation | Existing `DroneRotorVisuals`, `DroneAnimationPose`, `DroneWeaponEffects` and `DroneWreckMotion` retain pose/rotor feedback, accepted-shot flash/tracer, nonfatal hit, electrical failure, falling/impact/settled wreck and lifecycle cleanup. |
| Damage and mission | Health accepted-damage/fatal events and four drone-plus-console gate prerequisites are retained. No player damage, fire interval or movement tuning changes. |

## Current test assemblies

Production scripts remain in predefined `Assembly-CSharp`; fixtures use reflection instead of adding a production asmdef.

| Assembly | Location | Dependencies |
| --- | --- | --- |
| `ShiftFour.Navigation.EditModeTests` | `Assets/Tests/EditMode/ShiftFour.Navigation.EditModeTests.asmdef` | NUnit, UnityEngine.TestRunner, UnityEditor.TestRunner; Editor only; UNITY_INCLUDE_TESTS. |
| `ShiftFour.Movement.EditModeTests` | `Assets/Tests/EditMode/Movement/ShiftFour.Movement.EditModeTests.asmdef` | Same Edit Mode dependencies; movement, camera, animation, prediction and tactical coverage. |
| `ShiftFour.Movement.PlayModeTests` | `Assets/Tests/PlayMode/ShiftFour.Movement.PlayModeTests.asmdef` | NUnit, UnityEngine.TestRunner and Unity.Collections; UNITY_INCLUDE_TESTS; scene/native navigation and integration coverage. |

Actual results and reproduction commands are in [the verification report](evidence/integration-baseline/README.md). Earlier worktree reports are historical context, not proof of this copied candidate.

## Confirmed implementation gaps for later tasks

1. No data-driven four-level difficulty profiles. All four authored drone prefabs currently specify **75 HP**, **3.5 m/s**, **8 m/s² acceleration**, **120°/s angular speed**, with auto braking enabled. MainLab's maximum-health scalar shown directly in scene YAML belongs to the player; drone health is inherited from prefab instances without a maximum-health override.
2. `Health.Configure` restores current health to maximum. Runtime difficulty application needs a deliberate initialization/non-healing contract before use during combat or restart.
3. `SoundEvents` only provides `ReportShot(Vector3)` / `ShotFired`. Scout alone listens, with a 22 m distance check and exact source memory; no reusable noise categories, uncertainty, wall attenuation or sealed-room isolation.
4. Player footsteps, sprint noise and console noise are absent. Player movement remains approximately 5 m/s, 25 damage and 0.24 s weapon interval; these values should be preserved.
5. Visual checks already enforce range, field of view and solid-cover/fire-time validation. There is no shared detection-confidence or suspicion model. Base decision arguments still include the player's live position when hidden; current policies use remembered evidence, but future shared sensing should narrow that interface.
6. Scout has local shot/last-sighting memories; Guard has cover scoring; Flanker has basic left/right reachability/travel scoring. There is no reusable explicit awareness state machine, noise-source investigation for every policy, or committed adaptive Flanker recovery model.
7. Motor uses the configured agent cruise speed, not patrol/investigation/chase profiles. Interceptor ETA is route length divided by that speed and does not account for acceleration.
8. Existing presentation reacts to real movement, firing, damage and death. Suspicion/search/combat warning lights and alert-driven rotor feedback are absent.
9. Passing baseline automation does not establish future progressive balance, human control feel, a manual full playthrough or standalone target-device performance.

## Task 2 file boundary

Task 2 should run in `/Users/dev/Desktop/game/Shift-Four-Progressive`, preserving the imported baseline and its tests. This list identifies a safe implementation surface; it does not perform or authorize a new feature task by itself.

| Surface | Safe next scope |
| --- | --- |
| Proposed new `Assets/Scripts/AI/Difficulty/DroneDifficultyProfile.cs`, `DroneDifficultySettings.cs`, and their `.meta` files | Isolated serializable/profile definitions and validation; do not overwrite existing assets or regenerate GUIDs. These files do not yet exist. |
| New difficulty configuration assets under a new directory | New profiles only; connecting existing prefabs/scenes remains a separate approval boundary. |
| New focused tests under existing Edit/Play test assemblies | Profile values, validation and initialization contracts. Preserve all existing assertions. |
| `Assets/Scripts/AI/Movement/DroneMotor.cs` | Owned movement integration point for eventual smooth speed targets, preserving A*, collision, braking and terminal Stop semantics. |
| `Assets/Scripts/AI/Agents/InterceptorBrain.cs`, `InterceptorPolicy.cs`, `ControlTacticalNavigation.cs` | Owned prediction/tactical integration points if needed in a later task. Preserve the single-policy definition and visual-only observation history. |
| Animation source files | Owned integration points for future movement/alert presentation. Fatal presentation retains priority. No model/material/hierarchy edits. |
| `docs/` and new evidence paths | Record design, tuning, actual results and exact source manifests. |

Changes to shared `DroneBrain.cs`, `SoundEvents.cs` or `WaypointGraph.cs` need an explicitly scoped shared-file owner/review plan. `Health.cs`, `PlayerController.cs`, `ConsoleSwitch.cs`, `ScoutBrain.cs`, `GuardBrain.cs` and `FlankerBrain.cs` are teammate-owned; Task 1 does not change them. Earlier broad-task approval must not be inferred as a request to edit them in this baseline-only task. MainLab, existing prefabs, models, materials, baked navigation, gate/progression behavior and player tuning are protected.

The animation foundation already differs from dev in MainLab and four agent prefabs. Those inherited height/collision/visual changes remain subject to normal teammate review before any future merge; Task 1 introduces no additional shared-asset delta.
