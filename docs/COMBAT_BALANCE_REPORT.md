# MainLab combat balance

The four rooms now follow a steady hit-count progression. The player has 100 HP and an FN Five-seveN that deals 22 damage per accepted hit. It fires every 0.4 seconds, has an eight-round magazine and a 1.6-second reload. Player health recovers at 8 HP/s after three seconds without damage. Drones do not recover health.

| Room / drone | Drone HP | Drone damage per hit | Player hits to defeat drone | Drone hits to defeat 100 HP player | Drone shot interval | Drone chase speed |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Checkpoint / Scout | 88 | 13 | 4 | 8 | 1.05 s | 4.25 m/s |
| Server / Guard | 110 | 15 | 5 | 7 | 1.03 s | 4.35 m/s |
| Control / Interceptor | 132 | 18 | 6 | 6 | 1.01 s | 4.65 m/s |
| Storage / Flanker | 154 | 21 | 7 | 5 | 0.99 s | 5.05 m/s |

| Room / drone | Ideal player time to defeat drone | Ideal drone time to defeat player |
| --- | ---: | ---: |
| Scout | 1.2 s | 7.35 s |
| Guard | 1.6 s | 6.18 s |
| Interceptor | 2.0 s | 5.05 s |
| Flanker | 2.4 s | 3.96 s |

Time starts with the first successful hit at zero and equals `(hits - 1) * shot interval`. These are mathematical minimums for one attacker and uninterrupted, unobstructed hits. Detection, movement, cover, misses, recovery and other attackers change real encounter times. All four drones still fit within one player magazine if every shot lands. Their AI policies are unchanged. Patrol, investigation and chase speeds are each 0.15 m/s lower than the previous balance. All have 15 m vision and 11 m attack range; the player's weapon reaches 30 m.

The Guard no longer applies its former 0.75 firing-interval multiplier. Its effective interval is the 1.03 s value serialized on its prefab, which keeps firing speed in room order. The Storage Flanker is the most dangerous on paper: five hits can defeat the player in 3.96 seconds while the two-minute bomb timer is running. Playtesting should check whether cover and the Storage fight leave enough time for the final objective.

Values come from `Assets/Resources/DroneDifficulty/*.asset` and `Assets/Prefabs/Agents/*.prefab`. Damage and shot timing are applied by `DroneBrain`, `WeaponController`, `WeaponRuntimeState`, and `Health`.
