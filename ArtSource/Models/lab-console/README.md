# Reusable wall-mounted access console — `SF_LabConsole`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Nimthara (with Nimsara interaction contract)
**Instances / destination:** 4 instances, 1 per room

## Intended appearance

Industrial wall terminal with dim cyan screen, off-white bezel, broken upper corner and a red locked indicator that changes to cyan when activated.

## Fixed target size / Unity use

Approx. 0.65W × 0.25D × 1.25H m; interaction hotspot ~1.2m above floor

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin bottom-centre near wall plane; front screen faces player approach.

## Blender object organisation

Create a named top-level collection `SF_LabConsole` with clearly named objects or empties:

`ConsoleRoot,Mount,Screen,StatusLamp,Buttons`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, dirty ivory #C0C2B9, cyan #61C7CF, red #BD493D, worn steel #58636A.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

Simple UV atlas, optional baked screen graphics; no floating labels on texture.

- Native editable source: `source/SF_LabConsole.blend` (create during implementation).
- Procedural generator, if used: `source/generate_lab_console.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_LabConsole.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/lab-console/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Small physical mount collider; interaction trigger in Unity, not imported mesh collider.

**Do not break:** Four instances point at their associated progression state; last console controls final objective.

## Ready-to-use Blender creation brief

> Create an original, game-ready **reusable wall-mounted access console** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Industrial wall terminal with dim cyan screen, off-white bezel, broken upper corner and a red locked indicator that changes to cyan when activated. Exact dimensions: Approx. 0.65W × 0.25D × 1.25H m; interaction hotspot ~1.2m above floor Object hierarchy/components: ConsoleRoot,Mount,Screen,StatusLamp,Buttons. Pivots: Origin bottom-centre near wall plane; front screen faces player approach. Material colors: Graphite #20272D, dirty ivory #C0C2B9, cyan #61C7CF, red #BD493D, worn steel #58636A. UV rules: Simple UV atlas, optional baked screen graphics; no floating labels on texture. Gameplay/collision requirements: Small physical mount collider; interaction trigger in Unity, not imported mesh collider. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
