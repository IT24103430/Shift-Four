# Tactical arms material finish

The five project-owned URP/Lit materials in `Assets/Materials/FPS/Arms/` use the existing anatomical glove UVs and repeatable sleeve/cuff/panel UVs. Their shared assignments remain on `FirstPersonArmsRig.prefab`. The FBX, UVs, rig and prefab bindings were not changed by the material pass.

The original Task 2 base and tangent-space normal maps (`LeatherBase`, `LeatherNormal`, `RipstopBase`, `RipstopNormal`) are preserved, including their GUIDs and import settings. These are small procedural grain/weave maps from the existing project, not scanned glove textures or a new external PBR package. No authored wear, logos, stitch paths, baked AO or other unsupported texture detail has been invented.

Two new `*MetallicSmoothness.tga` masks follow the luminance pattern already present in the corresponding base map. Their RGB channels are zero and their alpha ranges from 209/255 to 255/255. This is a restrained artistic smoothness modulation, not a claim of measured physical roughness. The installed URP Lit shader multiplies this alpha by each material's saved smoothness cap.

| Surface | Style | Effective smoothness range | Normal strength |
|---|---|---:|---:|
| GloveShell | dark charcoal leather | 0.213–0.260 | 0.14 |
| GloveReinforcement | slightly lighter gray reinforcement | 0.230–0.280 | 0.10 |
| KnuckleRubber | matte charcoal guards | 0.164–0.200 | 0.06 |
| SleeveRipstop | dark blue-gray fabric | 0.111–0.135 | 0.16 |
| CuffElastic | dark elastic cuff | 0.074–0.090 | 0.07 |

All surfaces use metallic zero. Specular highlights and environment reflections remain enabled with these low smoothness values. Glove grain repeats at 6×6; existing fabric/cuff repetition remains 3×5, with restrained normal strength to avoid exaggerating UV transitions. The masks are imported as linear 2D RGBA data, with input alpha, repeat wrapping, trilinear filtering, mipmaps and uncompressed storage. The original normal maps retain their normal-map imports.

`FirstPersonArms` now only installs the mesh/rig and applies presentation flags/pose. It does not create, assign, modify or destroy materials in either the owned or legacy path. It never accesses `Renderer.material` or `Renderer.materials`. Material assets can be edited normally and remain unchanged by spawn, firing, reload or restart. A legacy model without compatible authored URP materials would need its own editor setup; runtime tint generation is no longer used to conceal incomplete asset authoring.

To reproduce the additional mask data without altering the existing maps:

```sh
python3 tools/pack-arm-smoothness.py \
  --surfaces-dir Assets/Materials/FPS/Arms \
  --output-dir /tmp/shiftfour-arms-materials
```

Copy only the two resulting mask TGAs into their owned paths while preserving destination `.meta` files. Then explicitly run **Tools → Shift Four → FPS → Apply Tactical Arms Material Finish**, or `TacticalArmsMaterialAuthoring.ApplyFinish` in a disposable Unity copy. The editor utility updates only these five material assets and imports the two new masks. It does not edit base/normal maps, FBX import settings, prefab bindings, or scenes. The initial rig builder also applies this profile when creating fresh owned materials; subsequent runtime loads preserve any authored adjustments.

Focused verification uses `TacticalArmsMaterialsEditModeTests` and `TacticalArmsMaterialsPlayModeTests`, together with existing rig, firing, reload and restart regressions. The Play Mode fixture uses MainLab's existing lighting, three view directions, matching no-arms controls, three real scene restarts and twelve rig create/destroy cycles. Test captures and numerical results are under ignored `Logs/TacticalArmsMaterials/`.
