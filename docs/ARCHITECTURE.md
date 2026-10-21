> **V3 planned architecture note:** Keep visual meshes as children of stable gameplay prefabs; render a complete solid floor/roof and actual lighting per room. Replace the old four-section shortcut graph and single sliding door only after the new linear three-hinged-door layout passes greybox collision/navigation testing. See [GRAPHICS_IMPLEMENTATION_HANDOFF.md](GRAPHICS_IMPLEMENTATION_HANDOFF.md). Current source still implements the old baseline.

---

# Architecture - How the Pieces Connect

## Mental model

| Familiar software idea | In this Unity project |
| --- | --- |
| Application screen/world | `MainLab.unity` scene |
| Reusable component | Prefab: section, drone, console |
| Business/behavior logic | C# `MonoBehaviour` components |
| Session state | `GameSession` for progress/outcomes |
| Event | shot/sound, damage/death, door-state change |
| Shared search service | A* route search over the waypoint graph |
| Presenter/motor | `DroneMotor` converts ordered route points into movement/animation |

Unity calls `Awake`, `Start`, `Update`, and physics callbacks at different times. Do not call another component's Unity lifecycle methods manually. The greybox generator creates ordinary assets once; after the generated assets are committed, normal work happens through Unity and Git.

## Runtime flow

```text
observe -> choose tactical goal -> A* route -> follow route points -> act -> re-evaluate
```

1. **Player/world - Nimsara lead:** `PlayerController` reads movement/aim/fire/interaction; `Health` handles damage and delayed player regeneration; `LabDoor` performs the physical/controlled movement and publishes open/closed route state.
2. **Progress/outcomes - Nimthara lead:** `ConsoleSwitch`, `GameSession`, `ExitZone`, and HUD/outcome presentation track consoles, drone deaths, health/progress, exit gating, win/loss, and restart presentation.
3. **Perception/shared agent base:** `DroneBrain` samples allowed observations such as unobstructed sight, range, recent sound, damage/world events, and remembered last-known position. Agents must not read the player's live hidden position through walls.
4. **Individual decision policies:** `ScoutBrain`, `FlankerBrain`, `GuardBrain`, and `InterceptorBrain` each select a meaningful tactical destination/action. The four policy files are separate assessed IS contributions.
5. **Waypoint graph - Pamudi data ownership:** Pamudi places/maintains nodes and connections so they match real navigable routes. The closed shortcut edge is unavailable, while an alternate route remains.
6. **A* - Asmadala implementation ownership:** the search computes `f = g + h`, tracks parents, reconstructs an ordered route, and returns an explicit no-path result when necessary.
7. **Route following/animation - Asmadala lead:** `DroneMotor` consumes ordered route points, handles waypoint arrival/turning/braking, and drives readable movement/animation states. On an invalid route or changed graph version it requests a fresh path.
8. **Combat/cover:** a drone attack checks line of sight at fire time. Fixed walls, shelves, and pillars can intercept the shot, allowing the player to use cover and later recover.

The shared A* graph makes high-level route choices between areas/waypoints; `NavMeshAgent` handles local motion around geometry. Do not replace the students' decision policies with one shared `if (distance < x)` chase behavior. Do not perform full path search every rendered frame.

## Ownership by code/asset area

- `Assets/Scenes/MainLab.unity` - **Pamudi** coordinates final assembly, shared room connections, waypoint placement, and NavMesh integration.
- Checkpoint prefab + `ScoutBrain.cs` - **Pamudi**.
- Storage prefab + player/door/health/combat + `FlankerBrain.cs` - **Nimsara**.
- Server prefab + `GuardBrain.cs` + `ArtSource/` + `Assets/Models/` - **Nimthara**.
- `ConsoleSwitch.cs`, `GameSession.cs`, `ExitZone.cs`, HUD/outcome presentation - **Nimthara** as workload balance.
- Control prefab + A* implementation + `DroneMotor.cs` + animation assets + `InterceptorBrain.cs` - **Asmadala**.
- `WaypointGraph.cs`, `DroneBrain.cs`, shared prefabs/interfaces - one named owner in the PR, reviewed by another affected member.

## Shared interface rules

### Player/world events

Nimsara's systems expose only the information other systems need: shot/sound event, health/damage/death, door state, and observed/relevant player state. Do not let AI components bypass perception and reach directly into player internals for hidden information.

### Agent decision output

An agent policy outputs a **tactical destination/action**, not raw animation instructions. This separation lets the same A*/movement pipeline serve all four agents while keeping each student's AI contribution distinct.

### Path search output

A* returns either:

- an ordered list of route points/nodes, or
- a clear no-path result.

The requesting agent must have an intentional fallback for no-path/invalid-goal cases.

### Door update

The physical door and its visual are separate responsibilities:

- Nimsara owns the door behavior/collider/Rigidbody and publishes state;
- Nimthara supplies the custom door model/visual;
- Pamudi ensures the graph/level has both shortcut and alternate route;
- Asmadala invalidates/recalculates routes when the graph changes.

### Performance rule

Perception/decision/path requests run on events or modest intervals. Smooth movement/animation can update per frame, but expensive search should not.
