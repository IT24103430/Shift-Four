# Task 14 — Final progressive difficulty validation

**Overall: PARTIAL.** The latest active unified build passes **465/465 project tests** on Unity **6000.6.4f1**. Shared systems, presentation, navigation and mission progression are verified in automated fixtures. The complete advanced four-policy game is not ready for final acceptance: Scout/Guard/Flanker owner proposals remain unapplied, console sound sources need approved asset fixes, two authored posts fail body clearance, and human encounter fairness remains unmeasured.

Validation date: **19 October 2026, Asia/Colombo**. No production tuning, damage, saved scene/prefab/model/navigation change, commit, staging, push or merge command was performed by this validation. Concurrent workspace commits are recorded independently of content checks.

## 1. Feature completion matrix

PASS means the stated active behavior passed the named checks. PARTIAL identifies missing acceptance or unapplied features. FAIL identifies a demonstrated current gap; these entries are not concealed failures in the passing Unity suite.

| Feature | Status | Evidence / remaining work |
| --- | --- | --- |
| Four difficulty defaults and initialized HP | PASS | Actual MainLab instances and two production restarts bind the matching resource; damage/reapplication cannot heal. |
| Walk, gunshot, impact and console event contracts | PASS | Typed dispatcher, real movement/hitscan/console producers, idempotence, timestamps and lifecycle checks. Console source placement has a separate failure below. |
| Sprint sensing category | PARTIAL | Dispatcher, cadence helper and hearing support Sprint. Current player movement has no sprint input/mechanic and emits walking events. |
| Hearing attenuation, gates, occlusion, expiry and budgets | PASS | Existing sensor suite plus real dispatch/profile; separate sound estimates never refresh visual memory. |
| Awareness and visual-memory expiry | PASS | Genuine visual confirmation, uncertain sound, search/return deadlines, death shutdown and no hidden-position refresh fixtures. |
| Speed caps, acceleration, actual-motion feedback | PASS | All four mode targets; smooth caps; real flight, turning/braking and 30/60/120 Hz logic tests. |
| A*, native routes, Control junctions and collision | PASS | Independent 400-query gate/endpoint comparison, both Control arcs, six junctions, locked gates, raised obstacles and player/body recovery. |
| Active Scout policy | PARTIAL | Real sound investigation and patrol recovery demonstrated; Task 8 enhanced turning/local search remains Pamudi's proposal. |
| Active Guard policy | PARTIAL | Cover scoring/repositioning demonstrated. Sound-led investigation, explicit defensive boundary, stable cover commitment and validated post fallback remain Task 9's proposal. |
| Active Interceptor prediction/sensor integration | PASS | Existing advanced predictor retained; genuine reversal, confidence/commitment, speed-aware ETA, occlusion, failed-candidate cooldown and fallback tests pass. This is functional acceptance, not a human balance verdict. |
| Active Flanker side selection | PARTIAL | Both side labels occur with real observations. Advanced route-cost scoring, sound action, commitment and failure recovery remain Task 11's proposal. |
| Flanker adaptive recovery under ineffective routes | FAIL | Active code has no failed-candidate ledger/commitment. Diagnostic window produced 24 motor plan attempts and about 6.3 s of consecutive sampled zero-speed Following state. |
| Console sound-source attachment | FAIL | All four actual cabinet centres fail the strict NavMesh source rule. A separate five-path proposal passed isolated review tests but is unapplied. |
| Guard/Flanker authored post clearance | FAIL | Both Home points overlap the full drone body envelope. Native path completion does not prove a usable full-body goal. |
| Firing, damage, hit/death presentation and wreck priority | PASS | Accepted hitscan/occlusion, original cooldown/damage, all four models, fatal priority, gravity/impact/settling and animation allocation tests. |
| Four consoles, gates, exit and restart | PASS | Scripted real-console mission, drone death objectives, physical/carving release, win/loss restarts, no stale hearing targets or wrecks. |
| Missing scripts, duplicate components and model bindings | PASS | Read-only saved-prefab/scene audit and loaded runtime checks; one brain/motor/body/sensor/visual owner per drone. |
| Full human gameplay and progressive fairness | PARTIAL | Scripted fixtures only. Human completion time, unavoidable-hit rate, fair-death rate and repeated disengagement success have not been measured. |

## 2. Exact modified files

Task 14 added `Assets/Tests/PlayMode/ProgressiveDifficultyValidationPlayModeTests.cs` and its `.meta`. Its four cases measure actual player hitscan defeats, visible-policy/cover behavior, natural decision latency and live sensing costs. No existing project test assertion was removed or relaxed.

The preceding Task 13 added:

- `Assets/Tests/EditMode/Movement/FourRoomIntegrationEditModeTests.cs` and `.meta`.
- `Assets/Tests/PlayMode/FourRoomIntegrationPlayModeTests.cs` and `.meta`.

Documentation: this report, [FOUR_ROOM_INTEGRATION.md](FOUR_ROOM_INTEGRATION.md), the refreshed final-run `docs/evidence/four-room-integration/runtime-bindings.csv`, and the new `docs/evidence/progressive-difficulty/` package. [changed-files.json](evidence/progressive-difficulty/changed-files.json) enumerates every artifact path. The two optional diagnostic test templates live under that documentation package and are installed only in disposable projects.

**No existing active production C# file, profile, MainLab, prefab, FBX, material, gate or baked navigation asset changed.** All 86 active C# sources match the complete-suite verification copy. [Source fingerprints](evidence/progressive-difficulty/validated-source-sha256.json) and [integrity checks](evidence/progressive-difficulty/integrity.json) distinguish source bytes from Git/index activity.

`ConsoleAnchors.task14.patch` is review material, not an applied change. Its proposed production paths are listed in section 8.

## 3. Actual Unity test counts

| Run | Passed | Failed | Skipped | Scope |
| --- | ---: | ---: | ---: | --- |
| Complete Edit Mode | 306 | 0 | 0 | All available active project cases; 1.544 s runner duration. |
| Complete Play Mode | 159 | 0 | 0 | All available active project cases, rendered graphics enabled; 518.085 s runner duration. |
| **Complete active project total** | **465** | **0** | **0** | Includes the 450 previous cases and 15 added integration/validation cases. |
| Optional encounter diagnostic | 1 | 0 | 0 | One isolated fixture, four fresh-room windows; current production code unchanged. |
| Final console-anchor proposal | 3 | 0 | 0 | Unapproved patch in a different disposable project; excluded from the 465 project total. |

The earlier focused runs are not added again. Two failed initial console-proposal runs are retained and explained in the [review handoff](evidence/progressive-difficulty/CONSOLE_ANCHOR_REVIEW.md); they do not describe the active project's complete-suite result.

[Edit XML](evidence/progressive-difficulty/EditMode.xml), [Play XML](evidence/progressive-difficulty/PlayMode.xml), [full per-case matrix](evidence/progressive-difficulty/TEST-MATRIX.csv), [summary](evidence/progressive-difficulty/test-summary.json), [exact commands/environment](evidence/progressive-difficulty/commands.json). These cover difficulty/health, producers, hearing, awareness, speeds, graph/motor/body, policy/Interceptor regressions, firing/damage, visuals/death, tactical navigation, gates and cleanup. Tests of unapplied Task 8/9/11 proposal code are not claimed as tests of the active build.

## 4. Per-level difficulty and defeat measurements

| Level / room | Agent | Intended difficulty | HP | Patrol | Investigate | Chase | Actual accepted 25-damage hits | Measured first-to-final-shot interval |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 / Checkpoint | Scout | Easy–Medium | 100 | 1.8 | 2.2 | 2.6 | 4 | about .75 s |
| 2 / Server | Guard | Medium | 150 | 2.0 | 2.5 | 3.0 | 6 | about 1.25 s |
| 3 / Control | Interceptor | Hard | 125 | 2.5 | 3.1 | 3.7 | 5 | about 1.00 s |
| 4 / Storage | Flanker | Very Hard | 150 | 2.8 | 3.5 | 4.2 | 6 | about 1.25 s |

Speeds are m/s. All profiles use acceleration **8 m/s²** and angular cap **120°/s**. Difficulty names are the requested design targets, not an additional serialized enum. Health is intentionally not monotonic between Guard and Interceptor; progression also depends on mobility and policy.

[Shots-to-defeat data](evidence/progressive-difficulty/generated/shots-to-defeat.csv) counts real `PlayerController.Shoot` hitscan hits against each authored-room drone at the unchanged `.24 s` configured interval. Coroutine/frame quantization produced approximately `.25 s` between fixture calls. Targets and enemy decisions were paused. These are controlled exposed-target defeat measurements, not completed encounter times or sustained combat DPS.

Enemy attack damage **10**, range **11 m**, interval **1.1 s**; player damage **25**, interval **.24 s**, movement **5 m/s** remain unchanged. The saved drone health 75 is superseded once during normal initialization by its matching resource profile; no prefab resave was needed.

## 5. Gameplay verification and balance findings

**Every scenario below is automated. No manual keyboard playthrough was performed.** [Actual captures](evidence/progressive-difficulty/index.html) label their fixture conditions; [capture hashes](evidence/progressive-difficulty/captures.json) identify original Unity TGA images and converted PNGs.

The separate native-motion diagnostic enables the target policy/motor/physics in a fresh scene and pauses player input/other agents. Player placement/motion is scripted. At six seconds it attempts a reachable point behind real room geometry; if unavailable, the CSV explicitly records an off-map fallback. Flanker additionally receives a temporary raised obstruction on its current goal. Windows last at most 14 s and stop on player death.

| Room | Observed behavior | Window / travel | Motor plan attempts / native request changes | Actual player damage / outcome |
| --- | --- | --- | --- | --- |
| Checkpoint | Real gunshot → Investigate sound → Patrol; approached the heard estimate to **.450 m**. | 14.017 s / 17.332 m | 3 / 3 | 0; player hidden. |
| Server | Guard server room → Reposition → Take protected cover → return decision. | 14.017 s / 19.559 m | 3 / 3 | 20; survived. The moving fixture found no cover point for the player and used the explicitly logged off-map fallback. |
| Control | Genuine forward/reversal observations → observation/pursuit fallback; moving agent reacquired the scripted player after the cover placement. | 11.983 s / 10.744 m | 8 / 8 | 100; player died. The player gave no evasive input. |
| Storage | Flank right and Flank left after relocation, then Hold storage/ReturnToPost. The raised obstruction exposed prolonged ineffective movement. | 14.017 s / 21.287 m | 24 / 24 | 40; survived; about 6.3 s consecutive sampled zero-speed Following state. |

[Encounter summary](evidence/progressive-difficulty/encounters/encounter-summary.csv), [full traces](evidence/progressive-difficulty/encounters/trace-summary.json), [fixture conditions](evidence/progressive-difficulty/encounters/context.txt). Plan/request counts observe changes in the motor's scheduling fields each frame; they are not counts of every policy/hearing/NavMesh query. All sampled native statuses were PathComplete and motor Blocked entries were zero. Flanker's long stationary Following state shows why those counters alone cannot establish effective tactical recovery. Guard was still approximately 3.47 m from Home at the last sampled row; Flanker approximately 2.70 m. Both authored Home centres fail the separate body-clearance audit.

The standard rendered Control scenario separately demonstrates **InterceptAhead → PursueLastKnown on reversal → SearchLastSeenArea after cover loss**, retaining the last genuine observation. [Trace](evidence/progressive-difficulty/generated/rendered-prediction.csv). The nine sensor integration cases also verify sound-only investigation, real return-home routes, hidden-position isolation and gate rejection. The mobile diagnostic's rejected/late candidates are valid fallbacks; prediction is not forced for every observation.

Natural scheduling measured hearing consumption in **.033–.300 simulated seconds**, investigation onset in **.633–.900 s**, and visual confirmation in approximately **.300 s** in this final run. [Latency data](evidence/progressive-difficulty/generated/natural-response-latencies.csv). These use real sensory checks and normal brain scheduling, with drone movement paused. Small float/frame differences and phase within the `.3 s` decision cadence matter; these are not universal timing bounds for arbitrary scenes.

All four stationary-enemy cover checks prevented additional damage behind actual geometry, with genuine visibility cleared and awareness moving to Search. Each staged exposed observation allowed one real 10-damage hit. [Cover characterization](evidence/progressive-difficulty/generated/policy-cover-characterization.csv). A mobile Interceptor later moving around cover is different from shooting through cover. **Unavoidable-hit rate and unfair-death rate are unmeasured**; the scripted standing player's death cannot establish either.

The real-console mission fixture completed all four console/drone pairs, held each gate closed with one prerequisite, opened physical and navigation barriers after the animations, unlocked the exit, then passed victory and loss restarts. [Mission trace](evidence/progressive-difficulty/generated/scripted-mission.csv). It uses scripted positioning, `Interact`, accepted damage calls and `TryExit`; it does not demonstrate a human walking the complete route or keyboard-triggering every interaction.

The IDE's older `sensor-interceptor/before/mainlab-comparison.csv` is a controlled route-attempt artifact. Its one short route timing is not a human capture-rate benchmark or evidence of a general speed improvement. Current comparison fixtures and their assumptions remain in the evidence package.

## 6. Performance and reliability

Hardware/context: **Apple M1, 8 GB**, Unity Editor, graphics enabled, fixed simulated `1/60 s`, target frame rate 60. These are measured Editor wall intervals and inclusive CPU markers. Pacing, rendering, Editor activity and the harness are included in wall time. They do not establish standalone GPU performance or a 120 FPS hardware guarantee.

| Measurement | Actual finding |
| --- | --- |
| 720-frame navigation/prediction/firing/destruction profile | Wall median **16.673 ms**, p95 **17.078 ms**, p99 **17.408 ms**, max **26.135 ms**. |
| 240-frame live sensing profile | Wall median **16.662 ms**, p95 **17.168 ms**, max **18.056 ms**. |
| Sound dispatch, 20 emission frames | Stopwatch p95 **.096 ms**, max **.097 ms**, including reflection overhead and listeners. |
| Hearing, 31 evaluations | Active-frame p95 **.064 ms**, max **.074 ms**; nested native/graph work included. |
| FindRoute, main 720-frame profile | 188 calls; active-frame p95 **.299 ms**, max **.301 ms**. Native validation is inside relevant scopes; no isolated C++ NavMesh-search duration is claimed. |
| Interceptor decision/scoring, main profile | 21 calls; active-frame p95 **4.349 ms**, max **5.501 ms**. Includes nested tactical/route work. |
| DroneBody, main / sensing profiles | Active-frame p95 **1.655 / 3.568 ms**, max **6.539 / 5.619 ms** across the four agents. |
| Animation / VFX, main profile | Active-frame p95 **.116 / .019 ms**. Animation max **9.518 ms** includes the destruction frame. |
| Four wreck constructions in one frame | **9.387 ms** aggregate; nested geometry **7.033 ms**, actor exclusion **.553 ms**. These are not additional costs to sum on top of the parent scope. |
| Physics.Simulate, main profile | Active-frame p95 **.228 ms**, max **5.666 ms**. |

[Performance summary](evidence/progressive-difficulty/performance-summary.json), [720 raw frames](evidence/progressive-difficulty/generated/profile-frames.csv), [240 sensing frames](evidence/progressive-difficulty/generated/sensing-profile-frames.csv), [hardware/fixture context](evidence/progressive-difficulty/generated/profile-context.txt). All reported markers were available. Inclusive nested scopes must not be summed.

Calibrated allocation checks found **zero GC.Alloc events over 24,000 warmed awareness-animation ticks**; positive 1 MiB calibration succeeded. Warmed nonsaturated body queries and motor/animation microbenchmarks also recorded zero events. A* recorded **10 allocation events/query** in its 100-query workload; warmed Interceptor decisions recorded **18 events/call** in the specific hidden-target microbenchmark. Counts are allocation events, not allocated bytes. Full-frame GC bytes were unavailable and are explicitly recorded as `-1`. See [allocation microbenchmark](evidence/progressive-difficulty/generated/allocation-microbench.csv) and [animation allocation evidence](evidence/progressive-difficulty/generated/allocations.csv).

The live sensing sample recorded **zero budget rejections**, 21–24 physics queries and 10 native-path queries per sensor over its four simulated seconds. This does not supersede the dedicated burst/per-frame/per-second budget tests, which also pass.

Reliability checks passed actual win/loss reload, repeated sensor enable/disable/unload, stale delegate removal, idempotent objectives, no residual wreck/effects, camera protection and collision regression. The 14 s tactical windows and the two unusable Home centres remain limitations despite passing regression checks.

## 7. Known limitations and evidence-based tuning

1. **Resolve functional gaps before changing difficulty numbers.** Review the console anchors and the Guard/Flanker post/failure-recovery proposals. The active Flanker's 24 plans and prolonged stationary state justify commitment/cooldown review; increasing damage would conceal the tactical issue.
2. **Human balance acceptance is outstanding.** Measure first-time and repeat-player completion times, damage distributions, deliberate cover escape success and deaths with player input across all rooms. The current stationary scripts do not support claims about unavoidable hits or a target encounter duration.
3. **Retain current weapon/enemy damage for that playtest.** The measured 4/6/5/6 hits supply a baseline. The Interceptor's 100-damage scripted kill warrants checking visual warning/readability and cover escape with a moving human before changing HP, damage or cooldowns. No numeric combat adjustment is justified by these fixtures alone.
4. **Profile a standalone development build next.** Body checks and Interceptor decision peaks are larger measured CPU costs than sound dispatch. Investigate only demonstrated hotspots, preserving gate/body validators. Budget wreck geometry preparation if simultaneous destruction needs a tighter frame budget.
5. **Allocation improvements need measurements and review.** Route-list/candidate scratch reuse may reduce the observed A*/Interceptor allocation events. Do not infer a leak or byte volume from event counts, and preserve decision/gate invalidation behavior.
6. **Scope limits:** sprint input is absent; confidence/ETA are heuristic/conservative; hearing estimates can be rejected by clutter or distance; no network/device sweep, long-duration soak, human capture-rate study or standalone GPU benchmark was performed. The mobile Guard cover-placement fallback is explicitly off-map, so it is not a successful mobile Guard disengagement demonstration.

## 8. Required team approvals

| Review | Owner(s) | Concrete material |
| --- | --- | --- |
| Enhanced Scout policy/search | Pamudi | [Task 8 handoff](ENHANCED_SCOUT_HANDOFF.md). |
| Advanced Guard policy, defensive post and shared movement hook | Nimthara; named shared-file/movement reviewer including Asmadala | [Task 9 handoff](ADVANCED_GUARD_HANDOFF.md). |
| Advanced Flanker route commitment, failure cooldown and post fallback | Nimsara | [Task 11 handoff](TACTICAL_FLANKER_HANDOFF.md). |
| Optional console source API + four section properties | Nimthara for `ConsoleSwitch.cs`/Server; Pamudi for Checkpoint/final integration; Asmadala for Control; Nimsara for Storage | [Five-path anchor proposal](evidence/progressive-difficulty/ConsoleAnchors.task14.patch), [review/isolated results](evidence/progressive-difficulty/CONSOLE_ANCHOR_REVIEW.md). |

The new asset proposal changes one `noiseOffset` property on each existing console to `(0,0,-2.5)` metres. No MainLab edit is proposed. It passed **3/3 isolated proposal tests** with unchanged attenuation/uncertainty rules; it is not in the active 465-test build. The initial 1.25 m attempts failed and remain available. Reconcile its producer/Server hunks with the older Task 9 draft rather than applying duplicate changes blindly. After approval, use targeted Editor property edits preserving structure/GUIDs, then rerun full verification.

Ownership records and task-specific instructions require these reviews; no external messages or approval requests were sent. No approval is implied by a branch or commit title. The three prior policy patches remain applicable but unapplied.

## 9. Suggested PR breakdown and reproduction

1. **Integration validation:** Task 13's read-only asset/binding/restart tests, Task 14's four measurement tests and this evidence/report. Production behavior unchanged.
2. **Console source anchors:** the optional producer field and four single-property section edits, reviewed by the named owners. Include the real-console review fixture in the project suite after acceptance.
3. **Owner policy PRs:** separate Scout, Guard and Flanker changes from their existing handoffs; reconcile shared/console hunks and rerun the latest unified suite after each integration.
4. **Measured performance/tuning follow-up:** body/route/decision allocation work or presentation readability changes only after standalone/human evidence. Keep combat-number adjustments separate and justified.

Reproduce the full current unified project without patching source assets:

```sh
python3 docs/evidence/progressive-difficulty/reproduce.py
```

Use `--scenarios-only` for the project scenario/measurement subset or `--prepare-only` to inspect a disposable current-source project. Optional native-motion windows:

```sh
python3 docs/evidence/progressive-difficulty/run_encounters.py
```

Optional unapproved console proposal:

```sh
python3 docs/evidence/progressive-difficulty/rehearse_console_anchors.py
```

Each prints its separate project/results path. Fresh imports can take several minutes. No script commits, pushes, merges, regenerates scenes or writes the proposal into the source checkout.

## 10. GV and IS viva explanation

**GV:** show the authored four rooms, original drone/door models, preserved model colours and authored height. Explain the stationary navigation/collision root, separate model pose owner, rotor child pivots and reusable muzzle/tracer/hit effects. Awareness uses restrained material property blocks; actual motor movement drives pitch/rotors/banking. Fatal damage takes priority, releases the visual into a gravity-driven wreck, then impact/settling ends rotor motion. Demonstrate the four automatic sliding gates: each waits for its own drone and console, and releases physical collision/carving only after the animation. Do not describe the old hinged-gate plan as current behavior.

**IS:** explain the shared evidence contract: genuine visual observations and bounded velocity history are separate from uncertain, expiring sound locations. Awareness determines urgency/speed; each student policy owns its own decisions. A* plus native and full-body validation respect gates and geometry. Interceptor scores reachable forward junctions using confidence and conservative speed/acceleration-aware arrival times, retains a commitment, invalidates reversal/missed candidates, and falls back on evidence expiry. Show the actual reversal/search trace and the mobile diagnostic's rejected candidates. Explain that a complete ground path can still be ineffective for the raised drone body—the console/post audits and Flanker obstruction trace make that distinction concrete.

Keep the official roles and boundaries: Pamudi—World Builder/Checkpoint Scout; Nimsara—Systems Engineer/Storage Flanker and combat; Nimthara—Core Developer/Server Guard, original models and mission UI; Asmadala—Agent Controller/Control Interceptor, navigation and movement. Present the pending policy proposals as pending work. The evidence supports working shared integration and a preserved advanced Interceptor; final four-policy fairness and owner-approved recovery integration remain acceptance work.
