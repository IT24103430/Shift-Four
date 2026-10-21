# Core GV final implementation report

Follow-up: [current model/Server completion worksheet](../GV_MODEL_COMPLETION.md) separates the current asset audit from outstanding personal modeling and human acceptance. Geometry counts and verification statements below are historical to this report's run.

Date: 2026-10-14. Branch: `feature/gv-IT24610327-four-gate-implementation`. Baseline: `b471058` with fetched `origin/dev` already in its ancestry. The implementation history was recreated in eight local commits using IT24610327 naming. No push or merge was performed during this recreation; the previous branch is retained. See `git log b471058..HEAD` for real commit hashes.

## Delivered

- Repaired scene reload and Unity search APIs; coherent four-gate installer/door/graph contracts.
- Independent room-specific drone AND console progression, duplicate suppression, four automatic bolt-first sliding gates, permanent shortcut wall and gated exit.
- Migrated existing MainLab without greybox regeneration, retaining original scene/asset GUIDs; rebaked and tested navigation.
- Actual editable Blender Guard/door sources and FBX exports, stable rotor/panel hierarchy, UVs/normals, URP import and truthful procedural provenance.
- Reusable Server racks, retained cover colliders/destinations, ceiling/lighting/finish and tested cover LOS/reachability.
- Room/objective HUD with actual health maximum, gate state, individual mission progress, one-time consoles, victory/defeat/restart and optional agent details.
- Safe repeat installation and byte-preserving skip for untouched agent prefabs. Shared changes are documented for their team owners.

## Evidence and limits

Unity 6000.6.4f1 ran in an isolated copy. 154 acceptance assertions passed in headless Editor Play Mode; four additional assertions confirmed untouched prefab byte preservation. Compilation completed without C# errors/warnings. Tests exercised actual Health/death events, console interactions, gate animation, physics rays, player CharacterController passage, NavMesh paths, A*, live Guard movement, imported leaf dimensions, victory, defeat and build-index restart. Assets have no missing metas, duplicate GUIDs or unresolved project/package references.

Blender 4.5.8 opened the legacy source, generated the new editable sources and exported FBX; Guard is 3,200 triangles and door 2,484. Automated Cycles previews were visually inspected. These procedural models/renders are not proof of personal student modeling.

Recorded issue: UnityEditor.Search indexing threw ArgumentOutOfRangeException in batch Play Mode startup. Gameplay assertions completed, but an entirely error-free Editor session is not claimed. Legacy .blend auto-import in Assets also needs a system Blender installation/association; the new game visuals use FBX.

NOT_VERIFIED: human full combat playthrough, rendered HUD/cursor/resolution acceptance, target-desktop profiling, name-only scene restart, genuine individual modeling evidence and teammate signoff. Prior test failures and fixes are retained in the progress log.

## Open and verify

Open `Assets/Scenes/MainLab.unity` in Unity 6000.6.4f1 after reimport. It already includes the delivered migration. Test drone-first and console-first in every room; walk the full combat route; inspect F3/E/R and both outcomes. For older baselines use **Tools > Shift Four > Install Drone + Door Models**, **Polish Server Room**, then **Apply Game Models to MainLab** for an intentional visual refresh. Do not regenerate MainLab.

Open the two GV blends in `ArtSource/Models/security-drone/` and `security-sliding-door/`. Inspect/edit topology, UVs and pivots, record personal changes, then capture real Blender and Unity evidence. The procedural generator overwrites its GV output files, so retain personal refinements separately before rerunning it.

Priority handoff: the environment owner reviews scene/bake and the Editor indexing issue; the team performs full combat/UI acceptance; Core developer produces genuine modeling evidence; the combat systems owner reviews door/death contracts and the navigation owner graph/rotor integration. Profile after that visual review. No external messages were sent.

## Maintained documentation

- `docs/members/CORE_GV_IMPLEMENTATION.md`: ownership, architecture, exact menus, Blender/Unity workflow, reproduction, troubleshooting and viva.
- `docs/members/CORE_GV_PROGRESS.md`: chronological work, failures, fixes and phase results.
- `docs/QA/GV_ACCEPTANCE_MATRIX.md`: setup, steps, expected/observed results and explicit verification status.
- `docs/QA/GV_AUTOMATED_RESULTS.md`: executed assertion record.
- `docs/ART_ASSET_PROVENANCE.md` and `docs/QA/GV_ASSET_METRICS.json`: provenance and measured geometry.
- `docs/decisions/ADR-FOUR-GATE-CONTRACT.md`: shared gate/graph/migration decision.
- Existing team, room progression, door, master-plan and member instructions link to the superseding four sliding gates.

## Exact changed-file inventory

A = added, M = modified relative to baseline. No original assets were deleted.

- `A` — `ArtSource/GeneratedModels/prepare_game_models.py`
- `A` — `ArtSource/GeneratedModels/render_game_models.py`
- `M` — `ArtSource/Models/security-drone/README.md`
- `A` — `ArtSource/Models/security-drone/SF_GuardDrone_GV.blend`
- `A` — `ArtSource/Models/security-sliding-door/README.md`
- `A` — `ArtSource/Models/security-sliding-door/SF_HeavySecurityDoor_GV.blend`
- `M` — `Assets/Editor/BaselineSceneBuilder.cs`
- `A` — `Assets/Editor/GameVisualAcceptance.cs`
- `A` — `Assets/Editor/GameVisualAcceptance.cs.meta`
- `A` — `Assets/Editor/GameModelImportPostprocessor.cs`
- `A` — `Assets/Editor/GameModelImportPostprocessor.cs.meta`
- `A` — `Assets/Editor/ServerRoomInstaller.cs`
- `A` — `Assets/Editor/ServerRoomInstaller.cs.meta`
- `M` — `Assets/Editor/ShiftFourModelInstaller.cs`
- `M` — `Assets/Editor/ShiftFourUrpSetup.cs`
- `A` — `Assets/Materials/Floor_Shared.mat`
- `A` — `Assets/Materials/Floor_Shared.mat.meta`
- `A` — `Assets/Materials/Server_Cyan.mat`
- `A` — `Assets/Materials/Server_Cyan.mat.meta`
- `A` — `Assets/Materials/Server_Steel.mat`
- `A` — `Assets/Materials/Server_Steel.mat.meta`
- `A` — `Assets/Materials/Server_Trim.mat`
- `A` — `Assets/Materials/Server_Trim.mat.meta`
- `A` — `Assets/Materials/Server_Vent.mat`
- `A` — `Assets/Materials/Server_Vent.mat.meta`
- `A` — `Assets/Materials/Wall_Shared.mat`
- `A` — `Assets/Materials/Wall_Shared.mat.meta`
- `A` — `Assets/Models/Rack_Server_Cyan.asset`
- `A` — `Assets/Models/Rack_Server_Cyan.asset.meta`
- `A` — `Assets/Models/Rack_Server_Steel.asset`
- `A` — `Assets/Models/Rack_Server_Steel.asset.meta`
- `A` — `Assets/Models/Rack_Server_Trim.asset`
- `A` — `Assets/Models/Rack_Server_Trim.asset.meta`
- `A` — `Assets/Models/Rack_Server_Vent.asset`
- `A` — `Assets/Models/Rack_Server_Vent.asset.meta`
- `A` — `Assets/Models/SF_GuardDrone_GV.fbx`
- `A` — `Assets/Models/SF_GuardDrone_GV.fbx.meta`
- `A` — `Assets/Models/SF_HeavySecurityDoor_GV.fbx`
- `A` — `Assets/Models/SF_HeavySecurityDoor_GV.fbx.meta`
- `M` — `Assets/Prefabs/Agents/Guard.prefab`
- `M` — `Assets/Prefabs/Sections/Server.prefab`
- `A` — `Assets/Prefabs/Sections/ServerRack.prefab`
- `A` — `Assets/Prefabs/Sections/ServerRack.prefab.meta`
- `M` — `Assets/Scenes/MainLab.unity`
- `M` — `Assets/Scenes/MainLab_NavMesh.asset`
- `M` — `Assets/Scripts/AI/Core/DroneBrain.cs`
- `M` — `Assets/Scripts/AI/Core/WaypointGraph.cs`
- `M` — `Assets/Scripts/Gameplay/ConsoleSwitch.cs`
- `M` — `Assets/Scripts/Gameplay/DroneRotorVisuals.cs`
- `M` — `Assets/Scripts/Gameplay/ExitZone.cs`
- `M` — `Assets/Scripts/Gameplay/GameSession.cs`
- `M` — `Assets/Scripts/Gameplay/LabDoor.cs`
- `A` — `Assets/Scripts/Gameplay/MissionProgress.cs`
- `A` — `Assets/Scripts/Gameplay/MissionProgress.cs.meta`
- `A` — `Assets/Scripts/Gameplay/RoomZone.cs`
- `A` — `Assets/Scripts/Gameplay/RoomZone.cs.meta`
- `A` — `docs/ART_ASSET_PROVENANCE.md`
- `M` — `docs/DOOR_REDESIGN_IMPLEMENTATION.md`
- `M` — `docs/FOUR_ROOM_PROGRESSION.md`
- `M` — `docs/GRAPHICS_FIRST_MASTER_PLAN.md`
- `M` — `docs/LEVEL_BLUEPRINT.md`
- `A` — `docs/QA/GV_ASSET_METRICS.json`
- `A` — `docs/QA/GV_AUTOMATED_RESULTS.md`
- `A` — `docs/QA/GV_ACCEPTANCE_MATRIX.md`
- `A` — `docs/QA/previews/Door.png`
- `A` — `docs/QA/previews/Guard.png`
- `M` — `docs/TEAM_RESPONSIBILITIES.md`
- `A` — `docs/decisions/ADR-FOUR-GATE-CONTRACT.md`
- `M` — `docs/members/03-models-guard.md`
- `M` — `docs/members/03_MODEL_IMPORT_QUICKSTART.md`
- `A` — `docs/members/CORE_GV_IMPLEMENTATION.md`
- `A` — `docs/members/CORE_GV_PROGRESS.md`
- `A` — `docs/members/CORE_GV_FINAL_REPORT.md` (this report)
