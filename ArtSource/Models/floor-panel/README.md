# Solid worn industrial floor tile — `SF_FloorPanel_2m`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** All room floors, floor slab under door thresholds

## Intended appearance

Durable graphite/concrete flooring with occasional recessed seam and hazard trim near doors. Whole room must feel grounded, not floating planes.

## Fixed target size / Unity use

2.0W × 2.0D × 0.30T m; finished top Y=0

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at slab top centre to simplify snapping to Y=0.

## Blender object organisation

Create a named top-level collection `SF_FloorPanel_2m` with clearly named objects or empties:

`FloorRoot,FloorSlab,TopSurface,SeamDetail`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Concrete #747875, steel #58636A, dark graphite #20272D, optional amber #E5A54B near thresholds.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Tileable top UV; high-detail normal map optional but not required.

- Native editable source: `source/SF_FloorPanel_2m.blend` (create during implementation).
- Procedural generator, if used: `source/generate_floor_panel.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_FloorPanel_2m.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/floor-panel/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Box collider spans entire slab. Check seams and prevent tiny collision gaps.

**Do not break:** No decorative bump colliders protruding above walking surface.

## Ready-to-use Blender creation brief

> Create an original, game-ready **solid worn industrial floor tile** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Durable graphite/concrete flooring with occasional recessed seam and hazard trim near doors. Whole room must feel grounded, not floating planes. Exact dimensions: 2.0W × 2.0D × 0.30T m; finished top Y=0 Object hierarchy/components: FloorRoot,FloorSlab,TopSurface,SeamDetail. Pivots: Origin at slab top centre to simplify snapping to Y=0. Material colors: Concrete #747875, steel #58636A, dark graphite #20272D, optional amber #E5A54B near thresholds. UV rules: Tileable top UV; high-detail normal map optional but not required. Gameplay/collision requirements: Box collider spans entire slab. Check seams and prevent tiny collision gaps. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
