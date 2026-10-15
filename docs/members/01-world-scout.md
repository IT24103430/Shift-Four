> **2026-10-14 implementation update:** See [visual upgrade status](../ENVIRONMENT_VISUAL_POLISH_PLAN.md), [door implementation](../DOOR_DOUBLE_SIDED_IMPLEMENTATION.md) and [fleet implementation](../DRONE_FLEET_VISUAL_IMPLEMENTATION.md). Earlier hinged-door, shortcut, OBJ-only or missing-Blender-source instructions below are historical and superseded. Four automatic sliding gates and the accepted ADR remain canonical. Generated additions are agent-procedural work, not claimed student hand modeling.

> **V3 graphics-first update:** Before implementing your room, read [Graphics-first master plan](../GRAPHICS_FIRST_MASTER_PLAN.md), [level blueprint](../LEVEL_BLUEPRINT.md), [style guide](../VISUAL_STYLE_GUIDE.md) and [per-model briefs](../../ArtSource/Models/README.md). The proposal adds fully enclosed roof/floor/ceiling/lights, AI-specific cover, and three hinged console doors while preserving your official GV/IS assessment role. The baseline still uses the old scene until a later implementation PR.

---

# Pamudi - Student 1: World Builder and Scout

**Official GV role:** World Builder  
**IS agent:** Scout  
**Section:** Checkpoint

Your assessed GV responsibility is the connected level layout, NavMesh, lighting, and texturing. Your assessed IS responsibility is the Scout's distinct decision behavior. You also coordinate `MainLab.unity` integration and waypoint locations/connections.

## Work in this order

1. Open the generated baseline and verify the four connected sections plus the alternate route around the shortcut door.
2. Own the routine edits to `MainLab.unity`. Integrate teammates' section prefabs instead of asking everyone to edit the scene directly.
3. Refine the Checkpoint and shared room kit. Keep the whole facility visually coherent.
4. Place/maintain waypoint nodes and graph connections that correspond to real navigable routes.
5. Re-bake and inspect the NavMesh after meaningful geometry changes. Test every corridor/doorway with player and agent.
6. Improve lighting/materials/textures with a consistent palette and defend at least one optimization/design choice.
7. In `ScoutBrain.cs`, make visible player pursuit outrank recent sound, recent sound outrank patrol, remember last-known position, search for a limited time, then return to patrol.
8. Test hidden-player shots, lost sight, open/closed shortcut, unreachable destination, and return-to-patrol fallback.

## Branch examples

```text
feature/gv-pamudi-level-layout
feature/gv-pamudi-lighting-textures
feature/gv-pamudi-navmesh
feature/is-pamudi-scout-investigation
feature/is-pamudi-scout-lost-target
```

## Evidence

- connected layout + alternate route;
- visible NavMesh/rebake workflow;
- lighting/texturing choice and optimization;
- Scout sound/sight/last-known-position decision;
- numerical priority/score/cooldown explanation;
- lost-target fallback;
- regular personal commits.

**Done when:** all four sections remain reachable, the level looks like one facility, navigation areas are valid, and Scout investigates/searches/recoveries correctly without tracking through walls.
