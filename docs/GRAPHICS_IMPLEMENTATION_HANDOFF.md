> **2026-10-14 implementation update:** See [visual upgrade status](ENVIRONMENT_VISUAL_POLISH_PLAN.md), [door implementation](DOOR_DOUBLE_SIDED_IMPLEMENTATION.md) and [fleet implementation](DRONE_FLEET_VISUAL_IMPLEMENTATION.md). Earlier hinged-door, shortcut, OBJ-only or missing-Blender-source instructions below are historical and superseded. Four automatic sliding gates and the accepted ADR remain canonical. Generated additions are agent-procedural work, not claimed student hand modeling.

# Shift-Four — Graphics-first Implementation Handoff

This file is a *work-order* for the upcoming code/art iteration. It does not claim the code has already been migrated.

## Work sequence

1. Review master plan, level blueprint, style guide, and all per-asset briefs.
2. Create a dedicated `feature/gv-environment-geometry` branch from `develop`; snapshot the old `MainLab.unity` and do not regenerate it after decoration.
3. Create/commit the four-room rectangular greybox, one floor plane per room with physical thickness, roof slab coverage, door openings, and temporary console + cover placeholders.
4. Verify player capsule size, movement, closed-door collision and the roof from the first-person camera; only then lock dimensions.
5. Generate/produce meshes in `ArtSource/Models/<asset-id>/source/`, then export `.fbx` to `Assets/Art/Models/`.
6. Assemble reusable environment prefabs (floor, wall, ceiling, light, door, console, cover), and install visuals as children of logical roots.
7. Verify **each room alone** in a quick test scene or small subsection before integrating into `MainLab.unity`.
8. Position ceiling lamps and configure Unity URP lighting; inspect for darkness, hotspots and light leaks.
9. Place all four drone visual variants and validate movement, raycast blocking, navigation and routes around cover.
10. Only after the graphics geometry is accepted, migrate old shortcut/one-door runtime logic into three hinged console doors; coordinate graph, NavMesh, GameSession and win condition.
11. Merge via pull requests to `develop`; preserve assessed student-by-student commits and screenshots.

## Visibility of edits

- `.cs`: save in external editor, wait for Unity compile, check Console; then Play.
- Prefab/scene positioning, roof, floor and lights: edit in Scene view outside Play mode; changes show immediately in Scene view.
- Blender `.fbx`: re-export to the same tracked asset and allow Unity to reimport; verify mesh orientation and material mapping.
- **Scene-generation code does not auto-rebuild a saved scene.** Create tools that only explicitly update targeted prefabs/rooms, not a destructive rebuild-all command.
- Verify modifications with `git status`, `git diff`, and Inspector/Play Mode. Open the extracted/checked-out project that contains the changes; avoid repeatedly creating separate ZIP-based projects.

## Definition of graphics-complete

- 4 rooms: all floors, walls, ceiling/roof surfaces sealed and collision-correct.
- 3 correctly measured single-leaf hinged doors and 4 distinct consoles.
- 1 L barrier, 2 server racks, 1 central island, 4 crates in the agreed tactical arrangement.
- Legible abandoned-facility visual theme with actual Unity lights and consistent materials.
- No broken player paths, invisible collider barriers, line-of-sight failures or navigation dead ends from prop placement.
- Every model has an editable source and per-model design brief; mandatory original Blender drone/door have UV/topology evidence.
- Play Mode validates scene before AI policy refinements begin.

## Migration hazards

- `BaselineSceneBuilder.cs` still creates the old four-section layout. Do not rerun over the new scene without replacing/retiring it.
- `LabDoor.cs` still represents the baseline shortcut/sliding behavior. New hinged doors require scene/runtime changes.
- Old `WaypointGraph` data does not match three sequential gated connections.
- Unity `.meta` files must be committed for new assets.
- Source-only Python syntax checks do not prove Blender meshes, Unity import, lighting, or Play Mode work.
