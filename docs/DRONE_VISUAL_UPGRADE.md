> **2026-10-14 implementation update:** See [visual upgrade status](ENVIRONMENT_VISUAL_POLISH_PLAN.md), [door implementation](DOOR_DOUBLE_SIDED_IMPLEMENTATION.md) and [fleet implementation](DRONE_FLEET_VISUAL_IMPLEMENTATION.md). Earlier hinged-door, shortcut, OBJ-only or missing-Blender-source instructions below are historical and superseded. Four automatic sliding gates and the accepted ADR remain canonical. Generated additions are agent-procedural work, not claimed student hand modeling.

# Drone visual upgrade — October 2026

## Source and build

- Original procedural generator: `ArtSource/GeneratedModels/generate_premium_fleet.py`
- Primary textured/UV model exports: `Assets/Models/SF_{Scout,Flanker,Guard,Interceptor}Drone.obj`
- Shared materials: `Assets/Models/ShiftFourPalette.mtl`
- Five editable Blender `.blend` files can be exported using `create_blend_sources.py` on a PC with Blender 4.x. They are **not pre-generated** in this ZIP.
- The sliding-door geometry is preserved from the earlier asset pass.

## Visual direction

All models are original, UV mapped, bevelled hard-surface sci-fi. Scout has antennae and a sensor mast; Flanker has outboard swept fins; Guard has bulky shields and bumpers; Interceptor has an arrowhead form and rear thrusters. Different optics and emissive accent colors reinforce each type. The four models share a graphite/titanium surface language.

## Unity integration

1. Open the project in Unity matching `ProjectSettings/ProjectVersion.txt`.
2. Let Unity finish importing the OBJ models and C# scripts.
3. Existing project: `Tools > Shift Four > Install Drone + Door Models`.
4. Fresh baseline: `Tools > Shift Four > Generate Greybox Once`.
5. Run `MainLab` and inspect all four agents. The `DroneRotorVisuals` component animates imported OBJ `Rotor0`...`Rotor3` groups, and has no gameplay responsibility.
6. Check the front sensor faces the player, motor housings do not block corridors, and that all AI agents, doors and combat still work.

> Unity's OBJ importer may merge group meshes depending on importer settings. If rotors don't spin, open each OBJ Model Import Settings and enable **Preserve Hierarchy** or object/group splitting as supported, then reimport. The C# script intentionally leaves the model static if named rotor groups are absent.

## Validation status

Static checks performed: all OBJ vertex/UV/face indices are in range; source generator regenerates four models; C# integration points are present; the ZIP integrity check passed. **Unity Editor compilation and Blender execution cannot be claimed here**; those are required on the student's machine before merging.

## Cross-platform conventions

`.gitattributes` normalizes text files to LF in Git, while Cursor on Windows and teammates on macOS can use their preferred editor. Avoid re-copying entire ZIPs over `.git`; transfer tracked changes and their Unity `.meta` files if relevant. Review with `git diff --ignore-space-at-eol`.

## Branch and commits

Branch: `feature/gv-drone-fleet-visual-upgrade`

Suggested separate commits:
- `feat(gv): redesign original four-drone hard-surface fleet`
- `feat(gv): animate independent drone rotors in Unity`
- `docs(gv): document Blender source workflow and validation`

Never include Unity `Library`, `Temp`, `Obj`, or `.git` in the archive.
