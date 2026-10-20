# Implemented four-drone refinement

Current compact scale, visible barrel, matching muzzle/shot direction and verification workflow: [compact drone presentation](DRONE_PRESENTATION_FINISH.md).

| Variant | Implemented silhouette/detail additions |
|---|---|
| Scout / Specter S-01 | Elevated survey camera on a mast, camera glass, slim shoulder rails, antenna bases and underslung gimbal; cyan accent |
| Guard / Bastion G-03 | Wide plated nose, deep side shields with upper edge armor and recesses, reinforced belly heat-sink fins, underslung optic; red accent |
| Interceptor / Phantom I-04 | Narrow forward keel, swept stabilizers, enlarged trailing thrust housings and individual exhaust vanes; violet accent |
| Flanker / Viper F-02 | Angled tracking bars, split vented thrusters and offset upper sensor; amber accent |

All share a graphite shell, metal edge response, recessed cooling, glass optics and modest emissive eye. Original chassis/rotor cage shapes remain, so identity and physical center are preserved. No collision is added. Existing DroneMotor bobbing and DroneRotorVisuals spin/death stop are retained without duplicate presentation motion or health subscriptions.

Source/export map for each `{Scout,Guard,Interceptor,Flanker}`:

- `ArtSource/Models/security-drone/SF_{Type}Drone_GV.blend`
- `Assets/Models/SF_{Type}Drone_GV.fbx`
- `Assets/Prefabs/Agents/{Type}.prefab`, plus actual MainLab instances.

`upgrade_visuals.py` v1 imports legacy OBJ groups when a new source is needed, retains independent `Rotor0`–`Rotor3` mesh objects, centers their origins, welds the OBJ per-face vertices before recalculating outward normals, UV maps and exports FBX. It extends the existing Guard source. Static additions are consolidated; all rotors stay separate. Legacy OBJ assets/metas are kept. The installer now selects a GV FBX for every type when available. Existing GameModelImportPostprocessor already covers all `_GV.fbx` files and maps materials to URP/Lit.

The targeted refresh preserves each existing visual's local transform and updates the motor's serialized visual reference. Brain, Health, NavMeshAgent, colliders and gameplay fields are not rebuilt. Read [metrics](QA/visual-upgrade/asset-metrics.json) and [QA](QA/VISUAL_UPGRADE_TEST_MATRIX.md); mesh budgets are not measured frame rates.
