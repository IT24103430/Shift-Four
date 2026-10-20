# Shift-Four first-person arms

Open `FirstPersonArms.blend` in Blender to inspect the fitted, skinned arms.
`Reference.png` shows the two-hand grip without the pistol obscuring the fingers.
Select `FirstPersonArmsRig` and enter Pose Mode to inspect the forearm, hand,
and finger bones. The saved rest pose is the gameplay pistol grip.

The model adapts the existing CC0 arms by para / MakeHuman, rather than being
newly sculpted anatomy. Original source and attribution are retained in
`../ThirdParty/FirstPersonArms/`.

The prepared model has three surface regions: dark gloves, slate sleeves, and
wrist cuffs. These are material regions on the original skin, not separately
modeled garments. Unity's `FirstPersonArms` component maps the named slots to
URP materials and retains the existing procedural support-hand reload motion.

Regenerate the Blender source, reference render, and Unity FBX from the project root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python tools/prepare-first-person-arms.py
```

Export: `Assets/Resources/Weapons/FirstPersonArms.fbx`. The Blender file stays
outside `Assets`; the game does not need Blender installed. Regeneration
overwrites the prepared file, so save manual modeling variations under another
name. Reference lights and camera are not exported.
