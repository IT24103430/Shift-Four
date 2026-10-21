> **V3 PROPOSED LEVEL DESIGN (not yet implemented):** Replaces only the *level layout/art/door design details* below: four sequential rectangular enclosed rooms with solid roofs and floors, three console-unlocked permanent hinged doors, and cover supporting the respective AI. See [GRAPHICS_FIRST_MASTER_PLAN.md](GRAPHICS_FIRST_MASTER_PLAN.md). The assignment's four-agent, four-console, original-model, GV/IS role and final evidence obligations remain. The old loop/shortcut description below documents the *current baseline*, not the proposed future scene.

---

# Shift Four — locked scope

**Joint assignment:** SE3062 Intelligent Systems (IS) and SE3032 Graphics and Visualization (GV)  
**Team:** Exactly four students  
**Project type:** Single-player, first-person, interactive 3D shooting game  
**Status:** Initial greybox source is included. Generate it once in Unity, commit the generated assets, then follow the locked ownership and development plan below.  
**GV submission deadline in the brief:** 21 October 2026

## 1. Project summary

The player enters a compact security lab made of four connected sections: a checkpoint, a storage lab, a server room, and a control room. Each section has one control console and a distinctive security drone. The player explores the lab, shoots drones, activates the four consoles, and reaches the exit. Drone attacks reduce health. Fixed walls, shelves, and pillars block shots, allowing the player to take cover until health begins to recover.

The four agents run in the same level at the same time. Each has a distinct decision policy and uses a shared, explainable A* pathfinding service. Each student owns **one agent** for IS, **one prescribed GV role**, and the arrangement of one lab section. Section ownership does not replace the prescribed GV role.

**Design rule:** Deliver a small, complete, stable game whose AI and visual decisions every team member can defend in the viva. Features outside the scope below require an explicit team decision and must not delay required work.

## 2. Locked scope

| Included | Acceptance condition |
| --- | --- |
| One playable 3D level | Four small sections are physically connected; the player can reach all of them and the exit. |
| First-person player | Keyboard movement, mouse look, collision with the environment, and a working shooting action. |
| Four autonomous drones | All four exist and update in the same running level; each behaves differently and can be shot. |
| Player and drone health | Hits apply damage, zero health has a clear result, and the player can restart after death. |
| Fixed cover and recovery | Solid environment geometry blocks enemy fire; recovery begins after a short period without taking damage. No crouch mechanic is required. |
| Four control consoles | One reusable interaction component is placed in each section. The UI shows which consoles are active. |
| One interactive door | The player can open it; it has a collider and a physical or controlled-hinge response. Its state can change an AI route. |
| Navigation and replanning | A small waypoint graph spans the level. Shared A* returns a route; agents choose goals and recalculate when a route or target changes. |
| Two original 3D models | A security drone and a lab door are created from scratch in Blender or Maya, with sensible topology and UV mapping, then imported. |
| Cohesive graphics and animation | Reusable materials, lighting, drone turning and hover/fire/hit/death feedback; no unrelated asset styles. |
| Final delivery | Complete source project and a three-minute demo video, plus a working live showcase and individual viva preparation. |

**Out of scope:** VR, multiplayer, online services, procedural levels, a campaign, inventory, weapon switching, ammo/reload systems, movable barricades, multiple enemy waves, save games, and elaborate menus. Basic win/lose/restart screens and a small HUD are sufficient.

### Exact play loop

1. Start at the checkpoint with 100 health.
2. Explore the four sections, survive the drones, and activate each section's console with the same interaction key.
3. Defeat the four drones. Their different tactics should make the player use routes and cover.
4. Once all four consoles are active and all four drones are disabled, enter the exit area to win.
5. If health reaches zero, show a loss state with a restart action.

Initial balancing values: a drone hit removes 10 health; after 3 seconds without damage, health regenerates at 8 per second up to 100. These are tuning values, not extra mechanics. Give drones a readable attack interval and visual firing cue. A simple raycast weapon for the player and a line-of-sight-checked drone shot are enough.

## 3. Level and agent plan

Use **one Unity scene**. Arrange the sections around a small loop so the player and agents have more than one route between important points. Place the interactive door on one shortcut; closing it blocks that shortcut, while another route remains available. Use tall, fixed cover objects whose colliders actually interrupt a shot raycast. Build rooms from a shared wall/floor/light/material kit so four contributors still produce one coherent lab.

| Section | Level purpose | Agent behaviour and individual IS contribution |
| --- | --- | --- |
| **A. Security checkpoint** | Starting area with a corridor, sight lines, and a console. | **Scout:** patrols; detects a nearby shot or the player; investigates the event or last known position; returns to patrol when the search fails. Owner implements event memory, action scoring, and search fallback. |
| **B. Storage lab** | Shelves create two practical approach routes and fixed cover. | **Flanker:** evaluates side positions based on angle to the player, reachability, and route cost; selects a viable side route, then changes route if blocked. Owner implements position evaluation and replanning rules. |
| **C. Server room** | Racks and pillars offer clear, reliable cover. | **Guard:** evaluates cover points for protection, distance, and firing visibility; attacks from cover and relocates when exposed or the player moves. Owner implements cover evaluation and invalid-cover fallback. |
| **D. Control room** | Multiple entrances and the final exit make route choice visible. | **Interceptor:** estimates the player's travel direction from recent positions; chooses a reachable junction ahead of that direction, then adjusts when the prediction is wrong. Owner implements prediction, junction scoring, and fallback to direct pursuit. |

Agents start in their assigned sections but may pursue across section boundaries and later return to their posts. They must remain active in the shared simulation; sections are not separate levels or mutually exclusive encounters. Reuse the same drone model with restrained colour accents and labels for identification.

## 4. Team ownership

Names and roles are locked below. Each student owns their section prefab and agent script. Shared interfaces and the main scene have named integrators.

| Student | Name | GV responsibility from brief | IS and section responsibility | Concrete evidence for viva |
| --- | --- | --- | --- | --- |
| **1 - World Builder** | **Pamudi** | Overall level layout, NavMesh baking, lighting, and texturing; visual integration of all four sections. | Checkpoint prefab and Scout policy. | Walkable layout, alternate routes, baked NavMesh, lighting decisions, Scout state/score explanations, individual commits. |
| **2 - Systems Engineer** | **Nimsara** | Player movement/collision, shooting/interaction physics, physical door, player health/recovery, and shared combat/damage behavior. | Storage prefab and Flanker policy. | Door/player physics demo, damage/cover/recovery demo, Flanker goal-selection logic, individual commits. |
| **3 - Core Developer** | **Nimthara** | Create/import at least two original models: drone and door, including topology and UVs; maintain source model files. Additional integration: consoles, HUD, outcomes. | Server prefab and Guard policy. | Blender source/imports, topology/UV screenshots, Guard cover evaluation, console/HUD integration, individual commits. |
| **4 - Agent Controller** | **Asmadala** | Transform calculated routes into smooth movement, turning, and animations for all drone types. Own shared A* implementation. | Control-room prefab and Interceptor policy. | A*/route-following/animation demo, movement and rotation code, Interceptor prediction, individual commits. |

**Shared work:** Agree on the waypoint format, agent interface, event types, damage interface, and pathfinding service before writing four separate agents. Pamudi defines waypoint locations/connections; Asmadala owns the shared A* implementation and route follower; Nimsara publishes door state; Nimthara provides Guard cover points and shared visual/gameplay presentation assets. All four students must still understand A*. Pamudi integrates the final scene; other members contribute their room prefabs and scripts. Shared code ownership does not remove anyone's individual AI responsibility.

## 5. Technical approach

**Tools:** Unity 3D with C# for the game, Blender for original models, and Git/GitHub for collaboration. Lock one Unity editor version and the same required packages across the group before the first scene is built. The Unity project itself is the runnable application; a standalone build is useful for the demo, while the source project and video are the explicitly stated GV submissions.

### AI pipeline

```text
Game events and perception
        -> individual agent decision policy
        -> selected tactical destination
        -> shared A* waypoint path
        -> shared route follower and animation
        -> movement / attack / re-evaluation
```

- **Perception:** Check sight using distance, field of view, and raycast obstruction. Dispatch a simple shot/sound event for the Scout. Store a last known player position rather than magically tracking the player through walls.
- **Decision:** Each agent scores meaningful candidate actions or destinations. Define the inputs, weights, tie-breaker, cooldown, and fallback in its own code and explain them in the viva. A single disguised `if/else` chain is not the intended AI architecture.
- **Search:** Build a modest graph of walkable waypoints. A* uses `f(n) = g(n) + h(n)`, with travel distance for `g` and straight-line distance for `h`. Record parents to reconstruct the route. A closed door marks its shortcut edge unavailable; reopening makes it available. Do not rely on a one-way, always-open corridor.
- **Movement:** Student 4 consumes the returned ordered points, moves toward the current point, rotates toward motion/target, and triggers simple animation states. Unity navigation can supply walkable-space and local movement support, but the team's own A* and goal decisions must remain visible and explainable for IS.
- **Replanning:** Recalculate when the destination becomes invalid, the door changes, the player moves enough to change the goal, or the current route fails. Avoid running full searches every rendered frame.
- **Fallbacks:** Handle no route, lost player, invalid/depleted cover, dead agent, closed door, and an unreachable predicted junction without freezing or null-reference errors.

Keep logic independent of the visual model: an agent policy outputs a goal/action; navigation returns a route; the movement/animation component presents it. That separation also makes each student's code easier to find during the viva.

### Graphics and performance

- Use one consistent low-poly lab style, a small shared material palette, and lighting that makes the four sections distinct while still belonging to one facility.
- Model the drone and door from scratch, keep reasonable polygon counts, unwrap/UV-map them, import them, and retain the editable Blender source.
- Use simple colliders for fixed cover and the door, check that cover blocks actual enemy line of sight, and avoid unnecessarily expensive physics or lighting effects.
- Four agents must run together smoothly. Reuse prefabs/materials and recalculate decisions/paths on events or modest intervals rather than every frame.

## 6. Repository and Git workflow

Suggested project layout (adapt exact folders to the Unity version selected):

```text
README.md
Assets/
  Scenes/MainLab.unity
  Prefabs/Sections/             # One prefab per section
  Prefabs/Agents/               # Shared drone prefab and variants
  Models/                       # Imported finished models
  Materials/
  Animations/
  Scripts/
    Player/
    Gameplay/                   # Player, door, damage, consoles, win/lose
    AI/Core/                    # Graph, A*, perception interfaces
    AI/Agents/                  # Scout, Flanker, Guard, Interceptor
    AI/Movement/                # Shared path following and animation
ArtSource/                       # Original editable Blender files
Packages/
ProjectSettings/
.gitignore
```

**Git rules:**

1. Use one shared GitHub repository. Commit `Assets`, `Packages`, `ProjectSettings`, and every Unity `.meta` file alongside its asset. Ignore generated folders such as `Library`, `Temp`, `Logs`, and build output. Use visible meta files and text asset serialization for reviewable changes.
2. Keep `main` as the stable demo/submission branch and `develop` as the everyday integration branch. Each member works on a short-lived branch such as `feature/gv-nimsara-door-physics` or `feature/is-nimsara-flanker-scoring`, then opens a pull request to `develop`.
3. Merge assessed feature work with **Create a merge commit**, not squash, so the individual commit trail remains visible. Promote `develop` to `main` only after the full integration checklist passes.
4. Pamudi is the integrator for `MainLab.unity`. Other students primarily edit their **own section prefab**, agent scripts, and assigned assets; coordinate before touching the main scene or shared prefabs to reduce scene merge conflicts.
5. Before merging: open the project, check the Console for errors, run the changed behavior, and test an edge case. After merging: run the relevant shared integration checks.
6. Keep generated builds and caches out of Git. If model/texture files become unusually large, agree on Git LFS before adding them; everyone must install and use the same setup. See `BRANCHING_STRATEGY.md` for the full rules.

Example commits: `feat(ai): make scout investigate gunfire`, `feat(level): connect storage and server routes`, `feat(player): add health recovery after cover`, `fix(ai): recover from blocked door path`.

## 7. Milestones and order of work

The GV brief lists a **Week 11 concept pitch**, **Week 13 logic/asset prototype**, and **Week 15 showcase and viva**. It also states a **21 October 2026** submission deadline. The IS brief says five weeks but does not specify its own separate deadline or submission format; check module announcements for those details. Do not assume the brief's week labels map directly to dates without the course timetable.

| Stage | Required outcome | Gate before moving on |
| --- | --- | --- |
| **Concept and setup** | Agree on this scope, assign names/roles, select one Unity version, create the repository and greybox layout. | Everyone can open the same Unity project; four section prefabs and the pitch exist. |
| **First playable prototype** | Player moves/shoots; four placeholder drones are present; damage, fixed cover, door, and reusable consoles function. | One complete start-to-win or start-to-loss run is possible with placeholder visuals. |
| **IS prototype** | Shared graph/A* works; each person's decision policy can reach goals and handle changing conditions. | All four agents operate at once; door change and lost/blocked path do not freeze them. |
| **GV integration** | Two original models, UVs, lighting, textures, animations, smooth movement, final section layouts. | Coherent visuals, working collisions, interactions, and meaningful personal Git history. |
| **Final verification** | Test full playthrough, edge cases, performance, demo recording, and individual viva explanations. | Clean launch on the demo machine; source and three-minute video are ready. |

Prioritize a stable first playable build before polishing. If time gets tight, improve broken required behaviours and viva readiness before adding visual extras.

## 8. Acceptance checklist

### Group gameplay and stability

- [ ] The player can navigate the four connected sections and identify each console.
- [ ] Shooting, enemy hits, health recovery behind fixed cover, death/restart, console progress, and win state work.
- [ ] The interactive door works with collision and changes an available route without trapping agents.
- [ ] All four agents are alive/active together at the start and demonstrate their different behaviours.
- [ ] No recurring Console errors, agent freezes, route loops, or severe frame drops during a complete playthrough.

### Individual IS evidence (each student)

- [ ] Owns a distinct agent implementation, not just a differently coloured copy.
- [ ] Can show its observations, candidate actions/goals, scoring or decision structure, A* route, and replanning trigger.
- [ ] Can show and explain an edge case and why the chosen policy suits this gameplay.
- [ ] Has regular, attributable commits and can navigate directly to their agent's logic during the viva.

### Individual GV evidence (each student)

- [ ] Student 1: cohesive layout, lighting, textures, and initial NavMesh.
- [ ] Student 2: movement/collision, physics-based environmental interaction, and reliable door/player controls.
- [ ] Student 3: two original Blender/Maya models with editable source, topology, UVs, and correct import.
- [ ] Student 4: path-following movement, rotations, and visible animations for agents.
- [ ] Each student can explain their tools, workflow, design choices, and at least one optimization relevant to their role.

## 9. Demo and viva plan

**Three-minute video:** Briefly establish the lab and controls; show the player moving and using a door/console; show cover stopping damage and health recovery; demonstrate each of the four named agent behaviours; show a route change/replan; end with the win condition. Use a stable build and readable agent identifiers. The video is evidence of a real running game, not a substitute for the source or the live demonstration.

**Individual viva preparation:** Each member should be able to open their own code immediately, explain a concrete decision with actual values, trace a route through A*, show what changes after a door/player event, and justify their Graphics choices and optimization. Keep a short set of personal notes, but answer from the implementation.

## 10. Scope change rule

A proposed feature enters scope only if it directly strengthens an existing rubric requirement, has a named owner, can be finished and tested before the deadline, and does not destabilize the first playable build. Record any agreed change in this README. The default response to optional features is to finish and verify the locked scope first.

## Reference briefs and tool documentation

- `Assignmnet_IS.pdf` — SE3062 IS assignment specification (held by the team; not included in this ZIP).
- `Assignmnet_GV.pdf` — SE3032 GV assignment specification (held by the team; not included in this ZIP).
- [Unity: AI Navigation](https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-safe/com-unity-ai-navigation)
- [Unity: asset metadata and `.meta` files](https://docs.unity.com/en-us/engine/6000.6/manual/assets-and-media/import-assets/asset-metadata)
- [Unity: version control settings](https://docs.unity.com/en-us/engine/6000.0/manual/get-started/project-configuration/version-control/versioncontrolintegration)
- [Blender: UV tools](https://docs.blender.org/manual/en/5.0/modeling/meshes/editing/uv.html)
