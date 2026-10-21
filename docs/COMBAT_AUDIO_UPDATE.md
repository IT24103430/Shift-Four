# Combat and supplied audio update — 2026-10-21

Drones detect nearby visible players in every direction. After an accepted shot, combat commitment persists until the player or drone dies, or the session ends. Engaged drones pursue when sight or range is lost and retain their individual tactics at close range. Walls and closed gates still block shots and navigation. The Guard fires every 0.825 seconds, compared with the other drones’ 1.1 seconds.

The FN Five-seveN and fallback weapon use eight rounds per magazine and a 0.4-second shot interval, retaining the existing damage, reserve ammunition and reload timer.

Supplied pistol, drone shot, drone flight, sliding-door, background music and reload files are preserved under `ArtSource/Audio/`. See its README for conversions. Flight loops follow live drones at volume 0.45, music streams in stereo at volume 0.12, and the full door recording plays at its original speed on a dedicated positional source at volume 0.85. Door audio has an 8 m full-volume radius, linear falloff and priority over gunshots. Reload audio plays locally at volume 0.65 only after a reload is accepted and stops on cancellation or death.

Unity 6000.6.4f1 passed **7/7 Edit Mode** and **14/14 graphical Play Mode** checks with zero failures or skips on the final audio source. Tests cover imported PCM, stereo streaming, duplicate callback prevention, persistent attack, cover, flight-loop shutdown, dedicated door playback during a saturated shot pool, reload acceptance/cancellation/death and quieter music. Door and reload were isolated from other sounds and sampled from Unity’s actual mixed listener output; both were non-silent. The source and audio files were compared byte-for-byte with the tested local copy.

See [test totals and measured output](evidence/combat-audio/test-summary.json), [Edit Mode results](evidence/combat-audio/EditMode.xml) and [Play Mode results](evidence/combat-audio/PlayMode.xml). The older [drone-policy integration report](DRONE_POLICY_INTEGRATION.md) records the preceding policy baseline; its historical source hashes and broader test counts do not describe these later combat/audio edits.

Restart Play Mode after importing these changes to recreate the scene-owned audio sources.
