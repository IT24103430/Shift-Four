> **V3 PRIORITY CHANGE:** Begin with [GRAPHICS_FIRST_MASTER_PLAN.md](GRAPHICS_FIRST_MASTER_PLAN.md), [MODEL_INDEX.md](MODEL_INDEX.md), and [GRAPHICS_IMPLEMENTATION_HANDOFF.md](GRAPHICS_IMPLEMENTATION_HANDOFF.md). Phase priorities are now measured blockout → enclosed rooms/cover/roof/floors → Blender source/export → integrated lighting/art → AI/runtime stabilization. Historical targets below are preserved for reference and should not be mistaken for current migration status.

---

# Development Roadmap - Build in This Order

This roadmap is dependency-first. It tells the team **what to build first, what waits for something else, and when to integrate**. It is intentionally narrower than a normal commercial game plan because the goal is to satisfy the supplied GV and IS rubrics reliably before 21 October.

## Phase 0 - Repository and baseline lock

**Target: 4 October**

### Do first

1. One teammate opens the project in the agreed Unity 6.3 patch.
2. Run **Tools > Shift Four > Generate Greybox Once** exactly once.
3. Open `MainLab.unity`; fix any compile/import problems before feature work.
4. Verify the player can move through the baseline and at least one placeholder drone can run.
5. Commit all generated Unity assets and `.meta` files.
6. Push `main`, create `develop`, then start all new work from `develop`.
7. Apply the branch protection rules in `BRANCHING_STRATEGY.md`.

### Exit condition

A second teammate can clone the repository, open it in Unity, and enter Play mode without regenerating the scene.

---

## Phase 1 - Shared foundation

**Target: 4-7 October**

Build the systems that everyone else depends on. Avoid visual polish here.

### Pamudi

- Confirm the four-room loop and alternate route.
- Own `MainLab.unity` integration.
- Place/finalize waypoint node positions and graph connections.
- Bake/inspect the initial NavMesh after section layout changes.

### Nimsara

- Stabilize first-person movement/collision.
- Stabilize shooting and reusable damage/health behavior.
- Make fixed cover actually block enemy shots.
- Make the physical shortcut door open/close reliably and publish its route state.

### Nimthara

- Draft the security drone and lab door models early in Blender so scale/pivots are known.
- Confirm the reusable console/HUD/game-outcome contract and consume the shared gameplay state exposed by Nimsara.
- Prepare Server cover-point layout.

### Asmadala

- Prove A* on the existing small waypoint graph.
- Return an ordered route or explicit no-route result.
- Make one placeholder drone follow returned points without moving straight through walls/door.

### Integration checkpoint

The team tests one combined build with:

- player movement and shooting;
- four connected rooms;
- open/closed door;
- one A* route and one route recalculation;
- placeholder console and drone state.

Do not proceed to heavy art polish until this works.

---

## Phase 2 - Complete the first assessed slice for every member

**Target: 8-11 October**

Each student must finish one demonstrable GV slice and one demonstrable IS slice.

### Pamudi - World Builder + Scout

1. Refine Checkpoint and shared room kit.
2. Improve cohesive lighting/material direction.
3. Re-bake NavMesh after geometry changes.
4. Implement Scout priorities: visible player > recent sound > last known position/search > patrol.
5. Add timeout/fallback so Scout returns to patrol instead of searching forever.

**Personal demo:** show NavMesh/layout + a hidden-player gunshot investigation.

### Nimsara - Systems Engineer + Flanker

1. Finalize movement/collision and interaction feel.
2. Finalize physical door/collider behavior and route-state event.
3. Finalize damage, cover blocking, death, and delayed recovery.
4. Give Storage two meaningful side approaches.
5. Score Flanker candidate sides by reachability, lateral position/angle, and path cost; add fallback.

**Personal demo:** close the door, take cover/recover, then show Flanker choosing a reachable side route.

### Nimthara - Core Developer + Guard

1. Complete both original models (security drone + lab door).
2. Keep `.blend` sources, topology, UVs, and clean Unity imports.
3. Implement reusable console, progress HUD, exit/win/lose/restart display.
4. Mark valid Server cover points.
5. Score Guard cover by protection, route cost, and firing visibility; add no-safe-cover fallback.

**Personal demo:** show Blender topology/UVs and Guard moving to a defensible cover point.

### Asmadala - Agent Controller + Interceptor

1. Finalize shared A* search and path reconstruction.
2. Improve route following, arrival, turning, braking, and route invalidation.
3. Add readable hover/fire/hit/shutdown animation states.
4. Score reachable Control-room junctions ahead of recent observed player motion.
5. Fall back to pursuit/last-known-position if prediction fails.

**Personal demo:** show ordered A* points becoming smooth movement, then one successful and one failed interception.

### Exit condition

Every student can open the relevant Unity object/code/model and give a 60-90 second personal demonstration without relying on another student to explain it.

---

## Phase 3 - Full four-agent integration

**Target: 12-15 October**

Now combine the assessed pieces.

### Required integration order

1. Import Nimthara's models without changing gameplay roots/components.
2. Connect animation hooks to Asmadala's movement and combat states.
3. Connect Nimsara's door state to Pamudi's graph and Asmadala's A* invalidation.
4. Connect all four agent destinations to the same route interface.
5. Connect Nimthara's console/HUD/outcomes to drone/player state.
6. Run all four agents simultaneously.
7. Re-test every section after NavMesh or model collider changes.

### Required edge cases

- close the shortcut while a drone is travelling;
- player moves behind solid cover;
- Scout loses sight and searches last-known position;
- Flanker has no valid side candidate;
- Guard has no valid cover candidate;
- Interceptor prediction becomes invalid;
- one drone dies while another is routing;
- exit is entered before objectives are complete;
- a console is activated twice;
- player dies and restarts.

### Exit condition

One uninterrupted start-to-win run and one start-to-loss run work with all four agents active and no recurring Console errors.

---

## Phase 4 - Rubric polish and evidence

**Target: 16-18 October**

No major new mechanics.

### GV polish

- consistent low-poly visual language;
- lighting and materials read clearly;
- model scale, topology, UVs, and materials are clean;
- physical interactions/colliders are stable;
- agent movement/rotation/animation are readable;
- basic optimizations can be explained by each owner.

### IS polish

- each agent's observations and candidate decisions are explicit;
- numerical weights/heuristics/cooldowns are documented;
- fallback behavior is intentional, not an accidental `else`;
- A* `g`, `h`, `f`, parent links, route reconstruction, blocked edge, and no-path result are explainable;
- avoid recalculating expensive searches every frame.

### Evidence collection

Each member collects:

- screenshots of their component/code/Unity setup;
- small commit history spanning development;
- one normal case and one edge-case demonstration;
- short viva notes explaining **why** choices were made.

Use `ASSESSMENT_EVIDENCE.md` as the checklist.

---

## Phase 5 - Feature freeze and release candidate

**Target: 19-20 October**

### Freeze rule

Only bug fixes, stability work, missing rubric evidence, and tiny visual corrections are allowed. Do not add new enemy types, weapons, menus, levels, or systems.

### Release sequence

1. Merge the last tested feature PRs to `develop`.
2. Run the full integration checklist.
3. Build for the actual demonstration platform.
4. Test the build, not only Play mode.
5. Rehearse the three-minute video path.
6. Open `develop -> main` PR.
7. Another teammate verifies the exact commit.
8. Merge to `main` with a merge commit.
9. Record from/test the exact `main` commit.
10. Tag the final submission commit after verification.

---

## Phase 6 - 21 October GV submission

Submit the source and three-minute demo video according to the supplied GV brief/platform instructions. The source archive and video should correspond to the same tagged stable commit.

The supplied IS PDF does not specify a separate submission date or format. Confirm CourseWeb/lecturer instructions. If IS continues after GV submission, branch from `develop`; never rewrite the already submitted GV tag.

---

# Dependency summary

```text
Greybox + Git
    |
    +--> Level/NavMesh/Graph (Pamudi) -----------+
    |                                            |
    +--> Player/Combat/Door (Nimsara) -----------+--> Integrated Gameplay
    |                                            |
    +--> Models + Console/HUD (Nimthara) --------+
    |                                            |
    +--> A* + Route Follower (Asmadala) ----------+
                                                   |
        Scout / Flanker / Guard / Interceptor -----+
                                                   |
                               Full regression -> polish -> video/viva -> main/tag
```

If an upstream dependency is broken, fix it before polishing downstream work. This avoids four polished pieces that cannot integrate.
