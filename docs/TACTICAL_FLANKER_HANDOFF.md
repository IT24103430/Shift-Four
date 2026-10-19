# Task 11 — Advanced tactical Flanker review handoff

**Pending Nimsara's approval.** No approval for Task 11 was found in the local ownership/handoff records. The active Flanker implementation and all other active Assets/Packages/ProjectSettings remain unchanged. The proposed implementation exists only as review material and in a disposable Unity project. No commit, staging, push, scene generation or NavMesh bake is performed by this task.

Nimsara owns Storage and the Flanker policy: [team responsibilities](TEAM_RESPONSIBILITIES.md), [member handoff](members/02-systems-flanker.md). The user's Task 11 explicitly requires owner approval before editing the owned implementation and authorizes focused changes for review otherwise. This package follows that fallback; it does not treat an older Flanker branch or shared sensing approval as new policy approval.

## Baseline discrepancy

The current `FlankerBrain.cs` has two lateral candidates, a distance score, five-second last-seen memory and basic pursuit/home fallback. The owner's [scoring/stability handoff](branches/feature-IS-IT24103464-flanker-scoring.md) and historical commit `057f13c` describe a richer utility implementation with route cost, firing-line quality, 1.4-second commitment and a six-point switching margin. Later baseline commit `02155e7` replaced that implementation with the current short policy. The existing Flanker prefab still serializes the owner's former tuning fields.

The proposal preserves the left/right architecture and restores that documented utility formula and compatible tuning field names, then adds sensor separation, failure recovery and physical route validation. It is based on the actual current preimage, not a blind import of the historical branch.

## Review delta

[Complete patch](evidence/tactical-flanker/TacticalFlanker.task11.patch) changes **one production file**, `Assets/Scripts/AI/Agents/FlankerBrain.cs`, and adds two test files with stable meta GUIDs. It does not change shared movement, sensing, combat, health, graph or gate code; other policies, scenes, prefabs, models and configuration assets are excluded.

The replacement file contains the adapter plus two policy-local helpers, `FlankerPolicy` and `FlankerRoutes`. They remain in the existing file/GUID to keep the integration focused. The [exact proposed source](evidence/tactical-flanker/FlankerBrain.cs.txt), original preimage, tests, source hashes and actual Unity results are retained in the evidence package.

### Evidence and action rules

- Only genuine visible observations update `LastVisual`, `LastVisualAt` and `VisualVelocity`. Recent successive observations detect a substantial direction reversal and release stale commitment. Hidden input is discarded by the adapter and ignored by the pure policy.
- Approximate hearing comes only from fresh shared awareness in Investigate/Search, with received strength at least 0.1. Visual memory has priority. Once visual memory expires, a newer fresh sound may cause investigation or a bounded hold/search at its uncertain location. Hearing never updates visual position, velocity, timestamp or combat authorization. No sound source Transform is retained.
- Existing serialized `memorySeconds` is honored: the actual Flanker prefab uses **6 seconds**. Pure-policy/new-component default is 5 seconds. Neither noise nor repeated hidden ticks renews this deadline.
- Each left/right side evaluates a nominal five-metre offset plus two small angular alternatives, using complete reachable routes and useful standoff. The documented owner formula remains `42 + 30*lateral + 12*range + 14*clearLine - 1.15*routeCost` at the existing prefab defaults.
- Committed endpoints are revalidated every decision. Ordinary switching needs the 1.4-second commitment window and six-point improvement; a blocked route bypasses commitment. The side reference stays stable across failures so changing position cannot relabel a failed flank as a fresh side.
- No physical progress for 2.8 seconds, an invalid route, or arrival without a useful firing line for 1.5 seconds retires the candidate. Up to eight spatial failure records prevent nearby same-side retries for four seconds. A genuinely different target area can produce a new candidate; cooldown expiry permits reconsideration.
- Both flanks unavailable: try a reachable standoff of 2.8–3.5 m around the last genuine visual point, independently of the five-metre flank preference so a slightly distant player still causes an approach; otherwise return to Storage or hold a clear position. No fallback sends the body blindly to an unavailable player root.

### Navigation and Storage return

Each route uses the unchanged gate-aware A* graph and reconstructs complete native NavMesh legs. Every native corner and segment must clear the full raised `DroneBody` envelope plus 0.04 m. Candidate snapping is limited to 0.25 m, replacing the older permissive 2.2 m field behavior. This avoids snapping a candidate across a wall or into a different passage.

When a native shortest approach clips raised furniture, a bounded set of eight Storage-local connectors can supply an alternate path. Each link still passes A*, complete NavMesh and physical clearance. The connectors are read-only policy coordinates relative to Home; no graph edge, scene object, baked area, obstacle or gate is changed. The shared motor receives only the next safe stop and retains movement-time collision protection. Intermediate stops are skipped only when the native body-clear connector to the next stop is valid.

After evidence expires, the policy returns to Home when reachable. Otherwise it selects the lowest travel-cost validated adjacent post within 3.5 m of Home, including a displacement penalty, and retains that post while its route remains valid. This handles an obstructed/unreachable authored post without moving room furniture. The tests verify actual arrival at the selected post using normal motor stopping tolerance, rather than equating an intermediate waypoint with arrival.

### Configuration and preserved mechanics

The existing unchanged default Flanker difficulty resource already supplies **150 HP**, **2.8 m/s patrol**, **3.5 m/s investigation**, **4.2 m/s chase**, acceleration **8 m/s²** and angular speed **120°/s**. No prefab/profile edit is needed. Genuine visual memory retains chase urgency; sound investigation uses the investigation mode. The existing motor smooths cap transitions and supplies actual movement to animation.

“Very Hard” is the requested encounter target, not an existing enum or an automated balance result. No damage, attack range, shot cooldown, fire-time LOS, player damage/recovery, gate progression, death or animation mechanics are replaced. The existing visual and firing checks alone authorize attacks. Gizmos and public candidate fields expose score components, route cost, chosen side and sound/visual route distinction.

## Verification and review

**450 Unity tests passed: 294 Edit Mode and 156 Play Mode, with zero failures or skips.** [Results and exact commands](evidence/tactical-flanker/README.md) include the complete Edit/Play suites, all original Interceptor tests, the new deterministic tactical cases and real MainLab sensor/movement cases. [Visual scenarios](evidence/tactical-flanker/VISUAL_SCENARIOS.md) show both valid flanks, one blocked flank, unavailable flanks and sound-only travel/return, with actual traces and rendered views.

To inspect without changing active owned assets:

```sh
python3 docs/evidence/tactical-flanker/rehearse.py --prepare-only
```

Open the printed project in Unity **6000.6.4f1**. Omit the flag to run the new tests, use `--demo-only` for four rendered scenarios, or `--full` for complete project suites. The preparation path checks the Flanker preimage, copies the project and writes proposed sources only into that copy. The patch's applicability is checked with `git apply --check`; it has not been applied to active Assets.

Nimsara's review should confirm the restored owner tuning, side commitment/failure defaults, nearby post selection and gameplay feel before accepting the concrete patch. Any future shared movement, geometry or prefab changes require their respective owners; none are part of this proposal.

## Remaining limitations

- Flank utility and the Very Hard label are heuristic tuning. Automated movement/combat regressions do not establish human balance or a full-mission capture-rate improvement.
- Eight private Storage connector coordinates assume the current room layout. Changed furniture/NavMesh may require owner tuning; unavailable routes fail closed rather than force traversal.
- Physics and native route validation are conservative; a route the motor could skirt may be rejected. Dynamic actors can invalidate otherwise useful approaches between decisions.
- A four-second failure cooldown permits eventual retry; spatially different candidates around a genuinely moved target remain eligible.
- Sound search holds a reachable uncertain point during the existing awareness window. It does not infer a hidden player path or supply exact pursuit velocity.
- Candidate firing-line quality is a positional heuristic toward remembered space. Actual firing still requires the shared live visual and fire-time checks, including near-target cover.
- Runtime difficulty rebinding is unchanged; profiles bind through the existing startup contract.
