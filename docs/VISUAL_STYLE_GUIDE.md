# Shift-Four — Abandoned Survival Facility Visual Style Guide

**Intent:** believable abandoned research compound, practical player visibility, industrial wear, deliberate warning colors, consistent assets across all four rooms. Target: polished *small-scale* Unity game rather than a photorealistic scene requiring a large asset team.

## Mandatory palette (reference HEX in sRGB)

| Material / signal | HEX | Usage | Recommendation |
|---|---|---|---|
| Deep graphite | `#20272D` | main doors, structural trim, drone casing | largely matte, low-to-mid metallic |
| Worn steel | `#58636A` | wall plates, floor trim, cover reinforcements | muted metallic, moderate roughness |
| Concrete grey | `#747875` | floor and main cover barriers | nonmetal, coarse roughness |
| Dirty ivory | `#C0C2B9` | lab panel highlights / signage backing | stained, not pure white |
| Emergency red | `#BD493D` | alarm lamp, damaged panel accent, **locked** status | restrained use; remain readable |
| Hazard amber | `#E5A54B` | hazard striping, warning labels, maintenance devices | selected edges only |
| System cyan | `#61C7CF` | console screen indication (door-open status is green) | legible; avoid overuse |
| Very dark recess | `#12171B` | deep vents, shadowed mechanical gaps | keep physically plausible |
| Rust brown | `#6A4B37` | subtle corrosion and corner wear | no noisy full-frame grunge |
| Dust grey | `#9A9790` | scraped surface edges / settled dust | subtle masks/decals |

### Material families / Unity URP

- `MAT_Wall_Graphite`: #20272D; low gloss, faint rough scratches.
- `MAT_Steel_Worn`: #58636A; metallic (approx 0.65–0.9), medium roughness (URP smoothness approximately 0.25–0.45).
- `MAT_Concrete`: #747875; metallic 0; low smoothness.
- `MAT_Ivory_Dirty`: #C0C2B9; scratched paint on metal.
- `MAT_Emission_Locked`: #BD493D; emissive material for status indicator; optional subtle light.
- `MAT_Emission_Ready`: #61C7CF; emissive screens or door indicators.
- `MAT_Hazard`: #E5A54B; warning marks in small areas.

All values are artistic starting points; validate in the actual Unity URP lighting configuration. Do not assume Blender node materials exactly reproduce in Unity FBX—rebuild or remap URP materials if necessary. Don't rely on emission to light geometry by itself.

## Room lighting rules

| Room | Light plan | Avoid |
|---|---|---|
| Checkpoint | cool dim central ceiling panels, amber at console | extremely dark tutorial room |
| Server | cool status dots/racks, one overhead white-gray light | blinking lights that obscure the Guard |
| Control | steady compact red emergency accent, central neutral light for pathways | heavy red wash hiding route edges |
| Storage | sparse warm overhead lamps, red at critical area, cyan final terminal | blacked-out crate corners |

- Aim for a clear player silhouette, visible doorways, and readable cover at all times.
- Use only a few shadow-casting lights per room; rely on static lighting or lower cost lights for decorative props. Final baking is optional if project time is short.
- Minor light flicker is permitted as **presentation only**; do not attach core gameplay logic to random flicker.
- Optional dust/decal/grime texture overlays should not need complicated shader development.
- Fully enclose each room above the player; roof/ceiling tiles and emergency lighting are part of the required graphics milestone.

## Consistent art decisions

- Bevel visible edges modestly; avoid razor-sharp cubes and overly dense meshes.
- Match door/console/cargo styling: visible bolts, recessed vents, scratched paint, minimal warning stripes.
- Four distinct fleet silhouettes share graphite, titanium, recessed optics and restrained variant accents; see DRONE_FLEET_VISUAL_IMPLEMENTATION.md.
- Model wear can increase from Checkpoint to Storage, but keep shared asset color families.
- Doors are **four automatic two-leaf sliding security gates**. Historical hinged-leaf guidance is superseded by ADR-FOUR-GATE-CONTRACT. Both faces receive armor and status indicators.
- Use readable warning LEDs as material color hints; avoid multiple competing neon colors.

## Asset review checklist

- Is the shape recognisable from a first-person camera, even in low light?
- Are UVs sensible and textures present with source files?
- Does the model obey dimensions, pivot and material naming?
- Are there enough details for a coherent abandoned environment but not so many that they hurt FPS?
- Can a member import it into Unity without guessing how to orient/scale it?
