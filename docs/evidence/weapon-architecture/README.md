# Task 3 verification evidence

Unity 6000.6.4f1; source branch `feature/fps-player-weapons-prep`, base commit `2aece7603222c2fc3964779cf0da75e9a9456dd5` plus the verified, uncommitted Task 2 changes. No commit/push/merge was performed.

| Final run | Passed | Failed | Duration |
| --- | ---: | ---: | ---: |
| EditMode | 323 | 0 | 1.889 s |
| FocusedPlayMode | 32 | 0 | 1.049 s |
| OriginalPlayMode (selected regressions) | 50 | 0 | 109.385 s |

Each final run has XML, editor log and exit-code files. All final exit codes are zero. `test-summary.json` extracts the actual XML root attributes; `commands.json` records exact argument arrays and filters, and `environment.json` records test-output environment variables. The 32 focused cases comprise 17 new weapon tests plus all 15 Task 2 jumping tests. The 50 original cases cover combat, sound, attribution, cover, death/restart, difficulty, player noise and room integration. This selection does not represent a rerun of the entire Play Mode suite.

Tests run in the disposable verification copy from Task 1. The unchanged imported Samples have pre-existing Input System/TMPro compilation errors and remain excluded only there. `verification-copy-differences.json` lists excluded paths and confirms matching final source otherwise, apart from Unity platform-default serialization in the disposable `ProjectSettings/ProjectSettings.asset`. `verification-only-projectsettings.patch` records that difference. The worktree's settings, scene, imported assets and existing GUIDs remain unchanged.

`task2-baseline.json` records starting status/source hashes; `PlayerController.task2.cs.txt` is the starting player source. `final-source-hashes.json` and `integrity.json` record the final preservation check. Early failed fixture attempts and the pre-final Edit Mode pass are retained with distinct filenames; the unsuffixed run files above are the final results. Batch-mode cursor limitations and the resulting command/input boundary are explained in [the implementation report](../../FPS_WEAPON_ARCHITECTURE.md).
