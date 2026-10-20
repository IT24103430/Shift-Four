# SHIFT FOUR — FINAL VISUAL, ANIMATION & NAVIGATION POLISH
## Agent-ready implementation brief | SE3032 Graphics and Visualization | 20 October 2026

**Purpose:** Turn the existing playable four-sector sci-fi security-facility FPS into a visually coherent, polished, reliable submission **without rebuilding the game, changing its core mechanics, or requiring a full-body playable character**.

**Input:** Open the actual Shift Four Unity repository at its existing branch, inspect current scene/assets/scripts/settings, and use the supplied screenshot as the initial visual reference. The screenshot shows geometric-looking dark player arms/hands, a relatively detailed black pistol, flat wall and floor panels, an orange console accent, small/clipped HUD elements, and a subdued industrial color scheme. **All repository paths in examples are illustrative; discover the real paths before editing.**

**Assessment alignment:** SE3032 expects a playable interactive shooting game; level lighting/texturing, physical player interactions, two original custom 3D models (Student 3), agent movement/rotation/animations, visual cohesion, build stability, optimization, individual workflow and Git evidence. It does **not** require a complete first-person player body. Preserve the original-model evidence and the four-agent IS logic. Assignment deadline listed as **21 October 2026**; treat this as a short final-polish window.

---

## A. Non-negotiable agent rules

1. **Inspect before editing.** Read README, `docs/` instructions, Unity `ProjectVersion.txt`, packages, renderer settings, active scene, player prefab, weapon prefab, GUI canvases, door/console implementations, drone prefabs/controllers, animation controllers, and the imported asset inventory. Print a verified map of relevant files.
2. **Do not break a functional game.** Retain spawn, camera, mouse look, WASD, jump/sprint if present, combat, hit detection, ammunition/reload logic, damage/health, cover, door and console interaction, enemy behavior, progress counters, win/lose/restart, and route/pathfinding behavior. If something is not implemented, report it rather than silently changing product scope.
3. **Preserve visual identity.** Maintain a coherent stylized/grounded industrial research facility: desaturated blue/charcoal panels, controlled orange security accents, restrained cool emissive elements. Use one consistent level of realism. **Do not replace every asset wholesale** or add unrelated neon/cyberpunk/photo textures.
4. **Replace weak assets selectively.** Make an asset audit table with `asset -> defect -> keep/refine/replace -> source/license or original creator -> affected prefabs/scenes`. Prioritize obvious primitive hands, wrong-scale geometry, blurry/pixelated/mismatched texture images, stretched UVs, placeholder icons/decals, bland console faces, and broken materials. Keep effective models/materials and approved art.
5. **Respect original-work claims.** Assets modeled from scratch by the team must remain attributable to the true creator. Never call downloaded/Asset Store/AI-generated meshes 'modeled in Blender by the team'. Imported assets require allowed licenses/attribution. Avoid unverified downloads and package additions. If safe licensed assets are unavailable, create modest original Blender/Unity meshes/textures or improve existing ones.
6. **No invented Unity APIs or blanket edits.** Discover Unity version, input system, URP pipeline and actual code contracts first. Prefer minimal localized improvements; avoid prefab/scene GUID churn, package upgrades, broad serialized changes or replacing established architecture.
7. **Ownership and Git truthfulness.** Work on a dedicated branch, keep real contributor authorship; never switch Git authors to impersonate teammates or make fabricated commits. Preserve `.meta` files. Do not force-push, squash assessed contributor history, reset dirty user work or commit unrelated files. Ask before touching collaborator-owned files that are actively changing.
8. **Always demonstrate before/after.** Record comparison screenshots with the same camera framing, a short player-motion/weapon-firing capture, and a representative 3-minute submission walkthrough. Do not report build/tests as passed unless actually run.
9. **Safe release discipline.** Fix critical usability, visual and functional issues first. Avoid high-risk additions (full-body rig, major gun framework, new levels, full lighting rebake, new enemy AI, inventory, multiplayer, large new packages) close to deadline.

## B. Desired finished experience (visual specification)

### B1. First-person viewmodel — **P0**

- Keep **first person**. The camera sees **convincing gloved hands + forearms + pistol**, not floating abstract polygon blocks. A full playable-body rig is *not* required.
- Improve hand shape: recognizable palm/thumb/fingers, defined glove silhouettes/knuckle pads, correct right-hand grip around the pistol, believable wrist alignment and sleeves. Left hand may appear in reload/interaction animation as appropriate; do not force an unnatural two-hand grip if the pistol pose is wrong.
- Match textures and lighting to the gun and setting. Subtle dark graphite tactical fabric, rubber/leather contrast, restrained seams, readable normals/roughness. Avoid giant glossy solid-black arms.
- Set first-person-only layers/render ordering and camera clip distances carefully if supported by the actual architecture; never introduce duplicated weapon shadows, invisible hands or clipping. Inspect aiming across FOVs/aspect ratios. Hide backfaces and handle left/right meshes correctly.
- Preserve the actual firing origin, aim ray/crosshair alignment and colliders. **Decorative weapon mesh must not control hit detection or push the player through walls.**
- If an existing rig can be retained, retarget and improve it. If no viable rig exists, use a small, stable first-person arms model with simple bone animation or conservative procedural transforms; do not spend the submission window creating a cinematic full-body rig.

### B2. Polished weapon and hand animations — **P0**

Implement or refine the following *as supported by existing mechanics*, with inspectable durations and blending:

| State | Requirement | Failure to avoid |
|---|---|---|
| Idle | Small breathing sway; stable sightline; minimal camera/weapon mismatch | Constant oscillation or motion sickness |
| Walk/run | Smooth low-amplitude weapon bob driven by actual speed; transitions interpolate | Snapping, exaggerated bob at zero speed |
| Shoot | Short pistol kick/return, slight slide movement, restrained muzzle flash; hand and gun move together | Weapon detaches, repeated recoil accumulates, flash always on |
| Reload | When reload already exists, synchronize hand/weapon movement, ammo update and visual state | Firing during incompatible state; animation changes ammo rules |
| Interact | Brief natural hand gesture only when safe; maintain working raycast door/console prompts | Gesture blocks interaction or permanently hides weapon |
| Damage/death | Reuse current feedback and transitions; do not interfere with health/death system | Controller left in broken state on restart |

Use Animator where a valid rig exists, or a clean additive procedural animator on a dedicated viewmodel root. Keep gameplay and cosmetic animation separated. Ensure sprint/walk/fire/reload cancellation logic is deterministic; avoid allocation-heavy per-frame code and magic values without serialization/documentation. Weapon overlay must not obscure HUD or crosshair.

### B3. Player navigation and game-space wayfinding — **P0**

Interpret **navigation** as both responsive player controls and clear spatial guidance:

- Verify stable first-person mouse look (pitch clamp, no double-applied rotation), predictable WASD acceleration/deceleration, collision and step handling, consistent jump/sprint if present, cursor capture/release and pause/focus behavior. Improve feel only with contained, configurable changes. Do not swap the player controller or input package.
- Preserve room order, existing barriers, console logic and door progression. Add subtle navigation cues such as sector labels, wall stencils, directional arrows, warning decals and orange/cyan indicators around *existing* interactables; do not alter routes or A* graphs solely for decoration.
- Make physical-console interaction readable: clear small-range highlight/outline or emissive activation state and an on-screen contextual prompt using the game's current keybinding (e.g. `E` **only if verified**). Do not invent objective counts.
- Ensure the player can see important exits and doors without visual noise. Differentiate locked, unlocked and opened states with material/emissive treatment **without changing actual gameplay permissions**.
- Maintain AI navigation behavior: preserve the four distinct autonomous agents, waypoint/A* decisions, dynamic door blocking and their path-following. Asmadala's movement/animation polish should improve turning and transition smoothness without changing tactical scoring.

### B4. Environment/texture/visual asset audit — **P0/P1**

- **Keep and refine** successful sci-fi modular wall/floor forms. Add restrained beveled trims, panel seams, bolts, vents, utility channels, plausible manufacturing details, safety stripe decals and readable labels at key nodes, not everywhere.
- **Replace only mismatching/low-quality images and materials:** stretched wall decals, muddy thumbnails, arbitrary icons, inappropriate texture photos, placeholder console screens, blurry signage, wrong render pipeline materials. Prefer texture atlases/material instances and existing meshes over thousands of additional GameObjects.
- Improve console unit in screenshot: communicate purpose clearly with a legible security-screen treatment, state feedback and subtle emissive border; avoid a flat bright-orange square that looks like a missing texture. Preserve all collider and trigger placement.
- Use physically plausible material differentiation: matte painted metal, dark rubber on handgrips, powder-coated panels, glass/screens, floor wear only where stylistically consistent. Prevent excessive metallic reflections and shiny walls.
- Use approved or licensed source textures; document import settings, normal maps, max-size and compression. Update `.meta` and prefab/scene references safely.
- **Do not silently delete** source images/textures/models that are still referenced, or remove assets needed for the assignment's two-original-model requirement. Verify dependencies first.

### B5. Lighting and URP presentation — **P1**

- Confirm active URP renderer assets and shaders before changes. Eliminate pink materials and inconsistent exposure.
- Establish a readable base ambient lighting setup, then add modest cold facility overheads and purposeful warm security accents. Use soft shadow depth to make walls/props dimensional, but keep door edges and enemies visible.
- Configure post-processing sparingly: mild ambient occlusion (if supported), subtle tonemapping/color adjustments, low bloom on actual emissive signage only. Avoid extreme fog, vignettes, overexposure, crushing black levels or a drastic art-style shift.
- Use existing baked/mixed lighting where reliable; do not trigger an expensive full rebake late in development unless necessary and time permits. Check static flags and dynamic drone/weapon visibility.
- Check FPS and shadow/light budgets. Avoid unnecessary realtime shadow-casting lights, huge texture sizes, transparent overdraw and excessive draw calls.

### B6. HUD and screen composition — **P0**

The reference screenshot has clipped text/panels on the **left** and dense tiny information in the corners. Fix Canvas anchors/scaling first:

- Top left: sector/room name fully visible with margins; no clipped label.
- Top right: progress/override, consoles and active drones legible at standard 16:9 and a smaller desktop resolution. **Keep the actual counter semantics and values from the runtime**; do not replace 01/08 or 0/4 with guessed values.
- Bottom left: health bar/value visible (not cropped), clear safe margins.
- Bottom right: weapon/ammo and control hints legible, appropriate hierarchy; labels match real input bindings (`R` only if functional).
- Crosshair stays center, unobstructed. Keep UI small and tactical but not microscopic; improve contrast, consistent typography and hierarchy. Use Canvas Scaler appropriately and use anchors rather than pixel-fixed positions.
- Optional decorative HUD textures/icons should be vector/clean at target resolutions, not generic stock screenshots. Keep art consistent with the restrained security-facility UI.

### B7. Enemy/drone visual movement — **P1**

- Confirm each drone model prefab, pivot/orientation, animation/movement component, collider and navigation agent are aligned.
- Smooth steering, look rotation and animation state transitions on existing route segments. Avoid robotic zero-time snaps, foot/sliding-equivalent motion, wall clipping, or animation that decouples from actual speed.
- Preserve each of Scout, Interceptor, Guard and Flanker behaviors, health/combat and AI decisions; improve *visual output*, not the decision rules unless a verified defect requires a surgical fix.
- Avoid changing drone speed/difficulty unintentionally when adjusting visual interpolation.

---

## C. Required implementation sequence and stop gates

### Phase 0 — Baseline reconnaissance (no edits)

1. `git status --short --branch`; capture branch and latest commit; check whether there are local modifications. If dirty, preserve them and ask how to proceed before potentially destructive operations.
2. Identify Unity editor version, URP renderer settings, available models/textures/licenses, gameplay prefabs, animations, HUD canvases and active main level. Do **not** assume class names or paths from an older plan.
3. Run a baseline Play Mode session or clearly record why unable. Screenshot at the same view as the reference, check Console errors, save runtime UI counts and note current build target.
4. Produce **asset audit + issue inventory**, ranked P0/P1/P2, and a concrete file-change plan with likely risks.
5. Gate: know the actual playable path, all critical gameplay systems, and where the viewmodel/HUD are created before changing anything.

### Phase 1 — High-impact P0 visual fix (must complete)

1. Fix Canvas anchors/scaling and clipped HUD panels; ensure legible interaction instructions.
2. Improve existing viewmodel hand meshes or introduce a modest original/appropriately licensed gloved-hands mesh. Correct mesh pivots, glove materials, finger positions, grip, FOV/scale, draw order and weapon attachment.
3. Polish recoil, movement sway, muzzle flash and reload visuals only in agreement with current combat state logic. Keep cosmetic and gameplay responsibilities separate.
4. Upgrade the orange console display and any clearly broken/flat imagery, while keeping interactions and colliders unchanged.
5. Add restrained, useful sector/door/console directional cues; make sure player navigation, progression and enemy pathfinding remain unchanged.
6. Gate: all P0 acceptance tests pass; capture before/after screenshots of the same framing.

### Phase 2 — Controlled P1 polish (only if P0 is green)

1. Improve wall/floor material definition and selected props/decals in the most-visible rooms.
2. Refine URP environment lights/shadows/post effects with FPS measurement.
3. Improve drone animation/turning transitions **only** if the relevant integration is safe and tested.
4. Gate: no shader regressions, material errors or meaningful frame-time degradation; maintain coherent visual identity.

### Phase 3 — Regression, evidence, release (mandatory)

1. Full level playthrough: player spawn -> movement -> shooting -> door/console progression -> drones -> final exit/win; also death/restart path.
2. Edge cases: shoot during reload; interact while aiming; sprint + jump; pause/window focus; door closes near AI; drone defeated mid-route; repeated console use; viewport resize.
3. Visual matrix: 1920x1080, 1280x720, Game view vs standalone build; verify no HUD clipping and no viewmodel/weapon clipping. Check a bright room and a dark room.
4. Check Unity Console for **new** errors/warnings, broken/missing `.meta`, pink URP materials, missing animations/assets, prefab overrides, and build logs.
5. Use Unity Profiler or comparable metrics to compare baseline/final FPS, draw calls or frame time on the same hardware/quality settings; report actual numbers or state not measured.
6. Commit in small truthful phases and provide screenshots/short video evidence, changed-files summary, observed pass/fail log, attribution/license manifest and any follow-up risks.
7. Gate: release-ready means a demonstrated working build, not merely editor compilation.

---

## D. Acceptance checklist (definition of done)

- [ ] First-person gloves appear anatomical and actually grip the pistol; no primitive-looking slabs, floating hands or broken wrist intersections.
- [ ] Smooth idle/walk/fire visual changes; reload matches implemented ammo logic; movement/shooting mechanics unchanged.
- [ ] Player can navigate all intended sectors, identify interactable consoles/doors and complete the existing objectives.
- [ ] Main HUD fully visible and readable at 1080p and 720p; no clipped health/sector panel, wrong numbers or inaccurate hints.
- [ ] Console screen has purposeful design rather than a blank orange placeholder; retains interaction and state changes.
- [ ] Selective visual replacements are coherent, legal to use, correctly referenced and documented; no broken/missing materials.
- [ ] URP shading and lighting work; no pervasive pink assets or crushed/overexposed scene.
- [ ] Enemy drones continue to pathfind, fight and behave as before; new motion is smooth without changing agent behavior.
- [ ] No new Unity Console errors; builds on the assigned target; smoke-tested gameplay and loss/restart.
- [ ] Version control changes are reviewable, `.meta` files tracked, no unauthorized dependency/engine upgrades.
- [ ] Report lists actual QA results and remaining known limitations; include screenshots and model/asset attributions.

## E. Contributor ownership and handoffs

| Owner | Assessed work to protect | Polish handoff |
|---|---|---|
| **Pamudi (Student 1 / World Builder)** | room layout, NavMesh, lighting, textures | asset/material audit, level signage, lighting and wall/floor cohesion |
| **Nimsara (Student 2 / Systems Engineer)** | player/physical interaction, door/console physics | integrate viewmodel and animations without breaking controller, shooting, doors and interactions |
| **Nimthara (Student 3 / Core Developer)** | two original custom models, topology, UVs/import evidence | hands/weapon/console mesh and material refinement; preserve true original-model authorship |
| **Asmadala (Student 4 / Agent Controller)** | AI route movement, rotation and animations | visual drone turn/state polish, maintaining path-array logic and combat |

**One coding agent does not earn four people's author credits.** If work crosses ownership boundaries, prepare reviewable patches and have each *actual contributor* review/implement and commit their work. Document handoffs. Do not spoof Git author names.

## F. Implementation quality rules for the coding agent

- Before any edit, state the **exact files you verified** and how they relate to the visual/gameplay system.
- Prefer material/mesh/animation prefab changes over replacing mechanics. Make movement bob, recoil, sway and light values serialized/tunable with safe defaults.
- Avoid modifying scene YAML by naive regex; edit using Unity-aware tools and re-open the scene to confirm serialization. Include `.meta` on all new assets.
- Never delete assets automatically without a dependency scan/reference check.
- If Blender unavailable, do not fabricate Blender claims or ship pseudo-anatomical primitive blobs as a 'finished' glove. Prefer improving an existing usable model and report the limitation.
- Do not rely on external asset stores, machine-generated texture URLs or unsupported dependencies without a rights and import review.
- Test after **each** phase, not only after all edits. Revert localized regressions, not user changes.
- Limit work to demonstrable mark-improving polish. If time becomes tight: **HUD + arms/grip + fire/reload transitions + stable build** outrank decorative lighting and props.

## G. Required response format from implementation agent

At the end of **every phase**, output:

1. **Summary:** what was implemented and why it improves the assignment.
2. **Exact changed files:** including new/deleted assets and corresponding `.meta` files.
3. **Before/after proof:** comparable screenshots or recorded capture paths.
4. **Tests:** explicit command/action, environment, pass/fail; clearly mark tests that could not run.
5. **Performance:** measured results if available; note unmeasured metrics honestly.
6. **Asset provenance:** team-authored vs licensed third-party, license and attribution location.
7. **Git:** actual branch/commit hash/true author if committed, plus instructions for handoff.
8. **Known issues and next safe action.**

After Phase 0, stop for confirmation if: current repository has unsafe uncommitted edits, asset rights are unclear, a full rig/package upgrade would be required, or proposed visual changes would alter core gameplay/IS behavior. Otherwise continue sequentially and keep the next deliverable playable.

---

## H. Copy-and-paste execution prompt

> You are a senior Unity URP FPS technical artist and gameplay integration engineer working inside the **existing Shift Four repository**. Follow this document as binding implementation requirements. Begin with a strict audit of Git state, Unity version, active scenes, player/weapon prefabs, viewmodel rig, relevant scripts, UI anchors, imported textures/images and drone animation systems. Use the supplied Shift Four screenshot to identify the mismatched block-like FPS hands, weak console-screen presentation, flat industrial surfaces and clipped/small HUD. Make a prioritized P0/P1 plan with verified file paths, then implement changes in small reversible stages. Keep first-person hands + weapon rather than a full-body player. Make hands believable and gripping correctly; polish animations (idle/walk/fire/reload/interaction as supported), player navigation feel and level wayfinding; replace only low-quality/inappropriate existing image/texture assets with theme-consistent, lawful alternatives. Preserve approved blue/charcoal/orange styling, functioning controls, doors/consoles/combat, all objective counters, four distinct enemy AI policies and A* behavior. Protect original Blender model evidence and genuine teammate authorship. Keep URP materials valid and avoid dangerous late-stage re-architecture. Fix clipped HUD at 1080p and 720p. After every phase actually test gameplay and Console, capture comparative screenshots, record changed files and provenance, and report accurate test results. Do not claim the build is verified unless you ran it. Apply the required stop gates. Finish with a stable reviewed build, minimal clean commits, a regression report and the evidence needed for SE3032 submission. **Do not simply write another plan; audit and implement when safe, documenting exactly what happened.**
