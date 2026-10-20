# First playable weapon — FN Five-seveN

Implemented in `/Users/dev/Desktop/game/Shift-Four-FPS-Prep` on `feature/fps-player-weapons-prep`, continuing from verified Task 3 and its uncommitted changes. Base commit: `2aece7603222c2fc3964779cf0da75e9a9456dd5`. The exact starting files, hashes and Git status are in [task3-baseline.json](evidence/five-seven/task3-baseline.json).

## Try it

Open this worktree as the Unity 6000.6.4f1 project and open [FiveSevenRange.unity](../Assets/Scenes/FPS/FiveSevenRange.unity). Press Play: WASD moves, mouse looks, held left click fires, Space jumps and Escape releases the cursor. Click to resume. The orange target starts with 100 health and takes four accepted hits; stop/start Play Mode to reset the range. A platform and solid cover allow movement and occlusion checks. The range is not added to shared Build Settings.

The pistol also attaches at runtime to the existing player camera in MainLab: WeaponController loads the authored definition when no initial weapon was assigned, and FirstPersonWeaponMount creates a dedicated `WeaponSocket`. No MainLab scene save, camera replacement, player capsule edit or scene-builder regeneration is involved. Explicitly assigned initial definitions still take precedence; if the default asset is unavailable, the Task 3 transient hitscan fallback remains available.

The unchanged imported Samples have the previously recorded Input System/TMPro compilation blockers. Verification used the disposable preparation project with Samples excluded only there. The implementation does not change packages or source Samples. Native mouse/keyboard input was not synthesized by the batch tests; the native control bindings remain the existing PlayerController/WeaponController paths.

## Verified asset and hierarchy

The imported `Assets/PolyOne/Free Gun/Prefabs/FN_Five_Seven.prefab` contains three MeshFilter/MeshRenderer children: `FNFiveSeven_Body`, `FNFiveSeven_Magazine`, and `FNFiveSeven__Bullet`. It has no hands, skinned rig, Animator or usable clips. The body includes the slide geometry and has no separately movable slide. The separate round is a visual mesh, not a projectile.

The imported prefab zeroes all part positions, displacing its magazine. The project-owned wrapper retains a nested reference to that prefab and restores the original FBX root/part placement through overrides. It hides the chamber-round mesh. The source prefab, FBX, material, texture and import settings remain untouched. Corrected [side](evidence/five-seven/renders/assembly-side.png) and [front](evidence/five-seven/renders/assembly-front.png) renders are retained for inspection.

```text
Player (existing CharacterController / PlayerController / WeaponController)
  PlayerCamera (existing)
    WeaponSocket                 runtime identity pose
      FNFiveSevenViewmodel       FirstPersonViewmodel + PistolPresentation
        RecoilPivot              local (0.12, -0.12, 0.30) m; unit scale
          FN_Five_Seven          nested vendor prefab, +Z barrel direction
            FNFiveSeven_Body     FBX placement override
            FNFiveSeven_Magazine FBX placement override
            FNFiveSeven__Bullet  hidden
            MuzzlePoint          local (0, 0.0566, 0.105) m, at front bore
              MuzzleFlash        project-owned additive quad, normally hidden
```

This is a gun-only viewmodel. No placeholder arms, fingers, skeleton or animation clips are presented as a finished hands rig. A suitable licensed first-person arms rig and matching hand/reload animations remain future assets.

## Gameplay and presentation

The authored [WeaponDefinition](../Assets/Resources/Weapons/FNFiveSeven.asset) preserves **25 damage, 30 m range, and 0.24 s held-fire interval**. Only WeaponController performs the player damage raycast, from the current camera position/forward direction. The muzzle is a presentation anchor; recoil does not rotate the camera or redirect damage. The crosshair-hit regression projects the actual hit back to viewport `(0.5, 0.5)`. Existing Health.ApplyDamage attribution and SoundEvents.ReportShot behavior are unchanged.

FirstPersonWeaponMount follows the active definition, creates one socket/viewmodel, reparents the socket on camera replacement and removes the old model on equip changes. It hides the socket when the actor or weapon cannot operate. Destroyed actors release externally parented sockets too. Per-weapon cooldown remains on WeaponRuntimeState, including when switching away and back.

PistolPresentation listens only to accepted-shot events forwarded by FirstPersonViewmodel. It kicks the gun back 1.2 cm and up 2 degrees, then recovers exponentially using delta time. Stacking is capped at 1.5 times that pose. Rejected cooldown requests create no extra flash/recoil. Disable/death/equip cleanup hides the flash and resets the pose.

The flash is a 4.5 cm soft additive quad displayed for 35 ms, centred 2.5 cm forward of MuzzlePoint. The project has the built-in Particle System module disabled, so this effect uses a project-owned URP/Unlit material and radial texture with no package or settings changes. It has no collider, damage, extra raycast, gunshot producer or camera movement. There is no new recorded firing audio clip; the original gameplay gunshot-noise events are preserved.

All three imported mesh/material references are preserved, including `Props_FreeGun_M` and its `T_PropsFreeGun_BaseColor.png` atlas. The wrapper disables weapon shadow casting and uses Ignore Raycast layer 2 throughout. It contains no Collider or Rigidbody. FirstPersonViewmodel also isolates late-added descendants before queries. Normal cover continues to block camera hitscan, and interactions continue past the gun. The existing URP pipeline, camera near plane and project layer configuration are unchanged.

The renderer uses the existing camera and world depth. This task does not add a separate weapon camera or wall-avoidance animation system.

## Changed files

Existing source changes for Task 4 only:

- `Assets/Scripts/Gameplay/Weapons/WeaponDefinition.cs`: optional owned viewmodel-prefab reference.
- `Assets/Scripts/Gameplay/Weapons/WeaponController.cs`: authored default definition selection and presentation-mount initialization; hitscan/damage/sound implementation unchanged.

New source:

- `Assets/Scripts/Gameplay/Weapons/FirstPersonWeaponMount.cs`
- `Assets/Scripts/Gameplay/Weapons/PistolPresentation.cs`
- `Assets/Scripts/Gameplay/Weapons/FiveSevenRangeHud.cs` (isolated range only)
- `Assets/Editor/FiveSevenAssetBuilder.cs` (create-once authoring menu; refuses to overwrite existing weapon assets/range)
- `Assets/Tests/EditMode/Movement/FiveSevenAssetEditModeTests.cs`
- `Assets/Tests/PlayMode/FiveSevenPlayablePlayModeTests.cs`

New authored assets, each with a new meta/GUID:

- `Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab`
- `Assets/Resources/Weapons/FNFiveSeven.asset`
- `Assets/Scenes/FPS/FiveSevenRange.unity`
- `Assets/Materials/FPS/MuzzleFlash.mat` and `MuzzleFlash.png`
- `Assets/Materials/FPS/RangeFloor.mat`, `RangeWall.mat`, `RangeTarget.mat`

Documentation and evidence are added under this report and `docs/evidence/five-seven`. The prior Task 1–3 work remains uncommitted and intact. PlayerController, MainLab, all PolyOne source assets, existing prefabs, existing metas/GUIDs, Health, SoundEvents, existing tests, packages and project settings are preserved relative to the Task 3 snapshot. No shared-asset approval was required for these isolated additions and runtime wiring. No commit, push, merge, reset or stash was performed.

## Actual verification

| Unity run | Final result |
| --- | --- |
| Existing + new project Edit Mode assemblies | **329/329 passed** (six new pistol asset cases) |
| Focused Play Mode | **41/41 passed** (nine pistol, 17 architecture, 15 jumping cases) |
| Selected original Play Mode regressions | **50/50 passed**, 108.705 s |

The new Play Mode cases use the saved isolated scene and actual player/camera/weapon, Physics, Health and noise events. They cover runtime default wiring, camera-follow hierarchy, exact-once damage/noise, cooldown, crosshair-hit agreement, recoil/flash recovery, pitch/yaw, jump/landing, camera near-plane clearance, viewmodel physics isolation, solid cover, interaction prompts, equip/camera replacement cleanup, death and rendered visibility/materials. Existing architecture tests retain game-over and disabled-input guards.

The [idle](evidence/five-seven/renders/range-idle.png), [firing](evidence/five-seven/renders/range-fired.png), [recovered](evidence/five-seven/renders/range-recovered.png) and no-gun/no-flash control images are real camera render-target captures. They exclude the range's OnGUI HUD/crosshair. The firing pose is paused immediately after an accepted shot for a matched flash/no-flash image comparison; the separate lifecycle test measures its real-time expiry and recoil recovery. Original TGA captures are retained; PNG copies are lossless format conversions for viewing. Pixel counts are in [render-summary.json](evidence/five-seven/renders/render-summary.json).

At 1280×720, the pistol changes 42,955 pixels against the no-gun control, the flash changes 939 pixels against the matching no-flash control, and the pink-pixel check finds zero missing-shader pixels. Edit Mode also checks the actual shader support and unchanged imported material/atlas references.

The original selection covers sound, combat/cover/attribution/death/restart, difficulty defaults and player shots, progressive cadence, MainLab proximity/height, drone destruction, player noise and four-room integration, including the two previously intermittent Task 2 hearing/navigation cases. It is a selected regression run, not a rerun of the entire Play Mode suite. Final Edit Mode duration was 1.788 s and focused Play Mode duration was 2.348 s; all final process exit codes were zero.

The final preservation check finds two modified existing source files, 32 new source/asset/meta files, no missing existing files and no existing meta changes. The tested copy matches the worktree outside the intentionally excluded Samples and Unity's platform-default serialization in the disposable ProjectSettings file. URP validated only the new project-owned flash material; that tested serialization was adopted in the worktree, with its exact change retained in the evidence. The vendor material was unchanged. `git diff --check` passes.

Final commands, filters, logs, XML, exit codes, source hashes and preservation checks are in [the evidence directory](evidence/five-seven). Initial authoring attempts are retained: the disabled particle module was addressed with the quad effect, and the batch editor's untitled-scene restriction was addressed by safe isolated-scene creation. Weapon assets were created first and the range completed in a separate invocation, preserving the generated GUIDs.
