# Modular industrial wall plate — `SF_WallPanel_2m`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** All four rooms, door-side and corner variants

## Intended appearance

Large steel-reinforced concrete panel, inset dark vertical ribs, occasional access seam, lightly scraped lower section. Design wall opening modules compatible with a 2.20m clear door width.

## Fixed target size / Unity use

Module width 2.0m, thickness 0.30m, height 3.60m

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin bottom-left at wall plane; orient +X/+Z consistently when placing.

## Blender object organisation

Create a named top-level collection `SF_WallPanel_2m` with clearly named objects or empties:

`WallRoot,StructuralWall,PanelFace,VerticalTrim`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Concrete #747875, graphite #20272D, steel #58636A, subtle rust #6A4B37.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Use repeating seamless UVs with scale documented; avoid stretching.

- Native editable source: `source/SF_WallPanel_2m.blend` (create during implementation).
- Procedural generator, if used: `source/generate_wall_panel.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_WallPanel_2m.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/wall-panel/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Full-height opaque collider; split wall around actual door gap.

**Do not break:** Do not use an intact wall collider over any doorway.

## Ready-to-use Blender creation brief

> Create an original, game-ready **modular industrial wall plate** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Large steel-reinforced concrete panel, inset dark vertical ribs, occasional access seam, lightly scraped lower section. Design wall opening modules compatible with a 2.20m clear door width. Exact dimensions: Module width 2.0m, thickness 0.30m, height 3.60m Object hierarchy/components: WallRoot,StructuralWall,PanelFace,VerticalTrim. Pivots: Origin bottom-left at wall plane; orient +X/+Z consistently when placing. Material colors: Concrete #747875, graphite #20272D, steel #58636A, subtle rust #6A4B37. UV rules: Use repeating seamless UVs with scale documented; avoid stretching. Gameplay/collision requirements: Full-height opaque collider; split wall around actual door gap. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
