# FPS hands responsibility branches

The user authorized committing and pushing this work after the original task restrictions, supplied these four Git identities and confirmed the official role mapping. All branches start from the current remote `dev` baseline through the dependency stack below. No branch is merged and neither `dev` nor `main` is pushed.

| Order / branch | Responsibility | Supplied commit identity |
|---|---|---|
| 1 — `feature/gv-nimthara-fps-arms-assets` | Core Developer / Server / Guard: CC0-derived model, rig, materials, source and art authoring | `IT24610327 <IT24610327@my.sliit.lk>` |
| 2 — `feature/gv-asmadala-fps-arms-animation` | Agent Controller / Control / Interceptor: shared idle, movement, recoil, reload and transition sampling | `IT24103430 <sachithasmadala@gmail.com>` |
| 3 — `feature/gv-nimsara-fps-weapon-hands` | Systems Engineer / Storage / Flanker: equip lifecycle integration, gameplay guards and reusable per-weapon bindings | `Nimsara-Jayarathna <nimsarajayarathna1@gmail.com>` |
| 4 — `feature/gv-pamudi-fps-viewmodel-polish` | World Builder / Checkpoint / Scout: one-camera framing, final regression fixtures and visual evidence | `pamudiab <pamudiab@gmail.com>` |

These are stacked feature branches: each includes the preceding branch. Review in the listed order; the final Pamudi branch contains the complete tested integration. Do not independently merge overlapping whole-file versions in a different order. Shared files have explicit handoffs: rig installation moves from Nimthara to Nimsara for profile binding; the presentation pivot moves from Asmadala to Pamudi for cover framing; the mount moves from Nimsara to Pamudi for the reviewed retreat cap.

The implementation and scripted verification were performed by automation. Owner identities record the responsibility split explicitly requested by the user; they are not evidence of unaided human implementation, individual student effort or human playtesting. Commit timestamps are the actual publication time. Authentication used for pushing is distinct from commit author identity. Global Git configuration is not changed.

Original imported assets/GUIDs, MainLab, player movement, Health, WeaponRuntimeState and weapon damage data remain unchanged. Non-pistol profiles remain draft, non-playable previews. Known finger-contact and reload-magazine limitations are release blockers for a claim of fully polished hands. See [verification](FPS_HANDS_VERIFICATION.md), [initial inspection](FPS_HANDS_INSPECTION.md) and [multi-weapon integration](../ArtSource/TacticalArms/MULTI_WEAPON.md).
