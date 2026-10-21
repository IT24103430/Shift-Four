# Gameplay audio

The pistol, drone gunshot, drone flight, and sliding-door WAVs are converted from the user-supplied MP3s preserved in `ArtSource/Audio/`; see that directory for provenance and conversion details.

- PlayerPistol: 1.724 s, local player gunshot.
- DroneShot: 1.097 s, positional drone gunshot.
- DroneFlight: 44.040 s, volume 0.45 with a 4 m full-volume radius, one positional loop following each live drone; stops on drone death or session end.
- LabDoorOpen: 4.824 s, the full supplied sliding-door recording, played at normal pitch on a dedicated door source at volume 0.85.
- PlayerReload: 1.512 s, the supplied reload recording, local playback at volume 0.65 only on accepted reloads; stops on cancellation or player death.
- BackgroundMusic: 60.432 s, the supplied stereo MP3, streamed as a quiet 2D loop at volume 0.12.

All WAVs are 44,100 Hz mono 16-bit PCM. Shots use the existing pool of 16 voices; doors and player reloads have dedicated sources; flight loops use one additional source on each drone, and music uses one separate source. Audio playback does not emit extra AI noise events.

`tools/audio/generate-combat-sounds.py` produces the legacy synthesized effects. Running it would replace the newly supplied pistol, drone gunshots, and sliding-door sound.
