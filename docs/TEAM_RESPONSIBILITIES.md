> **Current contract (2026-10-14):** four automatic horizontal sliding gates, each requiring its own drone AND console. Historical hinged/shortcut and console-only instructions below are superseded. See `docs/decisions/ADR-FOUR-GATE-CONTRACT.md` and `docs/members/CORE_GV_IMPLEMENTATION.md`.

> **V3 graphics-first handoff:** Official student GV/IS roles below remain valid. The target build adds full room roofs, solid floors, ceiling lights, and asset-by-asset specs. Pamudi leads the shared environment kit, Nimthara the two mandatory original Blender models, Nimsara hinged door gameplay and Storage cover, Asmadala movement and Control/Interceptor geometry. See [GRAPHICS_FIRST_MASTER_PLAN.md](GRAPHICS_FIRST_MASTER_PLAN.md) and [MODEL_INDEX.md](MODEL_INDEX.md). The shortcut handoffs below refer to the still-existing source baseline and must be updated in implementation PRs.

---

# Team Responsibilities and Handoffs

The GV role names below are the **official prescribed roles from the supplied SE3032 brief**. Do not rename them in the final presentation or viva. Each student also owns one distinct autonomous agent for SE3062.

## Student 1 - Pamudi

**Official GV role:** World Builder  
**Section:** Checkpoint  
**IS agent:** Scout

### Owns

- overall connected level layout and final scene integration;
- shared visual direction, lighting, texturing, initial/rebaked NavMesh;
- waypoint locations/connections that reflect valid routes;
- Checkpoint section prefab;
- Scout policy: patrol, investigate sound, pursue visible player, search last known position, return-to-patrol fallback.

### Routine files/areas

- `Assets/Scenes/MainLab.unity`
- Checkpoint section prefab
- NavMesh data
- `Assets/Scripts/AI/Agents/ScoutBrain.cs`
- waypoint node placement / graph layout data

### Handoffs

- receives each member's completed section prefab for final scene integration;
- tells Asmadala when node/edge layout changes;
- keeps Nimsara's door shortcut and its alternate route valid;
- uses Nimthara's imported visual assets without changing their source ownership.

### Done when

The whole level is coherent and walkable, all required routes/nav areas work, lighting/textures are consistent, and Scout can investigate/search/recover from lost contact without knowing the player through walls.

---

## Student 2 - Nimsara

**Official GV role:** Systems Engineer  
**Section:** Storage  
**IS agent:** Flanker

### Owns

- first-person movement, collision, aiming/shooting interactions;
- shared health/damage behavior and enemy attack rules;
- player damage, delayed recovery, death/restart behavior;
- physical/interactive shortcut door and its collider/motion;
- door open/closed state published to navigation;
- fixed-cover line-of-sight blocking behavior;
- Storage section with two useful side routes;
- Flanker policy: choose a reachable side approach using angle/lateral value and path cost, then fallback/reselect when invalid.

### Routine files/areas

- `Assets/Scripts/Gameplay/PlayerController.cs`
- `Assets/Scripts/Gameplay/Health.cs`
- `Assets/Scripts/Gameplay/IDamageable.cs`
- `Assets/Scripts/Gameplay/LabDoor.cs`
- shared attack/damage code in the drone/gameplay contract
- Storage section prefab
- `Assets/Scripts/AI/Agents/FlankerBrain.cs`

### Handoffs

- tells Pamudi/Asmadala exactly which graph edge the door enables/disables;
- works with Nimthara so the custom door **visual** keeps Nimsara's physical root/collider/Rigidbody behavior;
- exposes clean player/health/drone state for Nimthara's HUD/outcome display.

### Done when

Player movement/collision/interactions are reliable, door physics visibly works, cover blocks shots, health recovers after the defined safe delay, and Flanker visibly chooses/rechooses a side route with a documented fallback.

---

## Student 3 - Nimthara

**Official GV role:** Core Developer  
**Section:** Server  
**IS agent:** Guard

### Owns

- at least two original 3D models from scratch: security drone and lab door;
- editable Blender source, sensible topology, UV mapping, export/import, model scale/pivots/materials;
- Server section and its marked cover points;
- Guard policy: score cover by protection, path/travel cost, firing visibility, relocate when exposed, fallback if no safe cover;
- additional workload balance: reusable console interaction, four unique console instances/progress, HUD progress, exit/win/lose/restart presentation.

### Routine files/areas

- `ArtSource/`
- `Assets/Models/`
- Server section prefab
- `Assets/Scripts/AI/Agents/GuardBrain.cs`
- `Assets/Scripts/Gameplay/ConsoleSwitch.cs`
- `Assets/Scripts/Gameplay/GameSession.cs`
- `Assets/Scripts/Gameplay/ExitZone.cs`
- HUD/outcome presentation assets

### Handoffs

- gives Asmadala a stable drone visual hierarchy so movement/animation hooks do not break;
- gives Nimsara a lab-door visual that preserves the physical root/collider/Rigidbody;
- exposes reachable Guard cover destinations through the shared agent/navigation contract;
- consumes player/drone state from Nimsara/shared gameplay rather than reimplementing combat.

### Done when

Both original models and editable sources exist, topology/UVs/import can be defended, consoles/HUD/outcomes work without double counting, and Guard chooses/changes cover sensibly with a clear fallback.

---

## Student 4 - Asmadala

**Official GV role:** Agent Controller  
**Section:** Control  
**IS agent:** Interceptor

### Owns

- shared A* implementation over Pamudi's waypoint graph;
- route reconstruction and explicit no-path result;
- shared drone route following, waypoint arrival, turning, braking, route invalidation/recalculation;
- readable hover/fire/hit/shutdown or death animation states;
- Control section junctions and exit placement handoff;
- Interceptor policy: use recent observed player positions to estimate travel direction, score reachable junctions ahead, retry/replan when prediction fails, fallback to pursuit/last-known-position.

### Routine files/areas

- `Assets/Scripts/AI/Core/WaypointGraph.cs` (A* implementation portion, coordinated with Pamudi's graph data)
- `Assets/Scripts/AI/Movement/DroneMotor.cs`
- `Assets/Scripts/AI/Agents/InterceptorBrain.cs`
- Control section prefab
- agent animation controllers/clips and movement-related prefab properties

### Handoffs

- consumes waypoint nodes/edges from Pamudi;
- responds to Nimsara's door-state change;
- consumes Guard/flank/scout/intercept destinations from each agent policy;
- connects Nimthara's drone model to movement/animation without taking over model authorship.

### Done when

A* returns correct ordered routes or no-path, all four agents move smoothly along chosen routes and replan when required, animation states are visible, and Interceptor makes a defensible ahead-of-player prediction with fallback.

---

# Shared ownership rules

1. **One person owns a shared file for a PR.** Other members review rather than editing the same shared file in parallel.
2. **Main scene is coordinated through Pamudi.** Section owners normally work in their own prefabs.
3. **A* is shared knowledge.** Asmadala implements it, but every member must understand `g`, `h`, `f`, parent reconstruction, blocked edges, and no-path handling.
4. **Agent decisions remain individual.** Shared `DroneBrain`, movement, or perception must not reduce four agents to one behavior with different colors.
5. **Additional tasks do not replace prescribed GV evidence.** Nimthara's HUD work, Asmadala's A*, etc. are useful shared work, but the official role component must still be demonstrable.
6. **No hidden-position cheating.** Agents only use information they are allowed to observe/remember.
