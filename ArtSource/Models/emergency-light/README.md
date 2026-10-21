# Red industrial hazard lamp — `SF_EmergencyLamp`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Pamudi
**Instances / destination:** Over door frames, 1–2 alert points per room

## Intended appearance

Small alarm beacon in thick cracked mount with warning grill; red bloom accent but should not overwhelm player visibility.

## Fixed target size / Unity use

~0.35W × 0.18D × 0.20H m

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at mounting surface; face points into room.

## Blender object organisation

Create a named top-level collection `SF_EmergencyLamp` with clearly named objects or empties:

`LampRoot,Mount,Glass,Guard`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Red #BD493D, amber #E5A54B, graphite #20272D.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Emissive lamp surface; UV any warning markings.

- Native editable source: `source/SF_EmergencyLamp.blend` (create during implementation).
- Procedural generator, if used: `source/generate_emergency_light.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_EmergencyLamp.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/emergency-light/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

No meaningful obstructing collider.

**Do not break:** Red/amber are signals, not a full-room light replacement.

## Ready-to-use Blender creation brief

> Create an original, game-ready **red industrial hazard lamp** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Small alarm beacon in thick cracked mount with warning grill; red bloom accent but should not overwhelm player visibility. Exact dimensions: ~0.35W × 0.18D × 0.20H m Object hierarchy/components: LampRoot,Mount,Glass,Guard. Pivots: Origin at mounting surface; face points into room. Material colors: Red #BD493D, amber #E5A54B, graphite #20272D. UV rules: Emissive lamp surface; UV any warning markings. Gameplay/collision requirements: No meaningful obstructing collider. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
