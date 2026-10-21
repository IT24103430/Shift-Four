# Staged reinforcement weight repair — NOT accepted for promotion

The derivative and FBX here repair 1,008 reinforcement vertices. They preserve
native skin weights, all geometry, UVs, material regions and the 48-bone hierarchy.
The original glove shell still spikes under extreme pinky stress. Full visual
acceptance failed, so the active owned Blender source and Unity FBX are unchanged.

See `docs/TACTICAL_ARMS_SKIN_WEIGHTS.md` for the actual tests and screenshots.
`Tests/*.cs.txt` are the exact test sources injected into the disposable Unity
project. They are outside Assets so they cannot introduce failing candidate-only
tests into the active project. No `.meta` or GUID is needed here.

To reproduce: the full generator applies `tools/tactical_arm_skinning.py` while
writing to its required staging output directory. The weights-only script can
also export this Blender candidate idempotently. Any further repair must pass
visual acceptance before copying the FBX into the owned Assets path with its
existing `.meta` retained.
