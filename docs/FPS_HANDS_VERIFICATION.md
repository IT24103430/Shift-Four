# First-person hands verification

**Overall: PARTIAL.** The shared rig, authored tactical surfaces and presentation layers work with the existing FPS gameplay, but these hands are not accepted as fully polished. Finger/palm contact, dorsal glove weights and several magazine-clearance poses still need artist work. Four additional weapon profiles are draft, non-playable previews. One Interceptor route test also fails with the original weapon presentation; another Interceptor test is sensitive to suite context.

Unity **6000.6.4f1**, macOS / Apple M1 / Metal. The original MainLab and range scenes were tested in `/tmp/ShiftFour-ArmsInspection`, a disposable project copy. MainLab was loaded but never regenerated or saved. All captures and actions below are **scripted Unity tests**, not human playtesting. No visual acceptance is inferred from compilation alone.

## Acceptance

| Area | Result | Verified behavior / remaining limit |
|---|---|---|
| Baseline inspection | PASS | [Ranked findings, preserved features and source/license audit](FPS_HANDS_INSPECTION.md); 19 existing Edit / 35 existing Play tests passed before implementation. |
| Owned mesh, scale, bones and weights | PASS | CC0-derived owned FBX/prefab; 48 bones, normalized four-influence skinning, unit scales, approximately 220 mm upper arms / 227 mm forearms and three finger segments. Imported source/GUIDs retained. |
| Rig visual quality and natural hand contact | PARTIAL | Improved garment silhouette and proportions; visible palm/finger pad gaps and dorsal reinforcement creasing remain. Strong pinky edits can spike incorrectly influenced panel vertices. |
| Glove, sleeve and cuff materials | PASS | Five shared authored URP/Lit surfaces, restrained source-derived grain/weave normal maps and smoothness masks; zero runtime material creation. Three MainLab views: 222,156 changed arm pixels, zero pink and zero clipped-white highlights. Three real restarts retain the same shared material assets. This is stylized tactical detail, not a scanned texture package. |
| Dominant/support wrist attachment | PASS | Rotation-only arm IK, original bone translations/scales, 35° wrist bound on the owned rig; pitch/yaw, ordinary recoil, reload contact and cancellation checks pass. Camera ray remains authoritative. |
| Fully natural closed pistol grip | **FAIL** | Front/side inspection shows splayed fingers and substantial finger-pad gaps. Target stability and a scoped intersection audit do not certify natural contact. |
| Idle | PASS | Restrained five-second breathing loop, 30-second standing run, no cumulative transform drift, stable 30/60/120 Hz sampling; camera/crosshair stationary. |
| Walking, strafing, takeoff, airborne hold and landing | PASS | Reads actual player travel and CharacterController grounded state; no stationary bobbing, camera/capsule changes or replacement jumping physics. |
| Firing | PASS | One accepted-shot event drives visual recoil and existing muzzle flash; single/repeated/walking/jump/wall/empty-magazine checks preserve ammo, damage, cooldown and camera hitscan. |
| Reload gameplay and hand/magazine timeline | PASS | Normal/empty/partial reserve, timing, death/switch cancellation and ammo conservation pass; gameplay remains the only ammunition-transfer authority. |
| Intersection-free reload / fingers | **FAIL** | Actual baked geometry has 57, 66 and 136 covered crossing triangle pairs at sampled progress 0.28, 0.38 and 0.70. Dominant pinky and magazine clearance need correction. Five other sampled stages have zero covered crossings. |
| Equip/unequip | PASS | 0.38 s raise / 0.26 s lower, bounded interruption/reversal, exact endpoints, one reused viewmodel and weapon-specific ammo/cooldown retention. **Firing during the raise is allowed**, as explicitly requested; lowering/hidden/mismatched-model transitions block firing and reload requires Ready. |
| Five weapon configurations | PARTIAL | Actual imported geometry inspected; one local shared rig per wrapper and unrelated mesh profiles rejected. FN remains playable; four long-gun profiles have no firing/switching implementation or reload clips. |
| Natural long-gun finger clearance | **FAIL** | Covered crossing pairs: MP5 118, M16 136, AK 169, Barrett 512. These are prepared data/integration examples, not accepted grips. |
| Framing, near plane and cover retreat | PASS within tested cases | Original camera and model scale retained, no overlay camera. At 0.35 m wall face, maximum retreat is 0.18 m with an anchored 50° high-ready pitch; both wrists remain framed and minimum wrist depth through recoil is 182.9 mm against a 50 mm near plane. A 440-sample reload/aspect sweep found zero near-plane segments inside the viewport. Camera-forward cover probing is not exhaustive elbow/side-corner clearance. |
| Death, restart, raycast/physics isolation | PASS | Death hides presentation, resets/cancels reload and flash; restart rebuilds one rig with stable shared materials. Hands remain Ignore Raycast with no active gameplay colliders/rigidbodies. |
| Full regression gate | PARTIAL | Full project Play run recorded five failures. Three obsolete arm-fixture readiness failures pass after correction; the two Interceptor failures remain documented below. AI production code is unchanged. |
| Human visual/playthrough acceptance | PARTIAL | Not performed. Actual scripted captures, geometry audits and test logs are supplied for review. |

## Actual test results

All XML files are original Unity Test Runner outputs. Counts below are per run, not added together as unique test coverage.

| Run | Passed | Failed | Other | Evidence |
|---|---:|---:|---|---|
| Unrestricted Edit Mode | 415 | 3 | 5 inconclusive | [XML](evidence/fps-hands/tests/EditMode.xml) |
| Final project-owned Edit Mode | **397** | **0** | 0 | [XML](evidence/fps-hands/tests/EditMode-project-final.xml), `ShiftFour.Tests.*`; includes the reviewed cover pose and preserved preview atlas. |
| Full project Play Mode | 265 | 5 | 1 graphical-only skip | [XML](evidence/fps-hands/tests/PlayMode.xml), approximately 598 seconds |
| Follow-up framing, rig and material tests | 9 | 0 | 1 graphical-only skip | [XML](evidence/fps-hands/tests/PlayMode-framing-materials.xml) |
| Original weapon presentation: two Interceptor cases | 1 | 1 | 0 | [Baseline XML](evidence/fps-hands/tests/Interceptor-original-baseline.xml) |
| Upgraded presentation: same two cases isolated | 1 | 1 | 0 | [Comparison XML](evidence/fps-hands/tests/Interceptor-upgraded-isolated.xml) |
| Final affected framing/movement/firing Play Mode | 20 | 0 | 1 graphical-only skip | [XML](evidence/fps-hands/tests/PlayMode-reviewed-anchor.xml) |
| Final graphical Editor run | **29** | **0** | 0 | [XML](evidence/fps-hands/tests/PlayMode-graphical-reviewed.xml), all requested captures and four real desktop resolutions |
| Responsibility checkpoint compilation | 8 assemblies | 0 | Existing warnings retained | [Results](evidence/fps-hands/branch-compilation/results.json); Unity's compiler and reference response files, runtime/editor assembly per checkpoint. This is source compilation, not a claim of complete Unity testing per branch. |

The unrestricted Edit failures are tutorial `Tests_14s.ScriptAddingTests.CreatedScriptIsVerified` (missing generated MyTempScript), `Tests_15.MyClassTests.YourTestGoesHere` (exercise assertion) and `Tests_16s.MyClassTests.SomethingElseReturnsTrue` (501 ms against a 500 ms limit). Five other sample exercises are inconclusive. No project Edit test failed, and sample failures were not suppressed or edited.

In the full Play run, `TacticalArmsMaterialsPlayModeTests` had two failures and `TacticalArmsRigPlayModeTests` had one because those older fixtures froze time before the new initial equip completed. Their setup/restart code now waits for the real Ready gate. Original pixel thresholds, material reference/inventory checks and assertions remain intact; the follow-up run passes all three affected cases.

`InterceptorMainLabPlayModeTests.MainLab_BaselineComparison_IdenticalMotorRouteAttempts` fails on a null predictive route with both original and upgraded weapon presentation. `MainLab_GenuineVisibleMotion_DrivesActualBrainDecisions` reports PursueLastKnown instead of InterceptAhead in the full suite, but passes with both presentations when those two cases are run in isolation. The full-suite failure remains recorded; no AI or navigation implementation was changed to conceal it. It needs a separate order/state-dependence investigation.

The full project suite covers CharacterController movement/jumping, weapon damage/cooldown, ammunition/reload, viewmodel raycast isolation, sound/noise, combat/death, drone AI/body/motor/destruction/collision, four-room progression and restart. Individual detailed test names remain in the XML. The full run precedes the final cover-framing review. The final common pose was rerun through the affected framing/movement/firing tests and graphical suite, including the newly added close-cover reload sweep. The broader gameplay/AI production code did not change during that final visual review.

## Actual visual evidence

| Requested state | Actual Game-view capture |
|---|---|
| Idle | [1280×720](evidence/fps-hands/framing/idle-1280x720.png) |
| Firing | [Accepted shot and muzzle flash](evidence/fps-hands/firing/single-impulse.png) |
| Reload beginning | [Access pose](evidence/fps-hands/reload/access.png) |
| Magazine removal | [Extraction](evidence/fps-hands/reload/extraction.png) |
| Magazine insertion | [Insertion](evidence/fps-hands/reload/insertion.png) |
| Reload completion | [Complete](evidence/fps-hands/reload/complete.png) |
| Walking | [Walking](evidence/fps-hands/movement/walking.png) |
| Jumping | [Airborne](evidence/fps-hands/movement/airborne.png) |

Motion evidence: [firing video](evidence/fps-hands/firing/firing-game-view.mp4), [reload video](evidence/fps-hands/reload/reload-game-view.mp4), [equip/unequip video](evidence/fps-hands/equip/equip-game-view.mp4). Additional movement stages include [takeoff](evidence/fps-hands/movement/takeoff.png) and [landing](evidence/fps-hands/movement/landing.png).

Framing: [4:3](evidence/fps-hands/framing/idle-1024x768.png), [16:10](evidence/fps-hands/framing/idle-1280x800.png), [21:9](evidence/fps-hands/framing/idle-1680x720.png), [looking up](evidence/fps-hands/framing/looking-up.png), [looking down](evidence/fps-hands/framing/looking-down.png) and [close cover](evidence/fps-hands/framing/close-cover-high-ready.png).

Actual Unity inspection cameras: [front](evidence/fps-hands/grip-inspection/inspection-front.png), [right side](evidence/fps-hands/grip-inspection/inspection-right-side.png), [left side](evidence/fps-hands/grip-inspection/inspection-left-side.png). These expose the remaining finger/palm contact defects rather than conceal them.

Draft previews, not playable weapons: [MP5](evidence/fps-hands/draft-previews/HKMP5.png), [M16](evidence/fps-hands/draft-previews/M16A1.png), [AK](evidence/fps-hands/draft-previews/AK47.png), [Barrett](evidence/fps-hands/draft-previews/BarrettM82A1.png). They preserve the supplied atlas and still fail finger-clearance acceptance. Their HUD remains the original FN pistol HUD because the active gameplay weapon is never swapped by the preview test.

[Visual provenance and source frame hashes](evidence/fps-hands/VISUAL_PROVENANCE.json) distinguish actual Game-view, direct player-camera and inspection-camera renders.

The baseline inspection images are direct player-camera renders without HUD; its articulation/front/side views are explicitly inspection cameras. Final Game-view captures are read from the actual `UnityEditor.GameView` render texture and include HUD. PNG conversion is lossless; video uses the captured 30 FPS frame sequence with only even-dimension padding needed for H.264. No generated, retouched or interpolated images are used. Tests pause drone AI and use deterministic input/time steps; close-cover probes use a temporary test cube. This does not certify an unaided human playthrough.

A first close-cover pose cleared the near plane but lost the wrists below/outside the screen in the actual capture. It was rejected. The reviewed pose rotates about the dominant wrist and compensates its orbit, with an explicit viewport assertion. An intermediate viewport test failed before that compensation was added; the retained [failed XML](evidence/fps-hands/tests/PlayMode-reviewed-initial-failure.xml) and final follow-up show the correction. Preview review also caught native FBX default white materials; the owned copies now preserve the supplied atlas.

Additional geometric evidence: [reload contacts/crossings](evidence/fps-hands/audits/reload-contact-audit.json), [pistol idle audit](evidence/fps-hands/audits/pistol-idle-surface-audit.json), [four-weapon audit](evidence/fps-hands/audits/multi-weapon-surface-summary.json), [aspect/pitch/yaw trace](evidence/fps-hands/measurements/aspects-pitch-yaw.csv), [close-cover recoil](evidence/fps-hands/measurements/close-cover.txt) and [reload near-plane sweep](evidence/fps-hands/measurements/close-cover-reload-near-plane.csv). Pure digit crossing coverage excludes mixed connected palm/web triangles and same-digit self-contact; zero covered pairs is not a complete visual acceptance test.

## Settings and integration

- [Rig/source and reproduction](../ArtSource/TacticalArms/README.md), [materials](../ArtSource/TacticalArms/MATERIALS.md), [pistol grip](../ArtSource/TacticalArms/GRIP.md).
- [Idle settings](../ArtSource/TacticalArms/IDLE.md): 12 breaths/minute, 0.8 mm vertical, 0.3 mm lateral, 0.25 mm depth, 0.18 s blend.
- [Movement settings](../ArtSource/TacticalArms/MOVEMENT.md): actual travel, grounded state and restrained takeoff/landing layers.
- [Firing](../ArtSource/TacticalArms/FIRING.md): accepted-shot 12 mm backward impulse, approximately 1.96° peak muzzle rise, bounded repeated impulses.
- [Reload](../ArtSource/TacticalArms/RELOAD.md): normalized authored curves against the configured gameplay duration (default 1.6 s), single existing magazine and no invented slide part.
- [Equip contract/hooks](../ArtSource/TacticalArms/EQUIP.md), [multi-weapon configuration/integration](../ArtSource/TacticalArms/MULTI_WEAPON.md), [framing](../ArtSource/TacticalArms/FRAMING.md).
- [Responsibility branch stack and supplied identities](FPS_HANDS_BRANCHES.md).

No imported animation clips were added. The original CC0 source's crude sample is not a pistol animation set; the installed layers are lightweight procedural sampling and authored normalized reload curves. Optional long-gun clip fields are preparation hooks without implemented playback/reload integration.

## Files and preservation

The exact publish inventory is [FPS_HANDS_CHANGED_FILES.csv](FPS_HANDS_CHANGED_FILES.csv). It covers the owned source/FBX/materials/rig, shared pose/motion/binding components, the owned pistol wrapper, four draft wrappers/five profiles, editor authoring tools, focused tests/fixture readiness changes and reports/evidence. Shared files are handed off explicitly across the branch stack.

[66 protected paths](evidence/fps-hands/audits/protected-assets.json) match remote `dev` byte for byte, including MainLab, PlayerController, Health, WeaponRuntimeState, WeaponDefinition/default pistol data, original CC0 assets/GUIDs, PolyOne imports, URP settings and camera/collision project settings. `FirstPersonViewmodel.cs` and its accepted-shot/raycast isolation implementation are preserved. WeaponController changes only integrate presentation readiness/cancellation guards; damage, raycasts, cooldown, ammunition transfer and runtime-state storage remain gameplay-owned. No movement speed, jump physics, footsteps, Health or drone AI production code changes.

[153 changed/new Unity asset and source paths](evidence/fps-hands/audits/tested-copy-comparison.json) match the final tested disposable copy byte for byte. Ignored Library/Logs/test scratch assets are excluded from commits. The original workspace remains on its existing branch with its worktree changes; publication uses a separate clean Git checkout to avoid changing that workspace's index or branch. No automatic merge or direct push to dev/main is performed.

## Remaining work

1. Correct owned dorsal reinforcement/pinky skin weights and review wrist/cuff creasing. Preserve anatomical bone lengths and original source assets.
2. Author and visually approve closed palm/finger contact for the pistol, then correct dominant-pinky clearance at extraction/insertion reload stages. The current single-magazine offscreen exchange is limited by available mesh parts.
3. Create separate finger/palm poses for MP5, M16, AK and Barrett; pass surface audits and front/side/Game-view review before making any weapon playable. Author compatible reload/idle clips or normalized presenters and wire them explicitly.
4. Investigate the baseline Interceptor null-route and full-suite observation-state failure separately. Complete human playtesting, including side-wall/door/corner approaches and motion review at target resolutions.

The current CC0 anatomical base can be retained for these repairs. If replacement art is chosen, the specific missing asset is a licensed FPS glove/sleeve rig with clean palm/pinky reinforcement weights, reviewed finger/wrist deformations, natural weapon-specific grip/contact poses and compatible reload animation or editable source. No unapproved replacement model was downloaded and the present derivative is not presented as a premium scanned arms asset.
