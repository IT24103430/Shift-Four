> **Current contract (2026-10-14):** four automatic horizontal sliding gates, each requiring its own drone AND console. Historical hinged/shortcut and console-only instructions below are superseded. See `docs/decisions/ADR-FOUR-GATE-CONTRACT.md` and `docs/members/CORE_GV_IMPLEMENTATION.md`.

# Shift-Four — Room Placement Blueprint (Design Draft)

Read [GRAPHICS_FIRST_MASTER_PLAN.md](GRAPHICS_FIRST_MASTER_PLAN.md) first. Coordinates below are **candidate blockout values**; freeze them only after Unity collision, raycast and NavMesh tests.

## Convention

- Room-local axes: X forward from entrance, Z across width from left wall, Y up. `Y=0` finished floor top.
- Objects specified as `(X, Z)` refer to their centre footprint, in metres, room-local.
- Room depth/length is **X** and width is **Z**.
- Place the entrance doorway at `(0, W/2)` and exit doorway at `(L, W/2)`.
- Room entrance/exit wall has a 2.2m clear gap only where a door/opening exists; no invisible closed wall colliders in the gaps.
- Exit of Storage uses an ordinary threshold/trigger, not a fourth door leaf.

| Room | Local size X × Z | Player/entry location | Drone spawn target | Console target | Cover centre(s) |
|---|---|---|---|---|---|
| Checkpoint / Scout | 14 × 12 | (2,6) | (9,6) | (12.0,9.5) | L wall main centre (5,4); return joins its end |
| Server / Guard | 14 × 12 | (2,6) | (10,6) | (12.0,9.5) | rack A (5,4), rack B (9,8) |
| Control / Interceptor | 16 × 14 | (2,7) | (12,7) | (14.0,11.0) | central island (8,7) |
| Storage / Flanker | 18 × 16 | (2,8) | (11,8) | (16.0,13.0) | crates (5,4), (5,12), (13,4), (13,12) |

## Doors and consoles

- Door-01: between Checkpoint/Server, world X=14; Console-01 inside Checkpoint near x=12.
- Door-02: between Server/Control, world X=28; Console-02 inside Server near x=12 local.
- Door-03: between Control/Storage, world X=44; Console-03 inside Control near x=14 local.
- Console-04: Storage x=16 local; terminal records final objective and participates in victory/exit logic.
- **IMPORTANT:** Door swings into the next room. Keep the full 2.3m radius clear of new-room spawn/cover and wall details. Test door leaf trajectory with real geometry.
- All console prompts and triggers must be reachable from the current room's side, outside the door swing and away from narrow pinch points.

## Roof and illumination placement

- Base finished floor surface is at Y=0 with solid slab below it. Collider matches visible slab footprint, not a single invisible floating plane.
- Roof/ceiling underside at Y=3.6; slab occupies Y=3.6–3.9; use 2×2m modular tiles, plus edge fillers cut to each actual room footprint.
- Place ceiling lights along each room's centerline, e.g. X at approximately L/3 and 2L/3, and optionally a red emergency fixture over the exit.
- Decorative flickering fixture effects must not make path hazards invisible; safe minimum illumination remains readable for gameplay.
- Lighting fixtures have visuals *and* configured Unity lights. Emission alone does not necessarily illuminate the room.
- Roof and ceiling should be opaque inside, and light leak tests must be performed along joins.

## AI and cover validity checks (must actually work)

1. Player stands fully behind Scout's barrier, a direct drone-to-player shot raycast hits the barrier first; Scout can investigate around an open end.
2. Guard can reach **two** separate candidate protected positions beside server racks without entering rack colliders.
3. Interceptor sees distinct left/right path junctions around the control island; both routes connect into the graph and NavMesh.
4. Flanker has two reachable lateral side approaches around crates, not just unobstructed direct-line chasing.
5. With all doors shut, no AI path crosses a closed leaf. Once opened permanently, relevant nav route is valid.
6. Player can move from entrance to console and through an opened door. No roof, floor, cover or decorative collider traps the player.
7. All four drones are able to update and be defeated in a single Unity scene as required by assessment.

## Do not finalize yet

- Drone flight height, exact aiming origins and agent radius require checking current prefabs and Unity imports.
- Console interaction range and prompt placement must be verified against real player controller values.
- Final coordinate data should be exported from the tested Unity scene into a `docs/PLACEMENT_FREEZE.md` handoff before final art production.
