# Final unified validation evidence

See [the final report](../../FINAL_PROGRESSIVE_DIFFICULTY_VALIDATION.md) for the completion matrix, values, gameplay observations, performance, required reviews, PR split and viva explanation. [Task 13 integration](../../FOUR_ROOM_INTEGRATION.md) records the resolved asset/binding contract.

## Active build

Unity **6000.6.4f1**, **306 Edit + 159 Play = 465 passed**, zero failures/skips. These are complete suites against the current active source, with graphics enabled for Play Mode. Task 8 Scout, Task 9 Guard and Task 11 Flanker proposals were not applied. No existing assertion was weakened.

- `EditMode.xml/.log`, `PlayMode.xml/.log`: raw complete results.
- `TEST-MATRIX.csv`, `test-summary.json`: actual counts and every case.
- `commands.json`, `review-project.txt`: exact commands, evidence environment and disposable verification project.
- `validated-source-sha256.json`, `before.json`, `integrity.json`: all 86 C# sources, baseline and saved-asset integrity. The optional encounter test was removed from the disposable Assets folder after its run. Active Assets/Packages/ProjectSettings content was preserved except for the new validation test/meta.
- `verification-settings-generated.patch`: Unity populated platform defaults only in the disposable settings copy. Active settings did not change.
- `generated/`: final-run raw CSV/text output for health, latency, policies, sound, allocation, collision, graph, prediction, tactical routes, mission and performance.
- `performance-summary.json`: statistics computed from actual 720-frame and 240-frame recordings. All frame timings are paced Editor observations; CPU scopes are inclusive.

The source Git head remained `6d0a28e` during Task 14. Earlier Task 13 results were committed externally while work continued; this agent issued no commit, staging, push or merge command.

## Additional diagnostics and proposals

`encounters/ScenarioPlayMode.xml/.log` records **one additional passing diagnostic case**, containing four fresh-room scripted windows. It is excluded from the 465 active-suite total. `encounter-summary.csv`, per-agent traces, `context.txt` and `trace-summary.json` identify actual travel, decision changes, motor planning/request counters, player damage and limitations. No human inputs were simulated. The optional harness is stored under `harness/` and never installed in the source checkout.

`ConsoleAnchors.task14.patch` is an **unapplied five-path review proposal**, tested in another disposable project. [Review instructions and failed iterations](CONSOLE_ANCHOR_REVIEW.md) distinguish its **3 final passing cases** from the active build and retain both earlier failures. The 2.5 m proposal passed actual activation/receipt, identity, duplicate suppression, mission and closed-gate assertions across three fresh scene loads. No sensor safety rule, range, damage, gate or policy was changed.

## Actual visual evidence

Open [index.html](index.html) to filter **23 actual Unity captures** by agent. `captures.json` contains original TGA and converted PNG hashes. The viewer labels synthetic awareness cues, accepted firing/death fixtures, temporary prediction overlays and native-policy encounter windows. It does not present them as a manual full-game playthrough. Raw encounter TGAs remain in `encounters/`; the other full-suite TGA files remain in the printed disposable project's `ValidationCaptures/` directory.

## Reproduce

```sh
python3 docs/evidence/progressive-difficulty/reproduce.py
python3 docs/evidence/progressive-difficulty/run_encounters.py
python3 docs/evidence/progressive-difficulty/rehearse_console_anchors.py
```

The first command verifies all current active tests. `--scenarios-only` selects its measurement, mission, sensing and rendered subset; `--prepare-only` produces a review copy. The second command adds only the diagnostic harness in a disposable project and removes it afterward. The third rehearses only the explicitly pending console patch. Fresh Unity imports may take several minutes. No command writes changes to the source scene/prefabs or commits/pushes/merges.

`summarize.py` regenerates summary statistics and the per-case matrix from the stored raw results. `build_viewer.py` converts the actual selected TGAs with `sips`; it requires the recorded disposable capture directory to remain available. `changed-files.json` lists exact delivered file paths.
