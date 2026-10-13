> **Current contract (2026-10-14):** four automatic horizontal sliding gates, each requiring its own drone AND console. Historical hinged/shortcut and console-only instructions below are superseded. See `docs/decisions/ADR-FOUR-GATE-CONTRACT.md` and `docs/members/03_NIMTHARA_GV_IMPLEMENTATION.md`.

> **V3 graphics-first update:** Before implementing your room, read [Graphics-first master plan](../GRAPHICS_FIRST_MASTER_PLAN.md), [level blueprint](../LEVEL_BLUEPRINT.md), [style guide](../VISUAL_STYLE_GUIDE.md) and [per-model briefs](../../ArtSource/Models/README.md). The proposal adds fully enclosed roof/floor/ceiling/lights, AI-specific cover, and three hinged console doors while preserving your official GV/IS assessment role. The baseline still uses the old scene until a later implementation PR.

---

# Nimthara - Student 3: Core Developer and Guard

**Official GV role:** Core Developer  
**IS agent:** Guard  
**Section:** Server

Your assessed GV responsibility is at least two original 3D models created from scratch with topology/UV evidence and correct Unity import. Use a **security drone** and **lab door**. Your assessed IS responsibility is the Guard's cover-selection behavior. As workload balance, you also own reusable consoles, HUD progress, and win/lose/restart presentation.

## Work in this order

1. Draft the security drone and lab door early in Blender. Keep geometry simple enough for a small real-time game.
2. Apply transforms, create sensible topology, unwrap UVs, and use a consistent material palette. Keep screenshots and editable `.blend` sources in `ArtSource/`.
3. Import/export through `Assets/Models/`, then verify orientation, scale, materials, shading, and pivots in Unity.
4. Replace **visual children** only; preserve gameplay roots/components. Coordinate the door visual with Nimsara and drone hierarchy/animation with Asmadala.
5. Implement/own reusable console interaction, four unique console instances/progress, HUD counters, exit gating, win/lose/restart presentation. Prevent duplicate console counting.
6. Arrange Server racks/pillars and reachable cover points.
7. In `GuardBrain.cs`, score cover by protection, route cost, and firing visibility; relocate when exposed; add no-safe-cover fallback.
8. Run with all four drones and confirm model complexity/materials do not cause visible performance or collider/raycast problems.

## Branch examples

```text
feature/gv-nimthara-drone-model
feature/gv-nimthara-door-model
feature/gv-nimthara-uv-import
feature/shared-nimthara-console-hud
feature/is-nimthara-guard-cover
```

## Evidence

- both editable Blender sources;
- topology + UV mapping screenshots;
- correct Unity import and model-in-game proof;
- polygon/texture/optimization explanation;
- reusable console/HUD/outcome proof;
- Guard candidate cover scoring and relocation/fallback;
- regular personal commits.

**Done when:** both original models are defensible and integrated correctly, consoles/outcomes work without duplicate state, and Guard chooses/changes cover sensibly with an intentional fallback.
