> **Current contract (2026-10-14):** four automatic horizontal sliding gates, each requiring its own drone AND console. Historical hinged/shortcut and console-only instructions below are superseded. See `docs/decisions/ADR-FOUR-GATE-CONTRACT.md` and `docs/members/CORE_GV_IMPLEMENTATION.md`.

# Shift-Four — Graphics-First Master Plan (Design V3)

**Status: DESIGN / DOCUMENTATION ONLY. This ZIP does not implement the new layout or art.**
**Source baseline:** `Shift-Four-main(1).zip`, Unity 6.3 LTS project. **Decision:** abandoned survival research facility, rectangular sequential rooms, four AI-specific cover arrangements, solid floors, fully enclosed roofs/ceilings, consistent lighting, three permanently opening hinged security doors.

## Design priorities

1. Fix measurements, player passage, collision contracts, asset pivots and materials **before** detailed meshes.
2. Create a simple playable *blockout* for scale and line-of-sight checks; freeze dimensions after testing.
3. Produce environment/roof/floor/light/cover assets in Blender using separate asset specifications; store `.blend` sources and `.fbx` exports.
4. Complete a visually coherent environment using reusable prefabs and direct scene editing; never overwrite polished scene data with the old generator.
5. Stabilize C# AI/gameplay logic around the accepted fixed geometry, not the other way around.
6. Preserve the assigned GV/IS assessment contributions and originality evidence.

## Topology — four rooms in a row, no roundabout

`START → CHECKPOINT (Scout) → [DOOR-01] → SERVER (Guard) → [DOOR-02] → CONTROL (Interceptor) → [DOOR-03] → STORAGE (Flanker) → EXIT`

- Four consoles: the first three open the respective *next* hinged door by pressing `E`; Room 4's console contributes to the final win condition.
- Doors **swing open once and remain open**. No fourth connecting door, no sliding door requirement in the new design.
- Drone defeat requirements for winning must remain compatible with the coursework's original four-drone/four-console objective; access to the next room requires console interaction, not necessarily defeating the current drone.
- Roof is a **solid roof and visible interior ceiling**, not a fifth playable rooftop level. No stairs or roof traversal.
- Floors are continuous and fully solid with colliders; walls, doors, roof, cover and major props have intentional collision policy.

## Coordinate system (proposed; test before freeze)

- Unity: +Y up; +X moves from Room 1 toward Room 4; +Z from left to right across each room. One Unity unit = one metre.
- Room-local position `X` starts at the entrance wall, `Z` starts at the left side wall, `Y=0` means finished floor top.
- All rooms sit on one floor plane; top of floor at Y=0; floor slabs from Y=-0.3 to 0.
- Clear ceiling height Y=3.6; ceiling underside at Y=3.6; structural roof spans Y=3.6..3.9. Ceiling lights hang downward without reducing important player/head clearance.
- Wall thickness 0.30. Each door is centred across room width. Main route has at least 1.8m clear walkway (aim 2.0m).

| Zone | Extent along X (global m) | Width Z | Length X | Clear height | Main drone | Cover |
|---|---:|---:|---:|---:|---|---|
| 01 Checkpoint | 0–14 | 12 | 14 | 3.6 | Scout | L-shaped barrier |
| 02 Server | 14–28 | 12 | 14 | 3.6 | Guard | 2 server racks |
| 03 Control | 28–44 | 14 | 16 | 3.6 | Interceptor | Control island |
| 04 Storage | 44–62 | 16 | 18 | 3.6 | Flanker | 4 crates |

Note: adjacent rooms with different widths need joined wall sections or short flush connectors; never leave cracks at the shared doorway planes.

## Permanent hinged door geometry (3 identical prefabs)

| Property | Contract |
|---|---|
| Clear opening | 2.20m wide × 2.65m high |
| Outer frame envelope | approx. 2.8m wide × 3.10m high |
| Single metal leaf | 2.15m wide × 2.58m high × 0.12m thick |
| Pivot | vertical Y-axis on the hinge edge, at the leaf's base |
| Swing | 0° closed → approx. 95° open, toward the next room (+X); verify handedness in Unity |
| State | opens after assigned console activates; stays open permanently |
| Safety | no cover/console/spawn in 2.3m swing envelope; static frame collision remains; leaf collision moves with leaf |
| Navigation | update door passage and A* connection only when sufficiently open; **do not** blindly keep legacy shortcut logic |

## Room tactical cover — simple and explainable

| Room | Cover specs | AI behavior it supports | Player tactic |
|---|---|---|---|
| Checkpoint | one solid L-barrier; main 3.2×0.35×2.4m, return 1.0m | Scout line-of-sight loss and last-known-position search | hide, recover, reposition, reach console |
| Server | two 1.2×2.8×2.7m rack meshes | Guard protected-position evaluation | use rack faces, change attacking angle |
| Control | one central 4×3×2.3m command island | Interceptor route prediction / route junction choices | take left/right route, reverse direction |
| Storage | four 1.8×1.8×2.3m crates | Flanker alternate lateral approaches | rotate between crates and watch both sides |

See [LEVEL_BLUEPRINT.md](LEVEL_BLUEPRINT.md) for coordinates, spawn/console targets, collision policy, and visibility validation. Place AI waypoint candidates after room cover placement; *never* promise correct AI with placeholder graph nodes alone.

## Complete environment surfaces

Every room must include: solid floor slab, four boundary-wall segments with door opening where applicable, ceiling panels, weathered roof slabs, trims, edge seals, lighting fixtures, functional light sources, and optional inexpensive damage decals/vents. The roof should fully hide the sky when the player looks upward from the interior. Do not use an expensive dynamic full-shadow light for every decorative bulb.

## Visual palette and material contracts

Read [VISUAL_STYLE_GUIDE.md](VISUAL_STYLE_GUIDE.md). Base architecture is graphite/worn steel and concrete; red means emergency or locked, amber means hazard/warning, cyan indicates interactable console/unlocked indication. Keep emissive materials distinguishable from actual lights.

## Model handoff system

Every individual asset lives under `ArtSource/Models/<model_id>/` with a **model-specific `README.md`** that covers: visual appearance, exact dimensions, pivot/origin, components, materials with HEX values, UV/texture rules, export path, collision rules, usage, ownership, acceptance checklist, and a ready-to-copy modeling prompt. The directories are ready even before `.blend` files exist. See [MODEL_INDEX.md](MODEL_INDEX.md) and [`ArtSource/Models/README.md`](../ArtSource/Models/README.md).

## Graphics-first milestone gates

| Phase | Outcome | What to verify |
|---|---|---|
| G0 Documentation | locked drafts + all asset briefs | every asset has contract and owner |
| G1 Geometry | four enclosed blockout rooms + 3 wall openings + full roofs | player fits, no gaps in floor/ceiling, doors fit |
| G2 Tactical cover | all cover blocks raycasts and permits routes | Scout LOS, Guard options, Interceptor junctions, Flanker routes |
| G3 Blender source | door, drone and prop source/export pairs | dimensions, UVs, pivot, Unity imports, originality proof |
| G4 Visual integration | material consistency, ceilings/lights, audio hooks | one coherent abandoned facility; prefab scene not overwritten |
| G5 Gameplay stabilization | existing AI + consoles adapted to accepted layout | four AIs, permanent doors, health/recovery, victory |
| G6 Demo QA | all members can explain and demonstrate their slice | Play Mode test, build, individual evidence |

**Scope discipline:** no rooftop exploration, weather, new weapons, new AI agent, interactive crates, complicated puzzles, advanced light baking pipeline, or multi-scene streaming unless an assessment requirement expressly forces it.

## Who builds what

- **Pamudi / World Builder / Scout:** main level geometry, roof/floor/wall kit, lighting integration, waypoint placement, Checkpoint cover, Scout.
- **Nimsara / Systems Engineer / Flanker:** player/collision and cover-damage contracts, permanent hinged door runtime interaction, Storage crate gameplay, Flanker.
- **Nimthara / Core Developer / Guard:** original **drone + door** Blender source, UVs and imports, Server rack appearance, reusable consoles and HUD, Guard.
- **Asmadala / Agent Controller / Interceptor:** Control island and drone-movement presentation, shared A*, Interceptor, navigation/route invalidation on doors.

Shared art assets beyond the two mandatory original models may be modeled by the relevant section owner, subject to review. Do not reassign the coursework's official GV roles.

## Change boundary

The uploaded baseline still contains a **four-section loop, a single sliding shortcut door, and old graph positions**. **Nothing is migrated in this documentation package.** Implementation must update layout, navigation, interactions and Prefabs as a coordinated future step. Preserve current code and original contribution history until that migration is tested.
