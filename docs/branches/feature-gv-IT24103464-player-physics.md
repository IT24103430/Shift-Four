# feature/gv-IT24103464-player-physics

**Owner:** Nimsara
**GV role:** Systems Engineer
**Branch scope:** Player movement, collision, mouse look, cursor handling, and the reusable interaction raycast.

This branch deliberately does **not** implement the later Nimsara work for combat/health refinement, physical door logic, Storage layout, or Flanker AI. Keeping those tasks on separate branches preserves a clear assessment history.

## Implemented

- `CharacterController`-based first-person movement.
- Raw WASD input with diagonal speed clamped to the same maximum as straight movement.
- Acceleration/deceleration so starts/stops are controlled rather than frame-dependent jumps.
- Gravity with ground-stick handling and a terminal fall speed.
- Collision response delegated to Unity `CharacterController.Move`.
- Stable yaw on the player body and clamped camera pitch.
- Escape releases the cursor; one left click re-locks it without firing on that same frame.
- Input stops when the player is dead or `GameSession` has ended.
- Forward interaction raycast with an explicit interaction range and mask.
- Parent lookup for `IInteractable`, allowing the ray to hit a child collider while the interaction script remains on the root object.
- Interaction prompt is cleared whenever no valid target is present.
- Baseline greybox player controller dimensions/settings are explicitly configured in `BaselineSceneBuilder`.

## Files changed

```text
Assets/Scripts/Gameplay/PlayerController.cs
Assets/Editor/BaselineSceneBuilder.cs
docs/branches/feature-gv-IT24103464-player-physics.md
```

## Important: existing generated scene

If `MainLab.unity` was already generated **before this branch**, do not rerun the greybox generator. The script changes apply automatically, but the existing Player object's `CharacterController` may still have the older generated dimensions.

On the Player object, use these values once in the Inspector:

```text
Center: (0, 0.9, 0)
Height: 1.8
Radius: 0.35
Slope Limit: 50
Step Offset: 0.3
Skin Width: 0.05
Min Move Distance: 0
```

If the scene is generated for the first time after this branch, `BaselineSceneBuilder` applies them automatically.

## Manual verification in Unity

Run all of these before opening the PR:

1. Hold `W`, `A`, `S`, `D` separately and confirm movement is predictable.
2. Hold `W + D`; diagonal movement must not be faster than forward movement.
3. Walk into a wall/shelf continuously for 10 seconds; the player must not pass through it.
4. Approach a wall diagonally and confirm the controller slides along it rather than teleporting through it.
5. Rotate 360 degrees several times; movement must remain relative to the new facing direction.
6. Look fully up and down; pitch stops at the configured clamp instead of flipping.
7. Press Escape; movement/look/interactions stop while the cursor is free.
8. Left-click once; the cursor re-locks, and that relock click should not also fire.
9. Stand farther than the interaction range from a console/door and press `E`; nothing happens.
10. Stand within range, aim at it, press `E`; the valid `IInteractable` responds once.
11. Aim at a child collider of an interactable root; the root interaction should still be found.
12. Trigger the player death/end state; movement and interaction input stop.
13. Keep the Unity Console open during the run; there should be no new red errors.

## Suggested real commits

Do not fake these if your local development happened in one tested step. If implementing locally in checkpoints, use commits similar to:

```text
feat(gv): refine character controller movement and collision
feat(gv): stabilize mouse look and cursor handling
fix(gv): constrain player interaction to valid nearby targets
```

## PR target

```text
feature/gv-IT24103464-player-physics -> dev
```

After review/merge, create the next branch from the updated `dev`:

```text
feature/gv-IT24103464-combat-health
```
