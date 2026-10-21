# Detailed first-person reload

`FirstPersonReloadMotion` on the project-owned pistol prefab stores normalized, authored AnimationCurves. `PistolPresentation` reads `FirstPersonViewmodel.ReloadProgress`, which comes directly from the gameplay-owned `WeaponRuntimeState`. Presentation does not start/end reloads, transfer ammunition or emit shot events. The existing 1.6-second Five-seveN duration is unchanged, and curves scale to any configured reload duration.

## Timeline

| Normalized progress | Default 1.6-second timing | Action |
|---|---|---|
| 0–0.16 | 0–0.256 s | Slight access tilt: 6° pitch, −8° yaw, −16° roll; position (−15, +30, +10) mm |
| 0.10–0.26 | 0.16–0.416 s | Support hand clears the main grip and reaches the magazine baseplate |
| 0.26–0.44 | 0.416–0.704 s | Extract along the slanted magazine axis: 160 mm down and 67 mm rearward |
| 0.43–0.60 | 0.688–0.96 s | Lower/reindex the magazine below the viewport for exchange |
| 0.60–0.82 | 0.96–1.312 s | Return the replacement presentation along the insertion axis |
| 0.80–0.88 | 1.28–1.408 s | Small 2 mm seating response |
| 0.85–0.98 | 1.36–1.568 s | Support hand clears the magazine and returns to its existing grip |
| 0.84–1.00 | 1.344–1.6 s | Recover pistol access pose smoothly; gameplay alone completes ammunition transfer |

All curve keys use zero endpoint tangents. `access`, `handContact`, `withdrawal`, `exchange` and `seat` are saved on the prefab and editable in the Inspector. Translation values are metres; rotation values are degrees. Exchange adds (−25, −40, −15) mm and a small local magazine reorientation after extraction. Support-hand approach/return arcs can move 50 mm outward to clear the dominant hand before rotating under the magazine.

## Magazine and hand binding

The vendor hierarchy supplies one separate `FNFiveSeven_Magazine` MeshFilter under the pistol model. Its mesh, parent, authored rest transform, materials, original source references and GUIDs remain intact. The owned prefab adds a layer-2 `MagazineGripTarget` child that records a wrist pose fitted to the original thumb/index surfaces at the lower baseplate.

The available asset has one magazine mesh. The animation reuses that part through a continuous extraction and below-frame exchange before reinsertion. It does not create a dropped magazine, a second simultaneously visible magazine, cartridges or physics objects. There is no independently animated slide in this asset; empty reloads use the same available magazine presentation rather than inventing a slide-lock animation.

The dominant wrist keeps its existing target. The support arm uses rotation-only two-bone IK to the magazine wrist target during held phases. Elbow guidance follows the desired forearm direction, with up to 75° of forearm pronation while retaining the original 35° wrist deviation limit. Bone local positions/scales and finger local rotations remain unchanged. Authored shoulder-to-elbow and elbow-to-wrist lengths are preserved. Approach/return uses the existing hand target, so cancellation or completion restores the owned grip without cumulative roll.

Normal completion returns the magazine and pistol to their exact rest transforms and blends breathing/movement back through existing presentation envelopes. Manual cancellation preserves the existing immediate restoration contract. Death and weapon switching cancel gameplay reload first and clear/reset the hidden or destroyed presentation; no animation event grants ammunition. Starting another reload always samples its new gameplay progress rather than continuing stale animation time.

## Artist setup

`Tools → Shift Four → FPS → Add Detailed Pistol Reload Once` installs missing presentation data while preserving an existing magazine target and curve tuning. `Apply Authored Magazine Grip` explicitly reapplies the reviewed baseplate wrist target, forearm limit and clearance settings. Source model files and scenes are not edited. Save artist changes on `Assets/Prefabs/FPS/FNFiveSevenViewmodel.prefab`.

## Verification

`FirstPersonReloadEditModeTests` checks phase ordering, bounded/continuous curves, deterministic endpoint/reset sampling, saved source magazine/owned target hierarchy and lack of ammunition authority. `FirstPersonReloadPlayModeTests` checks actual normal/empty/partial-reserve reloads at configured durations, blocked firing, early completion rejection, once-only ammo transfer, cancellation at several phases, death/switch cleanup, wrist/hand alignment, preserved skeleton/fingers and real Game-view stage/sequence captures.

Measured geometry and per-stage surface audits are retained under `Logs/ArmsReload/` alongside XML, traces and actual captures. The underlying [grip's existing wrap/creasing limits](GRIP.md) remain; this reload pass does not edit source geometry or weights.

## Remaining visual acceptance issue

The existing dominant pinky curls beneath the magazine baseplate. Actual baked-surface audits still find pinky/magazine intersections during extraction and insertion, although the support-hand approach, held pinch, exchange and return are aligned. This is a known remaining visual defect, not a fully clear reload grip.

A temporary pinky relaxation was rejected: the current glove reinforcement weights caused approximately 65 mm of skin-edge growth. A small rigid dominant-hand adjustment also introduced pistol-body intersections. Neither experiment is shipped. The final implementation preserves all finger rotations and the original dominant-hand target. Resolving the remaining clearance requires a corrected owned glove skin/weight pass and a reviewed lower-finger release pose; it should not be hidden with large bone translations or distorted fingers. This task does not modify the imported source, skin weights or materials.
