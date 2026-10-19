# Task 13 — Four-room MainLab integration

The current shared systems are bound in the actual MainLab scene without changing a saved scene, prefab, model, material or navigation asset. The expanded Scout, Guard and Flanker policies remain review proposals. A passing shared integration check does not imply those proposals have been accepted.

## Ownership and scope

Ownership was checked against [TEAM_RESPONSIBILITIES.md](TEAM_RESPONSIBILITIES.md), the member handoffs and the accepted four-gate ADR. Pamudi owns MainLab/final environment integration and Checkpoint/Scout; Nimthara owns Server/Guard and consoles; Nimsara owns Storage/Flanker and combat; Asmadala owns Control/Interceptor and movement/animation properties. No new approval covering the pending policy proposals or Server sound anchor was found. No external messages were sent.

No saved-asset operation was necessary. This task did not run the legacy scene installers, `NimtharaAcceptance` bake/save operation, prefab replacement, NavMesh baking or scene generation. Existing structure, references and GUIDs were preserved.

## Resolved scene inventory

The read-only Unity audit resolves the prefab instances and checks every loaded component for missing scripts and broken non-null serialized references. See [saved-assets.txt](evidence/four-room-integration/saved-assets.txt) for every root instance override, and [saved-script-guid-inventory.json](evidence/four-room-integration/saved-script-guid-inventory.json) for source and package script GUID resolution.

| Room / agent | Existing agent prefab | Saved scene spawn | Runtime HP | Patrol / investigate / chase, m/s |
| --- | --- | --- | ---: | --- |
| Checkpoint / Scout | `Assets/Prefabs/Agents/Scout.prefab` | `(-8, 0, -10)` | 100 | 1.8 / 2.2 / 2.6 |
| Server / Guard | `Assets/Prefabs/Agents/Guard.prefab` | `(-7, 0, 13)` | 150 | 2.0 / 2.5 / 3.0 |
| Control / Interceptor | `Assets/Prefabs/Agents/Interceptor.prefab` | `(10, 0, 6)` | 125 | 2.5 / 3.1 / 3.7 |
| Storage / Flanker | `Assets/Prefabs/Agents/Flanker.prefab` | approximately `(10, .013, -5.6)` | 150 | 2.8 / 3.5 / 4.2 |

The saved agent prefabs retain legacy health 75 and omit an explicit difficulty override. These are supported inputs: `DroneBrain.Start` loads the matching `Resources/DroneDifficulty` default, initializes undamaged health once, and binds the motor. Runtime checks verify the actual resulting health/profile on three successive scene generations. Reapplication cannot heal damaged drones; the existing difficulty suite also covers early damage and per-instance overrides.

Each actual drone has one brain, motor, body and rotor visual. `Start` adds exactly one enabled `DroneHearingSensor` when absent. Saved assets therefore do not need a second sensor or explicit difficulty property. The runtime visual points to its own brain and motor; the motor's cached visual owner is that same component. Four rotor pivots exist once per model. The model pose stays separate from the navigation root.

Real gunshot dispatch drives Suspicious and Investigate, and real clear sight drives Combat. The real shared brain update selects the expected movement mode; the real motor ramps to each configured cap; the visual reads that awareness. The controlled integration fixture pauses autonomous root movement and manually ticks decisions/speed ramps to isolate bindings. It does not claim that every existing policy travels to a sound. Actual flight, turning, braking and body clearance are covered by the existing movement/animation suites.

## Navigation, progression and restart

The complete suites include both actual Control tactical arcs, six body-clear junctions, locked gates, native carving synchronization, all 400 gate/endpoint graph queries against independent Dijkstra, raised obstacle/player collision and recovery, and all four higher drone envelopes. Camera near-plane protection and authored visual height tests remain included.

`ScriptedMission_AllFourRealConsoles_Gates_Exit_WinRestart_LossRestart` finds a reachable interaction sightline for every real console, activates it, confirms its gate remains locked, defeats the corresponding drone through accepted health damage, then waits for physical and graph release. Exit stays locked until all four pairs are complete. Both victory and loss reload paths reset progress and wrecks. This is scripted positioning and damage calls, not a manual keyboard playthrough.

The added runtime binding test dirties hearing and destruction state, loses the game and invokes production `Restart` twice. It verifies fresh health and profiles, closed collider/carving barriers, no wrecks, no old hearing memory, exactly one bus subscriber per current sensor, no destroyed subscribers, and disabled old awareness objects. It never resets the sound bus on behalf of production cleanup.

## Validation and files

The focused run passed **9 Edit Mode + 2 Play Mode checks**, with no failures or skips: [Edit XML](evidence/four-room-integration/FocusedEditMode.xml), [Play XML](evidence/four-room-integration/FocusedPlayMode.xml). The complete unified run, including these checks, is reported in [Task 14's final validation](FINAL_PROGRESSIVE_DIFFICULTY_VALIDATION.md).

Added executable files:

- `Assets/Tests/EditMode/Movement/FourRoomIntegrationEditModeTests.cs` and `.meta`.
- `Assets/Tests/PlayMode/FourRoomIntegrationPlayModeTests.cs` and `.meta`.

Added documentation/evidence: this file and `docs/evidence/four-room-integration/`. No existing production source or saved gameplay asset was changed. No commit, staging, push or merge command was issued.

Reproduce in a disposable project:

```sh
python3 docs/evidence/four-room-integration/reproduce.py --focused
```

Omit `--focused` for the complete current suites; use `--demo-only` for mission, sensing and rendered presentation fixtures. `--prepare-only` prints a separate project to inspect in Unity.

## Outstanding owner integration

- **Pamudi:** [Task 8 Scout proposal](ENHANCED_SCOUT_HANDOFF.md). Existing hearing investigation remains active; enhanced bounded search/turning awaits review.
- **Nimthara:** [Task 9 Guard/console proposal](ADVANCED_GUARD_HANDOFF.md), including the concrete `Server.prefab` console sound offset `(0,0,-1.25)` metres. Its shared `DroneBrain` movement hook needs the named shared-file/movement reviewer as documented there. The active Guard has shared awareness but its legacy policy does not investigate sounds. Its authored post overlaps cover.
- **Nimsara:** [Task 11 Flanker proposal](TACTICAL_FLANKER_HANDOFF.md). Current left/right selection remains active; sound-led investigation, failure cooldowns, adaptive recovery and a reachable post fallback remain pending.

All three existing patches still pass a read-only `git apply --check`; [applicability evidence](evidence/four-room-integration/pending-patch-applicability.json). None was applied. No MainLab change is proposed. Task 14 subsequently confirmed the console-source attachment defect in all four sections and prepared a separate, tested [four-console anchor proposal](evidence/progressive-difficulty/CONSOLE_ANCHOR_REVIEW.md). Its 2.5 m offsets supersede the earlier 1.25 m draft if accepted; the duplicate console/Server hunks in Task 9 must then be reconciled. The console and all four section owners must review that saved-asset proposal.
