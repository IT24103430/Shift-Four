# Model and Server GV completion

This is the current completion worksheet for the security drone, security door and Server/HUD review. Automated inspection does not establish personal student modeling or human playtesting. The older member report records its original test run, not current geometry measurements.

File and code names describe responsibility: `GV_ASSET_METRICS.json`, `GV_ACCEPTANCE_MATRIX.md`, `GameVisualAcceptance`, `GameModelImportPostprocessor` and `ServerRoomInstaller`. Contributor names or student IDs are not file/class prefixes. Ownership belongs in the team records. Historical raw evidence retains the original paths/hashes from its actual execution.

## Current source inspection

Run from the repository root (Blender is required):

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python tools/audit-gv-models.py
```

The read-only tool opens both native sources without saving them or exporting over the production FBX files. It writes hashes, topology/UV statistics and explicitly labeled automated UV overlays to ignored `Logs/GVCompletion/models/`. It also reimports each existing FBX into a temporary in-memory Blender scene and compares triangle counts. It checks for absent UV layers, non-finite UVs and missing exports; open boundaries and collapsed UV faces are reported for review. Overlaying all objects is not a UV overlap/distortion test.

Measured with Blender 5.2.2 on 2026-10-20:

| Source | Mesh objects | Evaluated triangles | Distinct assigned materials | Findings |
|---|---:|---:|---:|---|
| `ArtSource/Models/security-drone/SF_GuardDrone_GV.blend` | 6 | 5,948 | 13 | UV layers on all meshes; four zero-area UV faces on `GuardArmouredChassis` |
| `ArtSource/Models/security-sliding-door/SF_HeavySecurityDoor_GV.blend` | 36 | 14,256 | 9 | UV layers on all meshes; no zero-area UV faces detected |

Neither source had loose vertices or boundary edges. Both FBX reimports matched their source triangle counts. The collapsed chassis UV faces have zero-based polygon indices 822, 826, 838 and 842 in the audited source (indices can change after editing). These checks do not establish optimal topology, correct texel density, complete source/export equivalence or acceptable performance. Older 3,200/2,484 triangle values describe the earlier procedural pass.

## Personal modeling session: repeat for each mandatory model

1. Check the actual assessment brief's original-model requirement. Record inherited/procedural geometry honestly. If from-scratch personal construction is required, make that model yourself; inspecting or lightly editing a generated asset does not establish that requirement.
2. Open the native source listed above. Save a separate working copy before changes; retain the starting version. Do not rerun generators over personal refinements.
3. Record the real session. Capture the initial object hierarchy and dimensions, then show the meaningful geometry edits you actually perform. Explain the functional/design reason for each refinement. Save intermediate versions; do not create retrospective process screenshots presented as earlier work.
4. Capture Edit Mode wireframe and actual vertex/edge/face selection. Inspect duplicated/internal geometry, normals and smoothing. For the drone, retain the four movable rotor groups and their spin pivots. For the door, retain the stationary frame and independently movable panel/bolt groups expected by `LabDoor`.
5. Inspect seams and the active UV layer with a checker. Fix collapsed faces, including the four reported chassis faces; inspect distortion and texel density and pack islands for the intended material/texture workflow. Separate materials may legitimately share UV space. Save before/after UV views and explain the decisions.
6. Keep editable modifiers in the working source. Export to a separate FBX first, using metres, mesh/empty hierarchy, -Z forward and Y up. Check selected/exported objects and modifiers. Do not indiscriminately apply/reset transforms on animated parts.
7. In Unity inspect scale, orientation, material mapping, overall dimensions, rotor/panel pivots and movement. The existing `_GV.fbx` importer preserves hierarchy, uses file scale and axis conversion, imports normals and creates no mesh colliders. Preserve the gameplay root, Health/AI, collider and NavMesh components. Replace production exports only after verifying the separate export and preserving `.meta` GUIDs.
8. Record the final source/export paths, hashes, changes, tool versions and actual Unity observations. Rerun the audit after approved replacement. Keep external and agent-generated contributions identified separately from personal work.

## Human Server and HUD acceptance

Use the current standalone build. Record build/commit, machine/GPU, resolution, date, actual result and defect references for each row. All rows below remain **NOT VERIFIED by a human** until performed.

| Check | Required observation | Human result |
|---|---|---|
| Server lighting | Entry, cover sides, console and exit remain readable; inspect shadows, exposure, emissive glare and dark corners | NOT VERIFIED |
| Cover/navigation | Rack blocks shots; both cover routes and unlocked gate are traversable; closed gate blocks passage | NOT VERIFIED |
| HUD resolutions | 1280x720, 1920x1080 and 1024x768: health, ammo, objectives and prompts fit; no overlap with outcome/restart controls | NOT VERIFIED |
| Combat feedback | Visible enemy bars, occlusion, actual hit feedback, damage direction and delayed recovery behave understandably | NOT VERIFIED |
| Outcomes | Finish once and die once; restart by mouse and R; health/ammo/objectives/camera reset | NOT VERIFIED |
| Full route | Checkpoint -> Server -> Control -> Storage -> exit, using both console-first and drone-first orders | NOT VERIFIED |
| Target performance | Capture actual play profiling on the target desktop; record frame times and context before judging acceptability | NOT VERIFIED |
| Shared review | Record actual gate/combat/scene/navigation owner feedback and any resolved defects | NOT VERIFIED |

The isolated `tools/verify-combat-hud.ps1` harness adds three scripted Server viewpoints, a light inventory and a victory capture to the existing health/ammo/death/restart scenarios. These are automated review aids. Camera movement, objectives and damage are scripted; captures cannot replace the human checks above.

## Technical verification, 2026-10-20

In `Temp/GVCompletionVerification`, using Unity 6000.6.4f1: all 332 Edit Mode tests and all 93 selected Play Mode tests passed, with zero failed or skipped tests. The Play Mode result is a single clean run, distinct from the older multi-run result in `GV_FINISH.md`. Reports are `Logs/GVCompletion/EditMode.xml` and `PlayMode.xml` inside that isolated project. The model audit ran successfully and repeated source/export hashes stayed unchanged. Production scenes, prefabs, Blender sources and FBX assets were not modified by this completion pass.

The final Windows development build succeeded with zero build errors and five warnings. Its standalone probe completed 20 PNG captures, with zero runtime errors, a successful native-window restart-button click and a verified victory outcome. Final captures/result/light inventory are under `Temp/GVCompletionVerification/Logs/GVCompletionFinal/standalone/`. Build warnings include existing obsolete search APIs in `DroneWreckMotion`; the earlier build also logged a temporary-allocation warning at Editor shutdown. Neither warning record is presented as a clean, warning-free Editor session.

Visual inspection of the final Server captures at 1280x720, 1920x1080 and 1024x768 found the correct Server room/objective label, visible health/ammo/progress panels and no observed HUD clipping in those views. Racks, console accents and the gate remain distinguishable. The victory panel and Restart button fit at 1024x768. Bright cool floor lighting and emissive ceiling hotspots remain aesthetic review points for the human lighting pass; this is not target-desktop profiling or a human combat acceptance result.

The first automated capture attempt moved only the camera; the Checkpoint player collider caused the room trigger to overwrite the Server HUD label. The QA fixture now moves the player collider with each viewpoint and checks `CurrentRoom == 1`. The final capture run passed. Initial and corrected outputs are retained in separate ignored result directories. No production room-trigger change was needed.

## Evidence record

Keep each mandatory model's real evidence together: source versions, topology views, UV/checker views, before/after refinements, FBX export settings, Unity import/hierarchy and in-game movement. Add a short recording that connects these steps. Include only actual work and observations.

Suggested record fields: asset, date, source/export SHA-256, inherited contribution, personal changes, rationale, evidence paths, Unity checks, remaining defects and reviewer. Leave unknown/unperformed fields pending. Generated logs and bulk QA captures stay under ignored `Logs/`; select genuine final evidence separately according to submission requirements.

Finish when both models meet the brief with defensible personal process evidence, their current exports work in Unity, the human acceptance rows pass, and shared changes have real review. A successful automated audit alone does not close those requirements.
