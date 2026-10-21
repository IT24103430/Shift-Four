# Solid overhead structural roof module — `SF_RoofSlab_2m`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** Covers every room completely

## Intended appearance

Heavy industrial roof with beam-edge seal. Outside top simple; inside underside must be closed and block sky/lighting leaks.

## Fixed target size / Unity use

2.0W × 2.0D × 0.30T m, roof underside Y=3.6 and top Y=3.9

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at underside centre so snap to Y=3.6.

## Blender object organisation

Create a named top-level collection `SF_RoofSlab_2m` with clearly named objects or empties:

`RoofRoot,RoofSolid,EdgeSeal`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, worn steel #58636A, dark recess #12171B.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Tileable UVs; underside receives room interior material.

- Native editable source: `source/SF_RoofSlab_2m.blend` (create during implementation).
- Procedural generator, if used: `source/generate_roof_slab.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_RoofSlab_2m.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/roof-slab/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Opaque roof box collider; avoid unnecessary fine-detail colliders.

**Do not break:** Rooftop is NOT walkable or accessible. End panels cover residual room width/length; no open skylight.

## Ready-to-use Blender creation brief

> Create an original, game-ready **solid overhead structural roof module** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Heavy industrial roof with beam-edge seal. Outside top simple; inside underside must be closed and block sky/lighting leaks. Exact dimensions: 2.0W × 2.0D × 0.30T m, roof underside Y=3.6 and top Y=3.9 Object hierarchy/components: RoofRoot,RoofSolid,EdgeSeal. Pivots: Origin at underside centre so snap to Y=3.6. Material colors: Graphite #20272D, worn steel #58636A, dark recess #12171B. UV rules: Tileable UVs; underside receives room interior material. Gameplay/collision requirements: Opaque roof box collider; avoid unnecessary fine-detail colliders. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
