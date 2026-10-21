# Shared arms and five weapon profiles

Only the FN Five-seveN is playable through the existing weapon controller. The MP5, M16A1, AK-47 and Barrett wrappers are explicitly named `NOT_PLAYABLE` previews. They do not create weapon definitions, fire, switch weapons, grant ammunition or implement a reload. Their finger poses are drafts and have not passed surface-clearance acceptance.

`Assets/Data/FPS/WeaponGrips/` contains one `FirstPersonWeaponGripConfiguration` per weapon. Each profile records the actual imported body mesh, source FBX, native grip/foreend reference points, owned model rotation/position, wrist targets, elbow hints, whole-rig offset, forearm pronation and restrained idle settings. All imported long guns point along native −X; the owned geometry rotates +90° about Y. Native FBX child placements are retained, including magazines. The source vendor prefabs center all parts independently and are unsuitable as assembled references without correcting those placements.

`FirstPersonWeaponGripBinding` validates the actual body mesh and local hierarchy before applying a profile. Bone/target references resolve from that wrapper's own shared arms instance; configurations contain no runtime Transform references. An unrelated weapon profile is rejected without changing targets. The shared rig mesh, per-bone translations and scales are retained. Offsets move the entire rig root, and the existing rotation-only solver handles elbows/wrists. Ignore Raycast and disabled viewmodel colliders remain mandatory.

| Profile | Class | Imported body length along native X | Idle vertical amplitude | Reload |
|---|---|---:|---:|---|
| FNFiveSeven | Pistol | Original playable mesh | 0.8 mm | Existing normalized Five-seveN procedural timeline |
| HKMP5 | SMG | 0.6765 m | 0.6 mm | None prepared |
| M16A1 | Rifle | 0.9552 m | 0.6 mm | None prepared |
| AK47 | Rifle | 0.7835 m | 0.6 mm | None prepared |
| BarrettM82A1 | Sniper | 1.2022 m | 0.4 mm | None prepared |

The model-length figures are imported body bounds, not manufacturer specifications or full real-world weapon-length claims. Scale remains one on owned geometry.

## Integration

1. Inspect `Assets/Prefabs/FPS/GripPreviews/*GripPreview.prefab` in a disposable test scene. They contain a geometry pivot, one native FBX assembly, one shared arms loader, one binding and the same idle component used by the pistol. They do not duplicate the animation implementation or the arms mesh.
2. Fit the actual finger and palm contact pose for that weapon. The present JSON finger pose is pistol-oriented. Wrist reach and hierarchy tests do not establish natural hand wrapping or finger clearance. Correct the owned reinforcement weights before making large pinky curls; the current source weights can spike the dorsal glove panels.
3. When implementing a playable weapon separately, create a project-owned wrapper with `FirstPersonViewmodel`, an appropriate presenter and a correctly matched binding. Reuse the existing controller/ammunition state. Do not attach the preview binding to unrelated geometry, copy Transform references from another live rig, or treat a profile as an implemented weapon definition.
4. Add and explicitly wire an authored idle/reload clip or a weapon-specific normalized presentation timeline. The optional clip fields are preparation hooks only; no Animator/clip playback integration or long-gun reload is implemented here. The gameplay controller must remain the sole ammunition-transfer authority.
5. Test the new definition's equip/reload cancellation, actual geometry clearance and camera framing before exposing switching. `WeaponController.Equip` already preserves per-definition runtime state; this task does not add a five-weapon switching input or inventory.

`FirstPersonWeaponGripAuthoring.Create` creates missing profiles and owned preview wrappers. It preserves existing profiles for artist tuning, copies the existing imported URP/Lit atlas material into owned paths, disables chamber bullets in previews and never edits the imported PolyOne FBXs. The saved pistol binding is not preview-only; its original idle/recoil/reload composition remains controlled by `PistolPresentation`.

## Verified boundary and remaining asset work

Focused runtime verification passes local binding, preserved shared mesh/bone offsets/scales, sub-micrometre wrist-target reach and rejection of a mismatched profile. A separate audit of actual `SkinnedMeshRenderer.BakeMesh` output finds covered crossing triangle pairs: MP5 **118**, M16 **136**, AK **169**, Barrett **512**. These count pure digit regions against other digits, the opposite palm and weapon surfaces. Mixed connected web/palm triangles and same-digit self-contact are outside that audit; the numbers are not a complete penetration-volume measure.

Accordingly, the long-gun profiles are **prepared configuration and integration examples, not accepted natural grips**. The missing art deliverable is an artist-reviewed finger/palm contact pose per weapon, together with corrected dorsal reinforcement/pinky skin weights and compatible reload poses or clips. No new external model was downloaded and no imported FBX was changed.

The previews retain the same supplied PolyOne base atlas as the playable pistol. Native FBX default materials are not the authored vendor material; an initial white-material preview was corrected after actual Game-view review. `RestorePreviewAtlas` explicitly restores the four owned copies from the imported material without editing the source material, texture or any GUID. No absent normal/roughness maps are invented.
