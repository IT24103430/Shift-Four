# Shift Four – four-room gated progression

## Intended level flow

Checkpoint (Scout) → Server (Guard) → Control (Interceptor) → Storage (Flanker) → Exit.

The old direct Checkpoint → Storage doorway is **permanently sealed**. The player never needs it.

| Gate | Position | Unlocks when |
|---|---|---|
| Checkpoint → Server | (-10, 0, 0) | Scout defeated **AND** Checkpoint console activated |
| Server → Control | (0, 0, 10) | Guard defeated **AND** Server console activated |
| Control → Storage | (10, 0, 0) | Interceptor defeated **AND** Control console activated |
| Storage → Exit | (20, 0, -15) | Flanker defeated **AND** Storage console activated |

The gates open **automatically** when both prerequisites are completed, with a mechanical bolt-unlocking phase followed by dual horizontal sliding panels. The new doors are **intentional visual placeholders** for later Blender FBX imports. Preserve the hierarchy names `Door assembly/LeftPanel`, `RightPanel`, `LeftBolt`, `RightBolt` so `LabDoor` keeps animating your custom asset.

## Existing scene integration

1. Commit or back up your current working Unity project before copying files.
2. Merge updated `Assets/Editor`, `Assets/Scripts`, and docs into the existing repo. Keep existing Unity assets and `.meta` files.
3. Open Unity and let it compile.
4. Run **Tools → Shift Four → Install Drone + Door Models** (also updates existing `MainLab` layout).
5. Open `Assets/Scenes/MainLab.unity`; select the `NavMesh surface — baked greybox` object and rebake the NavMesh in Unity. **Save the scene and baked data.**
6. Verify the old south shortcut is sealed, all four gates initially block access, and the exit appears outside the east wall of Storage.
7. Play through all four rooms. In each room, defeating its drone without console interaction must keep the gate shut; activating console without defeating drone must also keep it shut. After both, wait for animation and walk through.
8. Ensure no NavMesh warnings, wall clipping, misplaced AI, or invisible collisions.

## Why this is stronger gameplay

- Every drone has a distinct encounter and a visible objective.
- A console matters; it is not a meaningless interaction.
- The player can choose whether to activate the console or defeat the drone first.
- Every gate is deterministic and driven by actual drone health and console state.
- The existing win condition still requires all four consoles online and all four drones defeated.

## Known limits and follow-ups

- These are **prototype placeholder doors**, not yet detailed Blender FBX models.
- Door colliders toggle once the panels are fully open; panels themselves are visual only.
- Door sounds, sensors/obstruction avoidance and multiplayer support are not part of this prototype.
- Blender models are independent assets; replace the visual children rather than the root collider or `LabDoor`.
- Existing scenes require a manual NavMesh rebake after layout changes.

## Implemented and verified update — 2026-10-14

MainLab now contains all four gates and the sealed shortcut. Independent MissionProgress flags and graph IDs implement the AND contract; doors use the exported `SF_HeavySecurityDoor_GV.fbx` visual with normalized animation pivots. The earlier placeholder-only statement describes the prior baseline.

The existing NavMesh asset has been rebaked in the isolated Unity integration and copied back with its GUID preserved. Automated Play Mode verified all four physical capsule passages, closed/open NavMesh routes, prerequisite order/duplicates, victory and defeat restarts. Final human combat/visual acceptance remains required. See `QA/GV_ACCEPTANCE_MATRIX.md`, `decisions/ADR-FOUR-GATE-CONTRACT.md` and `members/CORE_GV_IMPLEMENTATION.md` for exact workflow and ownership.
