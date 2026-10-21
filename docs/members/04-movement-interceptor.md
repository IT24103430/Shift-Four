> **V3 graphics-first update:** Before implementing your room, read [Graphics-first master plan](../GRAPHICS_FIRST_MASTER_PLAN.md), [level blueprint](../LEVEL_BLUEPRINT.md), [style guide](../VISUAL_STYLE_GUIDE.md) and [per-model briefs](../../ArtSource/Models/README.md). The proposal adds fully enclosed roof/floor/ceiling/lights, AI-specific cover, and three hinged console doors while preserving your official GV/IS assessment role. The baseline still uses the old scene until a later implementation PR.

---

# Asmadala - Student 4: Agent Controller and Interceptor

**Official GV role:** Agent Controller  
**IS agent:** Interceptor  
**Section:** Control

Your assessed GV responsibility is converting calculated paths into smooth agent movement, rotations, and animations. Your assessed IS responsibility is the Interceptor's predictive tactical behavior. You also own the shared A* implementation over Pamudi's waypoint graph.

## Work in this order

1. Prove A* on the small waypoint graph: costs, heuristic, parent reconstruction, ordered route, and explicit no-path result.
2. In `DroneMotor.cs`, follow ordered route points smoothly with correct arrival, turning, braking, and re-route behavior. Do not move directly through geometry toward the player.
3. Respond to Pamudi's graph data and Nimsara's door-state changes. Recalculate on relevant changes rather than every rendered frame.
4. Add visible hover/fire/hit/shutdown/death animation feedback. Coordinate with Nimthara's drone visual hierarchy.
5. Arrange Control-room junctions and the exit handoff without creating dead route ends.
6. In `InterceptorBrain.cs`, estimate direction from **recent observed** player positions, score reachable junctions ahead, and recalculate when the prediction fails. Do not read hidden movement through walls.
7. Test all four agents together, closed door, unreachable destination, reversing/stopped player, route loops, spinning, and stutter.

## Branch examples

```text
feature/shared-asmadala-a-star
feature/gv-asmadala-route-following
feature/gv-asmadala-drone-animation
feature/is-asmadala-interceptor-prediction
feature/is-asmadala-interceptor-fallback
```

## Evidence

Phase 3 implementation and actual test evidence: [Drone animation implementation](../DRONE_ANIMATION_IMPLEMENTATION.md).
Current weapon VFX, physical falling wrecks and rendered MainLab evidence: [Advanced drone animation](../ADVANCED_DRONE_ANIMATION.md).
The existing visual component owns model pose, independent rotors and accepted shot/hit/shutdown feedback;
the motor and DroneBody retain navigation and collision authority. Follow that report's manual checklist
for Scout, Guard, Interceptor and Flanker before visual sign-off. No MainLab or prefab migration is required.

- explain A* `g`, `h`, `f`, parent reconstruction, and no-path behavior;
- show ordered route points becoming smooth movement;
- show movement/rotation/animation states;
- justify path update frequency;
- show successful prediction and failed-prediction fallback;
- regular personal commits.

**Done when:** all four agents route and animate smoothly, A* handles blocked/unreachable destinations, and Interceptor visibly chooses a defensible junction ahead with a safe fallback.
