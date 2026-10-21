# IT24103464 - Flanker Three-Checkpoint Implementation Plan

**Branch:** `feature/IS-IT24103464-flanker-scoring`  
**Base / PR target:** `dev`  
**Module:** SE3062 Intelligent Systems  
**Agent:** Flanker  

This plan splits the Flanker work into three meaningful implementation checkpoints. Each ZIP is a **full project snapshot**, not a patch. Apply and test the checkpoints in order in the same Git branch.

> Do not backdate commits or claim tests you did not perform. These snapshots are provided so the implementation can be built and understood incrementally. Commit each checkpoint only after opening that version locally and testing it.

## Exact branch setup

```bash
git switch dev
git pull origin dev
git switch -c feature/IS-IT24103464-flanker-scoring
```

If the branch already exists remotely:

```bash
git switch feature/IS-IT24103464-flanker-scoring
git pull origin feature/IS-IT24103464-flanker-scoring
```

## Snapshot 0 - baseline (no IS implementation commit)

Purpose: establish the branch starting point. The Flanker is still the small starter policy with left/right candidates, simple reachability and direct-distance scoring.

Do **not** create a fake implementation commit for this package. It is the reference point before the three checkpoints.

## Checkpoint 1 - target memory + NavMesh-valid tactical candidates

**ZIP:** `Shift-Four-feature-IS-IT24103464-flanker-scoring-day1.zip`

Implementation:

- limited target memory using `LastSeenAt` / `LastKnown`;
- no hidden live-position tracking after sight is lost;
- left/right flank generation around the observed/remembered target;
- NavMesh snapping before considering a destination;
- graph reachability check;
- numeric lateral-quality and preferred-range scoring;
- close-range engagement and Storage fallback.

Recommended commit after local testing:

```bash
git add Assets/Scripts/AI/Agents/FlankerBrain.cs \
        BRANCH_STATUS.md \
        docs/branches/feature-IS-IT24103464-flanker-scoring.md \
        docs/branches/IT24103464_FLANKER_3_DAY_COMMIT_PLAN.md
git commit -m "feat(is): add remembered navmesh flank candidates"
git push -u origin feature/IS-IT24103464-flanker-scoring
```

Minimum test: see player -> choose a side; hide behind cover -> use remembered position; remain hidden beyond memory -> hold Storage.

## Checkpoint 2 - A* route cost + tactical route scoring

**ZIP:** `Shift-Four-feature-IS-IT24103464-flanker-scoring-day2.zip`

Adds on top of Checkpoint 1:

- direct use of shared `WaypointGraph.FindRoute()`;
- measure actual returned A* route length;
- reject candidate when no route exists;
- firing-line quality to the observed/remembered target point;
- route-cost penalty in utility score;
- route-aware pursuit and home fallbacks;
- choose best valid candidate rather than assuming a flank exists.

Recommended commit after local testing:

```bash
git add Assets/Scripts/AI/Agents/FlankerBrain.cs \
        BRANCH_STATUS.md \
        docs/branches/feature-IS-IT24103464-flanker-scoring.md
git commit -m "feat(is): score A-star flank routes and firing positions"
git push
```

Minimum test: make one side route longer/blocked -> confirm the other side or fallback wins; close the shortcut -> confirm the agent still finds a valid decision.

## Checkpoint 3 - stable decisions + diagnostics + viva-ready behavior

**ZIP:** `Shift-Four-feature-IS-IT24103464-flanker-scoring-day3-final.zip`

Adds on top of Checkpoint 2:

- short minimum commitment window;
- switch-margin / hysteresis so nearly equal scores do not cause left-right jitter;
- immediately abandon a commitment if its destination becomes unreachable;
- decision trace logging;
- Scene-view candidate gizmos;
- final explicit fallbacks and final viva notes/documentation.

Recommended commit after local testing:

```bash
git add Assets/Scripts/AI/Agents/FlankerBrain.cs \
        BRANCH_STATUS.md \
        docs/branches/feature-IS-IT24103464-flanker-scoring.md \
        docs/members/NIMSARA_IS_VIVA_NOTES.md
git commit -m "feat(is): stabilize flanker decisions and add diagnostics"
git push
```

Minimum test: stand where left/right are nearly equal -> no rapid oscillation; move enough to make the other side clearly better -> agent eventually switches; invalidate route -> agent changes immediately.

## Final PR

After all three checkpoints have been genuinely tested:

```text
feature/IS-IT24103464-flanker-scoring
                  |
                  v
                 dev
```

The PR should mention the three implementation checkpoints, the normal and fallback cases tested, and that shared A* itself remains outside Student 2 ownership.

## What belongs to this branch

Main code:

```text
Assets/Scripts/AI/Agents/FlankerBrain.cs
```

Consumed shared systems, not owned here:

```text
Assets/Scripts/AI/Core/DroneBrain.cs
Assets/Scripts/AI/Core/WaypointGraph.cs
Assets/Scripts/AI/Movement/DroneMotor.cs
```

The branch owns **Flanker tactical decision logic**. It consumes the team's perception, A* and route-following interfaces without taking over those shared responsibilities.
