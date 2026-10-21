# Faster drone response, owned audio and combat damage contracts

Verified base: `87d580376aacb0d6dd58f1f293b8fe1c35d8e40c` (origin/dev). Builds on the previously published `integration/compact-faster-drone-fleet` at `c325c5a8db1ee25b5c86ddd3555827414934e662`. Unity **6000.6.4f1**, macOS, 21 October 2026.

Complete implementation: `integration/rapid-drone-response-audio`. Production and test snapshot: `cf29e32c0b73a24b1109f2bdba4d4aac656fe0ad`. Worktree: `/Users/dev/Developer/ShiftFour-workspaces/drone-mobility-87d5803`. The original Desktop dev checkout, its divergent local commit and untracked recovery/evidence files remain preserved. Committed work is visible on the feature/integration branches, rather than as pending changes on the original dev checkout.

## Damage and weapon contracts

| Weapon / attacker | Damage per accepted clear hit | Unchanged firing interval |
| --- | ---: | ---: |
| Player FN Five-seveN | **22 HP** | 0.24 s |
| Scout | **10 HP** | 1.1 s |
| Guard | **12 HP** | 1.1 s |
| Interceptor | **15 HP** | 1.1 s |
| Flanker | **20 HP** | 1.1 s |

The pistol definition and safe fallback use 22. Its magazine remains 20 rounds, starting reserve 80 and reload duration 1.6 seconds. The gameplay camera remains the authoritative ray origin/direction. Accepted shots consume one round; misses and blocked shots apply zero target damage while still consuming that round and cooldown. Cooldown rejects and empty magazines produce no additional shot, damage or sound. Existing reload, death and equip contracts remain authoritative; firing during the equip raise stays allowed.

Drone damage is authored in the matching `DroneDifficultyProfile` (`shotDamage`) and copied through validated settings at startup. Legacy profiles without a positive field retain a 10 HP fallback. The existing living-target, range, visibility, pivot-to-muzzle and fire-time ray checks run before damage. Each accepted shot applies one damage event and emits one visual event and one gameplay noise. Immediate repeat calls are rejected by the same per-drone cooldown. Health clamps lethal hits at zero. No changes to WeaponRuntimeState, the player controller or Health.

`CombatAuditPlayModeTests.AllFourAttackers_RejectCoverEmbeddedOriginRangeDeadAttackerAndDeadTarget` independently asserts 10/12/15/20 amounts, one damage callback per accepted hit and zero callbacks for rejected attacks. WeaponArchitecture, FiveSevenPlayable, CombatHud, ProgressiveDifficultyValidation and firing/reload tests exercise actual accepted hitscan, miss/cover rejection, magazines, cooldown, reentry, reload transfer and cancellation. With unchanged health, the requested pistol requires 5 Scout hits, 7 Guard hits, 6 Interceptor hits and 7 Flanker hits from full health.

## Drone size, movement and perception

Visual scale is **0.50**, down from 0.65 in the preceding fleet branch (23.1% further reduction; 44.4% below origin/dev's 0.90). Root transforms remain unit scale. The owned installer measures complete rotating geometry and reauthors matching collider/avoidance bounds. Four owned prefabs and their existing MainLab drone overrides are updated. Source meshes, FBX files and GUIDs are preserved.

| Drone | Patrol m/s | Investigate m/s | Chase m/s | Full swept body/avoidance radius |
| --- | ---: | ---: | ---: | ---: |
| Scout | 2.8 | 3.6 | 4.4 | 0.621 m |
| Guard | 3.0 | 3.75 | 4.5 | 0.813 m |
| Interceptor | 3.8 | 4.6 | 5.4 | 0.619 m |
| Flanker | 4.2 | 5.1 | 6.0 | 0.686 m |

Acceleration is 16 m/s² and angular speed 240°/s. The collision capsule radius is 0.25 m; full-body navigation and mesh collision still account for the actual wider model. Movement remains subject to solid geometry, body clearance, valid routes and locked gates. No NavMesh rebake or scene regeneration. This work retains the previously approved one-prop Storage removal without further room geometry edits.

All brains evaluate at a configurable 0.08 s interval. Accepted hearing observations wake the next Update. Hearing sensitivity is 2, with bounded work (four queries per frame, 16 per second per sensor). Fresh reachable sound evidence immediately enters investigation. Existing attenuation, uncertainty, expiry, graph-gate restrictions and navigation checks remain active. A drone ignores its own noise. Hearing alone never grants visual confirmation or unrestricted player coordinates; Interceptor keeps its own validated tactical investigation and prediction. Visual velocity measurements accumulate sufficient elapsed time across the faster ticks. Guard can pressure genuinely visible players beyond its former four-metre preference.

## Audible effects

Three original, project-owned, deterministically synthesized 44.1 kHz mono PCM WAVs: PlayerPistol (0.28 s), DroneShot (0.23 s), LabDoorOpen (1.60 s). No downloaded samples. Source generator: `tools/audio/generate-combat-sounds.py`; ownership and format documented beside the clips. These are synthesized effects rather than firearm recordings.

The built-in Unity audio module is enabled. A scene-owned pool of 16 AudioSources plays accepted gunshots and the start of gate opening. Local pistol shots are 2D; drones/gates use spatial attenuation. Default volumes are 0.65 / 0.50 / 0.45. One listener is installed on the player camera when none exists. Opening sound fires once; physical passage still waits for the existing door animation. Playback never republishes gameplay noise. Subscriptions and voices are cleaned up on disable/restart.

The graphical Editor test used real accepted player/drone shots and a real mission gate unlock, sampling `AudioListener.GetOutputData` over real wall-clock intervals. Measured nonzero output: player peak 0.38780 / RMS 0.02824; drone peak 0.14685 / RMS 0.00841; door peak 0.15912 / RMS 0.01364. CSVs record actual listener measurements, not a WAV recording. Earlier AudioRenderer capture attempts returned zero in this Editor and are not used as passing evidence. Scripted verification is distinct from human listening/playtesting, which was not performed.

## Actual Unity verification

| Final run | Total | Pass | Fail | Skip |
| --- | ---: | ---: | ---: | ---: |
| Project Edit Mode assemblies | 409 | **409** | **0** | 0 |
| Complete project Play Mode assembly | 282 | **280** | **0** | 2 |
| Graphical Editor: audio output and actual Game-view resolutions | 2 | **2** | **0** | 0 |

The two batch skips are exactly the graphical audio-output and Game-view-resolution cases; both pass in the separate graphical run. No test was disabled. The two previously reported Interceptor MainLab scenarios pass in the complete final assembly. No unresolved failure remains in these final runs. These are scripted automated checks, not a human combat/balance playtest.

The first expanded run after damage tuning reported 274 pass / 6 fail / 2 skip. Those six failures were four stale pistol-damage expectations, the impossible Guard-sighting fixture and the fixed-size clearance expectation described below. The complete final rerun uses the corrected fixtures and retains all gameplay/penetration assertions. Earlier broad pre-damage and targeted intermediate runs are not counted as final passing evidence.

The complete project Play Mode assembly covers movement/jump, drone motor and animation, hearing/awareness, navigation/prediction, damage/death/restart, sound events, four rooms/gates, weapons/ammunition, presentation and arms compatibility. The two previously investigated Interceptor MainLab scenarios are retained. Batch-only graphical skips are reported explicitly and exercised separately in the graphical Editor.

Three fixture corrections preserve behavioral checks:

- Guard movement previously passed `visible=true` for the Checkpoint player despite actual visibility being false across a locked gate. The fixture now stages a genuinely visible, reachable six-metre Server target. All four agents must still move more than one metre, maintain full-body clearance/speed limits and finish their routes. No production navigation bypass was added. The original failure and diagnostic positions are preserved in `guard-fixture-diagnosis.txt`.
- The compact-fleet walking collision assertion derives its minimum separation from actual capsule radii and player skin width instead of the old fixed 0.7 m. It additionally asserts that the player stays on the approach side and that visible mesh colliders exist. The existing ComputePenetration limit of 0.01 m remains intact at every yaw/approach.
- Barrel direction error uses atan2(cross magnitude, dot) instead of acos(dot), avoiding cancellation near zero after resizing. The original 0.01° tolerance is unchanged. Damage assertions were updated from the old 25/10 values to the requested contract, including the HUD's five-shot Scout defeat.

Evidence: [XML, run commands, hashes, output measurements and captures](evidence/rapid-drone-response/). Drone images are actual Unity camera inspection renders, not human Game-view playtesting. Graphical framing captures are labeled separately. Protected source hashes cover the original model and saved navigation assets. No production mesh source was overwritten.

## Reproduction and responsibility branches

Create an independent disposable copy including Assets, Packages, ProjectSettings and ArtSource. Use the installed 6000.6.4f1 editor; never run a second editor on an already open project. Run `-batchmode -projectPath <copy> -runTests -testPlatform EditMode -assemblyNames 'ShiftFour.Movement.EditModeTests;ShiftFour.Navigation.EditModeTests' -testResults <edit.xml> -logFile <edit.log>`, followed by PlayMode with `-assemblyNames ShiftFour.Movement.PlayModeTests`. Do not combine `-quit` with these test runs. Exact commands, timestamps and counts are preserved in run JSON.

In the graphical Test Runner, run `RapidResponseAudioPlayModeTests.ActualAudioMixer_RecordsNonSilentPlayerDroneAndDoorEffects` and `FirstPersonViewmodelFramingPlayModeTests.GameViewEvidence_CapturesFourActualDesktopResolutionsAndCloseCover`. Keep Game view active. The graphical disposable copy enables runInBackground for uninterrupted capture; this setting is not promoted to production. The disposable automation exit observer is evidence-only and is not installed in production.

Member commits use the identities explicitly supplied by the user: Pamudi/pamudiab (Scout), Nimthara/IT24610327 (Guard, core combat), Asmadala/IT24103430 (Interceptor, shared agent/navigation), Nimsara/Nimsara-Jayarathna (Flanker, systems audio). [Branch tips and author emails](evidence/rapid-drone-response/responsibility-branches.json) identify each responsibility. These branches form a dependency stack; use the integration branch for the complete tested result. No merge into dev or main. GitHub transport uses the connected account; git author attribution follows the supplied identities.
