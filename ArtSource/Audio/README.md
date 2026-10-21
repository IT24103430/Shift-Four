# Gameplay audio sources

User-supplied MP3 files imported on 2026-10-21:

- `mrfriends-pistol-shot-233473.mp3` → `Assets/Resources/Audio/PlayerPistol.wav`
- `drone shot.mp3` → `Assets/Resources/Audio/DroneShot.wav`
- `soundreality-deep-drone-588746.mp3` → `Assets/Resources/Audio/DroneFlight.wav`
- `tanweraman-sliding-door-261122.mp3` → `Assets/Resources/Audio/LabDoorOpen.wav`
- `dragon-studio-gun-reload-504026.mp3` → `Assets/Resources/Audio/PlayerReload.wav`
- `background music.mp3` → `Assets/Resources/Audio/BackgroundMusic.mp3`

Converted using ffmpeg to 44,100 Hz mono 16-bit PCM, peaks reduced to approximately −2 dBFS, with a short leading fade. The flight loop also has a trailing fade. The supplied files are preserved here; license documentation was not included with the files.

The complete 4.824-second sliding-door recording is converted to mono PCM and plays at its original speed/pitch on a dedicated source. It starts when unlocking begins and rings out naturally after the panels finish moving. The 1.512-second reload recording is converted to mono PCM without time compression. The music MP3 is copied unchanged and imported as a streamed stereo clip. Music plays at volume 0.12 in a scene-owned loop, including the result screen, and restarts with the level.
