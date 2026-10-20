> Current procedural upgrade: `ArtSource/GeneratedModels/upgrade_visuals.py` (v1). Existing door/Guard source snapshots are retained as `_pre_visual_upgrade.blend`. See `docs/DOOR_DOUBLE_SIDED_IMPLEMENTATION.md` and `docs/DRONE_FLEET_VISUAL_IMPLEMENTATION.md`; older generation commands below describe historical passes.

# Shared master drone for four AI variants — `SF_SecurityDrone`

**Asset stage:** `IMPLEMENTED` (editable procedural sources and Unity FBX exports exist; personal modeling evidence pending). The design brief below is retained as reference. Current workflow, measured counts and outstanding UV review: [GV model completion](../../../docs/GV_MODEL_COMPLETION.md).
**Owner / handoff:** Nimthara (model), Asmadala (motion)
**Instances / destination:** Scout, Guard, Interceptor, Flanker variants

## Intended appearance

Compact damaged quad-rotor enclosed in protective ducts; layered graphite armor, optical lens, exposed mechanical seams, four visible fan assemblies; credible security robot, not cartoon. Variants use colored accent rings only.

## Fixed target size / Unity use

Master footprint ~1.25W × 1.25D × 0.55H m; hover height controlled in Unity, not in Blender geometry

**Coordinate and scale rules:** 1 Blender metre = 1 Unity unit after verified FBX export; in Unity Y is up. Origin at body centre; rotors separate and centred on their own local spin axes.

## Blender object organisation

Create a named top-level collection `SF_SecurityDrone` with clearly named objects or empties:

`DroneRoot,Body_Armor,Sensor_Front,Rotor_FL,Rotor_FR,Rotor_RL,Rotor_RR,Emitter,DamageDetails`

Keep structural/static and movable/animated objects separate where needed. Preserve exact names in the scene handoff or record the final names when they differ.

## Materials and color standards

Graphite #20272D, steel #58636A, sensor cyan #61C7CF, amber #E5A54B for Guard, emergency red #BD493D for Interceptor; keep variant accent subtle.

Apply the shared material naming, roughness and light rules in [`VISUAL_STYLE_GUIDE.md`](../../../docs/VISUAL_STYLE_GUIDE.md). Keep wear subtle and coherent across rooms.

## UV / exports

UV unwrap main body and rotor housing; separate emissive material slots; retain topology evidence.

- Native editable source: `source/SF_SecurityDrone.blend` (create during implementation).
- Procedural generator, if used: `source/generate_security_drone.py` (optional; not yet generated).
- Expected Unity mesh export: `Assets/Art/Models/SF_SecurityDrone.fbx` (future implementation).
- Texture destination: `Assets/Art/Textures/security-drone/` (future implementation).
- Use stable object names, applied transforms and a documented FBX axis-conversion test.
- Save UV/mesh/material evidence in this asset directory under `evidence/`.

## Collision and gameplay interface

Gameplay root collider separate from visual rotor meshes; no complex mesh colliders for spinning blades.

**Do not break:** Do not encode AI or movement in Blender; four child visuals feed existing C# policies.

## Ready-to-use Blender creation brief

> Create an original, game-ready **shared master drone for four ai variants** for *Shift-Four*, a small first-person abandoned research facility survival game. Appearance: Compact damaged quad-rotor enclosed in protective ducts; layered graphite armor, optical lens, exposed mechanical seams, four visible fan assemblies; credible security robot, not cartoon. Variants use colored accent rings only. Exact dimensions: Master footprint ~1.25W × 1.25D × 0.55H m; hover height controlled in Unity, not in Blender geometry Object hierarchy/components: DroneRoot,Body_Armor,Sensor_Front,Rotor_FL,Rotor_FR,Rotor_RL,Rotor_RR,Emitter,DamageDetails. Pivots: Origin at body centre; rotors separate and centred on their own local spin axes. Material colors: Graphite #20272D, steel #58636A, sensor cyan #61C7CF, amber #E5A54B for Guard, emergency red #BD493D for Interceptor; keep variant accent subtle. UV rules: UV unwrap main body and rotor housing; separate emissive material slots; retain topology evidence. Gameplay/collision requirements: Gameplay root collider separate from visual rotor meshes; no complex mesh colliders for spinning blades. Produce an editable `.blend` with meaningful hierarchy, a compatible `.fbx` and a screenshot of its UV layout. Optimize for Unity real-time rendering; preserve the original modeling evidence.

## Asset acceptance checklist

- [ ] Appearance matches the abandoned industrial survival style.
- [ ] Dimension check in Blender and Unity passes.
- [ ] Object hierarchy and pivot correctly match gameplay needs.
- [ ] UV mapping has been inspected; texture files are supplied if needed.
- [ ] Proper URP material appearances after Unity import.
- [ ] Collider/line-of-sight/nav checks succeed if this is solid cover or structural geometry.
- [ ] Native source, export and model evidence are committed by the assigned owner.
- [ ] Prefab test inside the actual target room succeeds without overwriting existing runtime scripts.

## Implemented GV variant (2026-10-14)

`SF_GuardDrone_GV.blend` is the editable procedural refinement of the existing Guard OBJ. `Assets/Models/SF_GuardDrone_GV.fbx` is its Unity export. Static chassis groups are consolidated; four Rotor0..Rotor3 assemblies retain independent centered pivots. All five mesh objects have regenerated UVs; evaluated export is 3,200 triangles with eight materials. Generator: `ArtSource/GeneratedModels/prepare_game_models.py`. Preserve root AI/Health/DroneMotor/NavMeshAgent/collider when replacing visuals. See `docs/ART_ASSET_PROVENANCE.md`; manual student authorship is not established.
