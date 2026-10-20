# Weapon ammo and reloading

Phase 2 adds finite ammunition and cancel-safe reloading to the existing hipfire pistol. The combat/player systems component owns ammo and timers; the viewmodel and HUD only display that state.

## Controls and tuning

- Hold left mouse to fire at the existing cadence. Each accepted shot spends one round, including misses and cover hits.
- Press R during live, cursor-locked play to reload. A full magazine, empty reserve or active reload rejects another reload request. There is no automatic reload.
- The Five-seveN starts with 20 rounds loaded and 80 in reserve. Reload takes 1.6 seconds. These are gameplay tuning values, not a firearm simulation. There is no additional chamber round.
- After win or death, R retains its existing scene-restart action. A fresh scene restores starting ammo.

`FNFiveSeven.asset` exposes magazine capacity, starting reserve and reload duration alongside the existing damage, range and fire interval. Each player's `WeaponRuntimeState` owns its counters. Equipping a previously used definition restores its existing counters and cooldown rather than granting fresh ammunition.

## Reload contract

Ammo transfers exactly once at the gameplay deadline: `min(missing rounds, reserve)`. Tactical reloads preserve ammunition already loaded. A partial reserve can fill only part of the magazine.

`CompleteReload` rejects early completion, even if called by an animation observer. Cancellation, weapon switching, player death, mission end or disabling the controller never transfer rounds. Re-enabling does not resume the cancelled timer. Reload uses scaled gameplay time, so pausing does not complete it in the background.

Empty fire returns false without damage, raycasts, accepted-shot callbacks, muzzle flash, recoil or AI gunshot noise. It briefly highlights the ammo display. It does not play a recorded dry-fire sound; no audio clip was added.

## Presentation

The HUD displays loaded and reserve rounds, low-ammo and empty prompts, and reload progress. The pistol tilts during reload and its existing separate magazine withdraws and returns. Completion or cancellation restores the authored pose. The procedural pose never moves the camera or changes the hitscan direction.

The source mesh has no separate slide, hand rig or usable reload clip. This phase uses its real magazine mesh without modifying vendor assets. Hands, a character model, ADS, death-camera animation, pickups and new sound assets are separate work.

## Verification

Tests cover ammo conservation, configurable validation, independent player state, empty rejection, real damage/noise counts, timed and partial reloads, cancellation, switching, disabling, death, cursor input gating, magazine pose restoration and actual MainLab restart. Batch Editor cursor capture may be unavailable; tests check the actual gate and use the public reload API when OS capture is refused. Native R input and animation feel still need human playtesting.

Use an isolated project copy containing the current source:

```powershell
./tools/verify-combat-hud.ps1 -ProjectPath ./Temp/CombatHudVerification -ResultsName WeaponReload
```

This runs all project Edit Mode checks and selected combat, HUD, movement and mission Play Mode checks, builds the Windows development player, and captures 15 standalone scenarios. Added screenshots cover tactical reload, magazine motion, completed transfer, low ammo, empty fire and reloading from empty. The probe uses real weapon APIs; incoming damage remains explicitly simulated. Reports and screenshots stay under ignored `Logs/WeaponReload` in that copy. Production packages, scenes and imported models are unchanged.

Validated on Unity 6000.6.4f1: all 332 Edit Mode and 61 selected Play Mode tests passed. The adjusted reload pose also passed a focused rerun. The Windows development player completed the automated visual scenarios without runtime errors. Human input and animation-feel acceptance remain separate.
