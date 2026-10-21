# Two-handed Five-seveN grip

The owned `Assets/Resources/Weapons/FirstPersonArmsRig.prefab` stores the hand/finger pose as bone rotation overrides plus four editable presentation targets:

- `RightHandGrip`: dominant wrist position and orientation.
- `SupportHandGrip`: support wrist position and orientation.
- `RightElbowHint` and `SupportElbowHint`: bend-plane guides.

The targets use the wrist bones' coordinate frames, in meters relative to the arms prefab root. The root is instantiated under the pistol's existing `RecoilPivot`, so camera rotation, ordinary recoil and presentation retreat carry both weapon and grip targets together.

`FirstPersonPistolGrip` uses rotation-only two-bone IK for each arm. Bone local positions and scales remain fixed. Unreachable targets clamp to the anatomical reach boundary. Wrist rotation is limited to 35° from the authored forearm-relative neutral orientation. Solving starts from the authored upper/forearm rotations each time, which prevents path-dependent roll or drift when a reload is canceled.

The finger pose uses the existing metacarpals and three segments per finger/thumb. Small palm cupping/splay distributes the grip through the hand. The dominant index is placed beside the trigger/guard. The support target places the wrist by the dominant grip, but several fingers remain visibly splayed or separated from the grip in side views; the closed contact pose is not accepted. No player, camera, pistol mesh, projectile/raycast, damage, ammo or collision component is changed by the grip layer.

The selected owned rig's reload hand follows the existing magazine offset through rotational IK. The dominant hand stays on its grip target. The existing magazine/pistol presentation path and gameplay timing remain intact. The legacy FBX fallback remains separate from this selected owned-rig implementation.

## Authoring and reproduction

`FNFiveSevenGripPose.json` records the authored local rotations, two wrist targets and elbow hints. **Tools → Shift Four → FPS → Apply Authored Two Hand Pistol Grip** (or `FirstPersonPistolGripAuthoring.Apply`) updates only the owned arms prefab. It first restores the source bone rotations, then applies the complete profile, so omitted bones cannot retain a previous profile's overrides. It preserves source model files, bone offsets/scales, skin weights, materials, source GUIDs and all scenes.

For artist tuning, open the owned prefab, adjust the wrist targets/hints and author finger/palm rotations. Update the profile when those edits should become reproducible; explicitly reapplying the profile replaces manual pose edits. To inspect individual joints without automatic solving in Play Mode, disable `FirstPersonPistolGrip` temporarily.

The CC0-derived rig's dorsal reinforcement has some distal pinky influences on back-of-hand vertices. Aggressive finger edits can therefore produce long panel spikes. The selected pose keeps those influences compatible and is checked for excessive edge growth. The FBX and weights are intentionally preserved in this pose/presentation pass; a wider animation range should include an owned skin-weight correction pass. Close-up glove/panel creasing remains an asset-quality limit, not a new scanned or corrective mesh.

Front/side inspection also shows gaps where finger pads need tighter contact with the grip and opposite hand. Wrist-target stability and the scoped intersection audit pass, but the pose is not yet visually accepted as a fully natural closed grip. Corrected palm/pinky reinforcement weights and a reviewed finger-contact pose are needed for that quality level; the idle layer preserves the current skin shape.

## Verification

`PistolGripEditModeTests` checks configurable targets, profile completeness, original bone offsets/scales/segment lengths, source mesh identity, finite/persistent setup and excessive skin-edge growth. Existing rig tests now validate the immutable FBX bind pose independently of the owned posed prefab.

`PistolGripPlayModeTests` checks nine pitch/yaw combinations, actual shot-ray direction, ordinary recoil, real reload/cancel restoration, extreme target reach/wrist limits, and actual Unity front/side/Game-view captures. Its export uses `SkinnedMeshRenderer.BakeMesh` and editor-only read-only mesh access for the pistol, preserving vendor importer settings.

`tools/audit-pistol-grip.py` reads that captured geometry using Blender's triangle BVH. It tests pure finger regions against other digits, the opposite palm and pistol. Mixed connected palm/web triangles and same-digit self-contact are explicitly outside this geometric test, so front/side visual inspection remains part of verification.

Test XML, logs, measured geometry, audit coverage, actual Unity captures and known limitations are under `Logs/PistolGrip/`. Blender authoring previews are labeled separately and are not substituted for Game-view evidence.
