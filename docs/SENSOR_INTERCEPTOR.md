# Task 10 — Sensor-aware predictive Interceptor

The active Interceptor now uses shared hearing/awareness alongside its existing advanced visual prediction and Control tactical routes. This is an applied implementation in the active checkout, separate from the pending Scout/Guard proposal packages. No other agent policy, shared brain/motor/body, difficulty asset, prefab, scene, gate or baked navigation change is included. This task issues no commit, staging or push commands.

## Preserved prediction

`InterceptorPolicy` still owns bounded genuine sightings, velocity smoothing, observed stop/reversal/acceleration checks, confidence decay, forward candidate scoring, commitment, switching hysteresis, failed-junction cooldown, prediction deadlines, last-known pursuit, bounded local search and return-home fallback. Its original normalized scoring terms and weights are unchanged:

`30 × timing + 20 × forward + 20 × positioning + 25 × confidence + 8 × stability`

Default history stays bounded to 12 observations over 3 seconds. Only `visible == true` can call `Observe`; an invisible position argument is discarded before reaching prediction. Reversals and stops still invalidate stale forward momentum. Hidden/repeated ticks do not extend arrival windows. Failed arrival candidates remain unavailable through the existing cooldown and until genuinely newer visual evidence exists.

`ControlTacticalNavigation`, the six local junction offsets, ring arcs, route commitment/following and `ControlTacticalRouteValidator` are unchanged. The primary prediction engine still evaluates both global gate-aware nodes and reachable local tactical destinations. It estimates the full validated route before the motor receives the next safe tactical stop. Complete native paths, raised-body clearance, dynamic occupancy and gate checks remain authoritative.

## Motion-aware arrival time

The live adapter now calls `StepWithMotion`, supplying the current NavMesh speed cap, the drone's own accepted `DroneMotor.ActualVelocity`, and the current agent acceleration. A disabled motor supplies zero current velocity; its last cached motion sample must not survive a frozen/warped fixture or a disabled movement phase as active momentum.

The estimator uses acceleration/cruise/braking profiles. Short legs use a triangular speed profile; longer legs use a capped trapezoid. It projects existing velocity onto the first route direction and adds time/distance to remove opposing or lateral motion. Each route corner is treated conservatively as a possible stop. This includes actual path lengths and corner costs rather than straight-line distance or instantaneous cruising.

Candidate timing rejection, timing score, player ETA, prediction horizon, confidence, commitment and switching thresholds continue through the existing algorithm. An actively justified interception retains chase urgency through brief visual loss; sound investigation uses the investigation cap. Invalid motion inputs reject optimistic ETA rather than creating a feasible candidate.

The explicit `Step`/`StepWithRoutes` simulation APIs retain their prior constant-speed contract. The new `StepWithMotion` wrapper uses the same prediction engine and clears its temporary motion/route context in `finally`. The live brain always chooses the new API. The standalone legacy `ControlTacticalNavigation.TryDecide` API is not the live predictor; the brain continues to use `RouteTo`/`Follow` under `InterceptorPolicy` candidate selection.

## Separate hearing decisions

Every decision ticks the visual predictor first, including during a sound action, so visual history, confidence, arrival deadlines and candidate failures continue aging normally.

A fresh approximate sound may select investigation or sound-area search only when:

- shared awareness is enabled and in Investigate or Search;
- the evidence is fresh, received strength is at least 0.1, and its timestamp is newer than the last visual sighting;
- the visual predictor is no longer executing a justified intercept or last-known pursuit;
- the ordinary tactical validator accepts the goal.

The adapter first tries the reported uncertain point. If it overlaps furniture, at most eight nearby directions are considered within `min(UncertaintyRadius, 1.75 m)` of that point. Every alternative must pass the same complete native path, gate-aware route and body-clearance checks. The selected goal is retained for that observation and revalidated; reads cannot renew its timestamp. On expiry, normal visual search/home fallback resumes.

Sound never calls `Observe`, supplies velocity, sets visual confidence, renews a visual arrival deadline, or authorizes firing. It cannot manufacture player history when there has never been a sighting. No source Transform is retained. Existing `DroneBrain` visual and fire-time cover/range/cooldown checks remain unchanged.

The inherited shared brain/motor still pass a player component through their existing combat interface. The Interceptor discards hidden position/distance values; the motor's invisible branch follows only the chosen policy destination. Physical occupancy may reject a location containing a collider, which remains normal collision protection rather than a hidden target observation.

## Configuration and diagnostics

The unchanged default Interceptor resource binds **125 HP**, **2.5 m/s patrol**, **3.1 m/s investigation**, and **3.7 m/s chase**, with acceleration 8 m/s² and angular speed 120°/s. The existing motor smooths cap changes and supplies accepted motion to animation. “Hard” is the requested encounter tuning target, not a new enum or a change to weapon range/damage/cooldown.

Existing optional `showControlTactics` gizmos keep candidate scores, player/drone ETAs, confidence, predicted trajectory and tactical arcs. They now also show the current ETA cap/acceleration, a distinct orange sound goal and `SensorDecision` explanation. `IsInvestigatingSound` suppresses stale active-action reporting after death. `HeardDestination` remains separate from `Policy.LastObserved`.

Production delta:

- `Assets/Scripts/AI/Agents/InterceptorBrain.cs`: motion timing adapter, prioritized sound fallback and diagnostics.
- `Assets/Scripts/AI/Agents/InterceptorPolicy.cs`: motion-aware timing entrypoint and pure ETA helpers; existing observation/scoring/commitment/failure logic retained.

New tests, with stable `.meta` GUIDs:

- `Assets/Tests/EditMode/Movement/InterceptorMotionTimingEditModeTests.cs`.
- `Assets/Tests/PlayMode/InterceptorSensorIntegrationPlayModeTests.cs`.

No existing test assertions are removed or relaxed.

## Verification and evidence

See [the evidence package](evidence/sensor-interceptor/README.md) for actual XML/logs, source hashes, integrity checks and exact commands. [Before/after scenarios](evidence/sensor-interceptor/SCENARIOS.md) include the saved original implementation, paired real MainLab sound runs, existing rendered prediction sequences, and an identical-input numerical ETA comparison. Tests use Unity **6000.6.4f1** in a disposable copy of the active source so verification does not rewrite scene/project assets in the active checkout.

Reproduce tests from the repository root:

```sh
python3 docs/evidence/sensor-interceptor/reproduce.py
```

The script creates a fresh disposable project. Use `--scenarios-only` for paired baseline/candidate sound evidence, or `--prepare-only` to inspect the current implementation in Unity. Select the Interceptor and enable its existing Control diagnostics to inspect candidate explanations.

## Remaining limitations

- Confidence is a heuristic, not a calibrated probability or proof of the player's intended destination.
- Treating every corner as a stop can reject interceptions the real motor could complete. The current cap is held fixed within each estimate and refreshed each decision; future cap increases are not assumed.
- Collision avoidance, dynamic obstructions, pending paths and controller timing can still delay real arrival. Existing route revalidation, missed-window detection, cooldown and bounded search provide recovery; ETA is not a capture guarantee.
- Hearing retains the approved planar navigation attachment and uncertainty rules. A disturbance with no validated reachable approach produces no investigation route.
- Runtime difficulty edits follow the existing startup-binding contract. No mid-life profile rebinding or weapon tuning was introduced.
- Rendered scenarios and controlled comparisons are reproducible evidence of decisions and movement, not a measured full-mission capture-rate or general speedup claim.
