# Recording the automated showcase

1. Open `Assets/Scenes/MainLab.unity` in Unity and press Play.
2. Select **NEW GAME**, enter a player name, start your screen recorder, then select **PLAY SHOWCASE**.
3. Let the countdown and scripted route run. The player watches each drone, draws it toward cover with movement and sound, fires the normal pistol, activates each room console, defuses the bomb and reaches the exit.
4. After the player reaches the exit, record the in-game 40-second ending and Group 26 credits, including each member's IT number. The result screen appears when it finishes. Press **Esc** or **Space** to skip the ending, or **F3** during the mission to show the drone decision overlay.

The route is paced for roughly 100 to 110 seconds after GO, within the two-minute mission timer. It finds usable positions around the live scene objects at runtime, and uses bounded movement and combat phases. The player cannot die in recording mode. A blocked combat phase uses a scripted finishing hit so the mission can advance; normal gameplay is unchanged.

The automated `ShowcasePlayModeTests` fixture checks that the route wins before the timer expires, completes every objective, shows an active decision from every drone and defeats each drone with normal pistol hits. An isolated Unity project copy passed at normal speed in 99.7 game seconds after GO with 66 accepted shots. The same test also passed at 4x speed for a shorter automated run.
