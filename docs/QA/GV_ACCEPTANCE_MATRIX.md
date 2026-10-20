# Core developer GV acceptance matrix

This matrix preserves its original execution record. Current model metrics, known UV findings and the remaining personal/human checks are tracked in [GV model completion](../GV_MODEL_COMPLETION.md).

Executed 2026-10-14 with Unity 6000.6.4f1 and Blender 4.5.8. `GV_AUTOMATED_RESULTS.md` records 154 passing assertions from `GameVisualAcceptance.RunAll`. Headless Play Mode is real runtime verification; it is not a human playthrough or rendered UI acceptance.

| Test | Reproducible setup / steps | Expected result | Actual observed result | Status |
|---|---|---|---|---|
| Compilation/API | Import isolated copy in exact Unity version | No C# errors/new obsolete warnings | Final acceptance compiled without C# errors/warnings | PASS |
| Installer contracts | Run RunAll; install/migrate twice | Configure/SetGateOpen/typed edge contract; stable hierarchy | Four doors/five nodes; repeated migration retained hierarchy/node positions | PASS |
| Drone-only | Fresh state each id, report drone death | Assigned gate closed | All four identities tested | PASS |
| Console-only | Fresh state each id, activate console | Assigned gate closed | All four identities tested | PASS |
| AND / both orders | Complete missing prerequisite in both orders | Only assigned gate unlocks | 8 combinations tested | PASS |
| Other gates | Complete one pair, inspect other ids | Other gates remain locked | Pure state checks and runtime first-condition barrier checks passed | PASS |
| Duplicate reporting | Repeat console and Health damage/death | No double count | Counts exactly 4 consoles/4 deaths at completion | PASS |
| Initial locks | Enter Play Mode | Colliders/obstacles enabled, graph closed | All four locked | PASS |
| Bolt-first / axes | Complete pairs, sample before .4s and after 1.6s | Bolts first, panels move local X | Both 0° and 90° gates passed | PASS |
| Stationary roots | Compare gate root position/rotation before/after | No movement | All unchanged | PASS |
| Synchronization | Inspect opening and fully open states | Collider/obstacle block until graph release | All four synchronized after animation | PASS |
| A* / invalidation | Closed/open edges, duplicate SetGateOpen, reclose | No route/route, Version only on changes | Standalone graph and scene exit route passed | PASS |
| Physical opening | Raycast across closed/open gate; move CharacterController in 20 steps | Closed blocks, open has no hidden collider; capsule crosses | All four gates passed | PASS |
| NavMesh opening | Calculate path across each gate closed/open | Closed incomplete, open complete | All four passed after carving update | PASS |
| Old shortcut | Inspect sealed wall and old door roots | Permanent wall, no moving shortcut | Migration assertions passed | PASS |
| Drone health/death | Damage actual drone Health twice | One event, stop motor, progress once | Actual death events and counts passed | PASS |
| Drone movement | Disable decisions, command live Guard route, wait | Position advances | Guard moved toward reachable cover | PASS |
| Server cover | NavMesh sample/path to both cover destinations, ray through rack | Reachable floor destinations and LOS block | Both destinations and rack ray passed | PASS |
| Player health/death | Kill player, restart with decisions isolated | Defeat then maximum health | Passed | PASS |
| Early exit | Call TryExit before objectives | No victory | Denied | PASS |
| Victory | Complete all four pairs, TryExit | Victory latched | Passed | PASS |
| Restart outcomes | Restart after victory and defeat | New objectives, health, closed gates | Passed for MainLab build-index reload | PASS |
| Imported art | Inspect FBX leaves and rotor mesh names | 2m x 3m leaves, four rotors | Bounds and hierarchy checks passed | PASS |
| Blender outputs | Execute generator; inspect metrics and renders | Editable blends, FBX, UVs, sensible mesh counts | Guard 3,200 triangles; door 2,484; all meshes UV-bearing; renders inspected | PASS |
| Asset references | Scan GUIDs/metas; inspect scene missing scripts | No duplicates/missing references | No duplicate GUIDs/missing metas/unresolved GUIDs; no missing scene scripts | PASS |
| Editor runtime errors | Inspect final log | No avoidable errors | UnityEditor.Search indexing exception remains; no game-code exception | ISSUE |
| Full combat progression | Manually walk/fight Checkpoint → Server → Control → Storage → Exit | Playable encounter loop | Automated gate traversal only; combat playthrough not performed | NOT_VERIFIED |
| HUD/prompt/cursor visuals | Play at 1280x720 and 1920x1080; use F3/E/Escape/R | Readable UI, correct room, indicators and cursor | Source implemented; rendered/UI interaction not assessed | NOT_VERIFIED |
| Name fallback restart | Load a valid name-only scene configuration and press R | Name overload succeeds | Only build-index reload exercised | NOT_VERIFIED |
| Performance | Profile desktop play with all assets/lights | Acceptable frame time/draw calls | Geometry budgets inspected; no profiler capture | NOT_VERIFIED |
| Genuine student evidence | Open/edit blends and capture actual topology/UV/import/gameplay | Defensible personal contribution | Procedural provenance recorded; student evidence outstanding | NOT_VERIFIED |

Run command and copy isolation precautions: `docs/members/CORE_GV_IMPLEMENTATION.md`. Never mark unperformed manual checks PASS. Retain failed-run notes in progress history: missing Restart harness API, native property-block initialization, FBX pivot normalization, and combat interference with restart health assertions were corrected before final acceptance.

Additional regression: `GameVisualAcceptance.VerifyUntouchedPrefabs` hashes/compares all four agent prefabs around installation. Observed byte-for-byte preservation for Scout, Guard, Interceptor and Flanker: PASS (4 additional assertions).
