# FPS feature branch handoff

These branches preserve the verified FPS baseline at `2aece7603222c2fc3964779cf0da75e9a9456dd5` and the actual preparation, jumping, architecture and pistol checkpoints. They form a dependency chain:

| Branch | Change | Depends on |
| --- | --- | --- |
| `docs/nimsara-fps-preparation` | Task 1 inspection and asset/test evidence | Verified baseline above |
| `feature/gv-nimsara-player-jumping` | Task 2 CharacterController jumping and movement tests | Preparation |
| `feature/gv-nimsara-modular-weapons` | Task 3 single hitscan weapon architecture and regression migration | Jumping |
| `feature/gv-nimsara-five-seven` | Task 4 owned Five-seveN prefab/definition, presentation, isolated range and tests | Modular weapons |

The repository ownership guide assigns player movement, collision, aiming/shooting and combat to Nimsara. Commits use `Nimsara-Jayarathna <nimsarajayarathna1@gmail.com>`, with ordinary current timestamps. The user explicitly authorized GitHub account `IT24103430` to push these Nimsara-authored branches. Commit authorship and the authenticated pusher are distinct. The existing commit history and other members' branches are preserved.

Final pistol verification: 329/329 project Edit Mode tests, 41/41 focused Play Mode tests and 50/50 selected original Play Mode regressions passed. The disposable test project excludes the already broken imported Samples; their Input System/TMPro source-project compile blockers remain. Native mouse/keyboard input was not synthesized. Exact earlier checkpoint results and limits are retained in each task report/evidence directory.

The remote integration branch is named `dev`. It has advanced since the verified FPS baseline; these branches do not merge or rebase that unverified change. Before integration, compare the baseline/integration history, coordinate shared-file reviews and run the combined project checks. No changes were pushed directly to `dev` or `main`, and no merge was performed. If reviewing the stack incrementally, compare each branch against its predecessor rather than treating each as independent work.

Task 5 (hands/animation) has not been implemented. The Five-seveN remains an explicitly gun-only viewmodel. MainLab, imported PolyOne assets and existing GUIDs were not modified by the FPS work.

Reports: [preparation](FPS_PREPARATION.md), [jumping](PLAYER_JUMPING.md), [architecture](FPS_WEAPON_ARCHITECTURE.md), [pistol](FN_FIVE_SEVEN.md).
