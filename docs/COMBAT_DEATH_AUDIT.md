# Combat death audit — Unity 6000.6.4f1

Reviewed `feature/player-camera-drone-separation`, starting at `01693a7`, with an initially clean working tree. Runs began on 18 October UTC / late 18 October Colombo and continued on 19 October Colombo. MainLab was loaded for tests, never regenerated or saved. No commit, push or merge was performed.

## Conclusion

`SYSTEM FAILURE` is the intended player-death overlay, not a Unity exception handler. `Health` reaches zero, raises `Died` synchronously, and `GameSession.OnPlayerDied` calls `Finish`. That sets `Ended`, releases the cursor and stops player input and further drone decisions/shots. It does not pause `Time.timeScale` or freeze the Unity player loop. Previously the generic wording and absence of hit feedback made combat death easy to mistake for a software failure.

Sustained valid combat reproduced death and restart. No illegal damage through cover, beyond range, from dead attackers, or during a shooter's cooldown was reproduced. Two movement defects were independently reproduced and fixed: the unsafe exhausted-standoff fallback, and reuse of an approach point after cover occupied it. Neither was shown to bypass damage validation or directly subtract health. The player's particular intermittent encounter cannot be attributed retrospectively without its damage trace.

## Complete health-loss trace

| Entry point | Runtime callers and effect |
|---|---|
| `DroneBrain.TryShoot` | Only gameplay caller that damages the player. All four policies share it. One accepted shot subtracts 10 HP. Now records the attacking brain and `Drone shot: <agent>` reason. |
| `PlayerController.Shoot` | Hits the first non-trigger collider within 30 m. Damages the owning Health by 25, explicitly excluding its own Health. It cannot shoot the player itself. Now records player attribution. |
| `Health.TakeDamage(float)` / `IDamageable` | Public scripted API, used by existing tests to force deaths. No other runtime script calls it. Preserved for compatibility; now records `Unattributed scripted damage`. Future direct callers must supply attribution through `ApplyDamage` or investigate this reason. |
| Initialization/configuration | Awake initializes current HP from maximum. Configure initializes/reset HP; it is not a combat damage source. There are no runtime configure calls on an already playing player. |

Search included runtime and editor scripts, scene and prefab configuration. No collision, fall, proximity, gate, timer, visual animation or environment component subtracts health. No inspector UnityEvent health-damage hooks were found. The four policy scripts do not apply damage directly.

MainLab serializes player maximum 100 HP, regeneration enabled, recovery delay 3 s, recovery rate 8 HP/s. All drones serialize 1.1 s attack intervals and 11 m range. Decisions occur every 0.3 s, so native Update firing generally lands at approximately 1.2 s intervals rather than exactly 1.1 s. Ten uninterrupted hits kill; hits arriving before the recovery delay keep postponing regeneration. Each drone has its own cooldown, so multiple attackers legitimately stack damage. The isolated four-attacker fixture reaches death after ten hits in about 2.2 s when it invokes the actual attack method every frame; it does not use the 0.3 s policy cadence. This is not a claim that four drones naturally attack together through MainLab's closed gates.

## Accepted-shot contract

The shared attack path checks, immediately before damage:

- Cached attacker Health exists and is alive, player exists and player Health is alive; session has not ended.
- Current time has reached this attacker's `nextShot`. It advances to current time + 1.1 s; no catch-up loop can emit a backlog of shots after a slow frame.
- Combat-origin-to-eye distance is at most 11 m; observation additionally requires vision distance and an 85-degree forward angle. Update also checks root-to-root distance, a potentially stricter filter, not an extra way to hit outside range.
- An origin overlap probe rejects embedded muzzle positions. Non-trigger raycasts ignore the shooter's own descendants, select the nearest remaining hit, and require that hit to belong to this player.
- A fresh final ray confirms cover at firing time. Solid walls, shelves, gates, pillars and other intervening colliders block the shot.

The muzzle is 1.8 m above the drone root in all four current prefabs, overriding the script's 1.25 m default. The eye is approximately 1.3 m above the player capsule bottom. The ray is sloped between those points; shallow cover may be insufficient even if it hides the lower player body. No damage, range, cooldown, recovery, FOV or weapon constants were changed.

Physics automatic transform synchronization is disabled in project settings. Static MainLab cover and normal controller movement passed the tests; external scripts that teleport cover and query immediately must synchronize physics first. The tests explicitly synchronize their teleports. The audit does not establish correctness for arbitrary third-party transform writes between queries.

## Movement findings and fixes

`GoToCombat` preserves Guard cover, Flanker lateral destinations and Interceptor junction choices outside the standoff ring. Direct approaches evaluate five ring points with NavMesh sampling, graph reachability and raised-body occupancy checks.

Previously, rejecting all five points executed `GoTo(destination)`, which could be the exact player root. The physical body still swept against colliders, but navigation no longer honored the standoff intent and could keep pressing against the player/obstacles. The regression proves the original code requests a route in this situation. The fix holds position, clears the obsolete route without terminal `Stop` or disabling the motor, and retries selection on the next policy tick. Removing the blockers demonstrates recovery and arrival at standoff.

Previously, a cached approach was reconsidered only after player movement over 0.5 m or a graph-version change. Moving cover into the point left that goal selected. The second regression proves the original behavior; the fix rechecks occupancy. Physically blocked candidates are rejected before the more expensive graph query. Policy scoring, A* search, gate graph rules, ordinary route following and retry rules remain unchanged.

`DroneBody` continues to sweep a conservative axis-aligned envelope around the raised model, synchronize accepted movement back to the agent, and validate local detours against physics and NavMesh edges. It does not inflict collision damage. The envelope includes yaw, rotor reach and hover travel; ground navigation alone is not its collision authority.

New checks verify actual mesh vertices at 15-degree yaw steps and both ±0.08 m hover extremes, and player controller movement into the four models from four directions at 45-degree yaw steps. The latter permits the controller's existing skin width but rejects penetration of its inner capsule by more than 0.01 m. Existing tests cover drone movement toward the player, overhead cover missing from ground navigation, temporary physical holds and resumed movement, plus eight seconds of MainLab movement with clear body envelopes. These sampled checks passed; they are not a guarantee for arbitrary teleports, newly authored models/cover or every possible player trajectory. No raised-model clipping was reproduced in the tested cases.

## Diagnostics and HUD

On the player's Health component, enable **Log Damage Diagnostics** in the Inspector during Play Mode. It defaults off and only emits when `Debug.isDebugBuild` is true (Editor or Development Build). No scene/prefab setting was changed. An accepted-hit line contains target and attacker names/entity IDs, damage, HP before/after, game time, frame, reason and fatal reason. Rejected attempts do not log. `LastDamage` retains the latest accepted structured record independently of logging. The legacy damage API is marked unattributed rather than assigning an invented attacker.

Example shape:

```text
[Damage] target=Player#… attacker=Scout#… damage=10 HP=10->0 time=… frame=… reason=Drone shot: Scout death=Drone shot: Scout
```

GameSession now explicitly says **SYSTEM FAILURE — PLAYER ELIMINATED**, identifies the final drone when available, and displays **Press R to restart**. Further recommended HUD work: a brief hit tint/direction indicator, clearly visible health bar and low-health state, a recent `-10 HP / attacker` cue, and a recovery-delay indicator. Those would explain damage without reducing it or disabling enemies. They are recommendations, not additional assets or UI systems introduced here.

## Performance

Six low-overhead ProfilerMarker scopes were added: `ShiftFour.FindRoute`, `CanConnect`, `GoToCombat`, `ClearShot`, `DroneBody`, and `SyncTransforms`. They can be selected in the Unity CPU Profiler. The A* implementation only gains measurement scopes.

The profiling fixture uses the actual MainLab graph, gates and all four enabled policies, with a stationary player, then explicitly seeds the Interceptor's recent-velocity branch in the second half of a 12 s window. Test-only 10,000 player HP prevents premature termination; it never saves or changes gameplay balance. The fixture captures every marker occurrence on the main thread using ProfilerRecorder and aggregates calls/time between player-loop yields. Unity's default per-frame recorder aggregation produced no samples in the headless runner; individual main-thread collection produced valid samples. Tests assert nonempty data and no buffer wrap. Earlier zero-sample attempts are not performance evidence.

PERFORMANCE_RESULTS_PENDING

Repeated route queries and native connector calculations are bounded by decision/planning cadence; moving-target interception can still request several routes per decision. `DroneBody` calls `Physics.SyncTransforms` once per active drone per frame, and sweeps/overlaps use allocating physics APIs. The repeated synchronization includes prior drone movement and animated hitboxes; coalescing it blindly to once per frame could introduce stale collision data. Measurements do not currently justify changing it, rewriting A*, or adding broad caches that might miss gate/carving changes. Future optimization should use a rendered Development Build capture with GC allocation and native navigation/physics tracks, then validate gate and clearance regressions again.

## Exceptions, freezes and reproduction limits

The available original editor log contains `InvalidOperationException: Unknown height session command` from `HeightSessionCommand.Tick`, an editor command script absent from the tracked current tree. That is a separate tooling exception and does not call `GameSession.Finish`. No gameplay exception or Unity deadlock causing the death overlay was reproduced. A normal loss leaves frame/time advancing and R reloads the scene; the sustained-combat regression verifies both.

Automated checks are isolated Play Mode fixtures, not a human start-to-win walkthrough. Shooting validation and simultaneous attackers use real MainLab components relocated outside level geometry with movement suspended, so they test damage rules without accidental cover or tactical movement. The sustained test enables the actual brain Update. Other existing tests exercise real MainLab movement and gate passages. Native R key input is not synthesized: tests invoke the same `Restart` method called by Update on R. No rendered GPU profile or exhaustive manual assessment of camera/combat feel was completed. The individual user's intermittent encounter remains unreproduced as a specific recorded incident.

## Executed validation

Unity executable: `/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity`. Tests run in `/tmp/ShiftFour-ProximityValidation`, refreshed from working Assets/Packages/ProjectSettings, to avoid the open editor's project lock. No generator, visual installer, asset refresh utility or NavMesh bake was invoked.

TEST_RESULTS_PENDING

The earlier committed evidence contains 71 passing Edit Mode tests, 28 passing Play Mode tests and one separately run camera-wall regression. This audit reran all 29 baseline Play Mode tests before fixes. New regression failures were demonstrated against the original DroneMotor in the isolated copy, then the working implementation was restored before final validation. Exploratory test-fixture problems (renderer AABB empty corners, reading non-readable meshes, and unsupported recorder options) were corrected; they were not confirmed gameplay defects. The final geometry check uses Editor read-only mesh access without changing import settings.

## Manual reproduction and review

1. Open MainLab in Unity 6000.6.4f1 and enter Play Mode. Enable the player's Health > Log Damage Diagnostics. Clear the Console. Keep F3 agent details visible. Leave Console Error Pause off when distinguishing an expected death from a paused editor exception.
2. Stand in Scout's clear view within 11 m without firing. Watch each 10 HP hit and attacker/time in the Console. Remaining exposed through ten hits should produce the explicit eliminated overlay. Taking full-height solid cover should stop hits; after 3 s of no hits, live-player recovery begins at 8 HP/s.
3. Shoot an attacker down and confirm its subsequent damage stops. Move beyond attack range and test full-height cover, wall corners and gate barriers. Damage should correspond to an unobstructed muzzle-to-eye ray, not whether the lower player body is hidden.
4. At death confirm the cursor is released, the Console has no new red runtime exception, time still advances, and **R** restores 100 HP, drones, camera height and locked gates. An actual exception shows a red Console entry/stack; an editor pause or stalled process must be diagnosed independently of the HUD text.
5. Let each drone pursue while walking laterally/backward and near cover; inspect wings, rotors and overhead corners. In a temporarily blocked approach, the motor must stay enabled and resume/reselect when space opens. Do not save temporary scene edits.
6. For deterministic exhausted-ring, occupied-cache, multiple-attacker and penetration scenarios, run `Combat_NoSafeCandidateHoldsAndRecoversWithoutChasingPlayerRoot`, `Combat_CachedStandoffIsRevalidatedWhenCoverMovesIntoIt`, and `CombatAuditPlayModeTests` in the Test Runner PlayMode tab. These construct temporary blockers/layouts and clean up afterward.
7. Open CPU Profiler, record a rendered run with all policies active, and inspect `ShiftFour.*`, native navigation, physics and GC allocation tracks near an observed stutter. Repeat in a Development Build for representative frame/GPU timing. Do not infer a freeze cause from the loss message alone.

Batch command (use a closed or isolated project; repeat with `-testPlatform EditMode`):

```sh
/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath /tmp/ShiftFour-ProximityValidation \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/ShiftFour-CombatEvidence/play-mode.xml \
  -logFile /tmp/ShiftFour-CombatEvidence/play-mode.log
```

## Files changed

- `Health.cs`: structured attribution, optional development logging, configuration resets diagnostic/recovery history.
- `DroneBrain.cs`, `PlayerController.cs`: attribute accepted shots; shared shot-query marker in DroneBrain.
- `GameSession.cs`: explicit eliminated/final-attacker/restart wording.
- `DroneMotor.cs`: safe exhausted-ring hold, occupancy revalidation, cheap candidate rejection first, combat-goal marker.
- `DroneBody.cs`: body/synchronization markers only; clearance/movement algorithm preserved.
- `WaypointGraph.cs`: route/connector markers only; A* and gates preserved.
- `DroneMotorPlayModeTests.cs`: two standoff regressions.
- `CombatAuditPlayModeTests.cs` and `.meta`: combat, diagnostics, restart, mesh-clearance and performance fixtures.
- Play Mode test assembly definition: reference the already installed Unity.Collections assembly for Editor read-only mesh inspection; no package dependency change.
- This report and `docs/evidence/combat-audit/`: actual XML results, performance output and preserved-asset hashes.

`preserved-assets.json` confirms MainLab, its baked NavMesh, all four drone prefabs, QualitySettings, InputManager and Packages/manifest are byte-identical to HEAD. All four teammate policy files and visual assets remain untouched.
