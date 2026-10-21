# Shared drone hearing — Task 5

Active worktree: `/Users/dev/Desktop/game/Shift-Four`

Branch: `feature/progressive-drone-intelligence-workspace`

Base HEAD: `a523d7d953118cfbb154042b6304723b02ea0e39`, plus verified uncommitted Task 1–4 integration.

Task 1–5 edits are now applied in this active checkout and visible in Source Control. The original verification runs used the separate `Shift-Four-Progressive` worktree. [Fresh focused verification](evidence/drone-hearing/active-workspace-verification/README.md) confirms the active source matches, with 13/13 Edit Mode and 14/14 Play Mode cases passing. The existing staged index is preserved.

## API and integration

`DroneBrain.Hearing` exposes a runtime `DroneHearingSensor` on every Scout, Guard, Interceptor and Flanker. DroneBrain.Start adds the component if absent, then configures it with the current GameSession graph and the difficulty profile's `hearingSensitivity`. No prefab or scene change is required. Profile-less custom brains use sensitivity 1; an unavailable graph disables receipt conservatively. Reconfigure the sensor explicitly if changing its graph or sensitivity at runtime.

```csharp
if (Hearing != null && Hearing.TryGetLatest(out SoundObservation sound))
{
    // This is an uncertain, reachable disturbance, never visual confirmation.
    Vector3 investigationPoint = sound.Position;
}
```

- `DroneHearingSensor.Configure(WaypointGraph graph, float hearingSensitivity)`: validates Inspector settings and clears observations. Sensitivity clamps to 0–4; zero disables receipt. Does not reset query budgets or replay protection.
- `TryGetLatest(out SoundObservation observation)`: newest fresh observation, or false. Reading never refreshes time or accesses the sound source. Same-timestamp observations prefer the latest accepted emission.
- `TryGetLatestAt(float now, out SoundObservation observation)` and `ObserveAt(NoiseEvent noise, float now)`: explicit-clock simulation/test entry points. Production subscription uses Time.time. Query budgets always use the live engine clock so replay-clock changes cannot bypass them.
- `SoundObservation`: immutable Position, Category, Strength, UncertaintyRadius, EmittedAt, ExpiresAt. No UnityEngine.Object/source reference, player target, visual confidence, velocity or shooting authorization is stored.
- `HearingModel.Strength(...)` and `Estimate(position, radius, seed)`: pure attenuation/uncertainty helpers. Estimates do not change global UnityEngine.Random state.

The sensor subscribes only to typed `SoundEvents.NoiseEmitted`. OnDisable removes the listener and clears observations; OnDestroy also removes it safely. DroneBrain disables hearing on death. SoundEvents' existing scene cleanup handles scene destruction and restarts. No sensor Update, global actor search or global event-processing queue is introduced.

Scout's existing exact-position `ShotFired` listener was the one legacy bypass: it ignored walls, locked gates and sound uncertainty. Its small sound-memory adapter now consumes Hearing.TryGetLatest. The patrol, last-visual-sighting search, visible chase, scores and movement handoff remain intact. It investigates supported noise categories at its existing investigation score of 45. Guard, Interceptor and Flanker policies are unchanged and can consume the same API in subsequent tasks; they do not yet choose sound investigations automatically.

## Detection and isolation

1. Reject invalid, future-dated, old, replayed, disabled/dead and out-of-range events without physics work.
2. Apply the per-sensor frame/second query budget; overload drops events rather than creating delayed knowledge.
3. Check two small endpoint overlaps and one ray using fixed nonallocating buffers. Triggers, the sensor's own colliders, actor Health colliders and console colliders do not act as acoustic walls. Other solid colliders attenuate sound multiplicatively. An intersected closed LabDoor suppresses sound completely, including while UNLOCKING. Full buffers and embedded solid endpoints reject conservatively.
4. Attach the emission's X/Z to the current planar navigation level, permitting at most 0.05 m horizontal projection. Require a complete native NavMesh path and an existing gate-aware WaypointGraph route to that position. Thus native connectivity alone cannot bypass a logically locked gate, and graph flags alone cannot bypass physical closed gates/carving. Disconnected/sealed areas reject receipt even if wall transmission is configured to 1.
5. Produce a deterministic, per-sensor uncertain X/Z estimate. Try at most three offsets, projecting and validating each against NavMesh and graph connectivity. No exact-position fallback is used when uncertain positions are inaccessible. Navigation still owns actual route following and final physical clearance.

Received strength is:

`clamp01(intensity * sensitivity * transmission - distance / referenceRange)`

Detection requires strength >= threshold. Unlike multiplying the distance-attenuated result, this approximation reduces the effective audible radius when a wall attenuates the source. Strength is acoustic data, not visual confidence or suspicion/combat authorization.

| Inspector setting | Default | Validation |
| --- | --- | --- |
| Full-strength reference range | 26 m | 1–100 m |
| Detection threshold | 0.01 | 0.001–0.5 |
| Per-solid-collider transmission | 0.25 | 0–1 |
| Memory lifetime | 5 s from emission | 0.1–10 s |
| Maximum incoming event age | 0.25 s | 0.01–1 s |
| Minimum uncertainty | 0.6 m | 0–4 m |
| Additional uncertainty | 1.4 m | 0–4 m |
| Maximum evaluations/frame | 2 per sensor | 1–8 |
| Maximum evaluations/second | 8 per sensor | 1–16 |
| Obstacle layer mask | All layers | Keep solid walls and security gates included |

Nonfinite floating configuration values revert to defaults. Uncertainty is `minimumUncertainty + additionalUncertainty * (1 - strength)`; offset magnitude is 50–95% of that radius. Estimate source/goal remain within the same reachable navigation region. Zero uncertainty is supported for tuning and isolated tests.

At default sensitivity with no obstacles, Task 4's intensities give approximate threshold radii of **4.94 m walking, 11.44 m future sprint, 25.74 m gunshot and 10.14 m console**. Sprint gameplay remains absent. These are computed cutoffs for this approximation, not AudioSource volumes or claims about every scene's obstacle behavior.

## Memory and critical constraints

Each sensor retains at most four value observations. Expiry is anchored to the event's emission timestamp, not receipt or polling time. Repeated payloads are rejected using 32 bounded value keys (position/category/intensity/emission timestamp, no source identity). Keys retain emission snapshots only for replay detection, never a live target. The bus itself still supports multiple same-frame events; exact same-payload events are intentionally deduplicated by hearing.

Expired observations are inaccessible. Any graph-version change invalidates earlier investigations, without extending their deadlines. This is conservative: opening an unrelated gate can invalidate an otherwise valid observation. Newly closed physical obstacles remain enforced by the motor and native navigation; consumers must continue using the existing motor rather than moving transforms directly.

Sound does not update DroneBrain.lastKnown, lastSeenAt, previous visual observations or observed velocity. DroneBrain.Update, CanSeePlayer, TryShoot and all ClearShot methods are unchanged. The existing fire-time line-of-sight, range, health, cooldown and gate/cover checks remain authoritative. Hearing alone cannot fire a shot.

## Limits and performance

This is a practical planar-lab approximation, not acoustic propagation, diffraction or material simulation. Source heights more than 3 m from the drone's root level reject conservatively; X/Z navigation attachment assumes the current single-level floor. Future multi-floor support needs an explicit floor/zone contract. Navigation agent type is the project's current default type; the sensor respects its owner's area mask if it has an agent.

A long wall can transmit a sufficiently loud reachable noise even if the shortest sound ray crosses it. Fully disconnected areas are rejected. A direct closed-gate intersection is suppressed even if another route might exist, intentionally conservative. Off-mesh emitters, embedded surface impacts, saturated buffers, inaccessible uncertain estimates and overload can produce false negatives. Actor bodies/console housings do not attenuate sound. Keep the obstacle mask inclusive of real walls/gates.

Per evaluated event: at most **3 physics calls** (two 8-collider overlap buffers and one 32-hit ray buffer); at most **4 direct native path requests and 4 delegated graph route requests** (emission plus up to three estimates). Existing graph route requests perform their own local connector/NavMesh work and allocate route containers. Graphs above 32 nodes reject conservatively to bound this existing API's search/connector work. Current MainLab has five nodes. There are no per-frame hearing physics calls when no new eligible event arrives.

**Unity 6000.6.4f1 results: 225/225 Edit Mode, 14/14 focused Play Mode, and 122/122 full Play Mode tests passed; no failures or skips.** The full suites contain 347 unique cases (27 new); the focused run is a subset. The fresh focused verification recorded 2 evaluations, 6 physics queries, 998 budget drops, 16 GC.Alloc events and approximately 2.02 ms including measurement overhead for a 1,000-report burst. The earlier full-suite sample was approximately 0.82 ms; timing varies by run. Four observations remain the fixed memory capacity. See [test evidence](evidence/drone-hearing/README.md) for raw outcomes and measured burst behavior. Allocation counts cover existing route allocation plus dispatch; the implementation does not claim an allocation-free accepted detection path.

## Exact files

Modified existing files:

- `Assets/Scripts/AI/Core/DroneBrain.cs`: sensor initialization/property and disable-on-death only.
- `Assets/Scripts/AI/Agents/ScoutBrain.cs`: replace legacy exact-shot subscription/memory with shared sound-observation consumption; earlier user approval covered this adapter.
- `Assets/Tests/PlayMode/SoundEventsPlayModeTests.cs`: restart regression now verifies four unique typed sensor subscriptions and zero legacy Scout subscriptions, including release of all old sensors/observers over three actual restarts. Legacy gunshot API/payload tests are retained.

New C# files, each with a new .meta:

- `Assets/Scripts/AI/Perception/HearingSettings.cs`
- `Assets/Scripts/AI/Perception/SoundObservation.cs`
- `Assets/Scripts/AI/Perception/DroneHearingSensor.cs`
- `Assets/Tests/EditMode/Movement/HearingModelEditModeTests.cs`
- `Assets/Tests/PlayMode/DroneHearingPlayModeTests.cs`

The Perception folder has a new .meta. No edits to SoundEvents, Task 4 producers, Health, gate code, NavMesh assets, scenes, prefabs, models, input settings or the other three individual policies. No commit or push.
