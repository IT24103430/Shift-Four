# Combat HUD

Stage 1 replaces the diagnostic gameplay overlay with a compact player-facing HUD. `GameSession` installs one `CombatHud` at startup, so existing scenes and saved prefabs do not need changes.

## Player feedback

- Player health shows the actual numeric value, a smoothed segmented bar and an amber damage trail. Critical health is red; recovery appears only while health actually increases.
- The hipfire crosshair expands on an accepted shot. A white hit marker requires accepted damage attributed to this player; a coral marker confirms a defeated drone. Misses, cover hits and other attackers cannot produce confirmation.
- Incoming damage briefly tints the screen edges and health panel. A directional chevron uses the attacker's position at the time of damage. Unknown sources show no direction; hidden attackers are not tracked afterward.
- Room objectives, mission progress, interaction prompts, locked-exit notices and the existing restart command remain visible in the new presentation. F3 toggles agent and current-gate details.

## Drone health

Red health bars appear above a living drone when aimed at or recently damaged by the player. They fade after looking away and immediately hide when the drone is behind the camera, outside the viewport, farther than 30 metres, defeated or physically occluded. The visibility ray uses the same solid gameplay mask as weapon hitscan, including other actors as blockers.

## Implementation

`CombatHud` observes `Health.Damaged` and `WeaponController.ShotAccepted`; it does not change damage, recovery, fire cadence or mission rules. It unsubscribes when disabled or destroyed and can be initialized repeatedly without duplicate observers. Restart reloads the scene and clears transient feedback.

`CombatHudRenderer` draws with the project's existing IMGUI module, using a 1280×720 design scale and preserving the caller's GUI matrix, color and depth. Production package dependencies are unchanged. No external art is required.

Ammo limits, reloading, new character/arm models and death-camera animation belong to later stages.

## Verification

`CombatHudPlayModeTests` exercises real MainLab hitscan, cover and miss rejection, defeat, recovery, label visibility, damage attribution, observer lifecycle and the actual scene restart.

Run the checks against an isolated copy containing the current `Assets`, `Packages` and `ProjectSettings`:

```powershell
./tools/verify-combat-hud.ps1 -ProjectPath ./Temp/CombatHudVerification
```

The script runs all project Edit Mode tests and selected combat/mission/player Play Mode regressions, builds a Windows development player, then captures full-frame HUD scenarios at 1280×720, 1024×768 and 1920×1080. `-BuildOnly` skips tests. The standalone probe is installed only in the isolated copy and activates only with `-combatHudProbe`. Its incoming damage is simulated through the real health API; outgoing hits use the real weapon API. It does not replace human playtesting.

Results, logs and screenshots stay under the copy's ignored `Logs/CombatHud` directory. The QA copy alone receives the built-in ScreenCapture and ImageConversion modules needed to capture IMGUI.

Validated on Unity 6000.6.4f1: 329 Edit Mode and 57 selected Play Mode tests passed, including all six HUD integration tests. The Windows development build and nine automated standalone scenarios completed with zero runtime errors. Screenshots were reviewed at all three resolutions. Human testing of combat readability and feel remains necessary.
