# Task 3 — shared noise-event API

Implemented in `/Users/dev/Desktop/game/Shift-Four-Progressive`, branch `feature/progressive-drone-intelligence`, continuing the verified Task 2 snapshot. This extends the existing `SoundEvents` entry point; it does not introduce a second sound manager, hearing behavior, producer polling, or scene component.

## Final API

```csharp
namespace ShiftFour
{
    public enum NoiseCategory
    {
        Footstep = 0, Sprint = 1, Gunshot = 2, ConsoleInteraction = 3, Impact = 4
    }

    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        public readonly NoiseCategory Category;
        public readonly float Intensity;
        public readonly float Timestamp;
        public readonly UnityEngine.Object Source;

        public NoiseEvent(Vector3 position, NoiseCategory category, float intensity,
            float timestamp, UnityEngine.Object source = null);
        public bool IsValid { get; }
    }

    public static class SoundEvents
    {
        public static event Action<NoiseEvent> NoiseEmitted;
        public static event Action<Vector3> ShotFired; // Existing compatibility API.

        public static float DefaultIntensity(NoiseCategory category);
        public static void ReportShot(Vector3 position); // Original exact signature retained.
        public static bool ReportNoise(Vector3 position, NoiseCategory category, float intensity,
            UnityEngine.Object source = null);
        public static bool ReportNoise(NoiseEvent noise);
    }
}
```

The declarations above summarize the signatures; the source files contain their implementations. `ReportNoise` returns true for an accepted dispatch, including when there are no listeners. It returns false for silent/invalid input. `ReportShot` retains its existing void return type and is a one-call adapter into that same dispatcher.

| Category | Default relative intensity |
| --- | ---: |
| Footstep | 0.20 |
| Sprint | 0.45 |
| Gunshot | 1.00 |
| ConsoleInteraction | 0.40 |
| Impact | 0.30 |

Intensity is normalized **source strength**, not metres, decibels or visual confidence. The bus does not perform distance attenuation, occlusion, hearing-range calculations or investigation. Task 4 can obtain these central defaults with `DefaultIntensity(category)` and expose producer tuning as needed.

## Payload and dispatch rules

- Position is a value snapshot at emission. Source is optional identity metadata; moving/destroying the source does not update the stored position. The bus stores no last event, player target or event history.
- The live-report overload captures scaled gameplay `Time.time`, matching the existing Scout memory clock. The explicit-record overload uses the supplied timestamp unchanged for deterministic simulation/replay tests.
- Positions must have finite components; intensity must be finite and greater than 0, at most 1; timestamp must be finite and nonnegative; category must be one of the five values. Zero strength is silent. Invalid reports produce no callbacks or routine logs. Explicit future/old timestamps are allowed for simulation; consumers must enforce their own evidence-age window.
- Dispatch is synchronous on Unity's main thread. Modern listeners run first, then legacy listeners only for Gunshot. Each channel follows registration order.
- Both listener arrays are captured at the beginning of one logical event. Registration/unregistration inside a callback affects the next event. Destroyed Unity targets are skipped immediately for safety. Nested reports run synchronously, depth first.
- Duplicate registrations of the same callback are idempotent and preserve its original order. Different methods on the same receiver remain different callbacks. Multicast additions/removals are supported; null additions/removals and repeated removal are safe.
- A faulting callback is removed and its exception logged once per registration. Later listeners and the legacy channel still receive the event. Explicitly re-registering a faulty callback can retry it and produce another exception report.
- Each accepted gunshot makes **one** modern notification per modern listener and **one** compatibility notification per legacy listener. There is no bridge back from ShotFired into ReportNoise. Two explicit reports at the same position/timestamp remain two disturbances, so legitimate simultaneous sources are not dropped.

## Lifecycle and allocation behavior

Runtime subsystem registration clears both channels before a new play session, including the domain-reload-disabled initialization path. The scene-unloaded hook is bound idempotently. It removes destroyed Unity targets and bound component/game-object listeners from the unloaded scene, preserving live listeners in other scenes and DontDestroyOnLoad objects.

Scene listeners should still pair **OnEnable +=** with **OnDisable -=** using the same named instance method. This promptly releases references and avoids callbacks while disabled. Static methods and ordinary CLR closures are session-wide registrations; remove them explicitly if their lifetime is shorter. Avoid anonymous closures that capture scene objects when a named component handler suffices. The bus cannot infer scene ownership from an arbitrary CLR closure.

Listener arrays are copied only when registration changes, stale listeners are pruned, or a faulting listener is removed. Normal dispatch uses the existing arrays and value payload; it creates no invocation-list array or formatted log. The focused allocation test measures the warmed explicit-record and legacy gunshot paths with allocation-free observers; this does not claim allocation-free registration, exception handling, arbitrary listener code or the whole game. Results are in the [test evidence](evidence/noise-events/README.md).

## Existing integration preserved

`PlayerController.Shoot()` still calls only `SoundEvents.ReportShot(transform.position)` once after setting its existing cooldown. That now emits the enriched Gunshot record as well as the original vector callback; its optional Source is null. No producer source, damage amount, fire interval or hit-validation code changed.

Scout still uses its existing ShotFired OnEnable/OnDisable subscription and gunshot response. No new drone has a hearing response. `DroneBrain.ShotFired` is a separate accepted-combat/presentation event and remains unchanged, along with its animation consumers.

GameSession restart remains the existing scene reload. The event bus cleans its own listener registry through scene lifecycle events; GameSession, MainLab and prefabs need no edits.

## Exact integration methods for Task 4

Task 4 should add producer calls at accepted gameplay actions. The following are integration examples, not code added to the producers by Task 3.

Walking, after actual grounded horizontal movement and a bounded cadence check:

```csharp
SoundEvents.ReportNoise(transform.position, NoiseCategory.Footstep,
    SoundEvents.DefaultIntensity(NoiseCategory.Footstep), this);
```

Sprinting, only when an implemented sprint mode is active and the player actually moves:

```csharp
SoundEvents.ReportNoise(transform.position, NoiseCategory.Sprint,
    SoundEvents.DefaultIntensity(NoiseCategory.Sprint), this);
```

Use one movement category per step, avoid stationary events, and rate-limit by actual distance/time. No sprint control was added in Task 3.

Gunshots already emit correctly through ReportShot. To attach player identity in Task 4, **replace** that one existing call with this one call:

```csharp
SoundEvents.ReportNoise(transform.position, NoiseCategory.Gunshot,
    SoundEvents.DefaultIntensity(NoiseCategory.Gunshot), this);
```

Do not also call ReportShot for that same shot: the typed gunshot automatically reaches legacy listeners. Keep the existing cooldown, damage and physical ray validation unchanged.

Console interaction, only in the accepted first activation branch after its existing guards:

```csharp
SoundEvents.ReportNoise(transform.position, NoiseCategory.ConsoleInteraction,
    SoundEvents.DefaultIntensity(NoiseCategory.ConsoleInteraction), this);
```

Optional accepted impact, once per meaningful collision/impact rather than every contact frame:

```csharp
SoundEvents.ReportNoise(contactPoint, NoiseCategory.Impact,
    SoundEvents.DefaultIntensity(NoiseCategory.Impact), this);
```

A future scene-owned consumer uses the typed event:

```csharp
private void OnEnable() => SoundEvents.NoiseEmitted += OnNoise;
private void OnDisable() => SoundEvents.NoiseEmitted -= OnNoise;

private void OnNoise(NoiseEvent noise)
{
    float age = Time.time - noise.Timestamp;
    // Enforce a nonnegative, bounded age; filter own/irrelevant sources; apply hearing
    // sensitivity, attenuation/gate rules and uncertainty before storing bounded memory.
    // Use noise.Position as the emission snapshot. Source is identity, not a live target.
}
```

Migrating Scout to a shared hearing consumer requires coordinating its existing legacy subscription so two independent memories do not process the same gunshot. A migrated consumer should subscribe to the modern event only. Sound-derived evidence must remain separate from visual observations and must never authorize shooting; retain existing visual/fire-time validation. No such migration or hearing behavior is implemented in Task 3.

## Exact Task 3 source delta

Modified production file:

- `Assets/Scripts/Gameplay/SoundEvents.cs`

Created production files:

- `Assets/Scripts/Gameplay/NoiseEvent.cs`
- `Assets/Scripts/Gameplay/NoiseEvent.cs.meta`

Created test files and their `.meta` files:

- `Assets/Tests/EditMode/Movement/SoundEventsEditModeTests.cs`
- `Assets/Tests/PlayMode/SoundEventsPlayModeTests.cs`
- `Assets/Tests/PlayMode/NoiseListenerProbe.cs`

This document and `docs/evidence/noise-events/` are new documentation/evidence. The [change inventory](evidence/noise-events/changed-files.json) distinguishes Task 3 from the pre-existing Task 1/2 uncommitted overlay. Policies, player/controller, weapon damage/cooldowns, difficulty profiles, NavMesh, movement/collision, animation, scenes, prefabs, existing test sources and GUIDs remain unchanged. No commit or push.
