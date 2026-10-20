> **ACTIVE V3 DESIGN OVERRIDE (not yet coded):** Use [GRAPHICS_FIRST_MASTER_PLAN.md](GRAPHICS_FIRST_MASTER_PLAN.md) and [LEVEL_BLUEPRINT.md](LEVEL_BLUEPRINT.md) for the proposed graphics-first linear level, complete roofs/floors, tactical cover, consistent lights/materials and hinged doors. The historical loop/shortcut plan below describes the **still-unmodified baseline**.

---

# Shift Four - Master Project Plan

**Modules:** SE3032 Graphics and Visualization (GV) + SE3062 Intelligent Systems (IS)  
**Project:** Security Lab Escape / Shift Four  
**Engine:** Unity 6.3 LTS + C#  
**Team:** Pamudi, Nimsara, Nimthara, Asmadala  
**GV deadline in the supplied brief:** 21 October 2026

## 1. Purpose of this plan

This is the team's master implementation plan. It keeps the two assignments aligned without splitting the game into two codebases. The project remains **one Unity project, one Git repository, one connected level, and four simultaneously active autonomous agents**.

The two modules assess different parts of the same project:

- **GV:** environment, modeling, physical interaction, movement/animation, visual cohesion, workflow, optimization, and Git evidence.
- **IS:** four distinct autonomous agents, decision architecture, dynamic goals, A* pathfinding/replanning, code quality, continuous effort, originality, and technical viva.

The official GV roles are fixed and must stay visible in commits, pull requests, the report, demo, and viva. Each student's room and IS agent are additional ownership, not replacements for the prescribed GV role.

## 2. Locked ownership

| Student | Name | Official GV role | Lab section | IS agent | Primary assessed output |
| --- | --- | --- | --- | --- | --- |
| 1 | **Pamudi** | **World Builder** | Checkpoint | Scout | Layout, lighting, textures, initial NavMesh; Scout investigation/search |
| 2 | **Nimsara** | **Systems Engineer** | Storage | Flanker | Player physics/interactions and physical door; Flanker side-route decisions |
| 3 | **Nimthara** | **Core Developer** | Server | Guard | Two original UV-mapped models; Guard cover decisions |
| 4 | **Asmadala** | **Agent Controller** | Control | Interceptor | Route-following movement/rotation/animation; Interceptor prediction |

### Additional workload balance

Nimthara also owns the reusable console interaction, HUD progress, exit/win/lose/restart presentation. This is additional gameplay integration work and **does not replace** the required Core Developer evidence of two original models with topology and UV mapping.

Shared AI navigation is split deliberately:

- Pamudi defines waypoint locations/connections and keeps them consistent with the level.
- Asmadala owns the shared A* implementation and route-following interface.
- Nimsara publishes the physical door's open/closed route state.
- Nimthara supplies Guard cover destinations and model visuals.
- Every member must understand the shared A* flow well enough to explain it in the IS viva.

## 3. Branching model

Use the full rules in [BRANCHING_STRATEGY.md](BRANCHING_STRATEGY.md).

```text
feature/gv-* or feature/is-* or fix/*
                |
                v
             develop       <- team integration / everyday playable branch
                |
        full regression pass
                |
                v
              main         <- stable demo/submission branch only
                |
              tag
```

**Do not create separate long-lived GV and IS repositories or branches.** The assignments are a joint project and both modules need the same integrated Unity source. Module ownership is expressed through short branch names and PR labels/descriptions instead.

## 4. Build order - what must happen first

The fastest safe order is:

1. **Baseline and Git first** - one teammate generates the greybox once, verifies Play mode, commits generated Unity assets, creates `develop`, and protects `main`.
2. **Shared contracts second** - confirm player/world events, damage interface, waypoint format, door-state event, A* API, route follower API, and section prefab ownership.
3. **Foundation gameplay/navigation third** - player movement/combat/health/door, connected rooms/NavMesh/waypoint graph, A* route, and a placeholder drone following a route.
4. **Individual assessed work fourth** - each member completes their official GV responsibility and their own distinct IS agent logic on separate short branches.
5. **Integration fifth** - all four agents operate simultaneously, door replanning works, console/HUD/outcome flow works, models and animations are integrated.
6. **Polish and edge cases sixth** - no route loops, clipping, broken cover, duplicate console count, null references, severe frame drops, or inconsistent visuals.
7. **Evidence and submission last** - stable desktop build, three-minute GV video, screenshots/evidence, viva practice, final `main` promotion and release tag.

Do not polish a room before the baseline loop, shared navigation, and required interactions work. Do not add optional features while a rubric item is incomplete.

## 5. Development windows

The detailed task order is in [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md). The high-level plan from 4 October is:

| Window | Goal | Exit condition |
| --- | --- | --- |
| **4-7 Oct** | Establish baseline, `develop`, shared contracts, player/door base, graph/A* route proof, first model draft | One clean clone opens; player can traverse; one drone follows an A* route; door state can affect routing |
| **8-11 Oct** | Complete first pass of all four GV role components and all four agent policies | Every student can demonstrate a personal component in isolation |
| **12-15 Oct** | Integrate four simultaneous agents, cover/flank/intercept behavior, models, HUD, animations | Full start-to-win and start-to-loss paths work with all four agents |
| **16-18 Oct** | Edge cases, optimization, visual consistency, personal evidence, viva preparation | No recurring Console errors or high-risk functional defects |
| **19-20 Oct** | Feature freeze, desktop build, final regression, record/rehearse three-minute demo | `develop` passes the release checklist and is promoted to `main` |
| **21 Oct** | GV submission/showcase using tagged stable source | Source and video match the same tagged commit |

The supplied IS brief gives a five-week duration but does not provide a separate submission date in the document. Confirm CourseWeb/lecturer instructions before assuming the GV deadline also applies to IS.

## 6. Integration contract

Runtime flow stays:

```text
observe -> choose tactical goal -> request A* route -> follow route points -> act -> re-evaluate
```

Rules:

- Agents cannot know the player's live position through walls. Use vision/raycast observations, sound events, and last-known position.
- Each agent owns its **decision policy**. Shared code may move it or calculate a route, but must not erase the student's distinct decision logic.
- A* returns an ordered route or a clear no-path result. Agent logic supplies a fallback if its preferred destination is unavailable.
- The closed door disables its graph shortcut; at least one alternate route remains.
- Route calculation is event-driven or periodic, not every rendered frame.
- Fixed shelves/walls/pillars block shooting line of sight.
- All four drones are active at the same time in the final integrated game.

## 7. Definition of done

A task is not done because the code compiles. A branch is ready for PR only when:

- the changed behavior works in Unity Play mode;
- there are no new red Console errors;
- relevant open/closed, reachable/unreachable, alive/dead, or fallback cases are tested;
- the change stays inside the assigned ownership or is coordinated with the shared-file owner;
- commits are small, attributable, and descriptive;
- the PR states whether it is **GV**, **IS**, **shared**, or **fix/docs** work and names the assessment evidence it creates.

A release candidate is done only when the full [INTEGRATION.md](INTEGRATION.md) playthrough passes.

## 8. Scope guard

Keep the required game small and demonstrable. Until every rubric requirement is complete, do **not** add VR, multiplayer, multiple levels, procedural generation, inventory, weapon switching, ammo/reload, movable barricades, online services, elaborate menus, or extra waves.

Explicitly authorized combat polish extends this boundary with [finite pistol ammo and reloading](WEAPON_AMMO_RELOAD.md), owned by the combat/player systems component. This addition keeps the existing level, damage rules and mission objectives.

## 9. Document map

Read these in order:

1. **This file** - overall project and ownership.
2. [DEVELOPMENT_ROADMAP.md](DEVELOPMENT_ROADMAP.md) - exact development phases and dependency order.
3. [BRANCHING_STRATEGY.md](BRANCHING_STRATEGY.md) - `main`/`develop`/feature workflow and branch names.
4. [TEAM_RESPONSIBILITIES.md](TEAM_RESPONSIBILITIES.md) - what each member owns, files to touch, handoffs, and done criteria.
5. [GIT_WORKFLOW.md](GIT_WORKFLOW.md) - day-to-day Git commands and Unity conflict handling.
6. [ARCHITECTURE.md](ARCHITECTURE.md) - runtime code flow.
7. [INTEGRATION.md](INTEGRATION.md) - PR and final playthrough testing.
8. [ASSESSMENT_EVIDENCE.md](ASSESSMENT_EVIDENCE.md) - what to preserve for marks, demo, and viva.
9. [SCOPE.md](SCOPE.md) - locked mechanics and exclusions.
10. `docs/members/` - personal step-by-step guides.

**Rule when documents appear to conflict:** official assignment instructions come first; then this master plan; then member guides. Update the plan through a reviewed documentation PR when the team agrees on a change.
