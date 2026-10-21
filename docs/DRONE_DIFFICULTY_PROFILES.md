# Task 2 — data-driven drone difficulty profiles

Implemented in `/Users/dev/Desktop/game/Shift-Four-Progressive`, branch `feature/progressive-drone-intelligence`, continuing the verified Task 1 snapshot over `a523d7d953118cfbb154042b6304723b02ea0e39`. No commits or pushes.

## Configuration

The four Inspector-editable ScriptableObjects live in `Assets/Resources/DroneDifficulty/`. Movement speeds are metres per second.

| Profile | Maximum HP | Patrol | Investigation | Chase |
| --- | ---: | ---: | ---: | ---: |
| Scout | 100 | 1.8 | 2.2 | 2.6 |
| Guard | 150 | 2.0 | 2.5 | 3.0 |
| Interceptor | 125 | 2.5 | 3.1 | 3.7 |
| Flanker | 150 | 2.8 | 3.5 | 4.2 |

All four initially share these additional values:

| Field | Value / units |
| --- | --- |
| Acceleration | 8 m/s² |
| Angular speed | 120 degrees/s |
| Hearing sensitivity | 1; relative multiplier, 0 disables future hearing |
| Vision range | 15 m |
| Suspicious threshold | 0.25 |
| Investigate threshold | 0.60 |
| Combat threshold | 0.85 |
| Search duration | 5 s |

**Scope:** Task 2 binds the profiles and initializes maximum/current drone health. Movement, hearing, vision, suspicion and search values are configuration for later tasks. They do not change decisions or movement modes yet. Existing runtime cruise remains **3.5 m/s**; NavMesh acceleration remains 8, angular speed 120, and motor turn responsiveness 7. No sound detection or policy changes were started.

## Classes and integration

- `DroneDifficultySettings` is a serializable value type. `Validated()` returns a normalized copy. `DroneArchetype` defines the four supported default lookups.
- `DroneDifficultyProfile` is a ScriptableObject containing those settings. `Settings` returns a validated value copy; callers cannot mutate shared asset data by changing that copy. `OnValidate` normalizes Inspector edits. Create additional assets with **Assets → Create → Shift Four → Drone Difficulty Profile**.
- `DroneBrain` has an optional serialized `difficultyProfile` override, plus read-only `DifficultyProfile` and `DifficultySettings` properties. In its existing `Start`, it resolves a missing reference by actual drone type and calls `InitializeDifficulty()`. Existing names/labels and prefab GUIDs do not determine difficulty.
- `Health.TryInitializeMaximum(float)` provides a guarded, one-time initialization contract. `DroneBrain` uses this API instead of calling `Health.Configure` for a profile refresh.

Four resources are small enough for a direct `Resources.Load` fallback; no service registry, editor installer, global scene scanning or update-loop binding is needed. The Resources path also includes these defaults in a normal player build. Custom brain types without an assigned override retain their legacy configuration.

No prefab or MainLab changes were necessary. Their serialized drone HP remains 75; successful startup profile binding supplies the runtime values above. Shared prefab/scene assignments for future per-instance overrides still belong to their owners. Current assets can be tuned directly in the Project Inspector without changing those existing shared assets.

## Health lifecycle

1. Health's `Awake` initializes the authored value unless the instance was already explicitly initialized or damaged while inactive. This guard fixes a reproduced activation-time heal. During normal drone startup, the shared brain resolves its profile and `TryInitializeMaximum` replaces both maximum and current health once, before accepted damage.
2. A profile reapplication returns `false` after the first successful initialization, even if a different maximum is requested. It changes neither current nor maximum HP and emits no damage/death notifications.
3. Any accepted damage latches the current life before callbacks. Late first binding cannot erase that damage, including after full natural healing or during a fatal callback. Fatal damage cannot be undone by reapplying a profile.
4. The existing `Health.Configure(max, playerRecovery)` remains an explicit full-health reset API, for backward compatibility. It clears the initialization/damage guards for a new life and retains its existing recovery/reset behavior. It must not be used for routine profile refreshes.
5. The supported game restart reloads MainLab. New Health/brain instances bind their default profiles again, while the existing scene lifecycle removes wrecks and subscriptions. This task does not add pooled resurrection of dead drones; restarting individual destroyed models/motors requires their own full lifecycle.

Damage amounts, accepted-damage records, recovery delay/rate, damage/death notification order, player HP, walking speed, gun damage and weapon intervals retain their existing behavior. Normal damage/healing never changes a profile asset.

## Validation rules

- All non-finite values (NaN, positive/negative infinity) recover to the corresponding safe starting value.
- Maximum HP is at least 1; patrol speed, acceleration, vision range and search duration are at least 0.1; angular speed is at least 1 degree/s.
- Chase speed is at least patrol speed. Investigation speed is clamped between them.
- Hearing sensitivity is clamped to 0–4.
- Suspicious/investigate/combat thresholds remain in 0–1 with strict ordering and a 0.01 minimum gap. Suspicious is capped at 0.98 and investigate at 0.99 so combat can remain greater.
- The initialization API independently rejects invalid/non-finite maximum HP without consuming the one-time initialization opportunity.

The safe starting values for newly created assets are defined once in the settings type. Per-drone tuning lives in the four authored assets. There are no new health/speed constants in individual policy scripts.

## Exact production delta from Task 1

Modified:

- `Assets/Scripts/Gameplay/Health.cs`
- `Assets/Scripts/AI/Core/DroneBrain.cs`

Created:

- `Assets/Scripts/AI/Difficulty/DroneDifficultySettings.cs` and `.meta`
- `Assets/Scripts/AI/Difficulty/DroneDifficultyProfile.cs` and `.meta`
- `Assets/Scripts/AI/Difficulty.meta`
- `Assets/Resources.meta`
- `Assets/Resources/DroneDifficulty.meta`
- `Assets/Resources/DroneDifficulty/Scout.asset` and `.meta`
- `Assets/Resources/DroneDifficulty/Guard.asset` and `.meta`
- `Assets/Resources/DroneDifficulty/Interceptor.asset` and `.meta`
- `Assets/Resources/DroneDifficulty/Flanker.asset` and `.meta`

Created tests:

- `Assets/Tests/EditMode/Movement/DroneDifficultyEditModeTests.cs` and `.meta` (profile/validation cases and Health contract cases)
- `Assets/Tests/PlayMode/DroneDifficultyPlayModeTests.cs` and `.meta` (real runtime binding, overrides, healing, restart)

This document and `docs/evidence/difficulty-profiles/` are new documentation/evidence. Their [exact Task 2 change inventory](evidence/difficulty-profiles/changed-files.json) distinguishes these changes from the pre-existing uncommitted Task 1 overlay.

All four individual policies, prediction, Control junctions, SoundEvents, player/controller, console/gates, movement, collision, animation scripts, existing tests, existing `.meta` GUIDs, prefab/scene/model/material assets, and package/test-assembly definitions remain unchanged from Task 1.

## Verification

See the [executed test results and implementation notes](evidence/difficulty-profiles/README.md), raw XML/logs, per-case matrix, source hashes and protected-asset integrity record. Test coverage includes all four authored defaults, invalid/negative/non-finite values, ordering/idempotence, Inspector/runtime validation, isolated overrides, fresh initialization, no healing on reapplication, one death per life, normal recovery, and actual MainLab loss/restart. Existing combat and animation/destruction fixtures are retained without weakening their assertions.
