# Camera and drone clearance fix

Implementation and evidence for Unity 6000.6.4f1, MainLab. Work is on
`feature/player-camera-drone-separation`, descended from latest fetched `origin/dev`
(`271de51`) and preserving the existing height-adjustment commit `98ba978`.
The initial working tree was clean. No commits, pushes or merges were made.

## Confirmed causes

- MainLab saved the camera at local/world Y 1.6 m with a 0.3 m near plane. PlayerController
  did not own or restore the camera's height. The player capsule is 1.8 m high,
  radius 0.35 m, center Y 0.9 m and skin width 0.05 m.
- Every policy can submit the exact player position, including the close-range overrides
  in Guard, Flanker and Interceptor. Motor stopping distance is 0.25 m for intermediate
  graph legs and 1 m for the final leg. Raising the final distance alone misses the
  intermediate-leg problem.
- The four imported meshes are 2.02–2.75 m wide. Their old capsules were only 1 m wide,
  centered at Y 1.55 m, 1.8 m tall; the navigation avoidance radius was 0.46 m. A moving
  NavMeshAgent did not sweep those colliders against the CharacterController.
- Flanker's MainLab spawn at (10, 0, -7) intersects Fixed cover 2, centered at (10, 1.1, -8).
  Ground navigation clearance also understates the clearance needed by raised wings.
- Combat already had line-of-sight checks and a shot timer. No evidence demonstrated
  repeated damage caused directly by camera overlap. A ray starting inside a solid,
  however, needs a separate overlap check. The fix adds that check and validates life,
  session state and range at firing time. Damage remains 10 per drone shot, with the
  existing 1.1 s interval; player damage remains 25 per shot at 0.24 s intervals.

## Configuration

| Setting | Before | After |
|---|---|---|
| Player eye height above capsule bottom | 1.6 m transform only | 1.3 m, PlayerController Inspector field |
| Camera near plane | 0.3 m | 0.05 m, clamped to fit capsule |
| Player capsule, speed, gravity, input | Existing configuration | Unchanged |
| Drone visual rest Y | 1.6 m, all four | 1.6 m, all four |
| Hover amplitude / frequency | 0.08 m / 3 radians per second | Unchanged |
| Combat origin Y | 1.8 m | Unchanged |
| Agent base offset / navigation height | 0 m / 1.8 m | Unchanged |
| Intermediate / final stopping distance | 0.25 m / 1 m | Unchanged |
| Combat approach target radius | None | 2.8 m, configurable on DroneMotor |
| Drone speed / acceleration | 3.5 m/s / 8 m/s² | Unchanged |
| Flanker spawn | (10, 0, -7) | (10, NavMesh surface Y, -5.6) |

The combat target radius and final stopping distance are additive in an unobstructed
straight approach: the usual resting center-to-center distance is approximately 3.8 m.
Cover, flank and interception destinations outside the ring remain policy-controlled.
Small player movements reuse the selected approach point; movement over 0.5 m or a gate
version change permits a new selection.

| Drone | Mesh size XYZ (m) | Capsule center XYZ (m) | Capsule height (m) | Avoidance / horizontal clearance radius (m) |
|---|---|---|---|---|
| Scout | 2.02, 1.09, 1.52 | 0, 1.765, 0.04 | 1.25 | 1.191 |
| Guard | 2.75, 0.75, 1.77 | 0, 1.60, 0.05 | 1.00 | 1.575 |
| Flanker | 2.51, 0.82, 1.64 | 0, 1.63, 0.09 | 1.00 | 1.322 |
| Interceptor | 2.11, 0.75, 2.02 | 0, 1.60, -0.05 | 1.00 | 1.187 |

Capsule radius stays 0.5 m. Each visual mesh receives a matching MeshCollider so wings,
rotors and chassis all route hits to the root Health. The central capsule includes hover
travel. Rotor transforms/animations, imported assets, materials and visual scales remain
unchanged. Resting flight height remains configurable on the motor's bound visual
Transform. After authoring another height/model, run the targeted utility to refit its
clearance envelope and capsule; review overhead clearance before play.

## Movement and collision

DroneBody uses the NavMeshAgent's simulated next position, sweeps the measured raised
model envelope through physics, and synchronizes the accepted position back to navigation.
The conservative horizontal envelope covers yaw and rotor rotation; its vertical bounds
include bobbing. It also checks solids above the original ground navigation envelope.

Ordinary player proximity never calls DroneMotor.Stop, clears the route or disables the
motor. Temporary physical holds retain their route and recover when space opens. Static
props/walls can trigger bounded local steering, validated against both physics and
NavMesh edges. Player bodies, other drones and security gates are excluded from that
static-obstacle detour. The existing A* route, gate versioning, native complete-path checks,
retry budget, death-only Stop behavior and objective prerequisites remain in place.

Unity documents separate control of simulated and Transform positions via
[NavMeshAgent.updatePosition](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent-updatePosition.html)
and [NavMeshAgent.nextPosition](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AI.NavMeshAgent-nextPosition.html).

## Safe scene/prefab application

`Tools > Shift Four > Apply Camera and Drone Clearance Fix` uses Unity Editor APIs to
change only the player configuration, four drone collision/navigation configurations,
matching hitboxes and the known overlapping Flanker spawn. It refuses Play Mode and
unsaved scenes, preserves asset GUIDs and backs up all five serialized assets under
`Library/ProximityFixBackup/<timestamp>/Assets/`. It restores those files if application
throws. The working-project backup is `Library/ProximityFixBackup/20261018-171324-1392470`;
all five backups were byte-verified against the original HEAD assets. To undo later, exit Play Mode and restore the specific backed-up files only after
checking for subsequent edits. No broad installer, visual refresh, scene generator or
NavMesh bake is invoked. Known teammate policy fields are preserved during prefab saves.

## Validation

Executed using `/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity`
in `/tmp/ShiftFour-ProximityValidation`, an isolated copy to avoid the running editor's
project lock. The targeted utility was also applied in the original editor after exiting
Play Mode. Final saved working-project assets were copied into the validation project for
the final Edit Mode run and additional camera regression.

| Run | Passed | Failed | Skipped | Evidence |
|---|---:|---:|---:|---|
| Full Edit Mode suite | 71 | 0 | 0 | [edit-mode.xml](evidence/camera-drone-clearance/edit-mode.xml) |
| Full Play Mode suite | 28 | 0 | 0 | [play-mode.xml](evidence/camera-drone-clearance/play-mode.xml) |
| Additional camera/wall Play Mode regression | 1 | 0 | 0 | [camera-wall-play-mode.xml](evidence/camera-drone-clearance/camera-wall-play-mode.xml) |

This is 71 Edit Mode and 29 unique Play Mode tests. The additional camera regression was
added after the 28-test full Play Mode run; it was executed separately. Test runs reported
no unexpected runtime exceptions. Existing compiler deprecation/platform warnings remain.
Source/Markdown whitespace checks passed. Unity-generated YAML retains Unity's existing
empty-string serialization style.

The test harness opens the actual MainLab scene and exercises its real prefabs, gates and
session restart. Synthetic fixtures additionally test intermediate route legs, close
pursuit, stable repeated goals, temporary collision holds and overhead collision.
Rendered camera captures were inspected: [initial eye height](evidence/camera-drone-clearance/mainlab-eye-height.png)
and [Scout at combat distance](evidence/camera-drone-clearance/mainlab-scout-standoff.png).
These are camera renders from controlled Play Mode fixtures, not screenshots of a manual
walkthrough or the HUD.

The full suite covers all four prefab movement contracts, stable pursuit, route recovery,
terminal Stop, solid cover, valid damage/cooldown, visible mesh hits, raised-body obstacle
clearance, live MainLab movement without geometry overlap, death/restart, all four security
gate prerequisites and post-opening native navigation.

## Manual gameplay check

1. Reopen MainLab after updating scripts/assets, then enter Play Mode. Confirm eye height
   around 1.3 m, WASD/mouse look, E interaction and shooting. Walk into walls, prop corners,
   doors and drones while looking up/down; no camera surface should pass into geometry.
2. Let Scout approach, move laterally, stand still, then back away. It should settle at
   combat range and resume pursuit without repeated destination resets or motor shutdown.
3. Progress through the rooms normally. Verify Guard changes cover, Flanker takes lateral
   routes around storage cover and Interceptor predicts a moving player's route. Check
   their raised models at door headers, corners and overhead props.
4. Shoot visible chassis, wings and rotors. Verify Health drops on the owning drone.
   Take cover behind a solid wall/prop; no damage should cross it. Re-emerge and confirm
   normal 10-point drone hits with the existing cooldown.
5. Verify each locked gate remains blocked until both its own console and drone objective
   are complete. Completing only one prerequisite must not open it.
6. Let the player die, press R and confirm restored health, camera height, drones and locked
   gates. Watch the Console for new exceptions throughout.

A rendered camera capture and automated scenario are evidence of those tested conditions;
they do not substitute for a human assessment of camera feel or an exhaustive walkthrough.
Very narrow spaces may still be physically impassable to these wide models. The fix retains
ground navigation rather than introducing 3D flight or rebaking the team's NavMesh.

## Exact implementation files

- `Assets/Editor/BaselineSceneBuilder.cs`
- `Assets/Editor/ShiftFourProximityFix.cs`
- `Assets/Editor/ShiftFourProximityFix.cs.meta`
- `Assets/Editor/ShiftFourVisualUpgrade.cs`
- `Assets/Prefabs/Agents/Flanker.prefab`
- `Assets/Prefabs/Agents/Guard.prefab`
- `Assets/Prefabs/Agents/Interceptor.prefab`
- `Assets/Prefabs/Agents/Scout.prefab`
- `Assets/Scenes/MainLab.unity`
- `Assets/Scripts/AI/Core/DroneBrain.cs`
- `Assets/Scripts/AI/Movement/DroneBody.cs`
- `Assets/Scripts/AI/Movement/DroneBody.cs.meta`
- `Assets/Scripts/AI/Movement/DroneMotor.cs`
- `Assets/Scripts/Gameplay/PlayerController.cs`
- `Assets/Tests/EditMode/Movement/DroneMotorEditModeTests.cs`
- `Assets/Tests/EditMode/Movement/PlayerCameraEditModeTests.cs`
- `Assets/Tests/EditMode/Movement/PlayerCameraEditModeTests.cs.meta`
- `Assets/Tests/PlayMode/DroneMotorPlayModeTests.cs`
- `Assets/Tests/PlayMode/MainLabProximityPlayModeTests.cs`
- `Assets/Tests/PlayMode/MainLabProximityPlayModeTests.cs.meta`

Documentation/evidence additions are this report and the five files linked above in
`docs/evidence/camera-drone-clearance/` (three XML results and two PNG captures).
`ShiftFourVisualUpgrade.cs` has only a preservation-helper allowlist change for DroneBody;
its broad refresh/installer entry points were never run. BaselineSceneBuilder only aligns
the generated player's height/camera defaults; it was not executed.

No changes remain in Packages or ProjectSettings. Unity's render-pipeline debug manager
briefly appended its standard legacy debug axes when the editor refreshed; those exact
additions were backed up and reverted. A*, policy scoring implementations, objectives,
gate prerequisites, Health, GameSession, visual assets/materials and the baked NavMesh
remain unchanged. MainLab was edited in place, never regenerated.
