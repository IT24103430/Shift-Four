# Task 4 — Five-seveN evidence

Unity 6000.6.4f1, branch `feature/fps-player-weapons-prep`, base commit `2aece7603222c2fc3964779cf0da75e9a9456dd5` plus uncommitted Tasks 2–3. See [the implementation report](../../FN_FIVE_SEVEN.md) for use, architecture, exact changed files and limitations.

| Final run | Passed | Failed | Duration |
| --- | ---: | ---: | ---: |
| EditMode | 329 | 0 | 1.788 s |
| FocusedPlayMode | 41 | 0 | 2.348 s |
| OriginalPlayMode (selected regressions) | 50 | 0 | 108.705 s |

All final exit codes are zero. `commands.json` records exact final test arguments/filters, `test-summary.json` records NUnit XML root attributes, and `environment.json` records paths/output environment variables. The unsuffixed XML/log files are the final runs. Earlier passed fixtures/pose iterations remain separately labelled `attempt1` and `pre-final`.

Focused tests comprise nine new pistol Play Mode cases, all 17 Task 3 architecture cases and all 15 Task 2 jumping cases. Edit Mode adds six pistol asset cases. Original regressions cover combat, sound, attribution, cover, death/restart, difficulty, player noise, MainLab and four-room behavior; the full Play Mode suite was not rerun.

Tests run in the disposable preparation project with the unchanged, previously broken Samples excluded only there. Source-project Input System/TMPro sample blockers remain unresolved in this task. Unity's disposable ProjectSettings defaults differ as recorded in `verification-only-projectsettings.patch`; no worktree project setting was changed.

`task3-baseline.json` captures starting source hashes/status. `final-source-hashes.json` and `integrity.json` confirm two existing script modifications, 32 new source/asset/meta files, no deleted existing source, no existing GUID changes and unique new GUIDs. All PolyOne originals, MainLab, PlayerController, Health, SoundEvents, packages, settings and existing tests are unchanged relative to that snapshot. `muzzle-flash-urp-validation.patch` records Unity's validation of the new owned flash material, adopted from the tested copy; it does not modify the vendor material.

`renders/` contains actual game-camera render targets, front/side assembly views, matched no-gun/no-flash controls, muzzle metadata and the pixel summary. Original TGA files were converted to PNG with macOS `sips` for viewing, without content edits. The render target excludes OnGUI HUD. The accepted-shot pose is paused only for a matched flash comparison; a separate real-time test verifies 35 ms flash expiry and recoil recovery. Native mouse/keyboard input was not synthesized.

The two failed authoring logs are retained. Attempt 1 exposed the disabled Particle System module; the implementation uses an additive quad without enabling that module. Attempt 2 successfully created the weapon assets but hit Unity's untitled-scene restriction while starting the range. The final `AssetBuild.log`/exit code and `range-build-command.json` record successful range completion through `CreateRangeOnce`, preserving the already-generated asset GUIDs.

No commit, push, merge, reset or stash was performed.
