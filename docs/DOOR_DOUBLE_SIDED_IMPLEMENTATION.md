# Double-sided reinforced doors

Authoritative source: `ArtSource/Models/security-sliding-door/SF_HeavySecurityDoor_GV.blend`; export: `Assets/Models/SF_HeavySecurityDoor_GV.fbx`. Existing source/export GUIDs are retained. `upgrade_visuals.py` v1 extends the original source, replacing only `VU_` additions on rerun. Original cores, hierarchy and static construction are retained. Known legacy procedural front ribs/stripes/status bars are replaced to avoid overlapping surfaces; a one-time source backup is checked in.

Each leaf now has graphite/recess layers, titanium edge rails, three segmented plates, visible fasteners and compact amber markings on both faces. The rear has its own vented maintenance pattern and cross-members. Front/rear status strips and header indicators contain `Status` in their names and therefore use the unchanged LabDoor property-block state logic: red locked, amber unlocking, green open. Graphic bars avoid mirrored text and imply no bypass interaction. There are no new panel lights or colliders.

The original two-metre-wide, three-metre-high leaf cores and assembly-local X pivots remain. `Door assembly/LeftPanel`, `RightPanel`, `LeftBolt`, `RightBolt` stay immediate children after normalization. Panel embellishments follow leaf pivots; track/header remain static. Existing bolt/slide durations, 2.15m travel, root physics, gate identities and navigation release logic are untouched. Threshold markings are flat, visual-only geometry on both sides.

Reproduce with Blender 4.5.8:

```sh
blender -b --python ArtSource/GeneratedModels/upgrade_visuals.py
```

In Unity 6000.6.4f1 run **Tools → Shift Four → Refresh Visual Upgrade**. This stages replacement art before deleting the prior named visual; it preserves assembly local transforms and root components. A dependency-hash marker makes an unchanged rerun a no-op for door/drone geometry. Room additions use an explicit revision marker. The menu describes scope and local rollback before writing. Do not run Generate Greybox Once.

All four existing MainLab gates are updated. See [QA](QA/VISUAL_UPGRADE_TEST_MATRIX.md) for actual yaw, materials, animation and passage verification, including remaining manual checks.
