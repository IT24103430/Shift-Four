# Task 8 — Enhanced Scout: pending owner review

**Status:** focused proposal and isolated rehearsal. The active `ScoutBrain.cs`, Scout prefab, difficulty asset, MainLab and other owned assets have not been changed. This task issued no commit, staging or push commands and performed no scene generation or NavMesh bake. Concurrent workspace commits are recorded separately in the integrity evidence.

## Approval verification

Pamudi owns Scout's policy and Checkpoint integration, per `TEAM_RESPONSIBILITIES.md`, `members/01-world-scout.md` and `BRANCHING_STRATEGY.md` §8. Those documents identify responsibility; they do not approve this task. The only existing Scout authorization found is the narrow Task 5 hearing adapter described in `DRONE_HEARING.md` and `evidence/drone-hearing/README.md`. No approval covering Task 8's policy expansion or related prefab properties was found in the available conversation or repository records. That earlier adapter approval is not treated as broader authorization.

The user's explicit fallback is therefore followed: prepare a focused patch and handoff. Pamudi should review the proposed policy and behavioral evidence before it is applied to owned production files. No external messages were sent. No prefab property change is proposed; movement/animation integration is already supplied by the shared services.

## Reviewable delta

[evidence/enhanced-scout/EnhancedScout.task8.patch](evidence/enhanced-scout/EnhancedScout.task8.patch) contains:

- `Assets/Scripts/AI/Agents/ScoutBrain.cs`: the only production change.
- New `Assets/Tests/PlayMode/EnhancedScoutPlayModeTests.cs` plus stable `.meta`: eight MainLab cases.
- Two narrow expectation updates to existing hearing/awareness tests. Scout now pauses to orient before investigating; unchanged-policy comparisons still apply to Guard, Interceptor and Flanker. Visual-memory and no-fire assertions remain in place.

The full proposed source is also available as [ScoutBrain.cs.txt](evidence/enhanced-scout/ScoutBrain.cs.txt). Its diff is against the captured working source, including the already-approved Task 5 adapter, rather than an older historical version.

## Resulting behavior

The existing two patrol destinations remain `Home + (-4,0,-3)` and `Home + (4,0,3)`, with the existing 1.6 m arrival threshold. Genuine visibility selects the existing chase priority of 80. Recent visual memory has priority 60, sound investigations 45, and patrol 10. A suspicious sound first selects a stationary orientation hold at priority 30.

Scout consumes `DroneAwareness` and the typed, uncertain `SoundObservation` produced by `DroneHearingSensor`; it does not subscribe to exact-position shot events or retain sources/transforms. A single weak footstep increases suspicion without immediately pursuing. Repeated walking or sprint-category events can cross the approved suspicion/investigation thresholds. A strong gunshot progresses through Suspicious → Investigate after the shared minimum dwell. Sprint is supported as a noise category; the project currently has no sprint input mechanic, and this patch adds none.

While suspicious, Scout turns toward the estimated sound direction. Only an idle motor with no route permits policy yaw; angular change is capped by the actual NavMeshAgent angular speed. The motor retains travel yaw. Investigation destinations must pass local NavMesh attachment, complete native path, body clearance and the existing gate-aware graph route. Invalid goals produce a listening hold while the unchanged evidence timer expires.

Search retains the original last-sighting priority over sound. It considers the evidence position and at most two local offsets, each 1.5 m away. Each candidate passes the same destination checks. The shared service owns the deadline: the validated default search duration is 5 s, sound expires 5 s after emission, visual memory expires 6 s after actual visibility, and arrival/polling never extends either lifetime. New credible evidence may begin a new bounded investigation. Expiry selects the existing home post, then the original patrol loop. A blocked return cannot hold the policy indefinitely because awareness also bounds ReturnToPost.

Hidden `playerPosition` and `distance` arguments are unused by policy decisions. `DroneBrain` still records visual memory only on actual `CanSeePlayer` success and retains its original visibility, range, cooldown and fire-time cover checks. Hearing or awareness state cannot authorize firing. No other drone policy, shared brain/motor/awareness implementation, health, attack tuning, gates, A*, geometry or baked data changes are included.

## Configuration and animation

The existing Scout resource supplies **100 HP**, **1.8 m/s patrol**, **2.2 m/s investigation**, **2.6 m/s chase**, acceleration 8 m/s² and angular speed 120°/s at runtime. Authored prefab values remain unchanged, as intended by Tasks 2 and 7. Awareness selects movement mode through the existing shared adapter; accepted motor velocity supplies the existing hover/travel/braking animation feedback. The existing death path disables hearing and awareness and stops the motor.

“Easy–Medium” is a behavioral target, not an additional enum in this project: lower Scout health/speeds, evidence accumulation, orientation dwell, bounded local search and unchanged visual/combat restrictions provide that target. Human difficulty/visual acceptance remains part of Pamudi's review; automated tests do not establish subjective difficulty balance.

## Validation and MainLab demonstration

**115/115 focused candidate Edit Mode and 99/99 focused Play Mode tests passed**, including all eight new Scout cases. See [the evidence README](evidence/enhanced-scout/README.md) for actual results, failed iteration explanations, source hashes and exact commands. Tests run on Unity **6000.6.4f1** in a disposable project copied from the active Task 1–7 working source. Candidate edits exist only in that copy and this proposal package.

Run from the repository root:

```sh
python3 docs/evidence/enhanced-scout/rehearse.py --demo-only
```

This creates a fresh disposable Unity project, applies the candidate there and runs `MainLab_Demonstration_GunshotTurnTravelSearchDecayReturn`. It loads the saved MainLab without regeneration, emits a normal typed gunshot at a reachable nearby location using production hearing defaults, checks bounded orientation, enables the real motor, and follows actual movement through investigation, local search, suspicion decay, return and patrol. It asserts no visual memory or player damage from the sound alone. A CSV records state, decision, suspicion, position and motor speeds. The script prints the disposable project and `ReviewResults` paths. First use may take several minutes to import Unity assets.

For interactive inspection, run the script with `--prepare-only`, open the printed project in Unity 6000.6.4f1, and run the named test in Window → General → Test Runner → PlayMode. This is an automated MainLab demonstration; no human playthrough or rendered video is claimed. Tests freeze unrelated brains/player input and control simulation delta, so they demonstrate Scout policy and real movement reproducibly rather than overall game balance. All fixture objects and gate assertions are runtime only; scenes are not saved.

After owner review, check and apply the patch from the repository root with `git apply --check docs/evidence/enhanced-scout/EnhancedScout.task8.patch`, then `git apply ...`. Rebase/retest if the working Scout or shared service contracts change. The patch is not applied by this handoff.
