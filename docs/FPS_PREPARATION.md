# FPS player and weapon preparation — Task 1

Inspection date: 2026-10-20 (Asia/Colombo). Preparation only; no gameplay implementation.

## Baseline and isolation

- Exact baseline: `2aece7603222c2fc3964779cf0da75e9a9456dd5` — `chore: import weapon assets and Unity samples`.
- Original branch: `feature/progressive-drone-intelligence-workspace`. Initial Git status reported a clean tree. `git ls-remote` confirmed that the remote branch also points to this commit; remote `dev` remains `271de5106fd3463529ac710de4478a1774155943`.
- Preparation branch: `feature/fps-player-weapons-prep`.
- Separate worktree: `/Users/dev/Desktop/game/Shift-Four-FPS-Prep`. The original checkout and open Unity session stay on their original branch.
- Editor: Unity `6000.6.4f1`, revision `12bfff696524`, matching ProjectVersion.txt.
- Verification runs use a disposable copy recorded in [baseline.json](evidence/fps-preparation/baseline.json). The active project is never opened by the batch runner.
- No reset, discard, stash, commit, push, package installation, scene regeneration, or gameplay edit was performed. Source asset GUIDs are retained.

This is the latest inspected branch state containing the requested imports. It is **not a compile-clean project baseline**: imported sample dependencies prevent both baseline test runs. No older commit was substituted to conceal that failure.

## Existing gameplay and integration contracts

| Area | Inspected behavior / consequence for future implementation |
| --- | --- |
| `PlayerController.cs` | Requires CharacterController and Health. Owns legacy mouse look, normalized planar movement, gravity, E interaction, cursor locking and a single hitscan gun. Movement goes through private `MovePlayer(direction, deltaTime)`. No jump, sprint, ADS, ammo, reload or switching behavior exists. |
| Ground movement | Walk speed 5 m/s; gravity is explicitly integrated at 20 m/s². Grounded movement sets vertical speed to −1 m/s. Footsteps require resolved horizontal movement and valid ground support. Future jumping must retain airborne suppression and avoid landing/catch-up noise bursts. |
| Camera | `Configure`, `ApplyCameraPose`, `EyePosition` are existing contracts. LateUpdate restores the camera position every frame; look writes local camera rotation. Animate a child viewmodel pivot instead of fighting those camera writes. Pitch is clamped to ±80°. |
| Existing shooting | Left mouse held fires every 0.24 s, 25 damage, 30 m camera raycast, all layers, triggers ignored. Calls `SoundEvents.ReportShot(transform.position)` once per accepted shot, even on a miss. Source for damage attribution is the PlayerController. |
| `Health.cs` | Player maximum 100, recovery starts after 3 s without damage at 8 HP/s. `ApplyDamage` supplies attacker/reason and publishes `Died` / `Damaged`; `Configure` explicitly starts a new life. `TryInitializeMaximum` must not heal damaged actors or resurrect dead ones. Weapons should consume this API without changing it. |
| `SoundEvents.cs` | One gunshot produces one typed Gunshot event and one legacy ShotFired callback. Dispatch handles listener cleanup and scene reload. Keep that exactly-once behavior when extracting firing; avoid emitting from both gameplay and animation events. This is an AI noise bus, not an audio playback system. |
| MainLab | Player is a scene-local object, not an existing reusable player prefab. It has CharacterController, Health and PlayerController, with one camera child and no hands/weapon children. Capsule: height 1.8 m, radius 0.35 m, center (0,0.9,0), step offset 0.3 m, slope limit 50°, skin width 0.05 m. Camera eye height 1.3 m, near clip 0.05 m, FOV 60°, all-layer culling. |
| Input | Legacy Input Manager (`activeInputHandler: 0`). Horizontal/Vertical, Mouse X/Y, Fire1/2/3, Jump and Mouse ScrollWheel already exist. Jump maps to Space and a joystick button but is unused by PlayerController. Fire2 has left-alt/right-mouse mappings. No named Reload or weapon-slot bindings exist. Direct key reads can avoid unnecessary InputManager edits. |
| Restart/HUD | GameSession reads R only after the mission ends, then reloads the scene. A future R reload action must be disabled while ended/dead. HUD/control text belongs to GameSession and currently describes WASD/mouse/fire/E/F3 only. |

Future extraction must move the firing responsibility out of PlayerController in one coordinated change; leaving its current Shoot call enabled alongside a new weapon controller would double-fire. Existing tests invoke private movement/shooting methods through reflection, so any later refactor needs deliberate test migration rather than silent loss of coverage.

## Weapon verification

The five requested weapons exist as FBX models and authored prefabs under `Assets/PolyOne/Free Gun/`. Every prefab has three static MeshFilter/MeshRenderer pairs: body, magazine and a separate bullet mesh. All serialized transforms have zero local position, identity rotation and unit scale. The non-pistol prefabs have an outer `SM_*` root and an inner model root; the pistol has a single root.

| Weapon | Prefab | Independently movable imported meshes |
| --- | --- | --- |
| FN Five-seveN | `Prefabs/FN_Five_Seven.prefab` | `FNFiveSeven_Body`, `FNFiveSeven_Magazine`, `FNFiveSeven__Bullet` |
| HK MP5 | `Prefabs/SM_HK_MP5.prefab` | `HK_MP5_Body`, `HK_MP5_Magazine`, `HK_MP5_Bullet` |
| M16A1 | `Prefabs/SM_M16A1.prefab` | `M16A1_Body`, `M16A1_Magazine`, `M16A1_Bullet` |
| AK-47 | `Prefabs/SM_Ak47.prefab` | `AK47_Body`, `Ak47_Magazine`, `AK47_Bullet` |
| Barrett M82A1 | `Prefabs/SM_Barrett_M82A1.prefab` | `Barrett_M82A1_Body`, `Barrett_M82A1_Magazine`, `Barrett_M82A1_Bullet` |

**All five authored prefabs have an assembly defect:** their part positions are zeroed, while their source FBX models contain nonzero body/magazine/bullet placement transforms. The rendered pistol has its magazine ahead of the grip; the rifles also have magazines intersecting or displaced from their intended wells. Do not treat these vendor prefabs as ready-to-animate assembled weapons. For later wrappers, preserve the imported FBX hierarchy/part placement, or explicitly restore those placements as wrapper overrides. No source prefab was repaired in Task 1.

A separate magazine can translate out of its well after assembly is corrected; convincing rotation/regripping requires an authored pivot or parent. The separate bullet object is a visual mesh and supplies no projectile, casing-ejection or firing behavior. No independent slide, bolt, charging handle or trigger transform is supplied; those details, if present geometrically, belong to the body mesh.

The pistol references external `Materials/Props_FreeGun_M.mat`; the other four prefabs reference a material subasset in their respective FBX. All five source FBX files contain an `AnimationStack` / `AnimationLayer` named `Take 001`, **with no AnimationCurve or AnimationCurveNode objects and no skin deformers or limb models**. An empty exported take is not a usable reload/fire clip.

Unity's [asset audit](evidence/fps-preparation/asset-audit/weapon-audit.json) confirms **zero imported AnimationClips, zero Avatars, zero Animator/Animation components, zero SkinnedMeshRenderers and zero Colliders on every weapon prefab**. Generic animation import is enabled, but there is no usable imported animation. All five model importers use global scale 1 and file scale 0.01 with file-unit scaling enabled. The following are authored-prefab bounds at unit root scale, measured in Unity metres; corrected FBX assembly can change the aggregate bounds.

| Weapon | Bounds X × Y × Z (m) | Barrel direction | Material result | Rendered inspection |
| --- | --- | --- | --- | --- |
| FN Five-seveN | 0.0328 × 0.1432 × 0.2069 | +Z | External URP/Lit; atlas assigned | [Side](evidence/fps-preparation/asset-audit/FN_Five_Seven-side.png) |
| HK MP5 | 0.6765 × 0.2191 × 0.0601 | −X | Embedded URP/Lit; no base texture | [Side](evidence/fps-preparation/asset-audit/SM_HK_MP5-side.png) |
| M16A1 | 0.9552 × 0.2098 × 0.0583 | −X | Embedded URP/Lit; no base texture | [Side](evidence/fps-preparation/asset-audit/SM_M16A1-side.png) |
| AK-47 | 0.7835 × 0.1734 × 0.0569 | −X | Embedded URP/Lit; no base texture | [Side](evidence/fps-preparation/asset-audit/SM_Ak47-side.png) |
| Barrett M82A1 | 1.2022 × 0.2720 × 0.0708 | −X | Embedded URP/Lit; no base texture | [Side](evidence/fps-preparation/asset-audit/SM_Barrett_M82A1-side.png) |

These are plausible metre-scale weapon dimensions; no 100× model rescale is indicated. Keep the pistol's forward basis and apply approximately +90° local Y to a long-gun visual wrapper to align its −X barrel with Unity +Z. Use per-weapon pose offsets after assembly is restored. Never rotate the player's movement root to compensate for imported model axes.

All resolved weapon shaders are supported URP/Lit. The four long guns render white because their embedded `Props_Gun_M` materials lack a texture, **not because a shader is missing**. The supplied `Texture/T_PropsFreeGun_BaseColor.png` atlas is available and already used by the pistol. Create a project-owned URP material referencing that atlas and visually verify each long gun's UVs before accepting it; an atlas assignment on those four guns was not performed or validated here.

### Attachment candidates and assembly reference

No muzzle, grip, sight/aim or ejection sockets are authored. The positions below identify geometry to attach to; they are not validated animation/IK targets. Longitudinal muzzle values refer to the inspected prefab's body bounds, before wrapper rotation; place the actual socket at the barrel centre, just outside the front face, after assembly.

| Weapon | Muzzle candidate | Right/main grip | Left/support grip and aim reference |
| --- | --- | --- | --- |
| Five-seveN | Front bore at +Z, near z = +0.103 m | Pistol grip below the rear slide, behind trigger guard | Support hand wraps the main grip; AimPose follows rear-notch/front-blade sightline |
| MP5 | Barrel front at −X, near x = −0.338 m | Pistol grip below receiver, behind trigger | Handguard forward of magazine well; AimPose through rear aperture/front sight |
| M16A1 | Flash-hider/bore at −X, near x = −0.478 m | Pistol grip below receiver | Forward handguard, away from magazine travel; AimPose through carry-handle rear sight/front post |
| AK-47 | Muzzle bore at −X, near x = −0.392 m | Pistol grip behind magazine well | Lower forward handguard; AimPose on rear-notch/front-post line; author a magazine pivot for rock-in/rock-out motion |
| Barrett | Muzzle-brake bore at −X, near x = −0.601 m | Grip below receiver | Fore-end ahead of magazine, behind muzzle/bipod; AimPose along scope optical axis; scope optics/reticle are not provided as a functioning aiming system |

The source FBX magazine-local placements are useful restoration references, **relative to their original FBX parent**, not drop-in hand targets:

| Weapon | Magazine local position (m) |
| --- | --- |
| Five-seveN | (0.000245, −0.013953, −0.063716) |
| MP5 | (−0.138837, −0.048568, 0.001873) |
| M16A1 | (0.084744, −0.061723, 0.000000) |
| AK-47 | (−0.014614, −0.034091, −0.013179) |
| Barrett | (0.184863, −0.083877, −0.007362) |

The JSON audit retains the full original model hierarchy transforms, body/magazine/bullet mesh names, vertex counts, bounds, material paths, model/prefab GUIDs and importer values. Inspect model-root/body placements as well as the magazine when restoring assembly. Rendering used temporary PreviewRenderUtility scenes, without saving MainLab or any asset.

## Hands and motion availability

- No first-person hands/arms model or rig was found.
- No `.anim`, `.controller`, `.overrideController` or `.mask` assets were found anywhere under Assets.
- No serialized prefab has an Animator, legacy Animation component or SkinnedMeshRenderer.
- Existing drone motion is procedural gameplay presentation; it does not provide hand poses or first-person weapon animations.
- The original-project [animation search](evidence/fps-preparation/project-animation-search.json) and [FBX inventory](evidence/fps-preparation/fbx-source-inventory.json) distinguish raw FBX takes from usable clips. One unrelated BanyanTree sample could not be parsed by the lightweight FBX inspector; no weapon parse failed.

## Rendering configuration

GraphicsSettings references `Assets/Settings/ShiftFour_URP.asset` (GUID `b2a59824edefa45729385fcdec7ac12b`). It uses the existing Universal Renderer in forward rendering mode, without renderer features. Every Quality-level override is null, so the Graphics pipeline supplies the default. No viewmodel layer or dedicated weapon camera exists; custom layer slots are unassigned.

The manifest requests URP 17.5.0, while Unity's lock file resolves the built-in URP/Core/Shader Graph packages to 17.6.0. The test framework resolves to 1.8.0. Do not silently downgrade or install packages as part of FPS preparation.

Start with hands/weapons under the existing camera, with their own presentation transforms. If a separate viewmodel FOV/depth pass is needed later, explicitly review a ViewModel layer plus a URP Base/Overlay camera setup and exclude that layer from gameplay queries. URP supports this compositing approach; its use here is a recommendation, not an implemented feature. See [Unity camera stacking](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/camera-stacking.html). Keep the existing CharacterController and explicit vertical motion when adding jump; [CharacterController.Move](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController.Move.html) does not supply gravity itself.

## Recommended implementation structure — not created in Task 1

```text
Assets/Scripts/Gameplay/FPS/
  PlayerWeaponController.cs     input routing, equipped slot, ADS/reload/switch state
  WeaponDefinition.cs           ScriptableObject: tuning, capacity, timings, viewmodel
  WeaponInstance.cs             per-player ammo, reserve and cooldown state
  HitscanWeapon.cs              accepted shot, Health damage, exactly-once shot noise
  WeaponViewModel.cs            anchors, aim/equip/recoil poses, magazine presentation
  ArmsAnimator.cs               per-weapon clip/pose selection and presentation events
Assets/Prefabs/FPS/
  FirstPersonPresentation.prefab
  Weapons/{FiveSeven,MP5,M16A1,AK47,Barrett}.prefab
Assets/Data/Weapons/             one definition per weapon
Assets/Animations/FPS/           arms controller, per-weapon clips/overrides
Assets/Materials/FPS/            project-owned URP materials when overrides are needed
```

```text
Player                          existing controller/health; later weapon controller
└─ First person camera          existing camera and collision-safe pose
   └─ ViewModelRoot
      ├─ ArmsRig                skinned arms + Animator; no root-motion locomotion
      └─ WeaponMount
         └─ WeaponWrapper       project-owned prefab/variant
            ├─ Visual           imported FBX hierarchy with original part placements
            ├─ Muzzle
            ├─ AimPose
            ├─ RightGrip
            ├─ LeftGrip
            ├─ MagazineGrip
            └─ EjectPoint       only if casing presentation is later authored
```

Store ammo/cooldowns on WeaponInstance, never on the shared definition asset. Keep weapon-specific transforms and references on wrappers rather than renaming vendor children or changing imported GUIDs. The weapon mount and arms are sibling presentation objects; avoid circular hand/weapon parenting or IK dependencies.

Define cancel-safe states before implementation: Unequipped, Equipping, Ready, Aiming, Firing, Reloading and Holstering, gated by player life, cursor and mission state. Decide when ammo transfers and how reload is cancelled by death/switching. Animation events can request magazine/VFX changes but must not independently spend ammo, apply damage or emit duplicate gunshots. ADS should align an authored sightline to the camera rather than moving the movement root.

## Missing assets and authoring work

1. A licensed first-person arms mesh, skin, skeleton and material/texture set, including appropriate wrist/finger poses. No arms package was installed.
2. Compatible motion for idle, equip/holster, firing/recoil, ADS and reload for each distinct weapon grip. Tactical/empty reloads need defined magazine/hand timing. Procedural ADS/recoil is an option; hand/magazine choreography still needs authoring.
3. Five project-owned viewmodel wrappers with corrected assembly, calibrated muzzle/grip/sight anchors, and per-weapon tuning/ammo definitions. Restore the source FBX placements instead of inheriting the zeroed vendor-prefab transforms. Create long-gun material overrides and validate atlas UVs. These are preparation assets to author; no repairs were made here.
4. Separate slide/bolt/charging-handle meshes and pivots **only if** those mechanical actions must animate independently. The current three-part models support magazine motion but not those separate actions.
5. Fire/reload/dry-fire/equip sounds and muzzle/casing effects if audiovisual feedback is required. No audio clips were found in Assets. Those are presentation additions, not prerequisites for implementing the core state model.

## Shared-asset handoffs for later work

| Proposed later change | Required coordination / review |
| --- | --- |
| PlayerController, player prefab extraction, jumping and weapon/combat split | Nimsara owns player/physics/combat. Choose one shared-file owner per PR and an affected-area reviewer. |
| MainLab player wiring, camera replacement/addition, scene changes | Pamudi owns MainLab integration. Provide a prefab/handoff for integration; do not regenerate the scene or rebake NavMesh for viewmodels. |
| Health, IDamageable, shot/noise contracts, AI hearing impact | Nimsara for combat; name a shared contract owner and obtain a reviewer from affected AI work. Asmadala should review movement/noise effects. Preserve current contracts unless a separate change is agreed. |
| HUD ammo/reload/control text or death/restart interaction | Coordinate with Nimthara (GameSession/HUD/outcomes) and Nimsara (player life/combat). |
| Shared model/material source, URP assets, Graphics/Quality/TagManager/InputManager or package setup | Name an owner and an affected-area reviewer. Coordinate rendering/scene work with Pamudi and model/visual work with Nimthara. Prefer new FPS wrappers/materials to global vendor or pipeline edits. |
| Imported sample compile blockers | Separate shared configuration/import cleanup review: decide whether to exclude unused samples or deliberately supply their Input System/UI dependencies. No FPS controller package is needed. |

These follow [team ownership rules](TEAM_RESPONSIBILITIES.md#shared-ownership-rules) and [branching ownership/review rules](BRANCHING_STRATEGY.md#8-unity-ownership-to-prevent-merge-conflicts). The repository does not name a specific owner for every project setting; appoint one rather than inventing a mandatory person. No approvals are needed to complete the read-only inspection and isolated preparation already requested.

The existing scope document lists weapon switching/ammo/reload as out of scope. This task explicitly authorizes their preparation; update that scope record with a named owner before later implementation is integrated. No additional approval was inferred to block Task 1.

## Test evidence and limits

Both unmodified-baseline attempts exited 1 during compilation, before test discovery. Neither produced NUnit XML. There were 15 distinct source-location compile errors: AI Navigation samples reference absent `UnityEngine.InputSystem` types (`Key`, `KeyControl`); SRP sample scripts reference absent `TMPro`, `TextMeshPro` and `TMP_Text` types. Logs are retained as [EditMode.log](evidence/fps-preparation/EditMode.log) and [PlayMode.log](evidence/fps-preparation/PlayMode.log).

For diagnosis only, `Assets/Samples` and its meta file were moved outside Assets **in the disposable copy**. No packages, gameplay scripts, scene or prefab content were changed to make that diagnostic compile. Runs explicitly select the two existing ShiftFour Edit Mode assemblies and the existing ShiftFour Play Mode assembly; intentionally instructional vendor tests are not included. Exact invocations are in [commands.json](evidence/fps-preparation/commands.json).

| Run | Actual result |
| --- | --- |
| Unmodified baseline Edit Mode | Compile blocked; exit 1; 0 executed; no result XML |
| Unmodified baseline Play Mode | Compile blocked; exit 1; 0 executed; no result XML |
| Diagnostic Edit Mode, Samples excluded | **306 passed**, 0 failed/skipped/inconclusive; exit 0; 1.793 s test duration |
| Diagnostic Play Mode, Samples excluded | **159 passed**, 0 failed/skipped/inconclusive; exit 0; 521.045 s test duration |

The Edit Mode assemblies contribute 266 Movement tests and 40 Navigation tests. Play Mode covers actual MainLab camera/collision, movement/noise, shot/damage/recovery/restart, events, drones, awareness/hearing, animation, navigation and four-room/progressive-difficulty regressions. These are the existing tests, not new FPS-feature tests; jumping/reloading/switching/hands are not implemented or validated. Unity serialized platform defaults only into the disposable ProjectSettings copy; [verification-settings.patch](evidence/fps-preparation/verification-settings.patch) records that difference. No such settings were copied back to either real checkout.

See [test-summary.json](evidence/fps-preparation/test-summary.json), [diagnostic EditMode.xml](evidence/fps-preparation/diagnostic-samples-excluded/EditMode.xml), and [diagnostic PlayMode.xml](evidence/fps-preparation/diagnostic-samples-excluded/PlayMode.xml). **Diagnostic passes are not a passing unmodified project baseline.** Fix the imported sample compilation issue in an agreed configuration/import change before beginning integrated FPS behavior work.

## Preservation checks

[integrity.json](evidence/fps-preparation/integrity.json) compares captured hashes with the original checkout and the preparation worktree. All re-readable original files match their captured hashes. Eighteen original files became cloud-offloaded during inspection, so the final check does not claim a fresh byte comparison for those files; their initial hashes are retained. The inspected player scripts, MainLab, InputManager, URP settings, weapon files and all weapon GUIDs were checked without changes.

Two unrelated binary sample Terrain assets have different working-copy bytes between the original checkout and the new worktree because the existing `.gitattributes` forces **all `*.asset` files to text**, normalizing CRLF sequences embedded in those binary files. The preparation worktree matches the committed blobs for both assets; neither asset was edited by this task. Keep any correction to binary Git attributes in a separate reviewed change. These two assets are excluded with the other Samples during the diagnostic tests and are not used by MainLab.
