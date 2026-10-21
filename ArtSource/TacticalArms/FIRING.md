# First-person firing animation

The existing accepted-shot chain is preserved: `WeaponController.TryFire` validates ammunition/cooldown, applies the gameplay raycast/damage and publishes `ShotAccepted`; `FirstPersonViewmodel` forwards that accepted event to `PistolPresentation`. Rejected cooldown, reload or empty-magazine attempts do not call the firing animation. No animation callback modifies ammunition, applies damage, emits sound or requests another shot.

`FirstPersonRecoilMotion` is a small presentation sampler, not a MonoBehaviour or Animator. `PistolPresentation` remains the only writer of the shared recoil pivot. Both arms, their grip targets, pistol and muzzle flash move together; finger local rotations, bone offsets, proportions and skin weights remain unchanged. The current support-hand pose follows rigidly rather than adding a separate hand lag that would break grip contact. The existing reload hand motion remains separate.

## Saved artist settings

Edit `PistolPresentation` on `Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab`.

| Setting | Default | Effect |
|---|---:|---|
| Recoil Distance | 0.012 m | 12 mm immediate backward impulse |
| Recoil Degrees | 2° | Full muzzle-rise scale; sampled single-shot peak is about 1.96–1.97° |
| Recovery | 22 /s | Damping response rate; higher values recover faster |
| Recoil Lift | 0.0006 m | Up to about 0.6 mm upward travel at the single-shot muzzle peak |
| Recoil Roll | 0.08° | Restrained shared hand/weapon roll |
| Flash Duration | 0.035 s | Existing muzzle-flash timer, unchanged |

The muzzle starts at 65% of its rise scale (about 1.3°), then peaks at approximately 30 ms. Rear travel is immediate and begins with zero recovery velocity. Both channels return with an analytic critically damped response. There is no oscillating rebound, randomized aim drift or accumulated transform delta. Most visible motion is recovered by about 0.35 seconds; the numerical residual reaches exact neutral around 0.7 seconds at the default rate.

Accepted shots add bounded impulses to the current response. Both normalized channels are capped at 1.5, corresponding to at most 18 mm rear travel / 3° pitch with the default scales. Remaining recovery velocity carries through ordinary repeated shots, so the pistol does not reset to an unrelated pose or build an unbounded offset. Dense artificial impulses are also bounded and settle after firing stops.

The sampler uses absolute **scaled** timestamps. The shot callback and that frame's `LateUpdate` share the same timestamp, avoiding an erroneous full-frame decay of a newly accepted impulse. Duplicate samples at one timestamp do not advance motion. Pausing holds the response; disabling presentation or starting/canceling a reload clears recoil/flash as before. The recoil layer returns to the current idle/walking/airborne pose, not a cached old breathing frame.

`Tools → Shift Four → FPS → Save Pistol Firing Settings` (`FirstPersonFiringAuthoring.Save`) serializes new fields while preserving existing artist tuning, transforms, references and source GUIDs. No new runtime material instances, physics objects, player root motion or camera offsets are created.

## Verification and evidence

`FirstPersonFiringEditModeTests` covers the two-stage response, same-frame timestamps, matched 30/60/120-FPS sampling, repeated/dense impulses, bounded recovery, pause/invalid time/reset and saved settings/flash timing.

`FirstPersonFiringPlayModeTests` uses the real MainLab player, actual accepted shots, actual CharacterController movement/jumps and a test-only high-health damage observer. It checks one round/damage/gunshot event per accepted shot, rejected cooldown/empty attempts, live flash timing, wrist attachment, unchanged finger rotations/bone offsets/scales, local baked-skin stability, camera/capsule preservation, moving/jumping/wall firing, reload/disable cleanup, exact combined pose reconstruction and rendered frame-rate samples. A graphical test records an actual Game-view sequence of one shot followed by repeated shots, retaining HUD/ammo and muzzle flash.

Results, CSV traces, raw captures and an encoded animation preview are under `Logs/ArmsFiring/`. The underlying [grip's known finger-wrap/creasing limits](GRIP.md) remain unchanged. This pass preserves that pose rather than reauthoring its anatomy or skin weights.
