# Reinforced single hinged security door — `SF_SecurityHingedDoor`

**Asset stage:** `PLANNED` (specification only; no model generated yet)
**Owner / handoff:** Nimthara (visual/model), Nimsara (interaction + collision)
**Instances / destination:** 3 connected-room thresholds

## Intended appearance

Dark armored industrial door, off-white scratched inset plate, strong hinge, emergency LED, amber chevron decals, visible mechanical reinforcement. Must look abandoned but operable; NO sliding leaves.

## Fixed target size / Unity use

Outer frame ~2.80W × 3.10H m; clear opening 2.20W × 2.65H m; leaf ~2.15W × 2.58H × 0.12T m

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. HingePivot at left or right vertical leaf edge at base; Y-axis rotation in Unity; open once to 95° towards NEXT room.

## Blender object organisation

Create a named top-level collection `SF_SecurityHingedDoor` with clearly named objects or empties:

`DoorRoot,Frame_Static,HingePivot,Leaf_Mesh,LockHousing,StatusIndicator,HazardMarkings`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

#20272D graphite; #58636A steel; #C0C2B9 aged inset; #BD493D locked; #61C7CF unlocked; #E5A54B warnings.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

UV unwrap visible leaf/frame; panel detail can use atlas/trims; keep independent pivot object.

- Native editable source: `source/SF_SecurityHingedDoor.blend` (create during implementation).
- Procedural generator, if used: `source/generate_security_hinged_door.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_SecurityHingedDoor.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/security-hinged-door/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Collider on static frame; box/compound collider follows leaf. Reserve 2.3m arc clear. No duplicate fixed collider across opening.

**Do not break:** Do not rely on existing sliding LabDoor.cs; hinged runtime migration is later. Model should import at exact size.

## Ready-to-use Blender creation brief

> Create an original, game-ready **reinforced single hinged security door** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Dark armored industrial door, off-white scratched inset plate, strong hinge, emergency LED, amber chevron decals, visible mechanical reinforcement. Must look abandoned but operable; NO sliding leaves. Exact dimensions: Outer frame ~2.80W × 3.10H m; clear opening 2.20W × 2.65H m; leaf ~2.15W × 2.58H × 0.12T m Object hierarchy/components: DoorRoot,Frame_Static,HingePivot,Leaf_Mesh,LockHousing,StatusIndicator,HazardMarkings. Pivots: HingePivot at left or right vertical leaf edge at base; Y-axis rotation in Unity; open once to 95° towards NEXT room. Material colors: #20272D graphite; #58636A steel; #C0C2B9 aged inset; #BD493D locked; #61C7CF unlocked; #E5A54B warnings. UV rules: UV unwrap visible leaf/frame; panel detail can use atlas/trims; keep independent pivot object. Gameplay/collision requirements: Collider on static frame; box/compound collider follows leaf. Reserve 2.3m arc clear. No duplicate fixed collider across opening. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.
