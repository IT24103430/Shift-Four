# Shared drone awareness — Task 6

Active project: `/Users/dev/Desktop/game/Shift-Four`

Branch: `feature/progressive-drone-intelligence-workspace`

Task 6 starts from `a4ff4fdebcd3a7c461d416dfcc2c565926b37b05` and the existing uncommitted hearing handoff/evidence changes. This implementation is in the active project, with a matching verification copy in `Shift-Four-Progressive` because the active project's Unity editor is running.

## Final interface

Every DroneBrain exposes a plain `DroneAwareness` service through `Awareness`. It is created after difficulty initialization at Start and updated at the existing 0.3 s decision cadence, before the individual policy is called. No extra MonoBehaviour, Update loop, event subscription or physics query is introduced.

```csharp
DroneAwarenessState state = Awareness.State;
float suspicion = Awareness.Suspicion;

// Never combine these into an exact, continuously tracked hidden target.
if (Awareness.TryGetLastHeard(Time.time, out SoundObservation sound))
{
    Vector3 approximateInvestigationPoint = sound.Position;
    // A later policy integration must still use the existing route/motor checks.
}
if (Awareness.TryGetLastSeen(Time.time, out Vector3 seenPosition))
{
    // A bounded visual memory snapshot; this does not prove current visibility.
}
```

- `DroneAwareness(AwarenessSettings config, DroneDifficultySettings difficulty)`: copies validated settings/thresholds. Does not mutate difficulty assets or hold a reference to their mutable data.
- `Step(now, visuallyConfirmed, visiblePosition, hasSound, sound, atPost)`: explicit-clock simulation interface. DroneBrain already supplies this; individual policies should consume its outputs rather than feed it again. `hasSound` means the hearing sensor's latest *currently valid memory snapshot*, not just a new-event flag. Missing/invalidated hearing clears goal availability. The position argument is ignored unless vision is confirmed and the position is finite.
- `State`, `Suspicion`, `IsEnabled`, `HasVisualConfirmation`: current alertness, bounded suspicion, lifecycle status and last decision's visual-confirmation flag.
- `TryGetLastSeen(now, out Vector3)` and `TryGetLastHeard(now, out SoundObservation)`: separate, lifetime-checked evidence access. The auditory record remains approximate and source-free.
- `LastSeenAt`, `LastHeardAt`, `StateSince`, `Transitions`: diagnostics. Unobserved timestamps and the initial untransitioned Patrol start use negative infinity sentinels.
- `Disable()`: immediate latched shutdown. Clears evidence and visual confirmation and prevents all further processing; it does not introduce a seventh tactical state. The last State remains available for diagnostics, so consumers must honor IsEnabled. A new life/restart gets a fresh service.

There is intentionally no `CanShoot`, attack command, player Transform, combined target, route request or movement command in this service. HasVisualConfirmation describes the last processed decision, not fire-time authorization. Existing TryShoot/CanSeePlayer/ClearShot checks remain authoritative.

## States and transitions

| State | Normal behavior and exits |
| --- | --- |
| Patrol | New active auditory evidence accumulates suspicion. Crossing the suspicious threshold enters Suspicious. |
| Suspicious | Reliable sound plus sufficient accumulated suspicion enters Investigate after dwell. Missing/uncertain evidence or suspicion below the exit band enters bounded Search. |
| Investigate | Refreshing the same observation does not extend the investigation window. Reaching its emission-anchored deadline, losing hearing validity, or sufficient decay enters Search. New genuine emissions can extend evidence within their own lifetimes. |
| Search | Search deadline is bounded by evidence expiry and configured search duration. New qualifying sound can interrupt it; observations received during dwell remain eligible afterward. Expiry enters ReturnToPost. |
| Combat | Entered immediately only from a valid visual confirmation. Losing sight clears HasVisualConfirmation immediately; after the visual-loss grace and dwell it enters Search. Sound cannot create visual evidence. |
| ReturnToPost | New evidence can interrupt recovery. Arrival feedback or the bounded return phase restores Patrol and retires the old sound so it cannot re-alert the drone. |

A confirmed visual observation can interrupt any enabled state immediately, including dwell. Other transitions respect minimum dwell. Death takes priority over both: DroneBrain.OnDied disables awareness synchronously before hearing shutdown, motor stop and the existing death handling. Already-dead actors are disabled at initialization; OnDestroy also disables any externally retained service reference.

ReturnToPost is an awareness/recovery hint. This task does not make policies move home. Its bounded timeout restores normal alertness even while existing tactics continue their own duties; future policies remain responsible for real home movement and arrival through DroneMotor. No teleport or navigation shortcut is used.

## Configuration

AwarenessSettings is serialized inline on DroneBrain; missing legacy scene data uses defaults without scene/prefab edits. Settings are captured at Start, not reapplied on every damage/heal/profile call. Nonfinite values fall back; finite values clamp in a validated copy.

| Setting | Default |
| --- | --- |
| Suspicion gain per new sound | strength × 1.25, capped at 1 overall |
| Suspicion decay | 0.15/s of simulation time |
| Minimum strength for investigation | 0.10 |
| Suspicious-state exit hysteresis | 0.10 below entry threshold, floored at zero |
| Minimum nonvisual transition dwell | 0.40 s |
| Investigation window | 2.50 s from emission, capped by sound expiry |
| Visual memory | 6.00 s |
| Visual-loss grace | 0.50 s |
| Maximum age for a newly accepted sound | 0.50 s |
| Bounded ReturnToPost phase | 4.00 s |
| Post arrival radius | 1.50 m |

Suspicious/investigate thresholds and search duration come from the validated difficulty data: current defaults **0.25 / 0.60 / 5 s**. The profile's combatThreshold never converts sound suspicion into Combat; this task uses the existing binary visual confirmation. Weak detectable evidence can build Suspicious without meeting the reliability condition for Investigate.

Only a strictly newer auditory emission timestamp adds suspicion. Polling, replays, older records and altered copies at the same timestamp cannot accumulate gain, replace the accepted goal or refresh its expiry. One latest record per timestamp is used conservatively; multiple simultaneous noise categories are not summed into instantaneous combat-like certainty. Long scheduling gaps decay accumulated suspicion and do not create a fresh search window for already-expired evidence.

Search may continue briefly with no available position, but remains bounded by the last valid evidence's deadline. When the next awareness decision receives invalid hearing (for example after a gate-version change), TryGetLastHeard immediately returns false. Knowledge is never copied into DroneBrain.lastKnown, lastSeenAt or observedVelocity from sound.

## Guidance for each individual agent

No individual policy is changed in Task 6. Existing tactics remain authoritative; the shared states are observations/advice, not an overriding controller.

| Agent | Future consumption guidance |
| --- | --- |
| Scout | Keep its patrol index, visible chase and last-sighting search. Later gate its sound-investigation option with Suspicious/Investigate and read only the approximate hearing record. Use Search/ReturnToPost as fallback signals; retain motor/path validation. |
| Guard | Preserve cover protection/travel/fire-visibility scoring and relocation. Later add an independent sound-investigation option using last-heard evidence. Hearing must not masquerade as a visible threat in cover scoring or trigger fire. |
| Interceptor | Keep InterceptorPolicy and Control tactical states intact. Prediction history, velocity, junction selection and interception continue to consume confirmed visual observations only. Shared alertness can later inform idle/search fallback; never feed sound into visual prediction history. |
| Flanker | Preserve reachable left/right side scoring and pursuit fallback. Later use approximate sound evidence for investigation/search duties; do not flank the hidden live player from auditory metadata. Recovery goals still go through existing graph/motor/body checks. |

All policies must check IsEnabled and obtain valid evidence with the appropriate accessor. Awareness.State == Combat is not permission to shoot. Use existing DroneBrain firing/LOS rules and normal A*/DroneMotor/DroneBody/gate restrictions for every action.

## Exact production changes

- Modified `Assets/Scripts/AI/Core/DroneBrain.cs`: serialized config, service property/startup, per-decision perception adapter, immediate death/destruction disable.
- New `Assets/Scripts/AI/Perception/AwarenessSettings.cs` and .meta.
- New `Assets/Scripts/AI/Perception/DroneAwareness.cs` and .meta.

New tests, each with .meta:

- `Assets/Tests/EditMode/Movement/DroneAwarenessEditModeTests.cs`: 26 deterministic state/config/evidence cases.
- `Assets/Tests/PlayMode/DroneAwarenessPlayModeTests.cs`: 6 real MainLab/lifecycle/performance cases.

The four individual policy files, InterceptorPolicy, ControlTacticalNavigation, DroneMotor, DroneBody, hearing, producers, Health, vision/firing methods, difficulty assets, MainLab and prefabs remain unchanged. No commit, push or index staging is performed.

## Validation

**Unity 6000.6.4f1: 251/251 Edit Mode and 128/128 full Play Mode cases passed, with no failures or skips.** All six focused Play Mode cases also passed. The full suites include 32 new cases. The four-model performance sample completed 4,000 ticks and phase transitions with zero GC.Alloc events (positive calibration verified), approximately 0.46 ms including measurement overhead.

See [raw results, exact delta and integrity checks](evidence/drone-awareness/README.md). Unit cases cover all required transitions, decay/hysteresis, expiry, new observations interrupting search, pending observations during dwell, replay suppression, distinct memories and death. MainLab cases use actual CanSeePlayer, typed hearing emission, real Health.Died and GameSession.Restart, plus unchanged policy-option comparisons. The service performance case advances four models through Combat → Search → ReturnToPost → Patrol for 4,000 ticks with a calibrated allocation recorder.

The active Unity editor is left running. Tests use the matching source copy in `Shift-Four-Progressive` and write evidence into this active project. Protected assets and the existing index are checked against the Task 6 starting snapshot; only incidental verification-worktree Unity settings defaults are restored after tests.
