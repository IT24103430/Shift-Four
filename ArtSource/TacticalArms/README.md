# Shift-Four tactical arms

This is a project-owned derivative of the **para / MakeHuman CC0 arms**, selected from the locally available, verified assets. No other imported arms package was present. No new model was downloaded for this task. Original anatomy, editable sources, legacy FBX and GUIDs are preserved at `../ThirdParty/FirstPersonArms/` and `../FirstPersonArms/`.

Publisher/license declaration: https://opengameart.org/content/fps-arms-rigged-only — **CC0 1.0**. The original source is byte-identical to the archive source verified in Task 1; SHA-256 `81bed56c1718957171cb6c16ee5b77f7067ccd0d3c39ec0ff052d25322e05d75`. See `../ThirdParty/FirstPersonArms/README.md` and `SOURCE_SHA256.txt` for provenance. This is third-party anatomical art with automated fitting and garment refinement, not a claim of original hand modeling by a student.

`SF_TacticalArms.blend` is the editable derivative. Its exported skin is `Assets/Models/FPS/SF_TacticalArms.fbx`. The owned runtime assembly is `Assets/Resources/Weapons/FirstPersonArmsRig.prefab`. The original `Assets/Resources/Weapons/FirstPersonArms.fbx` remains available as the loader's fallback.

The hands keep their original anatomical topology, with one subdivision and a thin glove allowance. New fitted dorsal/knuckle reinforcement and tailored sleeve/cuff surfaces have deform weights. The sleeves include cuff ribs, restrained cloth folds, thickness, finished hems, and a rounded elbow transition. They contain arms only; upper arm ends sit below/behind the player camera. No torso, head, pistol, camera, light, collider, or rigidbody is exported.

The source skeleton is uniformly scaled by **0.085**. Individual bones are not stretched to achieve the hold pose. Upper-arm length is about **0.22031 m**, forearm **0.22709 m**, and hand bone **0.10583 m**, on both sides. The fitted rest pose uses the actual Unity-imported Five-seveN geometry as a temporary reference. Original finger lengths, three segments per finger/thumb, elbow and wrist joints remain usable. The two obsolete hand control bones are removed and their weights merged onto the hands. The export has **48 bones**, one skin, at most four normalized influences per vertex, and unit object/bone scales. `source-metrics.json` records the source mesh counts and lengths.

The five shared URP surfaces and four repeatable 256×256 base/normal detail maps are under `Assets/Materials/FPS/Arms/`. Leather and ripstop maps are generated mathematically by `tools/generate-arm-surfaces.py`; they use no downloaded or AI-generated images. Normal maps import as linear 2D normal textures; base maps use sRGB. The original missing MakeHuman texture is not required by this derivative.

`FirstPersonArmsRig` serializes the renderer and left/right upper arm, forearm and hand references. The original rig upgrade installed the owned prefab and preserved its materials. Subsequent grip and animation layers described below now use rotation-only IK and a normalized procedural reload. Gameplay damage, ammunition transfer and MainLab remain unchanged. No Animator controller or imported animation clips are added.

## Reproduction

The [reinforcement skin-weight repair](../../docs/TACTICAL_ARMS_SKIN_WEIGHTS.md)
stages a candidate that removes finger-driven dorsal-panel spikes and cross-digit
knuckle-guard weights. The active owned source/export remain unchanged because
the candidate failed full visual acceptance. See `WeightRepairCandidate/`.
It preserves native glove/sleeve weights, all bind geometry and the skeleton.
Full visual acceptance remains partial: the original glove shell still strains
under extreme compound finger rotations, and the closed-grip pose needs more work.
The full generator below applies the same repair automatically. For a weights-only
staged export of the owned derivative, without regenerating surfaces:

```sh
/path/to/blender -b --python tools/tactical_arm_skinning.py -- \
  --input-blend ArtSource/TacticalArms/SF_TacticalArms.blend \
  --output-dir /tmp/shiftfour-arms-weight-repair
```

The repair is version-stamped on the Blender mesh so rerunning it does not smooth
the weights a second time. Preserve the existing FBX `.meta` and validate the
staged FBX in a disposable Unity project before updating the owned export.

Use Blender 4.5+ and Unity 6000.6.4f1. All output generation should start in a staging directory or disposable Unity project copy. Never run the legacy arms generator to update this derivative: that generator writes the preserved legacy export.

1. Export the existing pistol's actual imported geometry for fitting, using a disposable project copy containing `TacticalArmsAssetBuilder.cs`:

   ```sh
   SHIFTFOUR_ARMS_REFERENCE=/tmp/shiftfour-arms/pistol-reference.json \
   /path/to/Unity -batchmode -quit -projectPath /path/to/disposable-copy \
     -executeMethod TacticalArmsAssetBuilder.ExportPistolReference -logFile /tmp/arms-reference.log
   ```

   Create the temporary output directory first. The reference JSON is local authoring data; the vendor pistol mesh and texture are excluded from the saved derivative Blender file and FBX.

2. From this repository, generate the new source, FBX, metrics, and clearly labeled Blender previews:

   ```sh
   /path/to/blender -b --python tools/prepare-tactical-arms.py -- \
     --output-dir /tmp/shiftfour-arms \
     --pistol-reference /tmp/shiftfour-arms/pistol-reference.json
   python3 tools/generate-arm-surfaces.py /tmp/shiftfour-arms/Surfaces
   python3 tools/pack-arm-smoothness.py \
     --surfaces-dir /tmp/shiftfour-arms/Surfaces \
     --output-dir /tmp/shiftfour-arms/Surfaces
   ```

3. Copy only `SF_TacticalArms.blend`, `source-metrics.json`, `SF_TacticalArms.fbx`, and the six surface TGAs (four base/normal maps and two packed masks) into their existing derivative paths. Keep the destination `.meta` files to retain GUIDs. Reimport the new FBX; the owned prefab continues to reference it. For a material-only update, follow [MATERIALS.md](MATERIALS.md) instead and preserve the mesh and original maps.
4. For initial setup only, run `TacticalArmsAssetBuilder.Create` or **Tools → Shift Four → FPS → Create Tactical Arms Prefab Once**. This configures the new FBX and textures, creates owned materials and prefab bindings, and sets all presentation objects to Ignore Raycast. It refuses to overwrite an existing prefab/material. Later artist changes to the owned prefab should be preserved.
5. Run `TacticalArmsRigEditModeTests`, `TacticalArmsRigPlayModeTests`, the existing FiveSeven asset/playable checks, and `GVFinishPlayModeTests`.

The builder never edits a scene, vendor asset, original arms source, or existing weapon tuning. Original source credit belongs with this asset whenever it is copied elsewhere.

The subsequent [material finish](MATERIALS.md) retains these four maps and adds source-derived smoothness masks, a dark laboratory palette, and runtime material-lifetime verification.

The subsequent [two-handed grip pass](GRIP.md) stores editable wrist targets, rotational arm IK and an authored finger profile on the owned prefab.

The [idle motion layer](IDLE.md) adds saved, restrained breathing settings to the pistol presentation prefab while carrying the current authored grip as a shared pose.

The [movement animation layer](MOVEMENT.md) observes actual travel and ground contact to add walking, takeoff, airborne hold and landing poses without changing player physics.

The [firing animation layer](FIRING.md) uses accepted-shot impulses with controlled muzzle rise and damped recovery on the shared pistol/arms pivot.

The [detailed reload layer](RELOAD.md) uses authored curves and a magazine wrist target while leaving reload timing and ammunition transfer in gameplay.

See [EQUIP.md](EQUIP.md) for authored equip/unequip settings, state contracts and transition hooks.

See [MULTI_WEAPON.md](MULTI_WEAPON.md) for the shared binding, five configurations, non-playable preview limits and integration steps. See [FRAMING.md](FRAMING.md) for the one-camera cover pose and resolution verification.
