# External first-person arms

Source: **fps arms (rigged only)**, uploaded by **para**, with mesh and texture originally made using MakeHuman.

- Publisher and license declaration: https://opengameart.org/content/fps-arms-rigged-only
- License: **CC0 1.0**, https://creativecommons.org/publicdomain/zero/1.0/
- Original archive: https://opengameart.org/sites/default/files/fps%20arms.7z
- Retrieved: 20 October 2026.

`Source.blend` is the original archive's `FPS ARMS RIG 1 test anim.blend`, retained as editable source. This is third-party art, not a claim of original student modeling.

`tools/prepare-first-person-arms.py` fits a two-hand pistol pose, bakes the pose into the rest mesh/bones, normalizes metres/axes, and exports `Assets/Resources/Weapons/FirstPersonArms.fbx`. Run using Blender 5.2:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python tools/prepare-first-person-arms.py
```

The export keeps the skin weights and deform bones, trims geometry above the elbows, and replaces the source IK-control parenting with a conventional forearm/hand/finger deform chain while preserving rest matrices. Runtime code supplies a dark glove surface and procedural support-arm reload motion. Source sample animation clips and textures are not imported into the game. The game does not require Blender to open or build.
