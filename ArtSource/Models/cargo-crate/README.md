# Industrial stacked-looking cargo box — `SF_CargoCrate`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Nimsara (Storage cover)
**Instances / destination:** Four instances Room 4

## Intended appearance

Large robust cargo crate with diagonal metal bracing, worn yellow warning bands and dusty panels. Height blocks standing player LOS.

## Fixed target size / Unity use

1.80W × 1.80D × 2.30H m

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin bottom centre; avoid unnecessary tilted placement.

## Blender object organisation

Create a named top-level collection `SF_CargoCrate` with clearly named objects or empties:

`CrateRoot,ContainerBody,ReinforcementBands,CornerGuards`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, worn steel #58636A, amber #E5A54B, concrete #747875 dirt.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Tileable metal atlas, optional dirt mask.

- Native editable source: `source/SF_CargoCrate.blend` (create during implementation).
- Procedural generator, if used: `source/generate_cargo_crate.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_CargoCrate.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/cargo-crate/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Simple solid box collider, reliable shot blocking.

**Do not break:** Leave reachable side approaches for Flanker, do not form dead-end pinches.

## Ready-to-use Blender creation brief

> Create an original, game-ready **industrial stacked-looking cargo box** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Large robust cargo crate with diagonal metal bracing, worn yellow warning bands and dusty panels. Height blocks standing player LOS. Exact dimensions: 1.80W × 1.80D × 2.30H m Object hierarchy/components: CrateRoot,ContainerBody,ReinforcementBands,CornerGuards. Pivots: Origin bottom centre; avoid unnecessary tilted placement. Material colors: Graphite #20272D, worn steel #58636A, amber #E5A54B, concrete #747875 dirt. UV rules: Tileable metal atlas, optional dirt mask. Gameplay/collision requirements: Simple solid box collider, reliable shot blocking. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
