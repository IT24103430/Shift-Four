# Pending review — four-console sensing anchors

**Unapplied proposal.** The active console source and four section prefabs remain unchanged. [ConsoleAnchors.task14.patch](ConsoleAnchors.task14.patch) changes exactly five existing paths: `ConsoleSwitch.cs` and `Checkpoint.prefab`, `Server.prefab`, `Control.prefab`, `Storage.prefab` under `Assets/Prefabs/Sections/`. [Preimage hashes](console-anchor-preimages.json) identify the reviewed source. No MainLab, prefab hierarchy, model, collider, profile, gate, policy, sensor rule or GUID change is proposed.

## Observed defect

The current four console centres all fail the hearing sensor's strict navigation attachment rule: [live audit](generated/console-sound-anchors.csv). Their successful activation still updates mission progress and emits one typed event, but the event originates over the cabinet's excluded ground area. A sensor cannot produce a valid investigation observation from that source. This does not stop the consoles from unlocking mission gates once their own drones are also defeated.

The proposal reuses the optional producer anchor API already drafted in Task 9. Zero offset preserves existing behavior. `NoisePosition = transform.position + transform.rotation * noiseOffset` expresses metres along local axes independently of the scaled cabinet mesh. Each existing console assigns `(0,0,-2.5)` in its section prefab, placing the sensing event in its walkable interaction space. The source identity, category, `.4` intensity, mission update, indicator property block and duplicate-activation suppression remain the same.

This is a sensing location anchor, not an extra exact observation of the player. The sensor still applies distance, occlusion, gates, uncertain estimates, expiry and query budgets. Quiet console disturbances are not guaranteed to reach distant or occluded drones.

## Rehearsal and failures retained

The current project's complete **465-test** verification did **not** include this patch. It is separately rehearsed in a disposable project:

- Initial 1.25 m anchor rehearsal: **0/1 passed**, with Checkpoint receipt failing from its authored spawn; [initial XML](console-anchor-review/ProposalPlayMode.initial.xml).
- Nearby-listener 1.25 m rehearsal: **0/1 passed**, with Server receipt failing despite a nearby listener with clear transmission; [XML](console-anchor-review/ProposalPlayMode.nearby-1.25m.xml). The fixture was corrected to require nearby listeners because the `.4` event is not intended to reach every room spawn. The received-event assertions stayed intact. Default uncertainty and all sensor safeguards stayed unchanged.
- Final 2.5 m anchor proposal: **3/3 passed**, zero failures/skips, across fresh scene loads. All four actual consoles passed every run: valid source attachment, one production event, correct category/intensity/source, a received observation on the actual room sensor, no visual confirmation, idempotent repeated interaction, four console objectives and four living drones, and closed gates. [Final XML](console-anchor-review/ProposalPlayMode.xml), [last run's receipts](console-anchor-review/proposed-console-anchors.csv), [integrity](console-anchor-review/integrity.json).

The larger offset was tested to leave more walkable space around the approximate sound estimates. Three successful scene loads are bounded evidence, not a guarantee for every listener position or estimate seed. No failing assertion in the active project suite was removed or relaxed.

Reproduce the isolated proposal:

```sh
python3 docs/evidence/progressive-difficulty/rehearse_console_anchors.py
```

The script copies Assets/Packages/ProjectSettings, applies this patch only in that disposable copy, installs the review fixture there and runs it. It prints the project and results paths. A fresh import may take several minutes. The source checkout is not patched.

## Required approvals and integration order

Nimthara must review the console producer API and Server properties. Pamudi must review Checkpoint properties/final environment integration; Asmadala must review Control properties; Nimsara must review Storage properties. No external approval messages were sent.

If accepted, prefer targeted Editor property edits on those four existing console components after adding the optional source field. Preserve each section's existing overrides and GUID. Do not regenerate MainLab, replace prefabs, bake navigation or bulk-resave unrelated assets.

Task 9's Guard patch also contains the same producer API and a Server-only 1.25 m offset. Reconcile those duplicate hunks before integrating Guard; do not blindly apply both patches. The advanced Guard policy remains a separate owner decision. After approved integration, rerun the complete suites and this real-console fixture, then confirm actual Guard reaction under its approved policy.
