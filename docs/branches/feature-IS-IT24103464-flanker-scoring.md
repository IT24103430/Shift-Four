# Branch: `feature/IS-IT24103464-flanker-scoring` - Checkpoint 3 / Final

**Student ID:** IT24103464  
**Module:** SE3062 Intelligent Systems  
**Agent:** Flanker  
**PR target:** `dev`

## Final behavior

The Flanker now performs a utility-based tactical decision:

```text
perception
 -> target memory
 -> left/right candidate generation
 -> NavMesh validation
 -> A* route + route-length measurement
 -> tactical utility score
 -> commitment / switch threshold
 -> tactical destination
 -> shared route follower
```

Final flank formula:

```text
score = 42
      + lateralQuality * 30
      + rangeQuality * 12
      + clearFiringLine * 14
      - AStarRouteLength * 1.15
```

## Stability added in this checkpoint

- `minimumCommitSeconds = 1.4`: keep a valid tactical choice briefly.
- `switchMargin = 6`: after the commit window, another option must be clearly better before switching.
- an unreachable committed destination overrides the commitment immediately.

This prevents nearly equal left/right scores from making the drone reverse direction every decision tick.

## Final fallback order

```text
visible + very close -> Engage close target
valid flank exists   -> best utility flank
no useful flank      -> Pursue visible / last-known target
no target route      -> Hold storage
memory expired       -> Hold storage
```

## Diagnostics

- `DecisionTrace` shows selected option plus current left/right/pursuit values.
- optional Debug logging occurs only when the named decision changes.
- Scene-view gizmos show valid left/right candidate positions.

## Final Unity tests

1. One side shorter/clearer -> meaningful side choice.
2. One side invalid -> other side/fallback.
3. Lost sight -> remembered target; memory expiry -> Storage.
4. Similar side scores -> no rapid oscillation.
5. Make other side clearly better -> switch after stability rules permit it.
6. Door/route invalidates committed destination -> re-evaluate without freezing.
7. Run all four agents simultaneously -> no Flanker null references or severe stutter.

Recommended commit: `feat(is): stabilize flanker decisions and add diagnostics`.
