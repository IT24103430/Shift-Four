> **Current contract (2026-10-14):** four automatic horizontal sliding gates, each requiring its own drone AND console. Historical hinged/shortcut and console-only instructions below are superseded. See `docs/decisions/ADR-FOUR-GATE-CONTRACT.md` and `docs/members/CORE_GV_IMPLEMENTATION.md`.

# Member 3 — Done-for-you starter models: drone + sliding door

## Fastest Unity workflow (no Blender installation required)

1. Open the extracted project in **Unity 6000.6.4f1** (or the exact version your team has standardized on).
2. Let Unity import `Assets/Models/SF_SecurityDrone.obj`, `SF_SlidingLabDoor.obj`, and `ShiftFourPalette.mtl`.
3. If you **have not generated MainLab yet**, use **Tools > Shift Four > Generate Greybox Once**. The updated builder immediately uses both textured/UV-mapped models in the scene.
4. If you **already generated MainLab**, use **Tools > Shift Four > Install Drone + Door Models** instead. This updates existing agent prefabs and the door without rebuilding your level or changing AI/collision scripts.
5. Open `Assets/Scenes/MainLab.unity` and press Play. Inspect each drone and interact with the door using `E`.
6. Commit only after verifying meshes, colors, colliders, door animation and AI; commit generated `.meta` files as well.

**Do not regenerate an existing scene.** The installer is intended for both old and new codebases. Take a git commit before running it on a heavily edited scene.

## What is actually included

- Fully editable low-poly geometry stored in 2 Wavefront OBJ files, with UV coordinates (`vt`) on each face.
- Material palette from `ShiftFourPalette.mtl` (dark metal panels, light trims, luminous-color accents).
- Procedural source under `ArtSource/GeneratedModels/generate_obj_models.py` — the auditable definition of both original assets.
- A Blender helper script (`create_blend_sources.py`) which imports the meshes and saves actual `.blend` files. You only need Blender to generate/inspect these for your coursework evidence, not for gameplay.
- Safe editor integration for agent visuals and the existing door; keeps root `NavMeshAgent`, `DroneMotor`, health, colliders, rigidbody and `LabDoor` logic unchanged.

## Coursework/assessment caution

The assignment explicitly requests original **Blender** models, sensible topology, UVs, and modeling evidence. Generated geometry is not equivalent to proof that the student personally modeled in Blender. If the lecturer requires manual Blender authorship, you must **open and refine these models yourself**, retain the editable `.blend` files and capture authentic Blender topology/UV screenshots. Do not claim to have done manual modeling you did not perform.

For fast Blender source creation once Blender is installed:

```bash
blender --background --python ArtSource/GeneratedModels/create_blend_sources.py
```

This creates `ArtSource/GeneratedModels/SF_SecurityDrone.blend` and `SF_SlidingLabDoor.blend`. If the `blender` command isn't on PATH, run the script from Blender's Scripting workspace. For assessment, inspect and make personal refinements, then capture Edit Mode topology and UV Editing workspace screenshots.

## Next quality pass (recommended)

- Inspect face normals, UV islands and materials in Blender.
- Add bevels selectively, improve silhouettes, and refine rotor housings/door panels.
- Ensure the models' topology is suited to real-time rendering and their pivots match animation.
- Compare Unity render with the greybox; verify the raycast hit collider on the original drone root still works.
- Document asset changes in `docs/members/03-models-guard.md`, then continue with Guard AI.

## Known limitations

- I could not run the Unity Editor or Blender inside the package preparation environment. Meshes and installer were generated and checked structurally, but in-editor Play mode, material import and scene results must be verified locally.
- These are procedural starter assets, not hand-sculpted professional models. Unity's OBJ/MTL material appearance may vary depending on Render Pipeline/import settings.
