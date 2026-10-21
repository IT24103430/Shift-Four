# First-person equip and unequip

The project-owned `FNFiveSevenViewmodel.prefab` stores `FirstPersonEquipMotion` settings. `FirstPersonWeaponMount` owns one visual root transform and a four-state `FirstPersonEquipTransition`: Hidden → Equipping → Ready → Unequipping → Hidden. The pistol, magazine and both hands move as one assembly. Existing recoil, idle, movement and reload layers remain on the child recoil pivot; wall retreat remains on WeaponSocket. No player/camera root motion is used.

## Defaults

| Setting | Value |
|---|---|
| Equip / re-equip | 0.38 seconds |
| Unequip | 0.26 seconds |
| Full animated definition change | 0.26 seconds lower + 0.38 seconds raise |
| Lowered position relative to authored root | (+15, −240, +25) mm |
| Lowered pitch/yaw/roll | (+16, −6, +8) degrees |
| Block firing during equip | Disabled (explicit user preference) |

Quintic segments reach exact endpoints with zero endpoint velocity/acceleration. Reversals preserve the currently sampled position, velocity and acceleration. Visibility stays within [0,1]. Repeated requests for the current target do not restart its timer. Time is sampled from scaled `Time.timeAsDouble`, so pause freezes the transition. Each reversal uses the configured duration to reach its new endpoint; it does not reset to an authored endpoint first.

The mount returns the model root to its exact saved local position/rotation at Ready and disables the socket at Hidden. A holstered weapon retains its single model for reuse. Replacement disables/unbinds and detaches the old instance before deferred destruction, so two viewmodels do not remain under the player.

## Gameplay contract

`WeaponController.Equip(definition)` retains its existing synchronous gameplay contract: a valid different definition becomes `ActiveWeapon` immediately, reload is cancelled, and the controller reuses that definition's existing runtime state. Same-definition requests still return false. Ammo, reserve, `NextShotTime`, damage and fire interval are not reset by transitions.

Only definitions whose viewmodel opts in with `FirstPersonEquipMotion` animate. Between animated definitions the existing model lowers, then the latest requested definition is instantiated and raised. Intermediate requests are not put in a queue. Requesting the displayed definition again while it is lowering reverses the existing model. Model-less/legacy definitions retain immediate presentation replacement and readiness.

The owned pistol allows accepted shots while raising, as explicitly requested by the user. It blocks `TryFire` while lowering, hidden or awaiting a different model. Blocked calls return before ammo/cooldown, dry-fire UI, sound, damage or accepted-shot callbacks. Reload requests require Ready. `CompleteReload` and ammunition transfer remain owned by gameplay. `blockFiringDuringEquip` is an explicit per-prefab option for a future different firing contract; the saved pistol default is false. Lowering and Hidden always block firing.

`FirstPersonWeaponMount.RequestUnequip()` cancels an active gameplay reload before lowering. It does not clear the active definition or spend ammunition. `RequestEquip()` raises that same weapon again. Re-equip after holstering uses this hook; `WeaponController.Equip` deliberately still rejects the already active definition. Both hooks return false for repeated requests or an inoperable player. There is no five-weapon selection, inventory or new input binding.

Death, ended mission, disabled weapon controller or missing camera hide/reset the presentation. Disabling the mount also cancels reload and clears its transition. A valid re-enable starts from Hidden. No transition can bypass `PlayerController.CanAct`. The existing reload cancellation pose reset remains immediate; this task does not replace Task 8's cancellation contract with a second reload timer.

## Future transition hooks

`TransitionChanged`, `TransitionState`, `IsReady`, `EquipVisibility`, `EquipDuration` and `UnequipDuration` expose presentation lifecycle. Explicit holstering produces Hidden; a future coordinator can respond by calling `WeaponController.Equip(newDefinition)`. That hook is exercised by a real Play Mode test. `WeaponEquipped` continues to mean gameplay activation, not animation completion. Transition callbacks cannot reenter the mount's public raise/lower methods while being notified; a gameplay equip requested from a callback is reconciled safely on the next refresh.

## Authoring and verification

`Tools → Shift Four → FPS → Add Pistol Equip Settings Once` installs the component on the owned pistol prefab without replacing existing settings or transforms. Adjust artist values on that prefab and save. Original FBXs, GUIDs, materials, arms prefab and source weights remain unchanged.

`FirstPersonEquipEditModeTests` verifies deterministic endpoints, durations, idempotence, bounded interruption/reversal and 30/60/120 Hz sampling. `FirstPersonEquipPlayModeTests` exercises actual MainLab bootstrap, both death directions, reload cancellation, saved weapon states, late equip requests, camera replacement/disable, transition callbacks, grip/camera/bone invariants and real Game-view captures. Combat fixtures wait for the actual ready gate; no test bypasses the gate or forces the transition to complete.

Actual results and captures are retained in `Logs/ArmsEquip/REPORT.md`. The final pose returns to the existing grip, including its previously documented [mesh/weight limitations](GRIP.md) and [Task 8 pinky/magazine clearance issue](RELOAD.md). This transition layer does not repair those source defects.
