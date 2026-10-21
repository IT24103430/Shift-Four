# Reproducing this assessment

Unity: `/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity`.

The reviewed source came from `git archive origin/dev` after fetching `dev`. The feature snapshots copied the current Assets, Packages and ProjectSettings without saving MainLab. Cached Libraries were copied with macOS copy-on-write to avoid importing every model again. The snapshots are disposable; the actual feature worktrees retain their source changes uncommitted.

Run clean suites before installing measurement instrumentation:

```sh
Unity -batchmode -force-metal -job-worker-count 2 \
  -projectPath /absolute/disposable/project \
  -runTests -testPlatform EditMode \
  -testResults /absolute/evidence/EditMode.xml -logFile /absolute/evidence/EditMode.log
```

Repeat with `-testPlatform PlayMode`. The clean dev suites used `-nographics`; the feature suite used `-force-metal` so the rendered test ran. Set `SHIFTFOUR_DEMO_OUTPUT` to an absolute frames folder for `RenderedMainLab_AllFourAcceptedCombatAndDestructionSequences`. Set `SHIFTFOUR_EVIDENCE` to an absolute folder for policy CSVs. Convert TGA frames with `sips -s format png input.tga --out output.png`.

For added QA probes, copy a clean project into `/tmp/ShiftFour-Student4-<name>`, then run:

```sh
python3 harness/install_profile.py /tmp/ShiftFour-Student4-<name>
```

The installer deliberately refuses paths outside these disposable projects. It copies the QA tests and injects named inclusive profiler scopes; it never changes algorithm logic. `raw/*/qa-instrumentation.json` records the before/after hashes. Do not run the installer twice on the same copy. Then run PlayMode with `-testFilter Student4AssessmentPlayModeTests` and the evidence environment variable set.

The collision fixtures use fixed simulation delta 1/60 and 240 observation frames, then allow recovery after removing the obstruction. The beam is temporary geometry without NavMesh carving; its ground path stays complete. The player probe uses the real CharacterController. Root penetration is measured with `Physics.ComputePenetration`, with a 0.005 m failure threshold. Neither fixture is saved into the authored scene.

The 400 graph queries enumerate all 16 gate masks × 25 exact endpoint pairs against an independent Dijkstra implementation. This isolates graph correctness; changing graph flags does not animate doors. A separate test uses the real console and Health callback, waits for physical opening, checks Control-to-Storage reachability and the exit objective handoff.

The profile has 720 sampled frames: live brains 0–239, navigation 240–359, explicit visible-observation prediction stress 360–479, four accepted shots at 480, four fatal damage calls at 540, destruction until 629 and settled state thereafter. Gates are opened by an objective fixture before sampling. Simulation is fixed at 60 Hz; wall interval includes Editor, test harness, rendering and frame pacing. Inclusive nested scopes cannot be added as independent frame totals. Physics.Simulate measures native physics for the scene, not only wrecks.

The microbenchmark warms cached delegates and repeats methods at one timestamp. It is useful for CPU cost bounds under that workload; it is not an end-to-end decision benchmark. `GC.GetAllocatedBytesForCurrentThread` must first pass the known 1 MiB allocation calibration. It returned zero here; zero entries in the raw tables are unavailable measurements, not evidence of zero allocations.

No target-player build, long-duration player session, exhaustive navigation proof or statistical capture-rate experiment was performed. Keep raw failures when rerunning, and label scene and source variants explicitly.
