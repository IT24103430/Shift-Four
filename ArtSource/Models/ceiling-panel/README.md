# Interior ceiling treatment — `SF_CeilingPanel_2m`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** Ceiling below structural roof all rooms

## Intended appearance

Suspended/damaged metal service panel, vents and dirt; occasional missing decorative tile only where structural roof above still closes the space.

## Fixed target size / Unity use

2.0W × 2.0D; underside plane at Y=3.6 or just below slab

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at panel centre on underside; normals face room interior.

## Blender object organisation

Create a named top-level collection `SF_CeilingPanel_2m` with clearly named objects or empties:

`CeilingRoot,PanelFace,VentInsert,Fasteners`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Dirty ivory #C0C2B9, graphite #20272D, worn steel #58636A.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Repeating texture atlas; avoid giant detailed unique textures.

- Native editable source: `source/SF_CeilingPanel_2m.blend` (create during implementation).
- Procedural generator, if used: `source/generate_ceiling_panel.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_CeilingPanel_2m.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/ceiling-panel/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Usually no extra collider if roof already supplies solid overhead collider; exceptions documented.

**Do not break:** No ceiling panels intersect player or overhead lamps.

## Ready-to-use Blender creation brief

> Create an original, game-ready **interior ceiling treatment** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Suspended/damaged metal service panel, vents and dirt; occasional missing decorative tile only where structural roof above still closes the space. Exact dimensions: 2.0W × 2.0D; underside plane at Y=3.6 or just below slab Object hierarchy/components: CeilingRoot,PanelFace,VentInsert,Fasteners. Pivots: Origin at panel centre on underside; normals face room interior. Material colors: Dirty ivory #C0C2B9, graphite #20272D, worn steel #58636A. UV rules: Repeating texture atlas; avoid giant detailed unique textures. Gameplay/collision requirements: Usually no extra collider if roof already supplies solid overhead collider; exceptions documented. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
