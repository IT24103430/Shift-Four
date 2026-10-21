# Branch: feature/gv-drone-fleet-original-models

Suggested work owner: Student 3 (Nimthara), with team integration review.

## Scope
- Four original UV-mapped drone OBJ meshes
- Reusable Blender source conversion script
- Unity model installer and scene-builder visual integration
- Cross-platform line ending configuration
- Docs and model testing checklist

## Suggested commits
1. `chore(repo): normalize line endings and editor settings`
2. `feat(gv): add four distinctive uv-mapped drone meshes`
3. `feat(gv): integrate drone variants into Unity editor tooling`
4. `docs(gv): document Blender source generation and QA`

## QA
- Import all assets without Console errors
- Generate scene and verify all four distinct visuals
- Existing AI and Health, DroneMotor and collider intact
- Install command remains idempotent
- Confirm models have UV coordinates
- Export five editable .blend files using Blender
