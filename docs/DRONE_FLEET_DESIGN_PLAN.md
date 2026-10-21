> **2026-10-14 implementation update:** See [visual upgrade status](ENVIRONMENT_VISUAL_POLISH_PLAN.md), [door implementation](DOOR_DOUBLE_SIDED_IMPLEMENTATION.md) and [fleet implementation](DRONE_FLEET_VISUAL_IMPLEMENTATION.md). Earlier hinged-door, shortcut, OBJ-only or missing-Blender-source instructions below are historical and superseded. Four automatic sliding gates and the accepted ADR remain canonical. Generated additions are agent-procedural work, not claimed student hand modeling.

# Shift Four — Four-Drone Art Direction and Integration Plan

## Decision
Keep one coherent original sci-fi drone family, with four different silhouettes and shared hard-surface styling. Existing Scout, Flanker, Guard and Interceptor behavior remains intact. Preserve the drone root's AI brain, NavMeshAgent/motor, health, colliders, and shooting/visibility logic. Replace only visual children.

| AI prefab | Codename | Shape language | Accent | Signature details |
|---|---|---|---|---|
| Scout | Specter S-01 | compact, narrow | cyan | front optical eye, thin antenna/radar module, small protected rotors |
| Flanker | Viper F-02 | swept, angular | amber/orange | forward-swept arms, split thruster housings, side targeting bars |
| Guard | Bastion G-03 | broad, shield-like | red | armored canopy, thick rotor cages, heavy underbody plates |
| Interceptor | Phantom I-04 | arrowhead, elongated | violet | rear stabilizers, streamlined nose, twin thrust ports |

## Work sequence
1. **Baseline safety** — verify existing scene, prefab references and models work. Commit the current functional checkpoint first.
2. **Design approval** — make orthographic front/top/side sketches and a consistent dimension/pivot spec. The reference images in planning are *not* final assets.
3. **Common modeling kit** — produce original center core, propulsion pieces, optical sensor, armor segments and material palette as editable Blender objects. Make clean UV islands (the current OBJ's 0–1 face UVs are a basic mapping, not proof of optimized texture-atlas UVs).
4. **Four genuine variants** — alter silhouette/geometry, not only material colors. Each variant needs named parts and independent rotor/housing transforms for animation.
5. **Sliding lab door** — retain existing door animation/collision logic; refine the door mesh, UVs, matching graphite/metal materials and two sliding panel pivots where appropriate.
6. **Export** — save `ArtSource/Blender/{Scout,Flanker,Guard,Interceptor,SlidingDoor}.blend`; export meshes as FBX/OBJ with UVs into `Assets/Models/`. Source `.blend` files must be created and checked in Blender for coursework evidence.
7. **Unity prefab variants** — prefab visual assets under `Assets/Prefabs/Visuals/`; plug into `Assets/Prefabs/Agents/{Scout,Flanker,Guard,Interceptor}.prefab`. No AI root components moved or removed. Set common bounding dimensions, orientation, scale, and collision-independent visuals.
8. **Animation** — spin rotor children; add tiny hover/tilt response, hit light flash and destroyed state without controlling NavMeshAgent position through art scripts.
9. **Test** — each drone identifiable at a glance and in darkness, correct raycast hit detection, AI behavior unchanged, stable FPS, scene and editor re-open without missing materials.
10. **Evidence** — capture Blender edit-mode topology, UV editor, materials, four orthographic images, Unity prefab inspector and in-game video/screenshots. Record original contributions.

## Acceptance criteria
- Four different low-poly meshes with cohesive styling and valid UVs.
- Existing gameplay/AI functionality preserved.
- Original editable Blender models and evidence for Member 3 GV rubric.
- All visual resources versioned and documented; meshes affordable for target desktop hardware.
- Installer handles four separate model paths only when all four assets are ready (current one-model installer is a baseline, **not** a completed four-variant implementation).

## Git text standard
`.gitattributes` uses `* text=auto` and explicit LF for Unity YAML, C#, docs, OBJ/MTL, and configuration text. Binary assets are explicitly binary. `.editorconfig` makes LF the editor default. Git then normalizes text in the repository so Windows CRLF vs macOS/Linux LF does not create spurious full-file changes. To renormalize existing tracked files *once*, run:

```bash
git add --renormalize .
git diff --cached --stat
git diff --cached --check
git commit -m "chore(repo): normalize cross-platform line endings"
```

Review `git diff --cached --stat` carefully before committing. Git may still show file changes from content edits; line ending normalization does not suppress genuine edits. On Windows, avoid a conflicting global autocrlf setting when troubleshooting; per-repo `.gitattributes` is authoritative for matched files.

## Suggested PR boundaries
- `chore/repo-line-endings`: line ending policy and renormalization
- `design/gv-drone-fleet`: reference and art-direction docs
- `feature/gv-drone-scout` (shared base/source + Scout)
- `feature/gv-drone-variants` (Flanker, Guard, Interceptor)
- `feature/gv-door-refinement` (sliding door source/mesh)
- `feature/gv-visual-integration` (prefabs and gameplay checks)
