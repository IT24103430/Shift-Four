# Nimsara - Locked Implementation Plan

**Project:** Shift Four / Security Lab Escape
**Modules:** SE3032 Graphics & Visualization + SE3062 Intelligent Systems
**Member:** Nimsara - Student 2
**Official GV role:** Systems Engineer
**Section:** Storage Lab
**IS agent:** Flanker
**Plan status:** LOCKED FOR IMPLEMENTATION
**Plan date:** 4 October 2026

---

# 1. What you personally own

Your work is divided into two assessed areas plus one section responsibility.

## GV - Systems Engineer

You own the systems that make the human player physically interact with the game world:

- first-person movement;
- collision;
- mouse look;
- raycast shooting/interactions;
- physical interactive shortcut door;
- reliable door collider / Rigidbody behavior;
- shared player/drone damage behavior;
- player health;
- delayed health recovery;
- fixed-cover shot blocking;
- player death state/restart trigger;
- publishing the door's open/closed state so navigation can react.

The most important GV evidence is **player physics + environmental interaction**, especially the door. Do not replace this with UI work.

## IS - Flanker agent

You own one distinct autonomous enemy: **Flanker**.

The Flanker must not simply chase the player. It must:

1. observe/remember the player through the shared perception system;
2. create useful left/right flank destinations;
3. check whether a candidate is reachable;
4. use A* route information/path cost;
5. numerically score the candidates;
6. choose the best valid option;
7. move using the shared route follower;
8. re-evaluate when the player moves or the route becomes invalid;
9. use a clear fallback when no flank is possible.

This will be implemented as a small **utility-scoring decision system**, which is easy to explain in the viva and is clearly more than a basic chase `if/else`.

## Storage section

You own the Storage section prefab/gameplay setup:

- shelving / fixed cover;
- at least two meaningful approaches through the room;
- the shortcut doorway/door area;
- enough space for Flanker side-route behavior;
- geometry that really blocks enemy fire.

Pamudi remains the main-scene integrator. Work primarily in your Storage prefab and scripts, then hand the section to Pamudi for final `MainLab.unity` integration.

---

# 2. Current baseline - do not throw it away

The supplied repository already contains starter implementations:

```text
Assets/Scripts/Gameplay/PlayerController.cs
Assets/Scripts/Gameplay/Health.cs
Assets/Scripts/Gameplay/LabDoor.cs
Assets/Scripts/Gameplay/IDamageable.cs
Assets/Scripts/Gameplay/IInteractable.cs
Assets/Scripts/Gameplay/SoundEvents.cs
Assets/Scripts/AI/Agents/FlankerBrain.cs
Assets/Scripts/AI/Core/DroneBrain.cs
Assets/Scripts/AI/Core/WaypointGraph.cs
Assets/Scripts/AI/Movement/DroneMotor.cs
```

The baseline already has basic player movement, mouse look, shooting, interaction raycasts, health/recovery, a sliding physical door, Flanker left/right candidate logic, A* calls, drone line-of-sight attacks, and route recalculation.

**Therefore your job is not to delete everything and start again.** Your work is to understand it, test it, improve it, separate your responsibility cleanly, add missing edge cases, and produce real Git evidence as you dev it.

---

# 3. Git model - locked

```text
main
  ^
  |  release PR only
  |
dev
  ^
  |  normal PRs
  |
feature/gv-IT24103464-*
feature/IS-IT24103464-*
fix/IT24103464-*
```

## `main`

Stable/demo/submission branch. Do not dev directly on it.

## `dev`

Shared team integration branch. Every normal task starts from the latest `dev` and returns through a PR.

## Your first branch

**Start with:**

```text
feature/gv-IT24103464-player-physics
```

This is your first implementation branch because player movement/collision/interactions are the foundation for your Systems Engineer work and the rest of your testing.

---

# 4. Before writing code - exact Git commands

If `dev` already exists remotely:

```bash
git switch dev
git pull origin dev
git switch -c feature/gv-IT24103464-player-physics
```

If the team has not created `dev` yet, the repository owner/integrator should create it once from the verified baseline:

```bash
git switch main
git pull origin main
git switch -c dev
git push -u origin dev
```

Then you do:

```bash
git switch dev
git pull origin dev
git switch -c feature/gv-IT24103464-player-physics
```

Never begin by coding directly on `main`.

---

# 5. Your complete branch sequence

Use this sequence. Do not create all branches at once. Finish, test, PR, and merge one task before creating the next from the updated `dev`.

| Order | Branch | Main purpose |
|---|---|---|
| 1 | `feature/gv-IT24103464-player-physics` | Player movement, collision, mouse look, interaction foundation |
| 2 | `feature/gv-IT24103464-combat-health` | Shooting, damage, cover blocking, health, recovery, death state |
| 3 | `feature/gv-IT24103464-door-physics` | Physical shortcut door + collider + navigation-state event |
| 4 | `feature/gv-IT24103464-storage-section` | Storage routes, shelves, cover, door area |
| 5 | `feature/IS-IT24103464-flanker-scoring` | Proper utility-scored flank choices using reachability/path cost |
| 6 | `feature/IS-IT24103464-flanker-fallback` | Replanning, cooldown/hysteresis, invalid/no-route fallbacks |
| 7 | `fix/IT24103464-integration-polish` | Only genuine integration defects found after team merge |

Do not make one giant `feature/everything` branch.

---

# 6. Phase 0 - local setup and baseline verification

**Do this before Branch 1.**

## Goal

Prove that your local project is healthy before you claim any feature work.

## Steps

1. Install/open the exact Unity version agreed by the team.
2. Clone/extract the project.
3. Open the folder containing:

```text
Assets/
Packages/
ProjectSettings/
```

4. Wait for Unity to finish importing/compiling.
5. Open `MainLab.unity` after the baseline has been generated by the designated integrator.
6. Press Play.
7. Test:
   - WASD movement;
   - mouse look;
   - collision against walls;
   - left-click shooting;
   - `E` interaction;
   - door open/close;
   - taking damage;
   - hiding behind fixed cover;
   - health recovery;
   - loss/restart.
8. Check Unity Console for red errors.
9. Write down every existing defect before you change code.

## Do not

- rerun the greybox generator if the team has already committed generated assets;
- modify the main scene casually;
- claim baseline behavior as your new work;
- fix unrelated teammates' systems without coordinating first.

---

# 7. Branch 1 - player physics

## Branch

```text
feature/gv-IT24103464-player-physics
```

## Main files

```text
Assets/Scripts/Gameplay/PlayerController.cs
Assets/Scripts/Gameplay/IInteractable.cs
```

## Goal

A reliable first-person controller that demonstrates your Systems Engineer role.

## Required behavior

- player moves with WASD;
- diagonal movement is normalized;
- gravity keeps the player grounded correctly;
- collision uses `CharacterController` cleanly;
- player cannot walk through walls/shelves/door;
- mouse look is stable;
- vertical camera pitch is clamped;
- cursor locks during play and Escape releases it;
- interaction uses a forward raycast;
- `E` only interacts with a valid nearby `IInteractable`;
- interaction range is sensible;
- no movement/input continues after player death/end state.

## Current starter behavior to inspect

`PlayerController.cs` already contains CharacterController movement, mouse look, gravity, interaction raycast, and shooting. Understand every part before changing it.

## Tests

1. Walk forward/back/left/right.
2. Walk diagonally and confirm it is not faster.
3. Push into a wall for 10 seconds; do not clip through.
4. Walk into shelving/corners.
5. Look straight up/down; camera should stop near the pitch limits.
6. Stand just outside interaction range and press E; nothing happens.
7. Stand within range and press E; target responds once.
8. Die/end session; player input should stop.

## Suggested commits

Do not make commits merely to increase the count. Commit when real working checkpoints exist.

```text
feat(gv): verify and refine player collision movement
feat(gv): stabilize mouse look and cursor handling
fix(gv): prevent interaction outside valid range
```

## Done when

You can demonstrate player movement/collision/interactions for 1 minute without a red Console error or obvious clipping problem.

---

# 8. Branch 2 - combat, health, cover and recovery

## Branch

```text
feature/gv-IT24103464-combat-health
```

Create it only after Branch 1 is merged:

```bash
git switch dev
git pull origin dev
git switch -c feature/gv-IT24103464-combat-health
```

## Main files

```text
Assets/Scripts/Gameplay/PlayerController.cs
Assets/Scripts/Gameplay/Health.cs
Assets/Scripts/Gameplay/IDamageable.cs
Assets/Scripts/AI/Core/DroneBrain.cs
Assets/Scripts/Gameplay/SoundEvents.cs
```

Coordinate shared `DroneBrain.cs` changes because it affects every agent.

## Required behavior

### Player shooting

- raycast comes from camera/aim direction;
- only hit target receives damage;
- firing has a cooldown;
- shooting reports a sound event for AI;
- no shooting when dead/game ended.

### Health

Locked gameplay values from the team plan:

```text
Maximum player health: 100
Drone hit damage: 10
Recovery delay: 3 seconds without damage
Recovery rate: 8 health/second
Maximum recovery: 100
```

### Fixed cover

- shelves/walls/pillars use colliders;
- drone attack performs a raycast at firing time;
- if the cover is between drone and player, the player is not damaged;
- no bullets passing through fixed solid geometry.

### Death

- health cannot go below 0;
- death event only happens once;
- gameplay stops appropriately;
- Nimthara owns the final UI/outcome presentation, so expose clean state rather than taking over UI ownership.

## Tests

1. Take exactly one hit and verify `100 -> 90`.
2. Continue taking hits; verify zero produces death once.
3. Take damage then wait less than 3 seconds: no recovery.
4. Wait beyond 3 seconds: health rises at about 8/sec.
5. Recovery stops at 100.
6. Put a shelf between drone and player: no damage through it.
7. Step out from cover: attacks can hit again.
8. Shoot a drone: correct `Health` component receives damage.

## Suggested commits

```text
feat(gv): validate reusable player and drone damage flow
feat(gv): implement delayed player health recovery
fix(gv): block enemy shots with fixed cover raycasts
```

## Done when

You can deliberately demonstrate **take damage -> hide behind cover -> survive -> recover -> return to combat**.

---

# 9. Branch 3 - physical shortcut door

## Branch

```text
feature/gv-IT24103464-door-physics
```

## Main file

```text
Assets/Scripts/Gameplay/LabDoor.cs
```

Potentially the Storage prefab that contains the door root/collider.

## Your responsibility boundary

- **Nimsara:** physical behavior, collider, Rigidbody, interaction, open/closed state.
- **Nimthara:** custom modeled door visual.
- **Pamudi:** level placement/graph connection and main-scene integration.
- **Asmadala:** A* reacts to graph changes and agents re-route.

## Required physical setup

Door root should have:

```text
LabDoor
Rigidbody
BoxCollider
(optional) NavMeshObstacle
Visual child supplied by Nimthara later
```

Recommended Rigidbody approach for this small project:

```text
isKinematic = true
useGravity = false
```

The script performs controlled motion with `Rigidbody.MovePosition` during `FixedUpdate`. This is easier to stabilize and explain than uncontrolled dynamic physics.

## Required behavior

- E toggles requested open/closed state;
- movement is visibly animated;
- collider moves with the door;
- player cannot walk through a closed door;
- player can traverse once the opening is clear;
- navigation shortcut remains unavailable until the door has sufficiently opened;
- closing disables the shortcut again;
- route-state change is only published when state actually changes;
- another route always remains available.

## Tests

1. Closed: player cannot walk through.
2. Open: player can pass.
3. Close: collision returns correctly.
4. Spam E several times: door remains stable.
5. Stand near the door while it moves: no severe launch/jitter.
6. Drone routes with door closed: alternate route.
7. Open door: future route can use shortcut.
8. Close while drone is travelling: agent replans/falls back instead of clipping or freezing.

## Suggested commits

```text
feat(gv): add controlled shortcut door movement
feat(gv): publish door navigation state after opening clears
fix(gv): keep closed door collider and route state synchronized
```

## Done when

You can show the lecturer the door working physically and then show that its open/closed state changes AI routing behavior.

---

# 10. Branch 4 - Storage section

## Branch

```text
feature/gv-IT24103464-storage-section
```

## Goal

Make your area support your physics work and Flanker behavior without taking over Pamudi's World Builder role.

## Required layout

Storage should contain:

- entrance/connection points supplied/agreed with Pamudi;
- fixed shelving/cover;
- at least two meaningful ways around the shelving;
- the shortcut-door area;
- enough navigation width for player and drone;
- no dead-end accidentally trapping Flanker;
- correct colliders on solid cover;
- clear left/right approach possibilities.

## Important boundary

You own the **Storage section prefab/gameplay arrangement**. Pamudi owns the overall level design, lighting/texturing direction, NavMesh integration, and final `MainLab.unity` assembly.

## Tests

- both routes are physically traversable;
- cover blocks shots;
- door path and alternate path both work;
- drone does not clip shelves;
- section still works after Pamudi integrates the prefab.

## Suggested commits

```text
feat(gv): add storage cover and dual-route layout
fix(gv): widen storage route for player and drone clearance
```

---

# 11. Branch 5 - Flanker utility scoring

## Branch

```text
feature/IS-IT24103464-flanker-scoring
```

## Main file

```text
Assets/Scripts/AI/Agents/FlankerBrain.cs
```

You may read shared systems, but avoid editing Asmadala's A* implementation unless the team agrees on a shared-interface change.

## Why utility scoring

For each possible action, calculate a score. The action with the highest valid score wins.

This gives you:

- numeric logic you can defend;
- dynamic response to path length / visibility / position;
- behavior beyond one simple chase `if/else`;
- a clean way to add fallbacks and edge cases.

## Locked decision options

The Flanker evaluates:

```text
1. Hold / return to Storage post
2. Pursue last-known player position
3. Flank left
4. Flank right
5. Engage directly only when already close and visible
```

## Player information rule

The Flanker may use:

- current player position only when the shared perception says the player is visible;
- remembered `LastKnown` after sight is lost;
- time since last observation;
- route/A* result;
- legal world geometry checks.

It must not continuously read the hidden player's live position through walls for decision-making.

## Candidate generation

Start simple and explainable:

```text
target = visible ? observedPlayerPosition : LastKnown
sideDirection = perpendicular horizontal direction from drone toward target
leftCandidate  = target + sideDirection * flankDistance
rightCandidate = target - sideDirection * flankDistance
```

Initial `flankDistance` can stay near **5 m**, then tune it from playtesting.

## Route cost

For each candidate:

1. ask the shared `WaypointGraph.FindRoute(...)` for its A* route;
2. if there is no route, reject the candidate;
3. otherwise sum the distance between route points to estimate route cost.

Example helper inside **your Flanker code**:

```text
route = graph.FindRoute(transform.position, candidate)
if route.Count == 0 -> invalid
cost = distance(start, route[0])
     + distance(route[0], route[1])
     + ...
```

Do not rewrite A* itself; Asmadala owns it.

## Locked scoring idea

Use normalized, understandable factors rather than random numbers.

For a valid flank candidate:

```text
FlankScore = 60
           + LineOfSightBonus
           + DistanceBandBonus
           - RouteCostPenalty
           - RepeatedSidePenalty
```

Initial tuning values:

```text
Base flank score          = 60
Clear firing line bonus   = +15
Good distance band bonus  = +10
Route cost penalty        = routeLength * 0.6
Repeat same side penalty  = -8
Invalid/unreachable       = reject / very negative score
```

These are starting values, not sacred constants. Tune them through playtesting and record the reason for the final values for viva.

### Why this is defensible

- **+15 firing line:** a flank that cannot threaten the player is weak.
- **+10 useful distance:** discourages choosing points that are too close/far to be meaningful.
- **-0.6 per route meter:** prevents a theoretically good flank from taking an absurdly long detour.
- **-8 repeated side:** reduces predictable oscillation/repeated same-side behavior.

## Decision cadence

The shared `DroneBrain` currently re-evaluates about every **0.3 seconds**. Keep expensive route work out of every rendered frame.

## Required visual behavior

The lecturer should be able to see:

1. player appears;
2. Flanker does not run straight at player;
3. it chooses left or right;
4. it travels using the shared path system;
5. if the route/player situation changes, it chooses again;
6. if no flank is available, it uses a safe fallback.

## Suggested commits

```text
feat(is): generate left and right flank candidates
feat(is): score flank candidates using route cost
feat(is): reward useful firing position for flanker
refactor(is): separate flanker candidate scoring from selection
```

## Done when

You can pause/explain the code and state exactly why one candidate scored higher than another.

---

# 12. Branch 6 - Flanker fallback and edge cases

## Branch

```text
feature/IS-IT24103464-flanker-fallback
```

## Required edge cases

### No recent player information

If the player has not been seen recently:

```text
return/hold Storage post
```

or pursue the last-known point only within a short memory window.

### Left route unavailable

Reject left and consider right.

### Right route unavailable

Reject right and consider left.

### Both flank routes unavailable

Fallback:

```text
pursue last-known reachable position
```

If that is also unavailable:

```text
return/hold home position
```

### Player very close and visible

Direct engagement may temporarily outrank repositioning.

### Door closes during movement

The shared graph version changes, route follower requests a new path, then your policy is re-evaluated if the flank is no longer valid.

### Oscillation

Avoid changing left/right every decision tick. Add a short commitment/cooldown or repeated-side penalty.

Recommended initial approach:

```text
minimum flank commitment: about 1.0-1.5 seconds
```

unless the current route becomes invalid.

## Tests

- normal left flank;
- normal right flank;
- left blocked;
- right blocked;
- both blocked;
- player lost behind cover;
- door closes during route;
- player suddenly becomes close;
- repeated decisions do not create rapid left/right jitter;
- no route result never causes null reference/freeze.

## Suggested commits

```text
feat(is): add no-route fallback for flanker
feat(is): add flank commitment to prevent side oscillation
fix(is): reselect flank after door invalidates route
```

---

# 13. Integration-fix branch

Use this only after your completed work has merged and a real integration bug appears.

```text
fix/IT24103464-integration-polish
```

Do not hide unfinished features inside a generic polish branch.

Possible legitimate fixes:

- door collider not matching Nimthara's final visual;
- Storage clearance changes needed after final drone model import;
- health state not reaching Nimthara's HUD correctly;
- Flanker candidate point inside shelf after prefab integration;
- attack raycast layer issue after final scene assembly.

---

# 14. Pull request process for every branch

Before opening a PR:

```bash
git status
git diff
git log --oneline -5
```

Then test the branch in Unity.

Push:

```bash
git push -u origin YOUR_BRANCH_NAME
```

Open PR:

```text
YOUR_BRANCH -> dev
```

## PR description must contain

- Owner: Nimsara
- Module: GV / IS
- Official role or agent: Systems Engineer / Flanker
- What changed
- Files changed
- Normal test performed
- Edge case tested
- Shared files touched
- Who needs to review/handoff

## Merge method

Use **Create a merge commit** for assessed work so your genuine individual commits remain visible.

After merge:

```bash
git switch dev
git pull origin dev
```

Then create the next branch.

---

# 15. Handoffs you must actively coordinate

## With Pamudi

You need from Pamudi:

- Storage section connection points;
- final graph node/edge placement around your door;
- NavMesh re-bake/integration after meaningful geometry changes.

You give Pamudi:

- Storage prefab;
- exact door location;
- which graph edge is the shortcut;
- confirmation that an alternate route remains.

## With Nimthara

You need:

- final custom lab-door visual.

You provide:

- door root hierarchy/physics requirements;
- collider/Rigidbody behavior that must not be removed;
- clean health/drone/game state that her HUD can display.

Rule: **Nimthara changes the visual child; your physical root remains the source of truth for door behavior.**

## With Asmadala

You need:

- shared A* route result;
- shared route follower behavior;
- correct route recalculation when graph `Version` changes.

You provide:

- door open/closed state into the graph;
- tactical destination from Flanker.

Rule: **You do not own A*; you own the Flanker's decision about which destination to request.**

---

# 16. Timeline from 4 October

## 4 Oct - setup + understand baseline

- open project;
- verify no blocking compile errors;
- test current movement/door/health/Flanker;
- ensure `dev` exists;
- create `feature/gv-IT24103464-player-physics`;
- read every line of `PlayerController.cs` you will touch.

## 5 Oct - player physics

- movement/collision/mouse/interactions;
- tests;
- 1-3 meaningful commits;
- PR to `dev`.

## 6 Oct - combat + health

- shooting/damage;
- cover raycasts;
- recovery;
- death behavior;
- tests;
- PR.

## 7 Oct - physical door

- Rigidbody/collider;
- open/close;
- route state;
- open/closed tests with a drone;
- PR.

## 8 Oct - Storage section

- two routes;
- fixed cover;
- door area;
- test clearance;
- handoff to Pamudi;
- PR.

## 9-10 Oct - Flanker scoring

- candidate generation;
- A* route/reachability use;
- route-cost calculation;
- scoring;
- visual testing;
- PR.

## 11 Oct - Flanker edge cases

- fallback;
- invalid routes;
- anti-oscillation;
- door replan;
- PR.

## 12-15 Oct - team integration

- four agents active together;
- fix only genuine integration issues in your ownership;
- test start-to-win and start-to-loss paths.

## 16-18 Oct - evidence + viva

- screenshots;
- commit history review;
- explain code without notes;
- rehearse Systems Engineer demo;
- rehearse Flanker scoring/A* explanation.

## 19-20 Oct - feature freeze

- no new mechanics;
- regression fixes only;
- test desktop build;
- help verify `dev -> main` release.

## 21 Oct - GV submission/showcase

Use the exact stable tagged source that matches the demo/video.

---

# 17. Your personal definition of done

Your work is complete only when all of these are true.

## Systems Engineer

- [ ] Player movement works reliably.
- [ ] Collision prevents clipping through solid geometry.
- [ ] Mouse look works and is clamped.
- [ ] Shooting raycast damages the intended target.
- [ ] Fixed cover blocks drone shots.
- [ ] Player health starts at 100.
- [ ] Drone hit removes 10 in the locked tuning.
- [ ] Recovery waits 3 seconds, then restores about 8 health/sec to 100.
- [ ] Death occurs once and gameplay stops appropriately.
- [ ] Door visibly opens/closes.
- [ ] Closed door blocks player/shortcut.
- [ ] Door state reaches navigation.
- [ ] Closing the door cannot permanently freeze an agent.

## Storage

- [ ] Two useful side routes exist.
- [ ] Shelving/cover has correct colliders.
- [ ] Flanker can navigate the section.
- [ ] Alternate route remains usable with shortcut closed.

## Flanker

- [ ] Uses utility scoring, not only a basic chase if/else.
- [ ] Generates left/right candidates.
- [ ] Rejects unreachable candidates.
- [ ] Uses A* route/path cost information.
- [ ] Can explain every scoring factor/weight.
- [ ] Chooses visibly different side routes when appropriate.
- [ ] Does not know hidden live player position through walls.
- [ ] Has explicit no-route/no-flank fallback.
- [ ] Does not rapidly oscillate left/right.
- [ ] Reacts when the door invalidates a route.

## Git / evidence

- [ ] Work was done on short branches from `dev`.
- [ ] Commits are truthful and descriptive.
- [ ] No fake backdated/no-op commits.
- [ ] PRs show tests and ownership.
- [ ] You can find your code immediately in the viva.

---

# 18. What you need to understand for viva

You do **not** need to become a game-engine expert. You do need to understand your own pipeline clearly.

Be able to explain:

## Player physics

```text
Input -> CharacterController movement -> collision against colliders
```

## Shooting

```text
Camera forward ray -> first collider hit -> Health.TakeDamage()
```

## Cover

```text
Drone firing ray -> shelf/wall hit first -> player receives no damage
```

## Door

```text
E interaction -> requested state -> FixedUpdate Rigidbody motion ->
opening cleared -> graph door edge enabled/disabled -> graph version changes
```

## Flanker

```text
Perception -> observed/last-known target -> left/right candidates ->
A* route check -> route cost + tactical score -> choose best ->
shared movement follows route -> re-evaluate/fallback
```

## A* basics even though Asmadala owns it

You must still know:

```text
g = path cost travelled so far
h = estimated remaining cost to goal
f = g + h
```

The search prefers the open node with the smallest `f`, records parent links, and reconstructs the chosen route when the goal is reached.

---

# 19. Things you should NOT build

Do not spend your time on:

- VR;
- multiplayer;
- inventory;
- ammo/reload system;
- weapon switching;
- elaborate menu;
- movable barricade system unless the team later needs one;
- multiple levels;
- additional enemy types;
- procedural generation;
- online/backend features;
- fancy UI owned by Nimthara;
- replacing Asmadala's A*;
- taking over Pamudi's full environment/lighting role;
- making the custom door/drone models owned by Nimthara.

Your highest-value work is **stable physics/interactions + a technically defensible Flanker**.

---

# 20. First implementation session - exact checklist

When we start implementation together, do exactly this:

```text
[ ] Open Terminal in the repository
[ ] git status
[ ] git switch dev
[ ] git pull origin dev
[ ] git switch -c feature/gv-IT24103464-player-physics
[ ] Open project in Unity
[ ] Open MainLab
[ ] Clear Console
[ ] Press Play
[ ] Test current player movement/collision/input
[ ] Write down defects before editing
[ ] Open PlayerController.cs
[ ] Understand movement block
[ ] Understand look block
[ ] Understand interaction block
[ ] Make ONE controlled change at a time
[ ] Return to Unity and test after each change
[ ] Commit a real working checkpoint
```

**Do not begin the Flanker first.** Build and understand your Systems Engineer foundation, then move to the agent. This makes the implementation much easier because the player, damage, cover, and door are the world conditions that your Flanker eventually reacts to.

---

# 21. Locked plan summary

Your development path is:

```text
BASELINE VERIFY
      |
      v
feature/gv-IT24103464-player-physics
      |
      v
feature/gv-IT24103464-combat-health
      |
      v
feature/gv-IT24103464-door-physics
      |
      v
feature/gv-IT24103464-storage-section
      |
      v
feature/IS-IT24103464-flanker-scoring
      |
      v
feature/IS-IT24103464-flanker-fallback
      |
      v
TEAM INTEGRATION
      |
      v
fix/IT24103464-integration-polish   (only if actually needed)
      |
      v
VIVA + EVIDENCE + RELEASE
```

This is the locked implementation order unless a real shared dependency blocks a phase. If that happens, do not redesign the project casually; record the blocker and coordinate the minimum interface/handoff change with the responsible teammate.
