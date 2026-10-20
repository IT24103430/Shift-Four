# Task 9 — Advanced Defensive Guard: owner review handoff

**Status: integration patch, pending approval.** Nimthara's active GuardBrain, ConsoleSwitch, Server prefab and all other original project assets remain unchanged. This task issued no commit, staging or push commands and did not regenerate MainLab or bake navigation.

## Ownership and approval

`TEAM_RESPONSIBILITIES.md` and `members/03-models-guard.md` assign Guard policy, Server and console gameplay to **Nimthara**. No Task 9 approval was found in the available conversation or repository records. The earlier Scout hearing-adapter approval does not authorize Guard work. The unmerged historical Guard branch mentioned in `SAFE_INTEGRATION_BASELINE.md` is not approval for this proposal and was not imported.

The user's fallback therefore applies: prepare and rehearse an integration patch without editing active owned assets. Nimthara should approve the Guard, console and Server property changes before application. The small shared `DroneBrain` movement hook also needs a named shared-file reviewer; Asmadala should review its movement contract. No external approval messages were sent.

## Review package

[AdvancedGuard.task9.patch](evidence/advanced-guard/AdvancedGuard.task9.patch) changes six paths when applied:

| Path | Proposed change |
| --- | --- |
| `Assets/Scripts/AI/Agents/GuardBrain.cs` | Defensive bounds, cover commitment, shared evidence, validated local investigation and reachable post fallback. |
| `Assets/Scripts/AI/Core/DroneBrain.cs` | Protected virtual movement handoff; default implementation calls the same existing combat approach. |
| `Assets/Scripts/Gameplay/ConsoleSwitch.cs` | Optional sensing-event anchor; zero offset retains current behavior. |
| `Assets/Prefabs/Sections/Server.prefab` | One property on the existing Server console: sensing offset `(0,0,-1.25)` metres along its local axes. |
| New `Assets/Tests/PlayMode/AdvancedGuardPlayModeTests.cs` and `.meta` | Ten MainLab cases, including real console activation, physical cover arrival and rendered evidence. |

No existing tests are weakened or replaced. Scout, Flanker, Interceptor, movement/body implementations, hearing/awareness services, difficulty assets, health/weapon rules, gates, models, materials, MainLab and baked data have no proposed changes. The only prefab delta is the existing Server console's new sensing offset; no prefab hierarchy, collider, mesh or GUID changes.

## Defensive behavior

The original cover score remains:

`40 + (protected ? 22 : 0) - travelDistance * 0.3 - threatDistance * 0.1`

Actual visual confirmation and expiring last-seen evidence supply the threat used by cover scoring. A sound is not promoted to a visible player position. Protection checks ignore the Guard itself and actors; cover candidates must be physically occupiable and have complete native and gate-aware graph routes. Destinations, graph waypoints and native path corners must remain within the protected area. Guard can evaluate an observed threat beyond the room while choosing only in-room defensive positions.

Cover commitment defaults to **1.5 s**, with a **6-point** improvement needed for an ordinary switch after commitment. Every decision re-evaluates current cover against the current observed threat. An invalid destination or lost protection can release commitment immediately. Equal scores and small threat changes do not repeatedly restart the timer. If no safe authored cover exists, Guard returns to its defensive post.

The existing visible close-range pressure priority of **75** remains, now with a valid spaced destination instead of pursuing the player root. The existing cover scoring stays authoritative at longer range. Guard overrides the new movement hook to send its validated destination directly to the existing motor. This prevents the generic combat helper from shifting a defensive point to an unchecked side of a wall, cover object or doorway. Other brains keep the exact previous default movement handoff.

Only credible **gunshot** and **console interaction** observations produce disturbance investigations. They must be fresh, inside the protected area, above the configured strength threshold, and appropriate to awareness state. A strong shot first passes through Suspicious before Investigate. A credible single console activation may warrant a cautious check while Suspicious, since a successful console normally emits only once. This selects investigation speed without claiming combat or visual confirmation.

A sound estimate can overlap furniture. Guard first tries that estimate, then at most eight directions no farther than `min(UncertaintyRadius, 1.75 m)` from it. Every goal receives the same body, path and boundary validation. The selected goal is retained for that observation; polling does not extend evidence time. Sound search stays near the last heard area until shared awareness/evidence expires, then returns to post. Hidden player positions and velocities never drive these choices.

## Confirmed integration defects handled by the proposal

**Blocked post:** the saved Guard home `(-10,0,10)` overlaps two fixed cover colliders. A complete ground NavMesh route alone did not make it occupiable. The patch first tries Home, then a fixed set of nearby directions at radii 1.5, 2.5 and 4 m. It retains the first reachable, body-clear, in-bound post and revalidates it. The demonstrated fallback was approximately `(-8.94,0,11.06)`. No collider or home property was moved. If every candidate is unavailable, Guard holds safely rather than issuing an invalid route. The shared return timeout remains bounded; a fallback beyond the service's authored-home arrival radius may complete the ReturnToPost phase via that timeout while the motor continues toward the valid post.

**Console emission:** the actual Server console center `(-3.5,0.7,16.4)` has no valid navigation attachment. A real successful activation was rejected by shared hearing at projection, despite acoustic transmission of 1. The narrow producer fix supplies an optional anchor on the walkable interaction side. Only the Server prefab assigns a nonzero offset, giving `(-3.5,0.7,15.15)` in the current scene. Offset metres ignore cabinet mesh scale. Category, intensity, source identity, one emission per successful activation, duplicate suppression, progression order and other console defaults remain unchanged. The shared sensor's strict projection, wall attenuation, gate, replay, uncertainty and budget rules are untouched. The real-console test verifies the production activation path, receipt and Guard reaction.

## Configuration

Existing `Resources/DroneDifficulty/Guard` already provides **150 HP**, **2.0 m/s patrol/post**, **2.5 m/s investigation**, **3.0 m/s chase**, acceleration **8 m/s²**, angular speed **120°/s**. The existing motor ramps caps and supplies accepted motion to animation. No new health or movement constants are added to Guard policy.

New Guard tuning is serialized in its script defaults, so existing prefabs need no Guard property edit:

| Setting | Default |
| --- | ---: |
| Protected half extents around Home, X/Z | 8.5 m / 8.5 m |
| Cover commitment | 1.5 s |
| Switch score advantage | 6 |
| Minimum disturbance strength | 0.15 |
| Close defensive spacing | 3 m |

The default protected root area is X `[-18.5,-1.5]`, Z `[1.5,18.5]` for current Server Home. This remains inside both room gates. Configuration normalizes invalid/nonfinite values. Shared default sound memory is 5 s, visual memory 6 s, search duration 5 s; all remain bounded. Attack range **11 m**, damage **10**, and cooldown **1.1 s** are unchanged and tested through actual visibility and fire-time cover checks. “Medium” is the requested tuning target, not a new difficulty enum; human balance acceptance remains Nimthara's review responsibility.

## Verify and inspect

**269/269 Edit Mode and 108/108 focused Play Mode tests passed**, including all ten new Guard scenarios, with zero failures/skips. See [raw evidence and test commands](evidence/advanced-guard/README.md) and [the visual scenario](evidence/advanced-guard/VISUAL_SCENARIO.md). The proposal was tested only in a disposable Unity **6000.6.4f1** copy of the active shared Task 1–7 source; the separate Task 8 Scout proposal was not silently applied.

Reproduce the MainLab scenario without editing the active project:

```sh
python3 docs/evidence/advanced-guard/rehearse.py --demo-only
```

Use `--prepare-only` to open the printed project in Unity and inspect the pending implementation, or omit the flag to run all ten Guard cases. First import may take several minutes. The script prints its own results directory and never applies a patch to the source checkout.

After the named owners review the full delta, `git apply --check docs/evidence/advanced-guard/AdvancedGuard.task9.patch` verifies applicability; application remains an owner integration step. Rebase and rerun if these preimages or shared sensing contracts change. The active checkout's original asset bytes and any concurrent workspace commits are recorded separately in the integrity evidence; concurrent work is not reverted.
