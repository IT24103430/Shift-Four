# Task 5 — Active workspace focused verification

Active project: `/Users/dev/Desktop/game/Shift-Four`

Branch: `feature/progressive-drone-intelligence-workspace`

Unity: 6000.6.4f1.

Task 5 was already implemented in this checkout. A fresh verification confirmed all 72 C# files match the previously validated implementation; no production changes were needed. The active project was open in Unity (recorded editor PID 82129), so focused tests ran against the byte-identical `Shift-Four-Progressive` verification worktree. The active editor was left running. Results were written directly into this active project's evidence directory.

| Fresh run | Passed | Failed / skipped | Execution seconds |
| --- | --- | --- | --- |
| [FocusedEditMode](FocusedEditMode.xml) | 13/13 | 0 / 0 | 0.105 |
| [FocusedPlayMode](FocusedPlayMode.xml) | 14/14 | 0 / 0 | 3.839 |

Both Unity processes exited 0. Raw XML and corresponding local logs are retained. [TEST-MATRIX.csv](TEST-MATRIX.csv) lists all 27 focused cases. The previous unchanged-source full suite also passed 225 Edit Mode and 122 Play Mode cases; it was not repeated during this focused verification.

Coverage includes all noise categories, range and sensitivity, distance attenuation, wall occlusion, disconnected sealed areas, actual closed/open MainLab gate behavior, logical gate isolation despite native connectivity, fixed expiry and stale/replayed/future event rejection, uncertainty, source movement/destruction, listener cleanup, per-frame/second budgets, gate-version invalidation and sound-only firing prevention for all four drones. The estimate helper is deterministic and does not change UnityEngine.Random state. Visual memory fields remain unchanged by hearing.

## Fresh performance sample

[hearing-burst.csv](generated/hearing-burst.csv) reports:

- Reports: 1000
- Evaluations: 2
- Physics calls: 6
- Budget drops: 998
- Memory capacity: 4
- GC.Alloc events: 16
- Elapsed milliseconds including measurement overhead: 2.0204

The accepted path allocates through the existing graph routing API; this is not an allocation-free claim. Each sensor defaults to two evaluations/frame and eight/second, with four observations and fixed replay/physics buffers. No hearing Update/global actor search loop is used.

## API and limits

All four drones expose `DroneBrain.Hearing`. `Hearing.TryGetLatest(out SoundObservation)` returns an immutable approximate position, category, strength, uncertainty radius, emission timestamp and expiry. It stores no live source/player reference and cannot grant visual detection or firing authorization. Scout uses the shared observation for its existing investigation option; the other individual policies remain unchanged.

The implementation assumes MainLab's planar floor, rejects graphs above 32 nodes, and uses a conservative single-ray transmission approximation. It rejects inaccessible areas, embedded/full-buffer endpoints and inaccessible uncertain estimates; overload drops events. Existing graph/native route checks and motor collision protection continue to enforce investigation reachability. See [full API and limitations](../../../DRONE_HEARING.md).

## Preservation

[integrity.json](integrity.json) confirms:

- All 72 tested C# files remain unchanged in both active and verification checkouts.
- All 131 protected files and all difficulty profile assets remain unchanged.
- The active project's pre-existing staged index entries are preserved exactly.
- No temporary test scenes remain and the verification worktree's prior Git status is restored.
- Unity's incidental verification-worktree settings changes were archived, then its exact pre-test settings bytes restored. Active project settings were not modified.

Only this fresh verification evidence and the active-project path note in the hearing handoff were added/updated. MainLab, drone prefabs, firing/LOS code and existing staged content were not changed. No commit or push.
