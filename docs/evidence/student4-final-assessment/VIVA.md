# Student 4 viva notes

These explanations describe the inspected code. Say which branch is being demonstrated: the reviewed `dev` build is incomplete; richer evidence is from separate feature builds.

## GV — Agent Controller (short explanation)

“I implement the shared controller that consumes destinations from four different drone policies. A* returns ordered room waypoints. DroneMotor requests a complete NavMesh path for each leg, turns within an angular-speed limit and uses separate intermediate and final arrival rules. Final arrival requires low speed, which lets braking finish. Repeated goals preserve progress. A graph-version change invalidates a moving route and triggers bounded retries while door carving settles. Invalid or unreachable goals stop safely rather than skipping through geometry.

“In the animation feature build, the movement root stays responsible for navigation while a separate visual hierarchy handles hover, flight lean, turning, braking and accepted combat events. A collision envelope checks the raised body. Fatal damage creates a physics wreck, followed by one impact transition and a stable disabled state. These systems are demonstrated in the feature screenshots; they are not all merged into dev.”

## IS — Interceptor (short explanation)

“The predictive policy stores only successful visible observations. It estimates planar velocity from that bounded history and reduces confidence when the player stops, reverses or turns sharply. It considers reachable forward junctions and compares route-based player and drone arrival times. It rejects candidates outside its time horizon, behind the observed heading or too late to reach. Weighted terms score timing, direction, position, confidence and stability.

“The Control branch adds six private tactical junctions and two distinct perimeter arcs. They do not become shared graph nodes. Each leg must satisfy the original gated A* contract, a complete NavMesh path and a conservative collision envelope. The policy selects the final candidate; the tactical adapter sends the next stop to the shared motor. Commitment and a switching threshold limit oscillation. Failed predictions enter a cooldown, then fall back to remembered pursuit, bounded local search, home or a safe hold. An invisible player argument never refreshes memory.”

## Likely questions and defensible answers

| Question | Answer |
| --- | --- |
| What are g, h and f? | g is the sum of Euclidean graph-edge costs from the start, h is straight-line distance to the goal node, f = g + h. |
| Why is this heuristic safe? | Every edge cost is Euclidean distance. The triangle inequality makes h admissible and consistent, so closing a node does not discard a cheaper future path under this cost model. |
| Is A* minimizing actual travel time? | No. It minimizes graph-edge distance. NavMesh detours and acceleration are separate. Prediction uses actual NavMesh corner lengths divided by estimated/configured speed, still an approximation. |
| How is the route ordered? | Follow parent links from goal to start, reverse the list, omit an already-reached initial node and preserve the requested destination. |
| How does no-path differ from already there? | No-path is an empty list. A connected stationary query returns a successful destination point. |
| Can a nearest node on the opposite side of a door create a shortcut? | Local attachments need complete native paths. Locked graph edges are excluded. The 40 graph tests include alternative attachment and blocked same-nearest-node cases; the physical door probes check carving separately. |
| What opens a gate? | Its own drone defeated AND its own console activated. The physical blocker and carving release only after the animation; publishing graph Version causes moving motors to replan. |
| Why bounded retries? | Native carving can lag a graph event by a frame. Two retries tolerate that delay without a permanent retry loop. A new destination or graph change resets the appropriate budget. |
| Why not place every Control junction in the global graph? | The five-node graph expresses room/gate connectivity. Private candidates express one agent’s local tactics and preserve the other policies and shared data ownership. |
| How do you know the two arcs differ? | They use opposite x = 3 / x = 17 sides and enclose both actual cover blocks. Tests load MainLab, validate every leg and drive the motor along both. A central island model remains planned. |
| Does the Interceptor know the player through walls? | The upgraded policy ignores invisible position arguments and ages its last successful observation. Cover/hidden-position tests verify that moving the hidden player does not update its memory. |
| What happens after a reversal or missed intercept? | Direction changes invalidate commitment. A missed window or arrival without the player blocks the failed candidate until cooldown and newer evidence, then reachable fallback actions are considered. |
| Can the destination be reachable for a player but not a drone? | Yes. Ground NavMesh connectivity does not prove clearance of the raised silhouette. The Control validator performs continuous box casts and checks endpoints; the animation build also has a per-frame DroneBody guard. |
| Why separate navigation root and visual animation? | Visual bob/lean should not move the agent off its navigation plane or corrupt path queries. A separate wreck takes over physical motion after death in the animation build. |
| Do screenshots prove every animation transition? | No. Fresh images demonstrate rendered appearance. Transition, impact-count, shot/tracer attribution and restart assertions come from the PlayMode tests. Filenames identify capture stages; a transient state can advance before the rendered frame. |
| Is the current release collision safe? | No. The dev snapshot penetrated a temporary raised beam and the player in the new probes. The DroneBody feature snapshot passed those same fixtures. Integration and a unified rerun remain necessary. |
| Did profiling prove zero allocations? | No. The allocation API failed its known-allocation calibration. CPU and physics measurements are available; allocation attribution needs a supported recorder or development-player capture. |
| Why does a green test suite not imply completion? | Dev’s existing 19 PlayMode tests cover its motor contract, not the missing animation/prediction layers. New scene probes revealed collisions. Assessment must combine executed coverage with source and integration evidence. |
| What is the next integration step? | Review and combine the existing height/collision/animation work with Control prediction, resolve the two Interceptor adapters, obtain shared-scene ownership review for existing asset differences, then rerun the combined rendered suites and profile a target build. |
