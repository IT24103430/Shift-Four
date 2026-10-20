# Compact drone presentation

The Scout, Guard, Interceptor and Flanker use a 0.9 model scale. The navigation root stays at unit scale and the authored flight height stays at 1.6 m. Living mesh hitboxes follow the smaller visual transforms. `DroneBody` clearance and native agent avoidance are remeasured from the actual meshes and rotor sweep; the central capsule radius becomes 0.45 m, with height fitted separately to each model. Wreck collision shapes are generated from the current scaled meshes, including the new gun geometry.

`DroneWeaponMount` creates a short steel barrel, muzzle collar and compact gimbal housing beneath the front area. This is agent-generated runtime geometry, not personal Blender modeling evidence. The gun has no live colliders and cannot intercept shots. It aims independently of the flight pose, using visible player observations and accepted shots; navigation, role policies and movement turning/braking remain in control of the original systems.

The physical muzzle supplies the firing ray origin. An additional ray from the mounting point prevents a protruding barrel from bypassing thin cover. The existing field of view, range, cooldown, damage, living-target and outcome checks still apply. The flash/tracer uses the exact accepted muzzle position and hit point; it no longer estimates an unrelated point outside the body collider. Death cancels firing and the barrel falls with the wreck.

## Apply to authored assets

`Tools > Shift Four > Apply Compact Drone Presentation` updates only the four agent prefabs and their MainLab instances. It saves backups under `Library/DronePresentationBackup`, retains `.meta` GUIDs, preserves teammate gameplay YAML and does not regenerate or rebake the level. Save scene edits and stop Play Mode first. Reapplying uses an absolute 0.9 scale, not repeated multiplication. New visuals created by the model installer use the same scale. The shared clearance fitting tool also respects visual scale.

## Acceptance and evidence

The animation regression checks the four scaled models, matching muzzle/ray/tracer direction and an obstruction between pivot and muzzle. Existing suites cover native movement, body clearance over rotor/tilt phases, cover blocking, gates, recovery, falling wrecks and restart. The updated geometry alignment checks account for each model's scaled height, including the taller Scout.

Evidence captures from `DroneDestructionPlayModeTests` can be directed with `SHIFTFOUR_DEMO_OUTPUT`; each drone has hover, approach, fire, hit, critical failure, fall, impact and settled-wreck frames. These are scripted rendered scenarios, not a recorded human playthrough.

The standalone world-builder probe records per-room and aggregate frame times plus hardware/build context with all four policies active during sampling. It uses 120 samples after 30 warm-up frames per room, an uncapped development player, a scripted camera and increased QA player health. This is a short automated baseline, not worst-case combat or GPU profiling. Generated captures/logs belong under ignored `Logs/` inside the verification copy.

## Executed verification, 2026-10-20

- 332 Edit Mode checks passed after the scale/height changes. The final combined Play Mode run passed all 75 selected checks in a single run, with zero failed or skipped cases, including the new all-four muzzle/tracer/thin-cover test.
- Rendered evidence includes 36 TGA frames: barrel close-up plus eight movement/combat/destruction stages for each drone. The sampled rotor/hover/tilt envelope check retained a minimum margin of approximately 0.03265 m.
- The Windows development build succeeded with zero errors and 11 warnings. Existing obsolete API warnings and an Editor shutdown temporary-allocation warning are retained in the logs; a warning-free Editor session is not claimed.
- The standalone tour passed startup/NavMesh binding, four console/death/gate pairs, actual player-capsule traversal and victory. It sampled all four active policies before pausing them for the scripted traversal.
- At 1920x1080 on an Intel i5-12400F / GeForce GTX 1650, the 480 sampled frame intervals had median **1.796 ms**, p95 **2.216 ms**, maximum **4.839 ms**. This was an uncapped development-player tour with the main Unity editor also open. It is a local automated baseline, not sustained human-combat FPS or a GPU profiler capture.

The stable verification project is `E:/Projects/ShiftFourVerification/DronePresentation`, outside the working project's `Temp` folder. Its `Logs/DronePresentation/` contains the final Play Mode XML, 36 frames, build log and standalone performance/context/traversal results. The runnable player is `Builds/WorldBuilderGV/ShiftFour.exe` inside that copy; normal launches do not activate the scripted probe. The earlier 332-case Edit Mode report and failed/interrupted runs are retained under `Logs/PreviousRuns/`.

Failures retained: the first migration forced an unsuitable common capsule height on Scout; fitting the scaled model fixed it. Earlier combat fixtures assumed a fixed 1.8 m firing origin/root-distance range, and a raised-obstacle fixture allowed the smaller drone to skirt its ends. They now test the actual muzzle, a clearly out-of-range target and a barrier spanning all candidate routes. A later run was interrupted when the main Unity editor cleaned the verification copy from the working project's `Temp` directory; relocating the copy resolved those missing-scene failures. The final 75-case run passed after these fixes.

Only 24 targeted transform/collision/clearance/prefab-instance blocks were copied into the four authored prefabs and MainLab. All 35 other gameplay script blocks were retained from the working files, including teammate fields unknown to the current C# definitions. The saved NavMesh, four FBX files and existing asset GUIDs were retained. Production-file backups and the transfer manifest are under ignored `Logs/DronePresentationReview/`.

Final human acceptance remains: inspect each drone's silhouette/barrel and target-facing direction at normal gameplay distance; fight around cover and through gates; check jump-height targets and death/wreck contact; play the complete mission and capture the actual demonstration build on the target machine. Preserve personal modeling evidence separately.
