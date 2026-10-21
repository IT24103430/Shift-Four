# First-person framing and cover clearance

The owned Five-seveN keeps its existing model scale and camera-relative base pose. `FirstPersonViewmodelFraming` adds a configurable obstruction pose on the common arms/weapon pivot. No additional camera, render feature, depth override or gameplay collision layer is introduced. MainLab's camera, lighting and URP settings remain authored as before.

The old maximum 0.24 m retreat placed the dominant wrist near the 0.05 m camera near plane. The owned pistol now caps retreat at **0.18 m**, with a smooth **50° high-ready pitch** blended over 0.06–0.18 m retreat. The complete assembly rotates about the authored dominant-wrist anchor `(0.03, −0.022, −0.148)` m. A blended `(−0.06, +0.07, +0.10)` m anchor offset brings both wrists into view; this compensates the pivot orbit rather than translating bones. Both hands, weapon and magazine move in the same space. `cuffLift` is available but defaults to zero, preserving the base framing. Legacy weapons without the component retain their previous 0.24 m cap.

The existing sphere cast, gameplay raycast mask, immediate retraction and 0.8 m/s outward recovery remain in `FirstPersonWeaponMount`. `PistolPresentation` applies the common cover rotation after the mount updates, then composes reload, recoil, idle and movement. It remains the single writer of the child pivot. Equip/unequip continues on the model root; cover retreat stays on WeaponSocket.

An initial 35° pose passed depth clearance but actual Game-view review showed that the hands left the screen near cover. It was rejected. The final anchor-based pose also checks both wrist viewport positions during recoil; minimum measured depth is about **182.9 mm**, versus the unchanged 50 mm near plane. A 440-sample reload/aspect sweep checks actual baked triangle intersections with the visible near-plane footprint.

The same normal world depth test occludes arms behind geometry. The camera-forward probe is not a complete volumetric collision solver for elbows or side-wall corners. Close-cover/reload and resolution evidence must be reviewed; no claim of exhaustive world-geometry clearance is made.

`FirstPersonViewmodelFramingPlayModeTests` checks actual wrist depth and muzzle clearance through accepted recoil at a 0.35 m wall face, 36 aspect/pitch/yaw combinations, unchanged camera/capsule/near plane, persistent materials, no extra cameras and retreat recovery. A graphical test selects real fixed Game-view sizes at 1280×720, 1280×800, 1024×768 and 1680×720; captures come from Unity's Game-view render surface with HUD. Batch mode explicitly skips that graphical test.

The source glove weight/creasing and finger-contact limitations remain. A viewmodel overlay camera would hide world clipping, and was not needed by the demonstrated near-plane case.
