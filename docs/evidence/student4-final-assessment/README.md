# Student 4 final integration assessment — 19 October 2026

**Overall evaluation: PARTIAL.** The latest fetched reviewed `dev` has correct, tested A* and route following, but it is not the complete Student 4 build. New probes demonstrate raised-obstacle and player penetration. The advanced animation/collision implementation and the newly combined Control prediction implementation remain separate, uncommitted feature work. Their passing tests support completion of those features, not completion of integration.

No commits, pushes, merges, scene regeneration or navigation rebakes were performed. No teammate source or shared assets were changed during this assessment. Unity-generated project-setting changes in the Control worktree were restored to its reviewed baseline.

## Build provenance

| Evidence label | Source | What it contains |
| --- | --- | --- |
| Reviewed integrated baseline | `origin/dev` = `271de5106fd3463529ac710de4478a1774155943` | Five-node A*, shared motor, original per-drone policies, basic hover/rotor/white firing flash. No DroneBody, advanced pose/effect/wreck system, bounded predictive memory or Control tactical layer. |
| Animation/prediction feature | `feature/gv-asmadala-drone-animation`, HEAD `03e0861035bb328d8766b5838bcb564c8f957c3e`, plus uncommitted prediction/tests | Height/collision/scene work, advanced animation and combat/destruction interfaces, predictive policy using the existing global nodes. |
| Control/prediction feature | `/Users/dev/Desktop/game/Shift-Four-Control-Tactical`, `feature/is-asmadala-control-tactical-navigation`, based on reviewed `271de51`, plus uncommitted tactical/prediction work | Six private Control junctions, conservative route clearance, staged arcs, bounded predictive memory and recovery. Uses reviewed dev movement/combat interfaces; does not contain DroneBody or advanced animation. |

The animation feature's MainLab already differs from dev through committed height, collision and spawn work. Its scene was not copied onto dev or the Control branch. The Control branch's MainLab, Control prefab, graph implementation, other three policies, mission and door source match reviewed dev. See [source hashes](raw/source-sha256.json) and [shared-asset check](raw/shared-assets-check.txt). Profiling scopes were injected only into disposable `/tmp` copies, with separate before/after manifests. The Control profile predates the final gizmo-only score/ETA/state update; that update was covered by the seven-case diagnostic rerun. The recorded hashes distinguish those snapshots.

## Responsibility completion checklist

“PASS” means supported in the reviewed integrated build within the executed scope; “PARTIAL” means meaningful implemented/tested behavior with a material gap; “FAIL” means absent or a demonstrated failed requirement. Supplemental feature results do not change the integrated grade.

| Student 4 responsibility | Integrated result | Evidence and remaining work |
| --- | --- | --- |
| A* pathfinding | PASS | 40 graph EditMode cases plus 400 real-scene gate/endpoint queries matching independent Dijkstra. |
| Route reconstruction | PASS | Ordered reconstruction, cheaper-parent update, near-origin waypoint and exact endpoint regressions pass. |
| No-path handling | PASS | Empty-route contract, invalid endpoints, disconnected components, blocked attachments and bounded motor retry tests pass. |
| Shared drone movement | PARTIAL | All four prefabs pass the shared motor contract. New MainLab probes penetrate a beam/player; integrate and review the collision work. |
| Waypoint arrival and braking | PASS | Ordered arrival, final low-speed requirement, acceleration/speed, heading stability and displacement recovery tests pass. |
| Route invalidation and replanning | PASS | Version changes, blocked routes, native-path clearing, stall limits and asynchronous-carving grace tests pass. |
| Drone animations | PARTIAL | Hover/rotors/basic firing flash exist. Advanced flight, turn/brake poses, damage, tracer and physical destruction are only in the animation feature. |
| Control tactical junctions | FAIL | Not present in dev. Both arcs and six junctions pass in the Control/prediction branch. |
| Predictive Interceptor AI | PARTIAL | Dev has simple observed-velocity forward-node scoring; confidence, bounded history, route-based arrival comparison and local junctions remain feature work. |
| Prediction failure recovery | FAIL | Dev lacks explicit missed-window handling/candidate cooldown and bounded local search. Upgraded policy tests pass separately. |

## Executed test matrix

| Build/run | Passed | Failed | Skipped | Scope |
| --- | ---: | ---: | ---: | --- |
| Reviewed dev EditMode | 62 | 0 | 0 | 40 graph + 22 motor cases. |
| Reviewed dev PlayMode | 19 | 0 | 0 | Existing motor suite, including four existing prefabs. |
| Reviewed dev added QA probes | 4 | 2 | 0 | Graph, console/exit, CPU probe and profile pass; player and beam penetration fail. |
| Animation/prediction EditMode | 110 | 0 | 0 | Graph, movement, camera, animation and predictive policy. |
| Animation/prediction rendered PlayMode | 60 | 0 | 0 | All suites, including the rendered four-agent combat/destruction test. |
| Animation/prediction added QA probes | 6 | 0 | 0 | Same probes as dev, including obstruction recovery. |
| Combined Control/prediction EditMode | 111 | 0 | 0 | Existing 62 + 20 tactical + 27 policy + 2 comparison/trace tests. |
| Combined Control/prediction PlayMode | 33 | 0 | 0 | Existing 19 + 7 tactical + 7 predictive MainLab tests. |
| Combined Control/prediction diagnostics rerun | 7 | 0 | 0 | Tactical suite after final score/ETA/state gizmo update. |
| Combined Control/prediction added QA probes | 4 | 2 | 0 | Same two collision failures as dev; this branch intentionally uses the reviewed motor without DroneBody. |

Raw NUnit XML and the machine-readable per-case matrix are in [raw](raw/). All runs used Unity 6000.6.4f1. The fresh rendered feature run passes two frame-bound cases that failed in the earlier `-nographics` run recorded in `../interceptor-prediction/`; that older evidence is retained. No single cause for that rendering/timing difference has been established.

### A* detailed evaluation

| Requirement | Result | Actual evidence |
| --- | --- | --- |
| Correct g | PASS | Sum of Euclidean edge distances; reference shortest costs match all 400 queries. |
| Correct h | PASS | Euclidean goal-node distance; executed admissibility checks against reference costs. Triangle inequality establishes consistency for this edge model. |
| Correct f | PASS | Source selects the minimum `cost[i] + Distance(node[i], goal)`; competing-route and cheaper-parent cases pass. |
| Ordered reconstruction | PASS | Parent chain is reversed; reconstructed cost/order matches the expected fixtures. |
| No-path result | PASS | Empty list for disconnected/blocked queries. |
| Locked gates block paths | PASS | Four gate unit cases, 16-mask enumeration, physical MainLab gate probes. |
| Opening enables paths | PASS | Version changes and physical console/drone opening produce a reachable Control-to-Storage route. |
| Invalid endpoints fail safely | PASS | Nonfinite, missing nodes, invalid indices/gates, off-mesh and disconnected local connectors pass. |
| Heuristic/path-cost explanation | PASS | Worked example below; graph distance and native travel distance are explicitly distinguished. |

For all gates open, the five-node scene route is `0 → 2 → 3 → 1 → 4`: Checkpoint `(-10,0,-10)` → Server `(-10,0,10)` → Control `(10,0,10)` → Storage `(10,0,-10)` → Exit `(22,0,-15)`. Graph cost is **73 m**, comprising 20 + 20 + 20 + 13. Exact-node queries omit the already-reached origin in the returned waypoint list. These g values describe the final path; they are not a fabricated internal expansion log.

| Node on final path | g | h to Exit | f |
| --- | ---: | ---: | ---: |
| 0 Checkpoint | 0 | 32.388 | 32.388 |
| 2 Server | 20 | 40.608 | 60.608 |
| 3 Control | 40 | 27.731 | 67.731 |
| 1 Storage | 60 | 13 | 73 |
| 4 Exit | 73 | 0 | 73 |

With mask `0000`, node 0 → node 4 returns no path. Even with all other gates open, closing gate 2 disconnects Control from Storage/Exit. These exact graph queries do not prove native passage clearance; every motor leg still requires a complete NavMesh path. A* minimizes graph-edge distance, not travel time or the length of furnished-room detours.

### Shared controller detailed evaluation

| Requirement | Integrated result | Evidence / limit |
| --- | --- | --- |
| Scout, Guard, Flanker and Interceptor use shared controller | PASS | `AllFourExistingPrefabs_CanFollowTheSharedMotorContract`. Their policies remain distinct. |
| Smooth movement | PARTIAL | Speed/acceleration/arrival assertions pass; demonstrated collision failures prevent an unrestricted smooth-navigation claim. |
| Waypoint arrival | PASS | Ordered route and already-at-goal assertions pass. |
| Correct rotation | PASS | Angular limit and holding heading after arrival pass in the clean baseline run. |
| Smooth braking | PASS | Final arrival waits for low velocity; acceleration/speed/arrival suite passes. |
| Stable destination changes | PASS | Same goal preserves route/cursor; changed unreachable goal retries correctly. |
| Replan after gate changes | PASS | Bounded grace retries and correct independent gate state assertions pass. |
| Unreachable destinations | PASS | No-path cache, native partial path rejection and recovery tests pass. |
| No repeated spinning | PASS | Turn-bound/arrival-heading tests and stable-goal cases pass within test duration. |
| No persistent stuttering | PARTIAL | Stall recovery and retry exhaustion pass; no long-duration human play session was performed. |
| No wall penetration | FAIL | Temporary raised beam: root penetration reaches about 0.570 m despite a complete ground NavMesh route. Authored-wall motor tests pass; the general collision requirement fails. |
| No player penetration | FAIL | Real player CharacterController probe reaches about 0.775 m root penetration. |

Both baseline probes and their counterparts in the combined Control branch eventually reach the goal after removing/moving the obstruction; recovery does not erase the penetration failure. The animation/DroneBody snapshot passes the same two probes with zero measured root penetration and successful recovery. The beam is a clearly labelled temporary QA fixture, not an authored Control obstacle. See collision CSVs and actual [captures](screenshots.html).

### Animation detailed evaluation

| Requirement | Integrated result | Supplemental evidence / remaining work |
| --- | --- | --- |
| Hover | PASS | Authored visual rest position and no-drift hover test pass. |
| Forward flight pose | FAIL | No advanced flight pose in dev; animation feature pose tests/rendered approach pass. |
| Turning pose | FAIL | Root rotation exists, but dedicated visual turning response is feature-only. |
| Braking pose | FAIL | Motor braking exists; visual braking state is feature-only. |
| Firing flash | PASS | Legacy flash exists; QA profile verifies accepted shots. Richer muzzle effect passes in feature. |
| Tracer | FAIL | Feature-only; rendered test asserts accepted hit/visual target correspondence. |
| Taking damage response | FAIL | Dev Health accepts damage but no advanced hit pose; feature real player-shot test passes. |
| Fatal damage presentation | PARTIAL | Dev stops/disables collision and greys renderers; critical/fall sequence is feature-only. |
| Falling to floor | FAIL | No wreck fall in dev. Feature rendered and destruction tests pass. |
| Ground impact | FAIL | Feature-only; impact-count assertions pass. |
| Stable disabled wreck | FAIL | Dev corpse remains at its hover height. Feature settles with disabled state. |
| Restart cleanup | PARTIAL | No advanced wreck/effects cleanup exists to verify in dev. Feature restart/leak assertions pass. |

[32 fresh screenshots](screenshots.html) show hover, approach, accepted shot, hit, critical, fall, impact and wreck for each agent in the **animation/prediction feature**, not dev. They were captured by Unity, converted losslessly from TGA and visually inspected at representative firing and settled frames. Separate pose tests cover turning/braking; dedicated turning/braking screenshots were not produced. Test assertions, rather than still images alone, establish transitions and cleanup.

### Control integration detailed evaluation

| Requirement | Integrated result | Actual evidence / remaining work |
| --- | --- | --- |
| Left tactical route | FAIL | No tactical layer in dev. Combined Control branch traverses `0→1→2→3`. |
| Right tactical route | FAIL | No tactical layer in dev. Combined branch traverses `0→5→4→3`. |
| Reachable junctions | FAIL | Candidate transforms/data absent in dev; all six are native/clearance validated in the feature. |
| Valid Interceptor use | PARTIAL | Simple global-node policy exists. Actual timed observations select both local sides only in combined branch. |
| Console access | PASS | Player-sized path and ≤3 m interaction ray in loaded MainLab. |
| Security-gate handoff | PASS | Console alone holds closed; actual Interceptor death + console opens gate 2; Storage gate remains closed. |
| No shortcut through locked gates | PASS | Graph and native closed-gate tests; premature graph-open flag does not defeat the physical blocker in Control tests. |
| Player and drone clearance | FAIL | Baseline player penetration probe fails. Feature tactical routes have conservative static clearance, but that branch still lacks a per-frame DroneBody guard. |
| No route loops / oscillation | PARTIAL | Shared motor repeat-goal cases pass. Tactical distinct-stop, remaining-arc and commitment cases pass separately; unified feature build remains untested. |
| Exit handoff and mission sequence | PASS | Early TryExit rejected; all four drone/console objectives release Storage-to-Exit; final TryExit ends session. |

The suitable private junctions, anchored at Control home `(10,0,10)`, are:

| Local ID | Name | World coordinate |
| --- | --- | --- |
| 0 | South | `(10,0,4.5)` |
| 1 | WestSouth | `(3,0,4.5)` |
| 2 | WestNorth | `(3,0,15.5)` |
| 3 | Forward | `(10,0,15.5)` |
| 4 | EastNorth | `(17,0,13.5)` |
| 5 | EastSouth | `(17,0,4.5)` |

The full native left arc is **25 m**; right is **23.280 m**. Staged motor arrival uses the existing 1 m stopping distance and a 0.1 m observation allowance, so travelled distances are shorter than centre-to-centre lengths. Raw per-run motor measurements are included. These arcs enclose the actual two cover blocks at `(7,1.1,12)` and `(13,1.1,8)`; the planned central island model has not been authored. Do not describe the schematic as proof of a finished island asset.

The selected architecture keeps the five global nodes and all gate edges unchanged. `InterceptorPolicy` owns observation history, scoring and recovery. `ControlTacticalNavigation` supplies distinct validated arcs and preserves remaining stops; `InterceptorBrain` sends only the next stop to the existing motor. `ControlTacticalRouteValidator` checks the shared graph, complete native legs, endpoint occupancy and continuous yaw-safe box casts. Player hypotheses use native player-sized geometry. Conservative checks can reject useful routes; this is a safe refusal, not proof that every physically possible route is discoverable.

### Interceptor comparable scenarios

| Scenario | Integrated result | Upgraded policy evidence |
| --- | --- | --- |
| Player moves forward | PARTIAL | Dev forward-node scoring; upgraded ETA/confidence scoring and genuine visible MainLab motion pass. |
| Player stops | PARTIAL | Dev loses velocity-based candidates but lacks explicit confidence reset/failed-candidate treatment; upgraded stop regression passes. |
| Player reverses | PARTIAL | Dev changes observed velocity, without predictive commitment recovery; upgraded reversal regression passes. |
| Sudden turn | PARTIAL | Upgraded sharp-turn/acceleration handling passes; not integrated. |
| Disappears behind cover | PARTIAL | Dev remembers LastKnown temporarily; upgraded hidden-input and real raycast occlusion tests pass. |
| No reachable intercept | PARTIAL | Shared motor safely rejects no-path; upgraded candidate rejection and validated fallback tests pass. |
| Prediction fails | FAIL | Explicit arrival-window/cooldown/local search absent in dev; upgraded missed-window and failure-recovery cases pass. |
| Gate state changes | PARTIAL | Integrated motor replans; upgraded policy also invalidates candidate selection and revalidates routes. |

[Prediction trace](prediction-trace.svg) is a schematic plotted from actual policy output CSV with **controlled synthetic observations and route fixtures**. It is not a Scene screenshot or evidence of player capture rate. Nine scenarios share the same time steps and route rules within the fixture; hidden-position inputs are excluded from recorded sightings. The native MainLab comparison and real visible/cover tests are separate. Do not infer a speedup from one baseline/improved motor attempt, which can select different targets and path lengths.

## Performance and reliability

See [measured performance](PERFORMANCE.md), raw per-frame samples and allocation calibration. CPU profiles cover route search, motor, decision methods, animation, collision guard where present, accepted firing, effect ticks, wreck creation/update and native scene physics. Missing components on dev are recorded as absent, not zero-cost implementations. No architecture was changed on speculative performance grounds.

| Requirement | Result | Limit |
| --- | --- | --- |
| A* route search profiling | PASS | Inclusive live marker and warmed 73 m query microbenchmark. |
| DroneMotor profiling | PASS | Four live update scopes and cached-delegate microbenchmark. |
| DroneBody collision profiling | PARTIAL | Measured in animation feature; absent from integrated dev. |
| AI decision evaluation | PASS | Actual decisions plus explicitly synthetic observation stress. |
| Animation update profiling | PARTIAL | Legacy and advanced updates measured in separate builds. |
| Firing effects profiling | PARTIAL | Four accepted shots plus feature effect ticks; rich effects absent from dev. |
| Destruction physics profiling | PARTIAL | Wreck create/update plus native scene physics in feature; absent from dev. |
| Frame-spike investigation | PARTIAL | Measured Editor outliers and correlated scopes reported; no target-player capture. |
| Unnecessary allocations | PARTIAL | Known allocation proves thread API unavailable; source allocation sites identified, no fabricated zero-allocation conclusion. |

## Git references and handoff

Repository: [Nimsara-Jayarathna/Shift-Four](https://github.com/Nimsara-Jayarathna/Shift-Four).

| Reference | Role in evidence |
| --- | --- |
| [PR #11](https://github.com/Nimsara-Jayarathna/Shift-Four/pull/11), merge `0967dfe` | A* work integrated into dev; commits `c90ca3f`, `8d4da15`. |
| [PR #12](https://github.com/Nimsara-Jayarathna/Shift-Four/pull/12), merge `271de51` | Latest reviewed dev motor work; `111a2ef`, `84b52c2`, integration `d1544e7`. |
| `98ba978`, `01693a7`, `1b986d8`, `03e0861` | Height/collision, clearance evidence, combat audit and animation feature ancestry above dev. These are not evidence of a dev merge. |
| Prediction + Control changes | Uncommitted worktree changes. No new PR reference or merge result is established. |

PR numbers/titles come from fetched Git merge history; no new PR was created and no external review status was assumed.

Files added/changed for the accepted Control prediction implementation: `Assets/Scripts/AI/Agents/InterceptorBrain.cs`, new `InterceptorPolicy.cs`, extended new `ControlTacticalNavigation.cs`, their existing/new metas, `ControlTacticalNavigationEditModeTests.cs`, `ControlTacticalNavigationPlayModeTests.cs`, `InterceptorPolicyEditModeTests.cs`, `InterceptorComparisonEditModeTests.cs`, `InterceptorMainLabPlayModeTests.cs` and evidence documents. The original animation worktree's production source was preserved; this assessment adds `docs/evidence/student4-final-assessment/` there and a provenance update to the earlier prediction report.

Remaining integration work:

1. Review the existing height/collision/combat/animation branch and its already-committed MainLab/prefab differences with Pamudi, Nimsara and Nimthara. This assessment did not authorize new shared-asset edits or merge those changes.
2. Reconcile the two Interceptor adapters against the final movement/combat interfaces. The accepted Control prediction branch is now complete against dev interfaces; its validator cannot replace the animation branch's per-frame collision guard.
3. Review the six private coordinates against any future island model. If geometry or navigation changes are necessary, show exact asset edits and obtain the shared owners' approval before applying them.
4. Rerun all tests and added collision probes on one reviewed combined build, with rendered checks and manual console-to-exit playthrough. Add dedicated turn/brake captures and a longer session.
5. Capture CPU/allocation evidence in a target development player with a working allocation recorder. Do not call the present build allocation-free or claim a capture-rate improvement.

See [short GV/IS explanations and likely viva Q&A](VIVA.md), [reproduction instructions](REPRODUCE.md), [screenshots](screenshots.html) and raw source/test provenance. No automatic commit, push or merge was performed.
