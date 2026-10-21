# Storage cover rebalancing

Owner: Nimsara — Systems Engineer, Storage, Flanker. User ownership approval: **“Approve this one-prop removal”** for `Storage/Fixed cover 2` at `(10, 1.1, -8)`, including its visuals and collider, keeping other cover, gates, consoles and saved navigation unchanged.

## Exact change

Remove the entire `Fixed cover 2` GameObject from the MainLab Storage instance. The source Storage prefab and its GUID are preserved. The removed prop measured **2 × 2.2 × 1.5 m**; it provided full standing-eye cover. The two defensive props remain:

| Prop | World center | Size | Result |
| --- | --- | --- | --- |
| Fixed cover 1 | (7, 1.1, -13) | (2, 2.2, 1.5) | Preserved |
| Fixed cover 2 | (10, 1.1, -8) | (2, 2.2, 1.5) | Removed with collider and matching decoration |
| Fixed cover 3 | (13, 1.1, -11) | (2, 2.2, 1.5) | Preserved |

The visual-upgrade authoring code dresses each solid prop with separately parented decoration in `Storage/Visual polish`. Inspection caught these after the first removal. The removed cover's **four Cargo band objects** at x=10, z≈-8.782 and y≈0.605/0.913/1.221/1.529, plus its **Storage equipment face** at `(10,1.1,-8.762)`, are removed with it. These are the approved prop's matching surfaces, not additional cover. The regression checks that they cannot remain floating. No invisible blocker is left behind; the cover had no NavMeshObstacle.

Removed source GameObject IDs in the same Storage prefab instance: solid `4604590486330218604`; matching trim `339334225559209551`, `5042696197883533933`, `2667735801516918792`, `5581495398977779762`, `3097765748977056483`. All use existing prefab GUID `4de5547e8bb62e949ad99ae2b7d57ad8`.

The central standing cover position is reduced from three options to two, opening the center lane while keeping left/right defensive options. Room dimensions, architectural walls, support geometry, barriers, materials, mission zone, console and all four gates are unchanged. Player movement/jump physics and Flanker decision policy are unchanged. No new drone/player spawn exposure was introduced by relocating a spawn.

## Navigation and movement evidence

Saved NavMesh queries after the change:

| Route | Native status | Distance |
| --- | --- | ---: |
| Storage entry (10,0,-2.5) → console approach (15.5,0,-4.5) | PathComplete | 5.852 m |
| Flanker start (10,0,-5.6) → left approach (5,0,-13) | PathComplete | 9.001 m |
| Flanker start → right approach (15,0,-13) | PathComplete | 9.440 m |
| Storage entry → exit approach (18,0,-15) | PathComplete | 14.841 m |

The focused Play Mode fixture also drives the real Flanker motor to both side approaches, checking raised-body occupancy each frame rather than relying solely on native ground paths. Both approaches passed. Other motors are stopped for deterministic arrangement; this is scripted validation, not human combat balance playtesting.

The original saved bake still excludes the removed prop's footprint: `(10,0,-8)` is not on NavMesh within 0.05 m. Existing required routes remain complete, so this change does not rebake or modify navigation data. Traversal across that newly cleared footprint is a remaining optional navigation improvement requiring explicit approval. Locked gate restrictions remain covered by existing gameplay tests.

## Visual evidence and limitations

Before/after overhead and player-level Unity camera captures and a native-path debug overlay are in `docs/evidence/drone-mobility/images`. They show the actual authored scene, with an inspection camera below the ceiling for the overhead view. They are not human Game-view playtesting. Complete mission gate/death/restart regressions and exact XML counts are reported in [the integration verification](DRONE_MOBILITY_AND_STORAGE_VERIFICATION.md).

A manual combat pass is still needed to judge whether the increased exposure and faster Flanker provide the desired challenge; automated movement/path checks do not establish encounter fairness.
