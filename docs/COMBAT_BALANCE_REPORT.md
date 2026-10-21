# Combat balance after late-drone tuning

These are the live MainLab values after the Interceptor and Flanker adjustment. Damage is applied by an accepted, unobstructed hit; cover and misses deal no damage. The player has 100 HP and recovers 8 HP/s after three seconds without damage. Drones do not recover health.

The FN Five-seveN deals 22 damage per hit, fires at most once every 0.4 seconds, and has eight rounds per magazine with a 1.6-second reload. All four drone prefabs have an 11 m attack range and a 1.1-second base shot interval. Guard fires at 0.75 times that interval, or 0.825 seconds.

| Room / drone | Drone HP | Damage to player per hit | Hits for player to defeat drone | Hits for drone to defeat 100 HP player | Drone chase speed |
| --- | ---: | ---: | ---: | ---: | ---: |
| Checkpoint / Scout | 100 | 10 | 5 | 10 | 4.4 m/s |
| Server / Guard | 150 | 12 | 7 | 9 | 4.5 m/s |
| Control / Interceptor | 110 | 12 | 5 | 9 | 4.8 m/s |
| Storage / Flanker | 125 | 14 | 6 | 8 | 5.2 m/s |

| Room / drone | Earliest player kill time with continuous hits | Earliest drone kill time with continuous hits |
| --- | ---: | ---: |
| Scout | 1.6 s | 9.9 s |
| Guard | 2.4 s | 6.6 s |
| Interceptor | 1.6 s | 8.8 s |
| Flanker | 2.0 s | 7.7 s |

Time starts with the first successful shot at 0 seconds and is `(hits - 1) * shot interval`. These are theoretical minimums for one attacker, a full player magazine, uninterrupted line of sight and no missed shots. Detection, aiming, movement, cover, recovery, reloads and simultaneous attackers change real encounter times. Guard's faster firing makes its incoming damage rate higher than Interceptor's despite equal damage per hit.

## Late-drone changes

| Drone | Before | Now |
| --- | --- | --- |
| Interceptor | 125 HP, 15 damage, 5.4 m/s chase; 6 player hits to defeat; 7 hits to kill player | 110 HP, 12 damage, 4.8 m/s chase; 5 player hits to defeat; 9 hits to kill player |
| Flanker | 150 HP, 20 damage, 6.0 m/s chase; 7 player hits to defeat; 5 hits to kill player | 125 HP, 14 damage, 5.2 m/s chase; 6 player hits to defeat; 8 hits to kill player |

The Interceptor and Flanker retain their prediction and flank policies. The first two drones and the player's weapon are unchanged. Profile values come from `Assets/Resources/DroneDifficulty/*.asset`; combat timing and hit application come from `DroneBrain`, `GuardBrain`, `WeaponController`, `WeaponRuntimeState`, and `Health`.
