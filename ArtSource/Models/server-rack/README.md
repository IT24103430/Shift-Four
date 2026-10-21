# Guard-room high-cover rack — `SF_ServerRack`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Nimthara (model/Server room)
**Instances / destination:** Two instances Room 2

## Intended appearance

Dusty industrial compute rack with vent grills, a few surviving cyan LEDs, dark side panels and service cable ports; reads as solid obstacle from distance.

## Fixed target size / Unity use

1.20W × 2.80D × 2.70H m per rack

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at floor centre, front panel orientation documented.

## Blender object organisation

Create a named top-level collection `SF_ServerRack` with clearly named objects or empties:

`RackRoot,MainCase,SidePanels,VentGrid,StatusLeds`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, worn steel #58636A, cyan #61C7CF restrained, ivory #C0C2B9 for faded panel tags.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Repeat UV pattern for vents, optional small emissive atlas.

- Native editable source: `source/SF_ServerRack.blend` (create during implementation).
- Procedural generator, if used: `source/generate_server_rack.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_ServerRack.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/server-rack/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Main body box collider blocks projectiles; no thin LED colliders.

**Do not break:** Place Guard tactical candidates outside collisions with clear escape routes.

## Ready-to-use Blender creation brief

> Create an original, game-ready **guard-room high-cover rack** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Dusty industrial compute rack with vent grills, a few surviving cyan LEDs, dark side panels and service cable ports; reads as solid obstacle from distance. Exact dimensions: 1.20W × 2.80D × 2.70H m per rack Object hierarchy/components: RackRoot,MainCase,SidePanels,VentGrid,StatusLeds. Pivots: Origin at floor centre, front panel orientation documented. Material colors: Graphite #20272D, worn steel #58636A, cyan #61C7CF restrained, ivory #C0C2B9 for faded panel tags. UV rules: Repeat UV pattern for vents, optional small emissive atlas. Gameplay/collision requirements: Main body box collider blocks projectiles; no thin LED colliders. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
