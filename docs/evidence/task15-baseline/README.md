# Task 15 baseline evidence

[Baseline handoff](../../LATEST_BASELINE_TASK15.md) · [proposed Tasks 16–22 file/review matrix](PLANNED_TASKS_16_22.md)

Exact committed source: **`5a5cf7ad9c389576db0f2de1407c936289bc32ab`**, selected from `feature/progressive-drone-intelligence-workspace`. Clean new worktree: `/Users/dev/Desktop/game/Shift-Four-Security-Baseline`, branch `feature/shared-security-expansion-baseline`.

Fresh Unity **6000.6.4f1** complete results: **306 Edit Mode + 159 Play Mode = 465 passed**, zero failed/skipped. Play Mode used graphics so all existing rendered fixtures could run. No existing test was changed or supplemented with proposal code. No Task 16 feature was implemented.

## Source and local work

- `baseline.json`: exact source/feature branch/worktree, engine and disposable test project.
- `baseline-file-sha256.json`: all 345 Assets/Packages/ProjectSettings files.
- `git-before.txt`, `refs-before.json`: local/cached remote branches, history and unique commits; no fetch performed.
- `original-worktrees-before.json`: initial status and every original dirty file's content hash, including untracked work.
- `local-work-review.json`: content comparisons for the three older dirty worktrees; no overlay imported.
- `drone-prefabs-and-profiles.json`: actual existing agent prefab script/GUID inventory, serialized legacy values and matching runtime profile settings.
- `integrity.json`: clean feature-worktree state, source/asset preservation, original worktree/ref/stash checks and test summary. Original root checkout status changes only for the delivered Task 15 docs/evidence; the separate feature worktree stays clean.

The original workspace's `5a5cf7a` commit contains the previous Task 14 additions. The locally unmerged `b6e0bbf` native-partial-path test improvement remains a separate review item. No commit, staging, push, merge, cherry-pick, reset, discard or stash mutation command was issued.

## Test evidence

- `EditMode.xml/.log`, `PlayMode.xml/.log`: actual complete runs from the selected source.
- `test-summary.json`, `TEST-MATRIX.csv`: counts and every test case.
- `commands.json`: exact Unity arguments and output environment.
- `generated/`: newly produced asset/override audit, runtime bindings, sound/awareness/speed checks, post/console limitations, collision, prediction, graph, mission, allocations and performance fixture output.
- `review-project.txt`: disposable Unity copy used for verification. Rendering fixtures also generated raw images under its `ValidationCaptures/`; no new image is invented or presented as a manual playthrough.
- `verification-settings-generated.patch`: platform defaults Unity serialized only into the disposable settings copy. Original and feature-worktree settings remain unchanged.

Known console-source and Guard/Flanker Home-clearance gaps are reported explicitly. The passing suite does not imply approval/application of the Task 8/9/11 policy drafts or the Task 14 console-anchor patch, or completion of human fairness acceptance.

## Reproduce

```sh
python3 docs/evidence/task15-baseline/reproduce.py
```

This uses `git archive` to read the exact commit's three Unity source folders into a disposable project, then runs both complete existing suites. It does not depend on a later branch's working files or modify a checkout. `--prepare-only` extracts without launching Unity; that path was exercised and its 345 files matched the selected baseline byte-for-byte: `reproduce-prepare.txt`, `reproduce-prepare-check.json`. Fresh Unity imports may take several minutes.

Inspection/report date is 20 October 2026 in Asia/Colombo; NUnit timestamps in raw XML are UTC. Simulated fixture durations are not manual encounter timings.
