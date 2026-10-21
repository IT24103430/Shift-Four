# Task 15 — Latest Shift-Four baseline

Verified **20 October 2026, Asia/Colombo**, using **Unity 6000.6.4f1**. The complete existing suites passed **465/465 tests**. A separate, clean feature worktree is ready for the next phase. **Task 16 has not started.** This is a verified development baseline with documented gaps, not final gameplay/fairness acceptance.

## Exact baseline and isolated workspace

| Item | Verified value |
| --- | --- |
| Baseline commit | `5a5cf7ad9c389576db0f2de1407c936289bc32ab` |
| Selected source branch | `feature/progressive-drone-intelligence-workspace` |
| Original checkout | `/Users/dev/Desktop/game/Shift-Four` |
| New local feature branch | `feature/shared-security-expansion-baseline` |
| New clean worktree | `/Users/dev/Desktop/game/Shift-Four-Security-Baseline` |
| New worktree HEAD | The same exact baseline commit above; no overlay required. |
| Engine | `6000.6.4f1`, revision `12bfff696524` |

The original checkout was clean when inspection began. Baseline documentation/evidence is delivered in that original checkout; the new worktree remains clean. Unity tests ran in a disposable byte-matching copy of its Assets/Packages/ProjectSettings so imports and generated platform defaults could not dirty the new branch.

No existing branch was switched, reset, merged or overwritten. No changes were stashed/discarded. No commit, staging, push, merge, scene regeneration, prefab replacement or navigation bake command was issued. Existing stash `ef493c5be51f3583ad3ca5de600462e1d431629f` remains untouched.

[Machine-readable baseline](evidence/task15-baseline/baseline.json), [file fingerprints](evidence/task15-baseline/baseline-file-sha256.json), [final integrity](evidence/task15-baseline/integrity.json).

## Newer local work and branch review

The selected branch is the newest locally available committed workspace. Its locally cached origin ref also points to `5a5cf7a`; no fetch was performed, so this is not a claim about uncached remote updates. Relative to the previously inspected `6d0a28e`, commit `5a5cf7a` adds Task 14's validation test/meta and documentation/evidence. It changes no existing production script, profile, scene, prefab or project setting. Its 86 C# sources match the previous Task 14 verification fingerprints.

| Original worktree / branch | HEAD | Relationship / decision |
| --- | --- | --- |
| Main progressive workspace | `5a5cf7a` | Selected committed source; initially clean. |
| Control-Tactical / `feature/is-asmadala-control-tactical-navigation` | `271de51` | 18 selected-branch commits ahead of this older HEAD; 34 dirty/untracked files preserved. Of 15 changed asset files, 10 match the selected baseline and 5 differ. No overlay imported. |
| Progressive / `feature/progressive-drone-intelligence` | `a523d7d` | 13 selected-branch commits ahead of this HEAD; 345 dirty/untracked files preserved. Of 98 changed asset/config files, 94 already match. Different files are InterceptorBrain, InterceptorPolicy, DroneRotorVisuals and ProjectSettings. The selected source includes later sound/motion/awareness integration. |
| Student4-Integration / `feature/student4-unified-integration` | `a523d7d` | 13 selected-branch commits ahead of this HEAD; 271 dirty/untracked files preserved. Of 32 changed asset files, 25 match and 7 differ. Its existing source/test snapshot remains available. |

Uncommitted work has no Git commit timestamp establishing that it is newer. File/content comparisons, not branch names or modification times, informed the selection; nonmatching overlays remain available for owner review.

The local route-following branch contains one unique older commit, **`b6e0bbf`**, strengthening `DroneMotorPlayModeTests.PartialPath_IsBlockedAndCannotBecomeArrivalAcrossGap`. It explicitly observes native PathPartial before allowing the motor to block. This is a useful test follow-up, not part of the selected exact baseline, and was not cherry-picked or merged. Other locally cached alternative policy branches were inventoried and not imported.

[All refs and unique commits](evidence/task15-baseline/refs-before.json), [local-work comparison](evidence/task15-baseline/local-work-review.json), [original dirty-file hashes/statuses](evidence/task15-baseline/original-worktrees-before.json).

The older [Task 1 baseline](SAFE_INTEGRATION_BASELINE.md) remains historical evidence. Its statements that difficulty/hearing/awareness were absent do not describe this new baseline. Likewise, historical hinged-door/develop instructions do not override the accepted four-gate contract or this task's explicit latest-workspace selection.

## Existing features verified

| System | Verified existing behavior |
| --- | --- |
| Difficulty and health | Matching Resources profile initializes fresh health once; reapplication cannot heal or revive. Instance override and early-damage contracts remain tested. |
| Hearing / sound | Typed Footstep, Sprint, Gunshot, ConsoleInteraction and Impact channel; bounded uncertain memories, attenuation/occlusion, native/graph connectivity, gates, duplicate suppression, budgets and cleanup. Player walking/gunshot producers are active; no player sprint input exists. |
| Awareness | Patrol, Suspicious, Investigate, Search, Combat, ReturnToPost; credible evidence, expiry, last genuinely visible position and fatal disable. Sound cannot authorize a shot or refresh visual prediction history. |
| Movement | Awareness-driven patrol/investigation/chase caps, smooth acceleration/braking, real accepted velocity, angular limits, pending/blocked/retry behavior and terminal Stop. Existing A* and native routes remain authoritative. |
| DroneBody | Raised overlap/sweep envelope, physics synchronization, safe native next-position acceptance, static detours and actor/gate clearance. Saturated query buffers have complete-query fallback. |
| Weapon effects / animation | Accepted-shot flash/tracer, hit feedback, hover/rotors/turning/braking, awareness cues, original colours/property blocks and fatal priority. No competing pose owner was introduced. |
| Falling wrecks | Gradual rotor failure, gravity-driven falling, impact/settling, supported sleeping-body handling, terminal rotors and restart removal across all four models. |
| Control tactics | Six private junctions and both staged arcs; actual body envelope, native/gate-aware validation and decision-scoped cache invalidation. |
| Interceptor | Existing advanced predictor preserved: bounded visible history, separate sound action, velocity/confidence, speed/acceleration-aware ETA, route commitment/hysteresis, reversal/missed-candidate cooldown, search and return fallback. |
| Scene/prefabs | Read-only MainLab/section/agent audit resolves overrides and references; one brain/motor/body/runtime hearing sensor/visual owner per drone; no missing scripts or invalid required bindings in the tested scene. |
| Gates / mission | Four independent drone-and-console prerequisites, physical and carving release after bolt/panel animation, exit lock, accepted death objectives and idempotent progress. |
| Restart | Actual win/loss reloads restore profiles/health/progress, remove wreck/effects, clear old hearing memories/subscriptions and retire old awareness instances. |

| Room / drone | HP | Patrol / investigate / chase, m/s |
| --- | ---: | --- |
| Checkpoint / Scout | 100 | 1.8 / 2.2 / 2.6 |
| Server / Guard | 150 | 2.0 / 2.5 / 3.0 |
| Control / Interceptor | 125 | 2.5 / 3.1 / 3.7 |
| Storage / Flanker | 150 | 2.8 / 3.5 / 4.2 |

All four profiles retain acceleration **8 m/s²** and angular cap **120°/s**. Each agent prefab still serializes legacy **75 HP / 3.5 m/s**, superseded during normal startup by its matching profile. This supported fallback was verified; the prefabs were not resaved to change those legacy values. [Prefab/source inventory](evidence/task15-baseline/drone-prefabs-and-profiles.json), [resolved scene overrides](evidence/task15-baseline/generated/saved-assets.txt), [runtime bindings across restarts](evidence/task15-baseline/generated/runtime-bindings.csv).

## Inspected gate, session and brain contracts

- `LabDoor.cs`: stationary Rigidbody/BoxCollider/NavMeshObstacle root; bolts retract then panels slide; collider/carving stay enabled until animation completion, then `SetGateOpen` publishes graph version change. `Interact` does not unlock a gate; doors currently do not close.
- `MissionProgress.cs`: four fixed identities; each gate requires its own console **AND** defeated drone. Duplicates are idempotent; invalid identities fail closed.
- `GameSession.cs`: maps Scout/Guard/Interceptor/Flanker types to objectives 0–3, requires all four pairs for exit, subscribes/unsubscribes player death and reloads the scene for restart.
- `DroneBrain.cs`: one-time profile/health setup, shared hearing and awareness, genuine visual memory, policy-specific decisions and configured movement urgency. Fire still checks life/session/cooldown/range/vision plus a second obstruction ray. Death disables sensing/awareness, stops movement and reports the concrete drone objective once.

Current enemy shots remain **10 damage, 11 m range, 1.1 s cooldown**. Player shots remain **25 damage / .24 s**. `Health.Configure` is a new-life/reset API; it must not be used to refresh live difficulty because it restores health.

## Fresh complete Unity test results

| Suite | Pass | Fail | Skip | Runner duration |
| --- | ---: | ---: | ---: | ---: |
| Edit Mode | 306 | 0 | 0 | 1.898 s |
| Play Mode | 159 | 0 | 0 | 519.096 s |
| **Total** | **465** | **0** | **0** | |

These are fresh Task 15 runs against the exact selected source, not reused Task 14 results. No test was added, edited or weakened. Play Mode ran with graphics enabled so rendered fixtures were included. Automated scripted scenarios substitute for manual input where documented by their fixtures; no manual complete-game fairness claim is made.

[Edit XML](evidence/task15-baseline/EditMode.xml), [Play XML](evidence/task15-baseline/PlayMode.xml), [summary](evidence/task15-baseline/test-summary.json), [all case results](evidence/task15-baseline/TEST-MATRIX.csv), [exact commands](evidence/task15-baseline/commands.json).

## Ownership and required reviews

| Boundary | Owner / review requirement |
| --- | --- |
| MainLab, environment, baked navigation and waypoint placement | Pamudi coordinates changes and final scene integration. No regeneration. |
| Physical gates / collision / player / health and damage | Nimsara. Gate art/model hierarchy additionally involves Nimthara; graph release involves Asmadala. |
| MissionProgress, GameSession, consoles, exit/HUD | Nimthara's mission integration; physical/progression changes need the gate/graph owners too. |
| Checkpoint section and Scout policy | Pamudi. |
| Server section and Guard policy / original models | Nimthara. |
| Control section and Interceptor / A*, motor, movement and animation | Asmadala; graph placement remains coordinated with Pamudi. |
| Storage section and Flanker policy | Nimsara. |
| Four existing agent prefabs | Section/policy owner for behavior; Asmadala for movement/visual properties; Nimthara for authored model alignment; Nimsara for collision/combat properties. |
| Shared DroneBrain / perception / new camera evidence | Name one shared-file owner/reviewer before integration; shared work must preserve individual policy ownership. |

See [team responsibilities](TEAM_RESPONSIBILITIES.md) and the accepted [four-gate ADR](decisions/ADR-FOUR-GATE-CONTRACT.md). Creating this baseline is authorized by Task 15; it does not approve future owned-file or saved-asset changes.

## Integration risks before the next features

1. **Known gaps remain:** the fresh audit again finds invalid console sound-source attachments in all four rooms, and non-clear authored Guard/Flanker Home centres. Existing passing tests do not establish advanced Guard/Flanker recovery or human fairness. [Current audit](evidence/task15-baseline/generated/console-sound-anchors.csv), [post clearance](evidence/task15-baseline/generated/authored-post-clearance.csv).
2. **Pending policy proposals are not active:** Task 8 Scout, Task 9 Guard and Task 11 Flanker still need their owners. The separately tested [console-anchor proposal](evidence/progressive-difficulty/CONSOLE_ANCHOR_REVIEW.md) also remains unapplied and overlaps Task 9's producer/Server hunks. Reconcile those drafts before editing the same surfaces.
3. **Doorway ambush semantics:** a living drone cannot wait for its own exit gate to open because that gate requires its death. A next-room defender may ambush at the preceding gate; a different arrangement needs explicit mission design approval. Preserve all existing prerequisites and physical release timing.
4. **Camera authority:** external camera evidence must not silently become the drone's own visual observation or bypass firing checks. Define witness/freshness/uncertainty and teardown semantics; preserve bounded Interceptor history and normal occlusion.
5. **Height alignment:** the NavMesh root is planar and the four actual body envelopes differ. Any altitude change must align model pose, collider/envelope, muzzle/vision origin and tactical clearance, preserve roofs/camera clearance, and release control to fatal wreck physics. Avoid a second writer on the same Transform.
6. **Damage compatibility:** introduce backward-compatible profile defaults and preserve accepted-damage/fatal event order, non-healing initialization, mission identity and weapon feedback. Do not change player tuning or health reset rules incidentally.
7. **Historical branch/doc assumptions:** older overlays, cached remote policy alternatives and hinged-door plans are not approved replacements for this source. The stronger unmerged partial-path test is a distinct review item.

## Proposed Tasks 16–22 files and next step

The [proposed seven-task file/review matrix](evidence/task15-baseline/PLANNED_TASKS_16_22.md) covers camera sensing, camera evidence, ambush planning, gate/mission handoff, damage profiles, flying-height integration and final verification. Detailed future briefs were not provided, so that numbering is provisional. Existing navigation/perception systems remain the integration foundation.

Continue only when Task 16 is actually requested, using the clean worktree above. No security-camera, doorway-ambush, damage-progression or flying-height feature code was created by Task 15.

Reproduce this exact commit independently of later branch edits:

```sh
python3 docs/evidence/task15-baseline/reproduce.py
```

`--prepare-only` extracts the committed Assets/Packages/ProjectSettings into a disposable directory. Its archive extraction was checked against all **345 baseline files**, with zero mismatches. The full command runs both existing suites there. New imports can take several minutes. [Reproduction extraction check](evidence/task15-baseline/reproduce-prepare-check.json).
