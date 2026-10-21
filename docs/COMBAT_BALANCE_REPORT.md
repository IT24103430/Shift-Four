# MainLab combat balance

The four rooms follow a rising hit-count progression. The shared combat duration setting is 1.2: it increases MainLab player and drone maximum health by 20% and divides both sides' shot damage by 1.2. The player has 120 HP and a weapon displayed as the 1911 .45 ACP with 22 base damage, dealing about 18.33 damage per accepted hit. It fires every 0.4 seconds, has a 12-round magazine, 80 reserve rounds and a 1.6-second reload. Player health recovers at 8 HP/s after three seconds without damage. Drones do not recover health.

| Room / drone | Drone HP | Effective drone damage per hit | Player hits to defeat drone | Drone hits to defeat 120 HP player | Drone shot interval | Drone chase speed |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Checkpoint / Scout | 79.2 | 10.83 | 5 | 12 | 1.05 s | 4.25 m/s |
| Server / Guard | 105.6 | 12.5 | 6 | 10 | 1.03 s | 4.35 m/s |
| Control / Interceptor | 132 | 15 | 8 | 8 | 1.01 s | 4.65 m/s |
| Storage / Flanker | 158.4 | 17.5 | 9 | 7 | 0.99 s | 5.05 m/s |

| Room / drone | Ideal player time to defeat drone | Ideal drone time to defeat player |
| --- | ---: | ---: |
| Scout | 1.6 s | 11.55 s |
| Guard | 2.0 s | 9.27 s |
| Interceptor | 2.8 s | 7.07 s |
| Flanker | 3.2 s | 5.94 s |

Time starts with the first successful hit at zero and equals `(hits - 1) * shot interval`. These are mathematical minimums for one attacker and uninterrupted, unobstructed hits. Detection, movement, cover, misses, recovery and other attackers change real encounter times. All four drones fit within one fresh 12-round player magazine if every shot lands. The Flanker allows three misses before a reload is needed. Their AI policies are unchanged. Patrol, investigation and chase speeds are each 0.15 m/s lower than the previous balance. All have 15 m vision and 11 m attack range; the player's weapon reaches 30 m.

The Guard no longer applies its former 0.75 firing-interval multiplier. Its effective interval is the 1.03 s value serialized on its prefab, which keeps firing speed in room order. The Storage Flanker is the most dangerous on paper: seven hits can defeat the player in 5.94 seconds. The two-minute mission timer starts at GO and covers all four rooms, including the final defusal. Playtesting should check whether the whole mission fits that limit.

Base values come from `Assets/Resources/DroneDifficulty/*.asset` and `Assets/Prefabs/Agents/*.prefab`. `GameSession.CombatDurationMultiplier` is the single duration tuning value. MainLab player and drone maximum health is multiplied by it, while player and drone shot damage is divided by it. Increasing it extends encounters without changing movement or fire rates. At 1.2, player hits to defeat a full-health drone rise from 3/4/5/6 at 1.0 to 5/6/8/9; drone hits to defeat a full-health player rise from 8/7/6/5 to 12/10/8/7. The displayed 1911 .45 ACP's magazine capacity is set in `Assets/Resources/Weapons/FNFiveSeven.asset`. Its current 3D viewmodel is still the original FN Five-seveN asset. Direct scripted damage and other scenes keep their authored health values. Damage and shot timing are applied by `DroneBrain`, `WeaponController`, `WeaponRuntimeState`, and `Health`.
