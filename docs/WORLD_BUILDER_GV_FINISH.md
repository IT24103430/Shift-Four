# World Builder GV environment finish

Assessment reference: **IT24103460**. Code and asset names describe their feature, not their author.

Scope: World Builder completion for the existing four-room MainLab. This pass is agent-authored implementation and verification, not evidence that IT24103460 personally created the assets. IT24103460 should inspect the scene, explain the decisions and preserve truthful evidence of their own work.

## Delivered environment

- All four section prefabs inherit the same segmented wall kit: painted upper panels, graphite lower protection, metal sill/rail, dark expansion joints, service vents and restrained room-colour bands.
- Checkpoint uses amber identification, Server cyan, Control muted red and Storage warm amber. Two solid-wall plaques identify each room.
- Panels follow the **actual full-height wall collider segments** in MainLab, including the asymmetric Storage exit. Nothing spans a gate opening. Original collision, cover, consoles, waypoints, gates and baked navigation remain authoritative.
- Lettering is baked into static meshes and a persistent glyph atlas. A small URP shader depth-tests it against the level; signs cannot draw through neighbouring walls. There is no runtime font generation or camera-facing text.
- Skyless interior lighting uses a deliberately configured three-colour ambient probe. Existing practical lights remain in place; this pass adds no lights or shadows.
- A fresh standalone run exposed four native-agent initialization messages before the surface registered. `LabNavigationStartup` now enables the four scene agents after the saved surface is registered, before their policy `Start` methods. The prefab defaults, baked routes and AI decisions are unchanged. This is scene startup coordination, not runtime NavMesh generation.
- Hundreds of individual panel/vent pieces are combined into **six mesh renderers per room**, including the lettering. Shared material families, the existing SRP batcher, instancing-capable opaque materials and no decorative colliders keep the added work bounded.

The scene already has floors, ceilings, cover and practical fixtures. This finish extends those assets rather than regenerating the level. `Assets/Scenes/MainLab_NavMesh.asset` is preserved; no rebake is necessary for render-only additions.

## Editing and reproduction

Open `Assets/Scenes/MainLab.unity` with Unity **6000.6.4f1**. The finished prefabs and scene are saved assets; no menu command is required to see them.

For deliberate re-authoring, stop Play Mode, save all open scenes, then choose **Tools > Shift Four > Finish World Builder Environment**. The tool replaces only `World Builder finish` children and updates its own generated assets under `Assets/Environment/WorldBuilder`. It preserves generated asset GUIDs and existing gameplay-script serialization. First-use scene/prefab copies are stored in a timestamped `Library/WorldBuilderEnvironmentBackup` directory. Do not run the greybox generator.

The editor authoring source is `Assets/Editor/WorldBuilderEnvironmentBuilder.cs`; the runtime lettering shader is `Assets/Shaders/WorldBuilderSignage.shader`. Navigation startup lives in `Assets/Scripts/Gameplay/LabNavigationStartup.cs`. The generated wall meshes are editable outputs, with the C# authoring tool as their source.

For a new isolated verification copy containing Assets, Packages and ProjectSettings:

```powershell
# Use an isolated copy, never the project currently open in the Editor.
& 'E:/Unity/6000.6.4f1/Editor/Unity.exe' -batchmode -projectPath '<copy>' `
  -executeMethod WorldBuilderEnvironmentBuilder.FinishAndVerify -logFile '<copy>/finish.log'
./tools/verify-world-builder-gv.ps1 -ProjectPath '<copy>'
```

The first command renders the real saved scene before/after, checks existing collider and navigation signatures, samples floor coverage and produces layout/navigation evidence. The second runs Shift Four Edit Mode tests, focused scene/gameplay Play Mode regressions and a Windows development build. It deliberately filters out imported Unity tutorial exercises.

The standalone probe source is retained under `tools/verification/WorldBuilderBuildProbe.cs.txt`. It is installed **only in the isolated verification copy**, not production Assets. The resulting development build supports `-worldBuilderProbe -worldBuilderOutput <absolute-folder>` for a reproducible environment/performance/physical traversal check. A normal launch does not start the probe.

## Assessment demonstration

1. Show the four connected rooms, then Checkpoint from the player camera.
2. Show the wall-kit/material families and practical light settings. Explain why emission alone does not illuminate a room.
3. Show the baked NavMesh and waypoint graph. Explain that collider changes require a rebake/recheck, while this render-only pass preserves the existing bake.
4. Show one closed gate, complete its drone-and-console objectives and walk through after physical release.
5. Show the combined room meshes and explain the six-renderer budget and absence of extra point lights.
6. Use the actual screenshots/build results in the evidence folder; distinguish scripted verification from personal/manual playtesting.

## Verification and remaining personal acceptance

The feature validation passed 329 Edit Mode tests and 64 focused Play Mode tests. A Windows development build also passed the scripted four-gate player-capsule traversal and victory check. The later naming cleanup passed Unity compilation. These are recorded results; they are not fresh test runs performed when reading this document.

Generated XML reports, logs, captures, performance CSVs and showcase video remain local rather than being included in source commits. `tools/package-world-builder-gv-evidence.py` packages actual results from a verification copy into `docs/evidence/world-builder-gv/` when those results are available. The verification scripts and standalone harness source are versioned so the checks can be reproduced without committing their generated output.

IT24103460 still needs to perform their personal visual review, give the World Builder explanation and record any required personal presentation. Automated movement and a prepared showcase cannot establish human combat fairness, individual authorship or lecturer acceptance. Existing Scout/Guard/Flanker policy proposals and console hearing-source issues are IS/integration work outside this environment finish.
