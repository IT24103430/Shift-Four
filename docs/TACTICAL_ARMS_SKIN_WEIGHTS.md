# Tactical arms reinforcement weight repair

**Overall: PARTIAL; full visual acceptance FAIL.** The incorrect reinforcement
influences are repaired in the staged candidate. Active owned assets are unchanged. This
is not full acceptance of the hands: extreme compound pinky poses still distort
the original glove shell, and the existing closed pistol grip remains visually
unacceptable. Passing regression tests must not be read as a complete art signoff.

Work is uncommitted on `fix/tactical-arms-skin-weights`, based on
`87d580376aacb0d6dd58f1f293b8fe1c35d8e40c`, in the independent workspace
`/Users/dev/Developer/ShiftFour-workspaces/baseline-87d5803`. The original IDE
worktree is untouched. Unity is 6000.6.4f1; Blender is 4.5.11 LTS.

## Corrective changes

Exactly **1,008 Blender vertices** change weights in the candidate: two dorsal panels and six
knuckle guards, each with 126 vertices including its thickness. No native glove,
sleeve or cuff weight changes are included.

- Dorsal panels previously contained proximal/middle finger and thumb influences.
  Finger mass now transfers to the matching metacarpal (`palm_*`); thumb mass
  transfers to `hand.*`. They follow the hand while fingers articulate.
- Knuckle guards retain their own finger chain and metacarpal. Adjacent-finger
  influences transfer to that guard's proximal phalanx/metacarpal. The right pinky
  guard actually covers a fitted middle/distal region, so its valid `.02`/`.03`
  weights are retained rather than forcing the entire guard onto `.01`.
- Six neighbor-smoothing passes and thickness-pair averaging soften transitions.
  Each corrected vertex keeps at most four finite, normalized influences.
- Cuffs already follow forearms without finger weights. No speculative cuff
  reweighting is included. Wrist flexion/deviation and reload-range forearm
  rotation are checked separately.

`tools/tactical_arm_skinning.py` applies the correction by material/connectivity
and bone names. `tools/prepare-tactical-arms.py` calls the same implementation.
The Blender mesh stores a version stamp to make repeat exports idempotent.

Broader native-shell reweighting was evaluated in staging and rejected: smoothing
introduced finger/weapon intersections, and proximity-based pinky transfers
worsened extreme deformation. Those candidates were not retained in the owned
source/export. The original native skin weights are preserved exactly. The candidate is retained
under `ArtSource/TacticalArms/WeightRepairCandidate/`; it is not loaded by the game.

## Geometry and preservation

| Measurement | Result |
|---|---:|
| Blender vertices / polygons | 25,023 / 24,934 |
| Unity imported vertices / triangles | 26,945 / 49,872 |
| Bones | 48, unchanged names, parents, rest matrices and lengths |
| Upper arm / forearm / hand length | approximately 220.311 / 227.094 / 105.834 mm |
| Maximum significant influences | 4 |
| Maximum normalization error | 5.22e-8 |
| Unweighted vertices | 0 |
| Changes to vertex positions, topology, UVs, material regions, object transforms | 0 |

Before/after fingerprints and per-region weight hashes are in
[evidence](evidence/tactical-arms-skin-weights/). They also compare a full CC0-source
rebuild with the staged weights-only export. All finger lengths, arm proportions,
bind geometry and material regions are preserved. No bone translations or scales
were edited.

The original CC0 source remains byte-identical:
`81bed56c1718957171cb6c16ee5b77f7067ccd0d3c39ec0ff052d25322e05d75`.
Third-party/legacy sources and FBXs, PolyOne assets, MainLab, navigation data,
owned prefab bindings, runtime C# and all existing `.meta` GUIDs are preserved.
The unchanged owned FBX GUID remains `c2ff0989af0641698b21f7f82c56ebf6`.

## Verification

| Unity run | Pass | Fail | Skip | Status |
|---|---:|---:|---:|---|
| Original asset, new focused Edit Mode fixture | 13 | 11 | 0 | FAIL: reproduces the fault |
| Original asset, reinforcement stress capture | 0 | 1 | 0 | FAIL: reproduces panel pull |
| Staged candidate, focused Edit Mode suites | 89 | 0 | 0 | PASS |
| Staged candidate, focused Play Mode suites | 53 | 0 | 0 | PASS |

| Visual/geometry check | Evidence | Status |
|---|---|---|
| Current idle pure-surface contact audit | 0 crossing pairs within stated BVH coverage | PASS |
| Dorsal panels under compound pinky stress | 110.021 mm → 0.000143 mm maximum movement | PASS |
| Native glove-shell extreme stress | up to 25.751 mm Unity triangle-edge growth remains | FAIL: visual acceptance not met |
| Natural closed pistol grip | open/splayed contact remains | FAIL: pre-existing pose limitation |

The final two rows are visual/geometry findings, not additional passing Unity
tests. The contact audit excludes mixed connected palm/web triangles and same-digit
self-contact; zero crossing pairs does not prove natural contact. All test XMLs,
raw diagnostics and final captures are linked below. The candidate passed the disposable-project checks and contact audit, but failed
full visual acceptance. It was not promoted into the active owned source/export.


The regression fixture checks dorsal isolation under individual compound finger
rotations, finite skin, exact restoration, ±15-degree proximal finger motion,
35-degree wrist bending, 30-degree deviation, 75-degree reload-range forearm
rotation, and forearm-only cuff influence. Existing tests cover import/bind pose,
materials, prefab references, dimensions, gameplay-query isolation, grip targets,
reload/death/cancellation, accepted-shot recoil, idle, movement, equip and shared
weapon configuration.

Compound stress uses ±75/80/40 degrees on the three finger segments. These exceed
the shipped presentation pose. The tests assert dorsal isolation and finite skin;
whole-skin strain is separately recorded and **does not receive a visual PASS**.
The historical full-project Interceptor navigation failure is outside these
focused runs and was not reclassified as fixed.

## Actual visual evidence

All captures are scripted. Blender images use inspection cameras; Unity images
use actual rendered inspection/player-camera targets. These are not human
playtesting or desktop Game-view/HUD captures. The unchanged real player camera
is used for the player-view image.

- Blender dorsal/pinky stress: [before](evidence/tactical-arms-skin-weights/before/blender-pinky-positive-stress-right.png), [after](evidence/tactical-arms-skin-weights/after/blender-pinky-positive-stress-right.png).
- Unity pinky stress: [before](evidence/tactical-arms-skin-weights/before/unity-weight-pinky-positive-stress.png), [after](evidence/tactical-arms-skin-weights/after/unity-weight-pinky-positive-stress.png).
- Unity wrist: [before](evidence/tactical-arms-skin-weights/before/unity-weight-wrist-flex.png), [after](evidence/tactical-arms-skin-weights/after/unity-weight-wrist-flex.png).
- [Reload-range forearm rotation](evidence/tactical-arms-skin-weights/after/unity-weight-reload-pronation.png).
- Current authored grip: [front](evidence/tactical-arms-skin-weights/after/inspection-front.png), [right side](evidence/tactical-arms-skin-weights/after/inspection-right-side.png), [left side](evidence/tactical-arms-skin-weights/after/inspection-left-side.png), [player camera](evidence/tactical-arms-skin-weights/after/game-camera-idle.png).

## Remaining work

The dorsal reinforcement spike is fixed. The original glove shell still forms
stretched/compressed strips under the large compound pinky stress; its native
weights and fitted bind surface need a local modeling/rebinding pass with visual
inspection. That work must retain the current bone lengths and hierarchy. The
current open/splayed pistol fingers also need a separate authored grip pass.
Reload-range regression success does not establish magazine/finger clearance or
natural contact; those pre-existing visual limitations remain.

## Files changed

- `ArtSource/TacticalArms/WeightRepairCandidate/SF_TacticalArms.blend`: staged editable derivative.
- `ArtSource/TacticalArms/WeightRepairCandidate/SF_TacticalArms.fbx`: staged export tested in Unity; not visually accepted.
- `ArtSource/TacticalArms/WeightRepairCandidate/skin-weight-repair.json` and `README.md`: corrective influences and staging status.
- `tools/tactical_arm_skinning.py`, `tools/prepare-tactical-arms.py`: consistent repair/export path.
- `tools/audit-tactical-arm-weights.py`: bind fingerprints, region weight hashes, stress measurements and inspection renders.
- `ArtSource/TacticalArms/WeightRepairCandidate/Tests/*.cs.txt`: exact Edit/Play test sources used in the disposable project; active project test scripts are unchanged.
- `ArtSource/TacticalArms/README.md`, this report and its evidence directory.

Nimthara owns the derivative/rig change; Asmadala should review deformation and
pose compatibility before integration. No teammate scene or navigation asset
was changed. No commit, push or merge was performed.

## Reproduce

In Blender, export to staging with the command in the arms README. To repeat the
original correction rather than the idempotent export, extract the derivative
from the baseline commit into a separate staging input using `git show`; never
reset or replace the original source worktree.

```sh
/path/to/blender -b --python tools/audit-tactical-arm-weights.py -- \
  --blend /path/to/staged/SF_TacticalArms.blend \
  --output-dir /path/to/evidence --render
```

Create a disposable Unity copy containing `Assets`, `Packages`, `ProjectSettings`
and `ArtSource`. Copy the staged FBX into its existing owned path and retain the
baseline `.meta`. Copy the staged test sources into that disposable project,
removing the `.txt` suffix:

- `TacticalArmsSkinWeightEditModeTests.cs.txt` → `Assets/Tests/EditMode/Movement/TacticalArmsSkinWeightEditModeTests.cs`.
- `TacticalArmsRigPlayModeTests.cs.txt` → `Assets/Tests/PlayMode/TacticalArmsRigPlayModeTests.cs` (overwrite only in the disposable copy).

Let Unity import the new Edit Mode script and retain the existing Play Mode `.meta`. Run the recorded Edit/Play commands in
[evidence/run-commands.json](evidence/tactical-arms-skin-weights/run-commands.json)
from that project's root. Engine logs and rejected candidates remain in
`/Users/dev/Developer/ShiftFour-arms-weights-evidence/`; retained XML and PNGs are
in this repository's evidence directory.
