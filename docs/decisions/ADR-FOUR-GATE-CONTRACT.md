# ADR: four independent security gates

Accepted 2026-10-14. Supersedes three hinged doors / console-only access in GRAPHICS_FIRST_MASTER_PLAN, LEVEL_BLUEPRINT and old member import instructions; historical material remains for context.

Order: Checkpoint/Scout (0) → Server/Guard (1) → Control/Interceptor (2) → Storage/Flanker (3) → Exit. MissionProgress owns four console flags and four drone flags. Drone identity comes from concrete brain type, console identity from its configured room; array discovery order is irrelevant. Duplicate reports are idempotent. Missing objectives fail closed.

Each gate polls its own AND prerequisite. LabDoor.Configure validates identity; GraphEdge accepts SecurityGate or validated integer (-1 means no gate). All four graph states start false. A completed animation calls SetGateOpen(id,true), increments Version and causes DroneMotor to replan. Graph traversal still uses A* g + Euclidean h, parent reconstruction and an empty route for no path.

The stationary physical root owns Rigidbody, BoxCollider, NavMeshObstacle and LabDoor. Door assembly contains LeftPanel, RightPanel, LeftBolt, RightBolt. Bolts retract first; panels translate in assembly local X, independent of root yaw. Leaves have no colliders. Root passage collider and carving obstacle release only at the end; the door never closes again. No moving body can push/trap the player. Frame stays stationary. Unity NavMesh carving updates asynchronously; real agent traversal needs Play Mode checking after a frame.

Migration recognizes the legacy four named waypoints once, preserves their transforms, replaces loop edges with three gated connections plus an exit node/edge, seals Checkpoint–Storage and cuts Storage's east exit. Later runs preserve graph and existing gate visuals. Unknown custom legacy graphs are rejected for manual mapping. Do not regenerate MainLab. Save scene edits before invoking Tools > Shift Four > Install Drone + Door Models. Rebake navigation after geometry integration.

Nimthara owns progress/consoles/HUD/Server/art. Shared changes to LabDoor (Nimsara), graph contract (Asmadala) and MainLab/installer (Pamudi) are required integration work, not reassignment of ownership. Their review and final scene/navigation acceptance remain required. No external messages were sent.
