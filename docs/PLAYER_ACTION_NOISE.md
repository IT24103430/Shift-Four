# Player movement and interaction noise — Task 4

Implementation worktree: `/Users/dev/Desktop/game/Shift-Four-Progressive`  
Branch: `feature/progressive-drone-intelligence`  
Base HEAD: `a523d7d953118cfbb154042b6304723b02ea0e39`, plus the verified, uncommitted Task 1–3 integration.

## Production changes

- `Assets/Scripts/Gameplay/PlayerController.cs`: the existing gravity and `CharacterController.Move` operation now live in private `MovePlayer(direction, deltaTime)`, called from the same Update location. After collision resolution, measure horizontal displacement and require ground contact both before and after the move. `OnControllerColliderHit` accepts support normals within the controller's existing slope limit. No extra ground raycasts, input bindings, jump, sprint mechanic, camera changes, shot changes or damage changes.
- `Assets/Scripts/Gameplay/MovementNoiseCadence.cs`: new serializable Inspector configuration, held by PlayerController without requiring prefab edits. Distance and minimum elapsed movement time must both be reached. Emit at most once per movement sample; discard excess distance rather than queue catch-up steps. Reset progress when idle, blocked, unsupported, disabled, cursor-unlocked, dead, missing a camera, or mission-ended. Suppress samples longer than 0.25 s and displacement implying over 20 m/s (with a tiny float tolerance), so stalls/warps do not become footstep bursts. Horizontal motion below 0.1 m/s is treated as contact jitter.
- `Assets/Scripts/Gameplay/ConsoleSwitch.cs`: one `ReportNoise` call after the existing successful activation/progress path. Invalid identities, missing/ended sessions and repeated activation remain silent. This is the small successful-console hook explicitly approved earlier in this conversation; no other console or gate behavior changes.

Existing gunshots still call `SoundEvents.ReportShot(transform.position)` exactly once in `Shoot`. Its hitscan, damage, cooldown and legacy listener payload remain unchanged. The new producers register no static subscriptions. Airborne distance is discarded, including the landing movement sample; no landing/impact mechanic was added.

## Inspector configuration

| Setting | Default | Validation |
| --- | --- | --- |
| Walking step distance | 1.6 horizontal m | 0.2–4 m |
| Future sprint step distance | 2.1 horizontal m | 0.2–4 m |
| Minimum interval | 0.22 s of actual movement | 0.15–1 s |
| Walking intensity | 0.20 | 0–1 |
| Future sprint intensity | 0.45 | 0–1 |

Nonfinite values revert to defaults; finite values clamp. Validation runs in Awake and OnValidate. Zero intensity deliberately produces no bus event. Nonserialized cadence state restarts empty. At the existing 5 m/s walking speed, the fixture measured 30 steps during 50 m of movement sampled over 10 simulated seconds (600 real CharacterController.Move operations).

Sprinting does not exist in this baseline. It remains absent: no input or movement mechanics were added. `MovementNoiseCadence.Advance(displacement, grounded, locomoting, sprinting, deltaTime)` and `Intensity(sprinting)` support a future approved sprint mode. Such a mode would pass its actual sprint state and emit `NoiseCategory.Sprint` instead of `Footstep`. Current PlayerController always passes false. The louder configuration is tested; actual sprint gameplay is not claimed.

## Sound contract and distance targets

| Action | Category | Normalized intensity | Intended hearing distance |
| --- | --- | --- | --- |
| Walking | Footstep | 0.20, Inspector configurable | 4–6 m |
| Future sprint | Sprint | 0.45, Inspector configurable | 10–12 m |
| Firing | Gunshot | 1.00, existing bus default | 22–30 m |
| Successful console | ConsoleInteraction | 0.40, existing bus default | 8–12 m |

These strengths are sensing data, not AudioSource volume or measured detection radii. For later perception, a 26 m full-strength reference with linear intensity would yield 5.2/11.7/26/10.4 m respectively. No such attenuation rule or drone hearing response is added here. Current Scout's legacy gunshot response remains unchanged; it does not yet consume the new movement/console categories.

A movement event carries the resolved player position, category, intensity, current game timestamp and PlayerController source identity. A console event carries the console position and ConsoleSwitch identity. Positions are emission snapshots. No producer stores or publishes a continuously tracked player target.

## Validation

Unity 6000.6.4f1 results: **212/212 Edit Mode and 108/108 Play Mode passed**, with no failures, skips or inconclusive cases. The seven focused new Play Mode tests also passed separately. See [raw evidence and test matrix](evidence/player-noise/README.md). New Play Mode fixtures call the same private movement operation used by Update, using actual CharacterController collisions on runtime-only geometry. Input is not synthesized and no scene/prefab is saved. Existing full-suite tests cover actual MainLab gunshots, three mission loss/restart cycles, camera collision, movement, A*, policies, animation, health and death behavior.

New test files (each with a new .meta):

- `Assets/Tests/EditMode/Movement/MovementNoiseCadenceEditModeTests.cs`: 17 cases covering distances, cadence cap, separate strengths, idle/airborne/passive movement, jitter/vertical motion, stalls/warps, reset and configuration validation.
- `Assets/Tests/PlayMode/PlayerNoisePlayModeTests.cs`: 7 cases covering stationary silence, bounded real walking, wall blocking, airborne/trigger-only support, landing, accepted/repeated console interaction and rejected interaction.

Exact task delta, source archive, SHA-256 manifests and worktree/protected-asset integrity checks are in the evidence directory. Scenes, prefabs, models, navigation data, input settings, drone policies and previous tasks' work are preserved. No commit or push was made.
