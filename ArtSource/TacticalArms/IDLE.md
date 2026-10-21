# First-person idle motion

The FN Five-seveN prefab owns one `FirstPersonIdleMotion` component. Edit it on `Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab` to save tuning across editor restarts and player respawns.

| Setting | Default | Meaning |
|---|---:|---|
| Breaths Per Minute | 12 | Five-second overall loop |
| Vertical Amplitude | 0.0008 m | ±0.8 mm peak travel |
| Lateral Amplitude | 0.0003 m | ±0.3 mm secondary motion |
| Depth Amplitude | 0.00025 m | ±0.25 mm peak travel |
| Pitch Amplitude | 0.06° | Breathing pitch |
| Yaw Amplitude | 0.025° | Secondary yaw |
| Roll Amplitude | 0.04° | Secondary roll |
| Blend Seconds | 0.18 s | Exponential time constant; about 0.54 s to reach 95% |

Vertical/depth travel and pitch use one sine cycle; lateral travel, yaw and roll use its second harmonic. The resulting motion stays below 1 mm and 0.1° with these defaults. The complete waveform loops smoothly every five seconds.

`PistolPresentation` advances the sampler using scaled frame delta time. A double-precision phase clock and exponential activity envelope make the sample independent of frame count. Every pose is reconstructed from the cached rest position/rotation plus recoil, reload and the current idle sample, so transform deltas do not accumulate. Pausing game time holds the sample. Disabling/re-enabling the motion component clears its state and starts a smooth fade from neutral.

The shared recoil/presentation pivot carries both arms and pistol rigidly. The authored grip, finger skin shape and dominant-hand contact remain stable. Camera, crosshair, aim ray, player root and collision components keep their existing behavior. The procedural layer uses no Animator, animation clip or root-motion application.

Accepted shots suppress the idle envelope during the recoil impulse; an active reload holds it suppressed. The phase clock keeps running through both activities, and the envelope fades back when the activity ends. Reload cancellation preserves the current phase and blends back smoothly.

For initial setup, **Tools → Shift Four → FPS → Add Pistol Idle Settings Once** (or `FirstPersonIdleAuthoring.Install`) adds the persisted component to the pistol prefab. It preserves an existing component's artist tuning and all existing pistol transforms/components. Runtime presentation also supplies default settings if a compatible older pistol prefab lacks the component.

Tests: `FirstPersonIdleEditModeTests` checks 30-second sampling at 30/60/120 FPS, matched samples/activity envelopes, loop velocity continuity, invalid/paused time and saved settings. `FirstPersonIdlePlayModeTests` checks a 30-second standing run, camera/ray/physics-root stability, hand contact, local skin shape, drift, frame steps, rendered 30/60/120-FPS simulation and integration with real firing/reload/cancel. Results and unedited Game-view readbacks are under `Logs/ArmsIdle/`.
