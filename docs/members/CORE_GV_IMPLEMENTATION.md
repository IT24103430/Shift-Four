# Core developer — Student 3 / GV Core Developer

This branch implements the approved four-room sequence using the existing project. It includes automated Unity Editor/Play Mode checks and procedural Blender sources. It does not establish manual student modeling authorship or a human-playtested release.

## Scope and ownership

Core developer: Server prefab/cover presentation, Guard visual asset, heavy door visual asset, console identity/state, mission/HUD/outcomes and evidence documentation. Existing Guard policy, player movement/combat and Health rules are retained.

Runtime files:
- `Assets/Scripts/Gameplay/MissionProgress.cs`: four independent console/drone pairs, validation, duplicate suppression, completion; SecurityGate enum.
- `Assets/Scripts/Gameplay/GameSession.cs`: scene singleton, actual health maximum, identity reporting, room objectives, HUD, victory/defeat/restart, F3 decisions.
- `Assets/Scripts/Gameplay/ConsoleSwitch.cs`: room identity, one-shot interaction, persisted indicator and material property block.
- `Assets/Scripts/Gameplay/ExitZone.cs`: presence check, early denial and late completion handling.
- `Assets/Scripts/Gameplay/RoomZone.cs`: room trigger feeds HUD.
- `Assets/Scripts/Gameplay/LabDoor.cs`: shared the combat systems owner integration; stationary physical root, bolt/leaf timing, independent passage release and status colors.
- `Assets/Scripts/Gameplay/DroneRotorVisuals.cs`: model rotor grouping; stops spin after Health death.
- `Assets/Scripts/AI/Core/DroneBrain.cs`: shared event contract changed to report the actual brain on death.
- `Assets/Scripts/AI/Core/WaypointGraph.cs`: shared the navigation owner integration; four independent gate IDs, version invalidation, legacy schema recognition. A* algorithm preserved.

Editor files:
- `Assets/Editor/ShiftFourModelInstaller.cs`: migration by known named objects, additive scene access, graph preflight, stable gate/drone preservation, explicit GV visual refresh.
- `Assets/Editor/ServerRoomInstaller.cs`: reusable rack and Server-only finish generation.
- `Assets/Editor/GameModelImportPostprocessor.cs`: metre/file-scale FBX import, URP materials, no imported colliders, no animation/cameras/lights.
- `Assets/Editor/GameVisualAcceptance.cs`: dependency-free batch acceptance harness; intentionally uses a disposable project copy.
- `Assets/Editor/BaselineSceneBuilder.cs`: new-project generation uses the same four-gate contract. Never use to replace an existing scene.
- `Assets/Editor/ShiftFourUrpSetup.cs`: TypeCache replaces unsafe assembly enumeration warning.

Assets: `Assets/Scenes/MainLab.unity`, existing `MainLab_NavMesh.asset`, `Assets/Prefabs/Sections/Server.prefab`, new `ServerRack.prefab`, rack mesh assets, `Assets/Materials/Server_*.mat`, gate materials and updated drone visual references. Existing asset metas are retained. New FBX and source paths are listed in `docs/ART_ASSET_PROVENANCE.md`.

## Architecture and room flow

Checkpoint/Scout → Server/Guard → Control/Interceptor → Storage/Flanker → Exit. Console and death order are interchangeable. MissionProgress flags are indexed by validated identity; four counters alone cannot open another gate. Drone type, not FindObjects return order, defines its room. Duplicate callbacks do nothing. Missing objectives remain locked. Each console's serialized sectionName must exactly match Checkpoint, Server, Control or Storage.

Each LabDoor reads only its own pair. Animation is irreversible: 0.4s bolt retraction, then 1.2s smooth horizontal movement. Leaves move 2.15m outward; bolts follow their leaf after retracting. Door assembly, LeftPanel, RightPanel, LeftBolt and RightBolt are stable names. Frame/physical root never translate. Passage collider and carving obstacle remain until the opening is clear; the graph edge becomes available at the same completion point. Status lights change red → amber → green. Graph.Version lets existing DroneMotor replan.

The original Checkpoint–Storage opening is sealed. The exit has a fourth gate and waypoint. This is the existing quadrant layout, not the superseded proposed linear-room rebuild. The installer preserves known waypoint transforms and rejects unrecognized legacy graph data. After migration it preserves graph edits. It does not infer a safe migration for an arbitrary custom level.

Server racks occupy exactly the old 2 x 2.2 x 1.5m fixed-cover envelopes. Their original solid BoxColliders remain responsible for shots/LOS/navigation. Cosmetic geometry has no colliders. Each rack uses four combined mesh/material batches. Server finish adds a ceiling, maintenance markings, service bands and three unshadowed lights. Existing Guard destinations (-14,0,12) and (-6,0,12) remain; the harness checks their NavMesh reachability and cover ray blocking. Desktop frame-time profiling is still required.

HUD uses built-in IMGUI; no packages added. It scales from 1280x720, displays actual Health.Maximum, current trigger room, next objective, all drone/console/gate states, 8-part progress and interaction prompts. F3 toggles agent decision text. Exit denies early entry. Outcome is latched; R reloads by integer build index when valid, otherwise scene name. Scene-owned progress, health, doors and consoles initialize fresh. Name fallback requires the scene to be loadable by Unity; normal MainLab is in build settings.

## Unity integration

1. Open with Unity 6000.6.4f1, let compilation/import finish, and review Console.
2. Existing checked-in MainLab should already contain the migrated assets. For an older supported baseline, save edits, then **Tools > Shift Four > Install Drone + Door Models**. This is a migration, not the greybox generator.
3. **Tools > Shift Four > Polish Server Room** adds missing Server cosmetics without changing the cover physics.
4. After importing both GV FBX files, **Tools > Shift Four > Apply Game Models to MainLab** intentionally refreshes only known Guard/door visual children. Preserve custom visual edits before using this explicit refresh. Ordinary installation retains existing visual children.
5. the environment owner reviews geometry and rebakes the scene NavMesh surface after changes. Preserve the existing NavMesh asset GUID. Save MainLab and bake data together.
6. Play the test matrix. In each room try drone-first and console-first separately, inspect F3 decisions, walk through the gate, and test R after both outcomes.

Blender workflow: `blender -b --python ArtSource/GeneratedModels/prepare_game_models.py`; then review the saved sources, UVs and FBX imports. Legacy OBJ and door source files are preserved. `render_game_models.py` generates actual mesh previews for inspection. The import/installer normalize FBX axis/unit wrapper transforms without losing mesh geometry or physics-root identity.

## Reproduce automated checks

Use a disposable directory containing Assets, Packages and ProjectSettings; do not run the asset-writing acceptance command against unsaved personal work. Copy current files/metas together. Run the installed Unity executable:

```sh
Unity -batchmode -nographics -projectPath /absolute/path/to/disposable-copy \
  -executeMethod GameVisualAcceptance.RunAll -logFile /absolute/path/acceptance.log
```

Do not add `-quit`: the harness enters Play Mode, waits for animation, tests outcomes/reloads, then exits itself. Successful run prints `GV ACCEPTANCE COMPLETE` and exits 0. A failed assertion exits 1. The harness generates Server assets, migrates/reapplies visuals, rebakes navigation while preserving the existing asset GUID, and saves before Play Mode. Generated assets are reviewed before copying back.

## Troubleshooting and handoff

- Locked gate: inspect its id and its matching room console/brain; F3 shows decisions, HUD shows the individual prerequisites. Missing animation parts logs an error and keeps the barrier closed.
- Wrong imported motion/size: do not zero an FBX wrapper's unit/axis transforms. Run explicit GV art refresh, which supplies normalized metre-scale pivots. Verify 2m x 3m leaves in the acceptance suite.
- Pink model: let GameModelImportPostprocessor reimport `_GV.fbx`; check URP pipeline settings. Other legacy OBJ materials retain prior ownership/settings.
- Unreachable door/cover: inspect collider placement and rebake with the environment owner; graph route availability alone is not a NavMesh guarantee.
- Missing Blender warning on the legacy `.blend` inside Assets: Unity requires a system Blender association for automatic .blend import. New gameplay visuals use FBX and editable sources outside Assets. This pre-existing optional import warning does not prove FBX failure.
- Unity Editor Search indexing exception: observed in isolated batch logs, outside gameplay. Keep it visible in QA; verify the user's Editor after normal import rather than suppressing exceptions in game code.

the environment owner reviews MainLab and NavMesh; the combat systems owner reviews LabDoor/death event integration; the navigation owner reviews graph version/routing and rotor movement hooks; Core developer validates models, Server, HUD and evidence. No external team messages were sent and no merge/push was performed.

Viva preparation: explain why AND flags are room-specific and idempotent; why the passage remains blocked during animation; how local axes handle rotated doors; how A* filters edges and Version invalidates routes; why physical LOS uses colliders while art does not; why procedural source is not proof of personal modeling. Demonstrate actual behavior using the test matrix, not only code screenshots.
