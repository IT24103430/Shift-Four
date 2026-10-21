# Central multi-directional command desk — `SF_ControlIsland`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Asmadala (with Pamudi layout review)
**Instances / destination:** One Control room central island

## Intended appearance

Broken octagonal/rectangular industrial command station with sloped console faces, dead monitors, a few cyan instruments. Simple enough to route around.

## Fixed target size / Unity use

4.00L × 3.00W × 2.30H m max silhouette; maintain sightline around sides

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin bottom-centre for symmetric aisle clearance.

## Blender object organisation

Create a named top-level collection `SF_ControlIsland` with clearly named objects or empties:

`IslandRoot,MainBody,ConsoleSurface,MonitorFrames,VentDetails`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, steel #58636A, ivory #C0C2B9, cyan #61C7CF.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

UV main surfaces and console strips; no need for fully readable UI text.

- Native editable source: `source/SF_ControlIsland.blend` (create during implementation).
- Procedural generator, if used: `source/generate_control_island.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_ControlIsland.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/control-island/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Simplified box/compound solid collider; two 2m-ish side routes remain accessible.

**Do not break:** Interceptor needs separate reachable left/right waypoint graph branches, not just visual obstacles.

## Ready-to-use Blender creation brief

> Create an original, game-ready **central multi-directional command desk** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Broken octagonal/rectangular industrial command station with sloped console faces, dead monitors, a few cyan instruments. Simple enough to route around. Exact dimensions: 4.00L × 3.00W × 2.30H m max silhouette; maintain sightline around sides Object hierarchy/components: IslandRoot,MainBody,ConsoleSurface,MonitorFrames,VentDetails. Pivots: Origin bottom-centre for symmetric aisle clearance. Material colors: Graphite #20272D, steel #58636A, ivory #C0C2B9, cyan #61C7CF. UV rules: UV main surfaces and console strips; no need for fully readable UI text. Gameplay/collision requirements: Simplified box/compound solid collider; two 2m-ish side routes remain accessible. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
