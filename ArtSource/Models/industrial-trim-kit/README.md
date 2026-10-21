# Reusable pipes, vents, corner trims and damage pieces — `SF_IndustrialTrimKit`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi / all contributors with review
**Instances / destination:** Walls, floors, roof edges, room identity

## Intended appearance

Modular ribbed conduits, small vent panels, exposed cable cover, caution signage and corroded beam trims. Decorative only and reused sparingly.

## Fixed target size / Unity use

Lengths 0.5m, 1.0m and 2.0m modules; profile dimensions documented per subpart

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Pivots at modular snap edge or centre; align to 0.5m increments.

## Blender object organisation

Create a named top-level collection `SF_IndustrialTrimKit` with clearly named objects or empties:

`TrimRoot,Beam_01,Pipe_01,Vent_01,PanelDamaged_01`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, worn steel #58636A, rust #6A4B37, amber #E5A54B.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Tileable and atlas UVs; avoid unique high-resolution maps per detail.

- Native editable source: `source/SF_IndustrialTrimKit.blend` (create during implementation).
- Procedural generator, if used: `source/generate_industrial_trim_kit.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_IndustrialTrimKit.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/industrial-trim-kit/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Decorative unless large enough to block player physically; explicitly tag collision in prefab.

**Do not break:** Never narrow required player/AI paths or obstruct console interactions.

## Ready-to-use Blender creation brief

> Create an original, game-ready **reusable pipes, vents, corner trims and damage pieces** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Modular ribbed conduits, small vent panels, exposed cable cover, caution signage and corroded beam trims. Decorative only and reused sparingly. Exact dimensions: Lengths 0.5m, 1.0m and 2.0m modules; profile dimensions documented per subpart Object hierarchy/components: TrimRoot,Beam_01,Pipe_01,Vent_01,PanelDamaged_01. Pivots: Pivots at modular snap edge or centre; align to 0.5m increments. Material colors: Graphite #20272D, worn steel #58636A, rust #6A4B37, amber #E5A54B. UV rules: Tileable and atlas UVs; avoid unique high-resolution maps per detail. Gameplay/collision requirements: Decorative unless large enough to block player physically; explicitly tag collision in prefab. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
