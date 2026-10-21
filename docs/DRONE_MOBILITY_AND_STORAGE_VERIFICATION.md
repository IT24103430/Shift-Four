# Compact, faster drone fleet and Storage integration

Baseline: `87d580376aacb0d6dd58f1f293b8fe1c35d8e40c` (verified origin/dev). Unity 6000.6.4f1. Original Desktop checkout and its local dev commit/recovery files were preserved. Implementation folder: `/Users/dev/Developer/ShiftFour-workspaces/drone-mobility-87d5803`.

## Configuration

All four visual roots change from 0.90 to 0.65: **27.8% smaller**. Drone roots remain unit scale. Mesh colliders retain their original meshes and inherit the visual scale. The owned installer remeasures vertex bounds, complete rotor sweep, raised-body envelope, capsule and native avoidance radius. It does not bake navigation or rebuild the level. A repeat application produced byte-identical four prefabs and MainLab before the separate approved Storage edit.

| Drone | Patrol m/s | Investigate m/s | Chase m/s | Body/avoidance radius before → after |
| --- | ---: | ---: | ---: | --- |
| Scout | 2.2 | 2.7 | 3.2 | 1.077 → 0.792 m |
| Guard | 2.4 | 3.0 | 3.6 | 1.423 → 1.041 m |
| Interceptor | 3.0 | 3.75 | 4.5 | 1.074 → 0.789 m |
| Flanker | 3.4 | 4.2 | 5.0 | 1.194 → 0.877 m |

Acceleration increases from 8 to 12 m/s²; angular speed from 120 to 180 degrees/s. Saved difficulty profiles remain authoritative at runtime. Health, attack damage/cooldown, perception thresholds, player walk speed (5 m/s), jumping and all AI policy source are unchanged. Movement continues using the existing frame-independent speed controller, body sweeps and local static-obstacle steering.

## Responsibility branches

These are **stacked feature branches**, in the order below, with each member's responsibility-specific commit at its branch tip. Use the integration branch for the complete fleet; do not merge the stack out of order. No merge to dev or main was performed.

| Branch | Responsibility | Git author |
| --- | --- | --- |
| `feature/shared-asmadala-compact-drone-clearance` | Shared agent presentation, measured physical bounds, acceleration and turning | IT24103430 |
| `feature/gv-pamudi-scout-mobility` | Checkpoint / Scout speed profile | pamudiab |
| `feature/gv-nimthara-guard-mobility` | Server / Guard speed profile | IT24610327 |
| `feature/gv-asmadala-interceptor-mobility` | Control / Interceptor speed profile and explicit tuning assertions | IT24103430 |
| `feature/gv-nimsara-flanker-mobility` | Storage / Flanker speed profile and speed regression expectations | Nimsara-Jayarathna |
| `feature/gv-nimsara-storage-cover-balance` | Approved Storage central-cover removal | Nimsara-Jayarathna |
| `integration/compact-faster-drone-fleet` | Complete reviewed implementation and evidence | Member-authored commits retained |

GitHub authentication remains the connected IT24103430 account. Commit authors use the four names/emails explicitly supplied by the user; authentication was not switched to impersonate another account.

Separate branches preserve the read-only Interceptor diagnosis (`feature/is-asmadala-interceptor-diagnosis`) and the **unaccepted staging-only** arms weight candidate (`feature/gv-nimthara-arms-weight-candidate`). Neither is included in this integration branch. The candidate still fails full visual acceptance and does not replace active arms assets.

## Actual verification

Automated Unity tests in disposable project copies, not human playtesting:

| Run | Pass | Fail | Skip / inconclusive |
| --- | ---: | ---: | ---: |
| Final project Edit Mode assemblies | 401 | 0 | 0 |
| Final focused Play Mode: movement speeds, Interceptor sensors, unified regression, Storage | 31 | 0 | 0 |
| Complete Play Mode assembly | 273 | 1 | 1 |
| Final Storage visual-cleanup regression | 3 | 0 | 0 |
| Hearing fixture isolated recheck | 14 | 0 | 0 |

Focused tests include actual Flanker motor travel to both side approaches with raised-body occupancy checks every frame, complete console/exit native paths, all four gate references, speed bounds at 30/60/120 Hz, braking, query-buffer overflow, combat/session restart and Interceptor sensing. Four new Edit Mode cases validate real prefab mesh hitboxes and full rotor clearance against vertex geometry.

Initial development runs are retained: 353 pass / 8 fail in the movement Edit assembly, and 265 pass / 6 fail / 1 skip in Play Mode. These exposed explicit expectations for the old speed values and old body size; expected tuning was updated without disabling tests. The overflow test now checks actual obstacle separation against measured envelope extents and verifies the swept position is occupiable. No production AI correction was used to force prediction choices.

An unfiltered Edit run also discovered Unity Test Framework tutorial/sample assemblies: aggregate 419 pass / 3 fail / 5 inconclusive across 427 cases. All 401 project cases passed in that run. The final command explicitly selects both project-owned Edit assemblies, excluding unrelated package tutorial samples. Raw XML is preserved.

The full suite has one unresolved failure: `DroneHearingPlayModeTests.Wall_OccludesQuietSoundsAndAttenuatesLoudReachableSounds`, line 149. Its expected loud sound observation returned false in a synthetic fixture at (1000,0,1000). This fixture does not load MainLab or use the resized prefabs/difficulty profiles; its test, hearing sensor/model and waypoint implementation remain unchanged. It passed the earlier exact-baseline full suite and the first compact-drone full run. Causality and historical introduction are not established; this is an unclassified intermittent/suite-context failure, not a claimed pre-existing failure or a claimed fix.

Rechecking the entire hearing fixture alone passed **14 / 14** tests with no source changes. That does not erase the failed full-suite result. The smallest follow-up is to reproduce the fixture's suite arrangement, synthetic native paths and observation/localisation timing before considering a code change; no hearing production code or test was changed here.

The full assembly ran after the solid Storage prop removal. A subsequent visual-only cleanup removed that same prop's five decorative pieces and strengthened the Storage assertion; all 401 Edit cases and all 3 Storage cases were rerun on the exact final scene/test bytes. Physical colliders, scripts and difficulty profiles are identical to the full run. No second complete assembly run is claimed after this visual-only cleanup.

| Acceptance area | Status | Evidence |
| --- | --- | --- |
| Compact visible meshes, matching hitboxes and complete rotor sweep | PASS | Four real-prefab geometry cases and measured bounds |
| Faster movement, braking and turning at 30/60/120 Hz | PASS | Existing movement regression cases; peak approximately 5.002 m/s |
| Approved Storage prop and its trim removed; two defensive props remain | PASS | Final three-case Storage regression and inspection frames |
| Console, exit and both Flanker approaches | PASS | Complete native paths and actual left/right motor travel |
| Entire Play Mode assembly | PARTIAL | 273 pass / 1 unresolved hearing fail / 1 graphical skip |
| Newly cleared footprint usable by NavMesh | PARTIAL | Saved conservative bake retained; optional approved rebake still needed |
| Human balance/playtesting | PARTIAL | Not performed |

The skipped graphical Game-view resolution test requires a graphical Editor. This work does not claim human playtesting or a manual balance sign-off.

## Evidence and preservation

[evidence/drone-mobility](evidence/drone-mobility/) contains actual XML, exact test commands, vertex-derived measurements, repeat-migration hash checks, original/current input SHA-256 values, before/after Unity inspection images and navigation-debug captures. Images are scripted Unity camera renders at fixed inspection/player-level viewpoints, not hand-created illustrations or human gameplay captures.

Only nine existing MainLab prefab-instance blocks change: eight drone/model overrides and the Storage removal override. Scene object IDs, architecture, mission/player/lighting components and all other scene blocks are preserved. Original model FBX/Blender assets, GUIDs, Storage source prefab and saved NavMesh bytes are unchanged. See preservation.json and input-sha256.json for verification.

The saved NavMesh retains the old removed-cover footprint exclusion. Required console, exit and left/right routes remain complete; no rebake was needed to validate those routes. Using the newly cleared footprint itself for drone traversal would need a separately approved navigation update. Smaller collision bounds improve clearance but do not guarantee that every arbitrarily chosen AI destination is feasible.

See [Storage layout details](STORAGE_COVER_REBALANCING.md) for the exact approved object and its associated trim. Manual difficulty/visual acceptance and longer play sessions remain useful follow-up work.
