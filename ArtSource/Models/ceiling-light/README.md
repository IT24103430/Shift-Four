# Recessed overhead utility lamp — `SF_CeilingLight`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** 2–3 fixtures per room initially

## Intended appearance

Long scratched industrial LED strip with frosted diffuser, broken grill detail, occasionally one dark segment. Real room illumination is configured via Unity light(s).

## Fixed target size / Unity use

~1.20W × 0.20D × 0.12H m; mount against ceiling underside

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at ceiling mount plane; faces downward.

## Blender object organisation

Create a named top-level collection `SF_CeilingLight` with clearly named objects or empties:

`LightRoot,Mount,Housing,Diffuser`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, steel #58636A, neutral white diffuse; tiny cyan #61C7CF status.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Simple UV, dedicated emissive diffuser material.

- Native editable source: `source/SF_CeilingLight.blend` (create during implementation).
- Procedural generator, if used: `source/generate_ceiling_light.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_CeilingLight.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/ceiling-light/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

No gameplay-blocking collider; optional tiny non-trigger collider if needed.

**Do not break:** Emission shader alone does NOT light a Unity room. Restrict shadow-casting lights for performance.

## Ready-to-use Blender creation brief

> Create an original, game-ready **recessed overhead utility lamp** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Long scratched industrial LED strip with frosted diffuser, broken grill detail, occasionally one dark segment. Real room illumination is configured via Unity light(s). Exact dimensions: ~1.20W × 0.20D × 0.12H m; mount against ceiling underside Object hierarchy/components: LightRoot,Mount,Housing,Diffuser. Pivots: Origin at ceiling mount plane; faces downward. Material colors: Graphite #20272D, steel #58636A, neutral white diffuse; tiny cyan #61C7CF status. UV rules: Simple UV, dedicated emissive diffuser material. Gameplay/collision requirements: No gameplay-blocking collider; optional tiny non-trigger collider if needed. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
