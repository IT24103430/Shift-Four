# Project-owned synthesized gameplay effects

These three original mono WAV effects are synthesized mathematically for Shift-Four by `tools/audio/generate-combat-sounds.py`, using deterministic random seeds and no third-party samples or downloads. They are original project-owned assets; no third-party sample license is involved.

PlayerPistol: 0.28 s crack/body/slide. DroneShot: 0.23 s distinct shorter report. LabDoorOpen: 1.60 s bolt release, servo/rail movement and stop. 44,100 Hz, mono, 16-bit PCM, normalized peak 0.85 with edge fades. These are synthesized effects, not recorded firearm audio.

Playback uses a scene-owned pool of 16 AudioSources. Player shots are local; drone shots and gates use 3D attenuation. Audio does not generate duplicate AI noise events.
