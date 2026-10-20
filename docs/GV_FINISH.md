# Player systems GV finish

This pass completes first-person arm presentation, camera death feedback and an interactive restart overlay, and corrects the Storage drone's authored return point. It builds on the existing [combat HUD](COMBAT_HUD.md) and [ammo/reload](WEAPON_AMMO_RELOAD.md).

## Implemented behavior

- The existing Five-seveN installs one skinned first-person arms model at runtime. The hands have an authored pistol rest grip; accepted firing moves arms with the existing recoil pivot. The support forearm follows magazine withdrawal and return during reload while preserving the grip and bone lengths. Cancellation restores the grip. Neither arms nor gun visuals can intercept gameplay raycasts.
- External arms are CC0, from para's OpenGameArt submission. Source, license link, original-art distinction and reproducible Blender export instructions are in [asset provenance](../ArtSource/ThirdParty/FirstPersonArms/README.md). No character package or runtime Blender dependency was added.
- Fatal damage starts a short, restrained camera drop and tilt in unscaled time. The player capsule and world position stay fixed. A downward sphere query limits the drop near solid geometry. The viewmodel hides through the existing life-state gate, and weapon reload cancels without granting ammunition.
- The end overlay exposes a real mouse-clickable **Restart [R]** button. Live R still reloads; ended R still restarts. Reloading the scene resets camera, health, ammo, objectives, arm instances and transient feedback.
- Close solid cover retracts the weapon socket immediately and eases it back after clearing the obstruction. This changes presentation only: camera, collision capsule and camera-centred damage ray stay fixed.
- Storage's old Home point `(10, 0, -10)` overlapped cover. The scene now uses `(10, 0, -11)`, verified with the full drone envelope and actual motor arrival. This is one authored scene-property change; no policy algorithm or NavMesh bake changed.

## Checks and evidence

`GVFinishPlayModeTests` checks live startup (including Health/Player Awake ordering), skin/bone bindings, wrist alignment, death while gameplay time is paused, frozen physics root, reload cancellation, scene restart, close-wall muzzle clearance and restoration, Storage post clearance, actual return movement and complete native paths through both side approaches.

The existing gate-height suite verifies all four AND prerequisites, graph version changes, root collider/carving release and native passage after opening. Combat, cover, recovery, jump, collision, weapon, mission and restart regressions remain part of the verification script.

On Unity 6000.6.4f1, 332 Edit Mode checks passed. The broad Play Mode run passed 91 of 93 cases; it exposed an older unlimited-ammo capture fixture and the imported rig's separately parented wrist. Both were fixed. The final focused rerun passed all six affected/GV cases, including the rendered four-drone sequence. All 93 selected cases therefore have passing latest outcomes across those runs; this is not reported as a single clean 93-case execution. Original NUnit reports are retained with the focused rerun.

The final exported deform-chain revision also passed all five GV-specific cases. The standalone player reports a successful native restart-button click and zero runtime errors. Close-wall probe geometry uses an explicit URP material; it is QA-only.

```powershell
./tools/verify-combat-hud.ps1 -ProjectPath ./Temp/CombatHudVerification -ResultsName GVFinish
```

Use an isolated copy with current source and project configuration. The standalone probe renders real HUD/arms/reload/death states at three resolutions and sends native window messages only to its own process to exercise the restart button. Incoming damage and camera placement remain scripted. Generated reports/screenshots belong under ignored `Logs/GVFinish`, not production Assets or assessment proof presented as human play.

## Human acceptance still required

Automated fixtures and rendered captures do not certify a human playthrough. In the final standalone build, check:

1. Move, jump, turn and approach walls/shelves; inspect grip, weapon alignment and clipping.
2. Shoot a drone, shoot solid cover, miss, empty the magazine, reload and interrupt reload by dying.
3. Take cover and confirm delayed recovery and readable damage direction without seeing health bars through walls.
4. Walk through each gate after its console/drone pair; inspect blocked passage before completion.
5. Use both Storage approaches and their fixed cover.
6. Finish once, die once, and restart with both mouse and R. Repeat to check camera, health, ammo and objectives reset.

Record actual observations and defects. Shared gate/combat/scene changes still need the relevant team review. This pass does not implement Flanker intelligence, full-body third-person animation, ADS, extra weapons, pickups or audio assets.
