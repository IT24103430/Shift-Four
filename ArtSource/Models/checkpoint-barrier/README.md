# L-shaped tall protective wall — `SF_CheckpointBarrier`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** Room 1 Scout cover

## Intended appearance

Cracked concrete blast barrier with metal reinforcement and chipped corners. One open end allows Scout to investigate without a permanently unreachable safe zone.

## Fixed target size / Unity use

Main wall 3.2L × 0.35T × 2.4H m; 1.0m side return same height

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin bottom-centre at main section centre; local L geometry documented.

## Blender object organisation

Create a named top-level collection `SF_CheckpointBarrier` with clearly named objects or empties:

`CoverRoot,MainWall,ShortReturn,MetalCaps,DamageDetails`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Concrete #747875, worn steel #58636A, rust #6A4B37.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Use seams/UV that show roughness and weathering.

- Native editable source: `source/SF_CheckpointBarrier.blend` (create during implementation).
- Procedural generator, if used: `source/generate_checkpoint_barrier.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_CheckpointBarrier.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/checkpoint-barrier/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Strong opaque collider across both branches; confirm Scout LOS raycasts hit it.

**Do not break:** User has standing cover without any crouch mechanic.

## Ready-to-use Blender creation brief

> Create an original, game-ready **l-shaped tall protective wall** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Cracked concrete blast barrier with metal reinforcement and chipped corners. One open end allows Scout to investigate without a permanently unreachable safe zone. Exact dimensions: Main wall 3.2L × 0.35T × 2.4H m; 1.0m side return same height Object hierarchy/components: CoverRoot,MainWall,ShortReturn,MetalCaps,DamageDetails. Pivots: Origin bottom-centre at main section centre; local L geometry documented. Material colors: Concrete #747875, worn steel #58636A, rust #6A4B37. UV rules: Use seams/UV that show roughness and weathering. Gameplay/collision requirements: Strong opaque collider across both branches; confirm Scout LOS raycasts hit it. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
