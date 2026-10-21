# Per-model design briefs — read first

**What this is:** individual ready-to-hand-off art instructions. Every model has its own folder and `README.md`. Teams and modeling assistants can build one model from a brief without searching through all other documentation.

## How to use

1. Start at [`docs/MODEL_INDEX.md`](../../docs/MODEL_INDEX.md) and choose an asset.
2. Open `ArtSource/Models/<asset-id>/README.md` and follow its exact appearance, dimensions, parts, palette, UV and Unity rules.
3. Save original work to `ArtSource/Models/<asset-id>/source/SF_<Asset>.blend`. Place procedural source `generate_<asset>.py` in `source/` if used.
4. Export Unity-compatible files to `Assets/Art/Models/SF_<Asset>.fbx` and textures to `Assets/Art/Textures/<asset-id>/` when model implementation begins.
5. Preserve Blender screenshots of Object/Edit Mode, UV layout, material view and optional generation/version notes under `ArtSource/Models/<asset-id>/evidence/`.
6. Use root + visual-child Unity prefab structure; gameplay controllers/colliders live on roots and must not be overwritten when the mesh changes.
7. Confirm model footprint, panel pivots, orientation, colliders, material mapping and actual Play Mode result before marking the asset complete.

## Shared Blender conventions

- Units: metric, 1m = 1 Unity unit. Local +Y UP in **Unity**; Blender uses Z UP (test axis remapping on export).
- Default scale must be 1,1,1 on shipped meshes unless deliberate prefab parent scaling is documented.
- Origin: ground centre for static props, explicit edge/hinge origin for moving parts. No arbitrary remote pivots.
- Apply rotation/scale in Blender before UV/export, but **preserve hinge child pivots**.
- Keep material names stable; use palette values from [`VISUAL_STYLE_GUIDE.md`](../../docs/VISUAL_STYLE_GUIDE.md).
- Do not assume Blender procedural materials or emissive lights transfer perfectly through FBX; plan Unity URP material mapping.
- Set per-part names and collection organisation per the asset-specific spec.
- UV unwrap the final mesh (manual seams or automatic unwrap plus inspection); preserve evidence for the mandatory two original Blender models.
- No high-resolution texture dependency is required for blockout; target modest 1k textures, occasional shared 2k atlas only if worthwhile.
- Aesthetic goal: believable scratched industrial facility, readable under dim lighting, low-complexity game assets with clear silhouettes.

## Asset state tags

- `PLANNED`: this file exists; model doesn't necessarily exist.
- `MODELED`: native Blender source saved, materials/UV prepared.
- `IMPORTED`: FBX present in Unity and model prefab validated.
- `ACCEPTED`: tested in final room for appearance, collision and gameplay utility.

All assets in this package are **PLANNED**, not `IMPORTED` or `ACCEPTED`.
