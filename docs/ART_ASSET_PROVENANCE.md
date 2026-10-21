# Art asset provenance

No manual student modeling, screenshots or authorship are claimed by this implementation.

Current read-only inspection and personal evidence workflow: [GV model completion](GV_MODEL_COMPLETION.md). The October 20 audit measures 5,948 evaluated triangles for the Guard and 14,256 for the door after later procedural upgrades; the 3,200/2,484 figures below describe the initial pass. Four collapsed UV faces on `GuardArmouredChassis` require review before claiming texture-ready UVs. Current source/export hashes and counts are in `docs/QA/GV_ASSET_METRICS.json`.

| Asset | Source / origin | Work performed | Export |
|---|---|---|---|
| Existing fleet | Assets/Models/SF_*Drone.obj; ArtSource/GeneratedModels/generate_premium_fleet.py | Preserved existing procedural meshes | Original OBJ/MTL preserved |
| Guard GV | ArtSource/Models/security-drone/SF_GuardDrone_GV.blend | Procedural Blender import of existing Guard OBJ; grouped static chassis; welded coincident vertices; four rotor assemblies with centered pivots; regenerated UV islands and outward normals | Assets/Models/SF_GuardDrone_GV.fbx |
| Legacy door | Assets/Models/SF_SlidingLabDoor.blend, .fbx, .obj | Inspected in Blender 4.5.8; 22 UV-bearing mesh pieces and a root; legacy names do not match runtime animation groups. Retained unchanged | Original exports preserved |
| Heavy door GV | ArtSource/Models/security-sliding-door/SF_HeavySecurityDoor_GV.blend | New procedural two-leaf mesh with bevel modifiers, four ribs per leaf, frame, bolts, status strips, hazard trims, terminal and reader | Assets/Models/SF_HeavySecurityDoor_GV.fbx |
| Server rack | Assets/Editor/ServerRoomInstaller.cs | Procedural Unity blade server geometry combined by material; four mesh batches per rack | Assets/Prefabs/Sections/ServerRack.prefab and Assets/Models/Rack_Server_*.asset |

Reproduction: `blender -b --python ArtSource/GeneratedModels/prepare_game_models.py`. Blender 4.5.8 was run from a temporary official download (https://download.blender.org/release/Blender4.5/). Scripts are inputs; the saved blends are editable deliverables. Metrics are recorded in docs/QA/GV_ASSET_METRICS.json: Guard 3,200 evaluated triangles, five mesh objects, eight materials; door 2,484 evaluated triangles, 23 mesh objects, five materials. Materials are flat industrial colors with metallic/roughness values and selected emissive accents; no external textures or licensed image sources.

UVs: Blender smart-projected islands with margins exist on all exported meshes. These are suitable for the current flat materials, not an authored packed texture atlas. Student should inspect distortion/texel density and pack a shared atlas before painting unique wear. Apply modifiers only to export; keep editable door bevels in the blend. Normals are recalculated outward. Flat facets on the Guard preserve the existing industrial silhouette.

Scale/pivots: metres, Blender Z-up, FBX -Z forward / Y-up. Unity import enforces file scale, axis conversion and URP material conversion. FBX conversion can leave transformed wrapper nodes; installer normalizes the four door animation pivots while preserving world-space geometry. Guard rotor geometry is grouped Rotor0..Rotor3; DroneRotorVisuals adds centered runtime pivots and stops rotation on death.

Automated previews: docs/QA/previews/Guard.png and Door.png are Cycles renders made by render_game_models.py. They were visually inspected for silhouette/materials; they are not screenshots of a student's modeling process or Unity gameplay.

Required genuine evidence: the model owner opens the blends, makes and records personal refinements acceptable to the assessor, captures Edit Mode topology and UV Editor views, records exports/import settings, and captures real Unity animation and gameplay. Existing procedural geometry cannot establish individual manual authorship. Render polish, Unity lighting, UV painting suitability and measured draw calls/frame time still need final human acceptance.

## Visual upgrade, 2026-10-14

`upgrade_visuals.py` v1 procedurally extended existing door and Guard Blender sources and imported the repository's original OBJ variants for Scout, Interceptor and Flanker. Original pre-upgrade sources are preserved beside the new sources. Additions, UV preparation, FBX export, scene integration and evidence rendering were automated by the coding agent. No student hand-modeling claim is made. Legacy source credit is retained. `render_visual_upgrade.py` produces Blender asset previews; Unity evidence is labeled separately. Review/source editing by the named students remains their own responsibility.
