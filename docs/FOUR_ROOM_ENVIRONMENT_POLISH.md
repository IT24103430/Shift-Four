# Four-room environment implementation

The four existing section prefabs now carry `Visual polish` children, inherited by their MainLab instances. Shared materials are under `Assets/Materials/VisualUpgrade`. Generated elements are reusable as part of the section prefabs, contain no colliders and do not change room bounds, console locations, cover collision or gate roots.

Shared architectural kit: segmented ceiling cassettes, three overhead service beams/cable trays, wall pilasters, vented service panels, interrupted perimeter skirting, fine floor route seams and two recessed practical fixtures per room. Perimeter trim stops around central gate openings. Decoration stays flush to existing cover or overhead; gameplay traversal still needs human review.

| Room | Implemented treatment |
|---|---|
| Checkpoint | Cool neutral practical fill, clean service panels, barrier-face trim and console hood |
| Server | Cyan-white fill, rack-facing ventilation strips and overhead cable routes |
| Control | Neutral fill, console hood and equipment faces, compact red emissive emergency fixture |
| Storage | Warm practical fill, amber cargo bands on existing cover, architectural service detail |

There are two added non-shadow-casting point lights per room, range 12m, intensity 1.5. Emissive strips do not pretend to bake indirect light. No particles, bloom, random flicker or new pipeline dependency was added. Existing URP assets are used; the default Graphics pipeline assignment is repaired because the input checkout had it unset. Existing Standard material assets are converted in place while preserving base color, metallic and smoothness.

The architectural generator is `ShiftFourVisualUpgrade.Room`; increment its revision marker when changing generated room geometry. Repeated refresh skips an already-current room. Restore `Library/VisualUpgradeBackup` or reviewed Git commits for rollback. Profile on team hardware before claiming 60 FPS.
