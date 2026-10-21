> **2026-10-14 implementation update:** See [visual upgrade status](../ENVIRONMENT_VISUAL_POLISH_PLAN.md), [door implementation](../DOOR_DOUBLE_SIDED_IMPLEMENTATION.md) and [fleet implementation](../DRONE_FLEET_VISUAL_IMPLEMENTATION.md). Earlier hinged-door, shortcut, OBJ-only or missing-Blender-source instructions below are historical and superseded. Four automatic sliding gates and the accepted ADR remain canonical. Generated additions are agent-procedural work, not claimed student hand modeling.

> **V3 graphics-first update:** Before implementing your room, read [Graphics-first master plan](../GRAPHICS_FIRST_MASTER_PLAN.md), [level blueprint](../LEVEL_BLUEPRINT.md), [style guide](../VISUAL_STYLE_GUIDE.md) and [per-model briefs](../../ArtSource/Models/README.md). The proposal adds fully enclosed roof/floor/ceiling/lights, AI-specific cover, and three hinged console doors while preserving your official GV/IS assessment role. The baseline still uses the old scene until a later implementation PR.

---

# Nimsara - Student 2: Systems Engineer and Flanker

**Official GV role:** Systems Engineer  
**IS agent:** Flanker  
**Section:** Storage

Your assessed GV responsibility is player interaction physics: movement/collision and reliable environmental interaction, especially the physical shortcut door. Your assessed IS responsibility is the Flanker's side-position decision logic. You also own shared combat/health/cover behavior.

## Work in this order

1. Stabilize first-person movement, mouse look, collision, shooting, and interaction.
2. Arrange Storage with **two usable side approaches** and fixed cover. Coordinate geometry/NavMesh changes with Pamudi.
3. Stabilize shared damage/health rules, player death/restart, and delayed recovery after the player avoids damage.
4. Verify fixed walls/shelves/pillars actually block drone attack raycasts.
5. Finalize the shortcut door's controlled physical motion, collider, Rigidbody behavior, and open/closed state event. Tell Pamudi/Asmadala which route edge changes.
6. In `FlankerBrain.cs`, evaluate side candidates using reachability, lateral/approach value, and route/path cost. Re-select or fall back when a candidate is blocked/exposed/unreachable.
7. Test door open/closed while a drone is already routing, plus no-valid-flank fallback.

Nimthara owns the reusable console/HUD/outcome presentation as a workload balance. Do not spend your GV evidence time replacing the required Systems Engineer physics/interactions with UI work.

## Branch examples

```text
feature/gv-nimsara-player-physics
feature/gv-nimsara-door-physics
feature/gv-nimsara-combat-health
feature/is-nimsara-flanker-scoring
feature/is-nimsara-flanker-fallback
```

## Evidence

- movement/collision and raycast interaction;
- physical door motion + collider consequence;
- cover blocks shots + health recovery timing;
- at least two flank candidates and the score/reason one wins;
- invalid flank fallback;
- regular personal commits.

**Done when:** player interactions feel reliable, cover and recovery work, the door physically and logically changes a route, and Flanker visibly chooses/rechooses a side approach without freezing.
