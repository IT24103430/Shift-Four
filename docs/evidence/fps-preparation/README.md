# FPS preparation evidence

Baseline: `2aece7603222c2fc3964779cf0da75e9a9456dd5`, Unity 6000.6.4f1, inspected 2026-10-20.

- `baseline.json`: original branch, separate feature worktree, disposable verification path and initial source SHA-256 hashes.
- `EditMode.log`, `PlayMode.log`, exit-code files, `baseline-compile-errors.json`: unmodified baseline attempts; both compile-blocked before discovery. No baseline XML was produced.
- `diagnostic-samples-excluded/`: actual NUnit XML, logs and generated evidence from the existing gameplay suites, with Samples excluded **only in the disposable copy**. Edit Mode 306/306; Play Mode 159/159; no skips/failures.
- `commands.json`: exact Unity argument arrays. Test processes used `SHIFTFOUR_EVIDENCE=<this directory>/diagnostic-samples-excluded/generated` and `SHIFTFOUR_DEMO_OUTPUT=<disposable project>/RegressionFrames` for diagnostic output. Baseline attempts used this directory's `generated` path. Both used the same assembly filters and graphics-enabled batch editor.
- `diagnostic-isolation.json`, `diagnostic-integrity.json`, `verification-settings.patch`: exclusions and disposable-only platform serialization. Test/gameplay/scene/prefab source was not edited to get passing diagnostics.
- `asset-audit/weapon-audit.json`: imported component/mesh/material/bounds/clip data and original FBX hierarchy transforms for all five weapons. The PNGs are actual Unity previews of the unmodified vendor prefabs, including misplaced magazines and missing long-gun texture assignments; they are not repaired model previews.
- `FpsAssetAudit.cs.txt`: read-only inspection harness. Copy to `Assets/Editor/FpsAssetAudit.cs` **only in a disposable project** after tests finish. Set `SHIFTFOUR_FPS_AUDIT` to the desired output directory and use the `asset-audit` invocation from `commands.json`. The harness creates temporary preview scenes and writes evidence; it does not save game assets.
- `AssetAudit.log`, exit code 0: final successful asset inspection. `AssetAudit.attempt1.log` records a harness-only null-component error, corrected using explicit Unity null checks; it is not a gameplay test failure.
- `fbx-source-inventory.json`, `project-animation-search.json`: original-project file/prefab search and FBX inspection, including Samples. Raw empty FBX takes are distinguished from imported animation clips. An unrelated sample tree parser limitation is recorded explicitly.
- `integrity.json`: original-file hash comparisons, offloaded-file limitations, and two existing binary sample checkout normalization differences caused by `*.asset text`.
- `evidence-sha256.json`: hashes of retained evidence files.

The diagnostic result is **not** a passing unmodified project build. Shared sample dependency/import cleanup is still needed. See [the preparation report](../../FPS_PREPARATION.md) for inventory, socket candidates, missing assets and ownership handoffs.
