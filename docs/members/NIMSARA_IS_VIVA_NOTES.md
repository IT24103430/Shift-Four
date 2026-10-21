# IT24103464 - Flanker IS Viva Notes - Final

## 20-second explanation

> My assigned agent is the Flanker. It is a utility-based tactical agent rather than a direct chase enemy. It creates left and right flank destinations around the last legitimately observed player position, validates them on the NavMesh, requests A* routes, measures route cost, and scores each option using flank angle, useful firing range, line-of-fire quality, and route length. It then uses a short commitment window and switch threshold to stop oscillating between almost-equal choices. If a flank becomes unreachable, it falls back to pursuit or its Storage post.

## What makes it more than basic `if/else`

The `if` statements only handle safety/preconditions such as no memory or an emergency close target. The tactical choice itself is made by evaluating multiple valid goals numerically and selecting the highest utility. The destination also changes dynamically with perception and A* reachability.

## Formula to know

```text
flank score = 42
            + lateralQuality * 30
            + rangeQuality * 12
            + clearFiringLine * 14
            - AStarRouteLength * 1.15
```

The exact Inspector weights may be tuned after playtesting. If you change them, update these notes.

## Explain each term

- `lateralQuality`: highest near a 90-degree side approach, because the role is to flank rather than run straight at the player.
- `rangeQuality`: rewards a candidate near the desired attack distance.
- `clearFiringLine`: rewards a position that can attack the remembered/observed target point without cover in the way.
- `routeLength`: penalizes expensive A* paths so the agent does not choose a tactically pretty but impractical destination.

## A* connection

Asmadala owns the shared A* implementation. Your agent consumes `WaypointGraph.FindRoute()`.

For the viva you still need to know:

- `g`: travel cost accumulated from the start.
- `h`: estimate from the current node to the goal (the project uses straight-line distance).
- `f = g + h`: value used to choose the next most promising open node.
- `parent`: used after reaching the goal to reconstruct the ordered route.
- blocked door edge: skipped when the shortcut is closed.
- no path: returns an empty route; your Flanker treats the candidate as invalid and uses another option/fallback.

## Why use a commitment window?

The AI reevaluates frequently. If left scores 61 and right scores 62, then one frame later left becomes 62 and right 61, a naive highest-score agent would keep turning around. A minimum commitment plus `switchMargin` makes the choice stable while still allowing replanning when the current path is invalid or another option becomes clearly better.

## Anti-wall-hack answer

While the player is visible, the shared `DroneBrain` updates `LastKnown`. When line of sight is lost, the Flanker uses `LastKnown` for only `memorySeconds`. It does not use the live hidden position as the tactical target. That makes the behavior explainable and fair.

## Edge cases to demonstrate

1. One flank route unavailable -> choose the other.
2. Both flanks unavailable -> pursue last-known/visible target.
3. Memory expires -> return/hold Storage.
4. Door closes while moving -> shared route version changes; replan/fallback.
5. Similar left/right scores -> no rapid oscillation due to commitment/hysteresis.

## Code locations to memorize

```text
Assets/Scripts/AI/Agents/FlankerBrain.cs
  Decide(...)
  EvaluateFlankCandidate(...)
  TryMeasureRoute(...)
  LateralQuality(...)
  CommitOrKeep(...)

Assets/Scripts/AI/Core/DroneBrain.cs
  CanSeePlayer()
  LastKnown / LastSeenAt
  decision tick

Assets/Scripts/AI/Core/WaypointGraph.cs
  FindRoute(...)

Assets/Scripts/AI/Movement/DroneMotor.cs
  GoTo(...)
```

In the viva, navigate to these functions instead of searching around the project.
