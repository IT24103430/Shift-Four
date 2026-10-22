using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour
{
    // A recording-friendly, bounded route through the real MainLab mission.
    // Tactical positions are found at runtime so small scene edits do not break the demo.
    [DisallowMultipleComponent]
    public sealed class ShowcaseDirector : MonoBehaviour
    {
        private static readonly string[] Names = { "SCOUT", "GUARD", "INTERCEPTOR", "FLANKER" };
        private static readonly string[] Behaviours =
        {
            "SOUND INVESTIGATION", "DEFENSIVE COVER", "PREDICTIVE PURSUIT", "SIDE APPROACH"
        };
        private GameSession session;
        private PlayerController player;
        private WeaponController weapon;
        private Camera view;
        private float fade;
        private string caption = string.Empty;

        public void Initialize(GameSession owner) => session = owner;

        private IEnumerator Start()
        {
            while (session != null && !session.CanPlay && !session.Ended) yield return null;
            if (session == null || session.Ended) yield break;
            player = session.Player;
            if (player == null || player.ViewCamera == null) yield break;
            weapon = player.Weapons;
            view = player.ViewCamera;
            yield return PlantInCheckpoint();
            if (!session.BombPlanted || session.Ended) yield break;
            for (int room = 0; room < 4 && !session.Ended; room++)
            {
                if (session.MissionRemaining < 16f) break;
                yield return RunRoom(room);
            }
            if (session.Ended) yield break;
            // This contingency only runs if a scene obstruction or low frame rate consumed the route budget.
            CompleteRemainingObjectives();
            yield return FinishMission();
        }

        private IEnumerator RunRoom(int room)
        {
            float started = Time.time;
            DroneBrain drone = FindDrone(room);
            ConsoleSwitch console = FindConsole(room);
            LabDoor gate = FindGate(room);
            if (drone != null && drone.GetComponent<Health>().IsAlive)
            {
                Vector3 visible = FindDroneSpot(drone, true);
                caption = Names[room] + "  /  OBSERVE";
                yield return CutTo(visible, TargetPoint(drone));
                yield return Watch(drone, 2f);
                if (session.MissionRemaining > 20f) weapon.TryFire(); // A real shot draws the drone's attention.

                Vector3 cover = FindDroneSpot(drone, false);
                caption = Names[room] + "  /  " + Behaviours[room];
                yield return WalkTo(cover, drone, 3f);
                SoundEvents.ReportNoise(player.transform.position, NoiseCategory.Footstep, .7f, player);
                yield return Watch(drone, 5f);

                caption = Names[room] + "  /  RETURN FIRE";
                yield return CutTo(FindDroneSpot(drone, true), TargetPoint(drone));
                yield return Fight(drone, 12f);
            }

            caption = MissionProgress.Rooms[room].ToUpperInvariant() + "  /  ACTIVATE CONSOLE";
            if (console != null)
            {
                yield return CutTo(FindConsoleSpot(console), console.GetComponent<Collider>().bounds.center);
                yield return new WaitForSeconds(.6f);
                console.Interact();
            }
            else session.ConsoleActivated(room);

            if (gate != null)
            {
                caption = MissionProgress.Rooms[room].ToUpperInvariant() + "  /  SECURITY GATE";
                yield return Watch(gate.transform.position + Vector3.up, 2f);
            }
            while (Time.time - started < 24f && session.MissionRemaining > 18f && !session.Ended)
                yield return null;
        }

        private IEnumerator Fight(DroneBrain drone, float limit)
        {
            Health target = drone.GetComponent<Health>();
            float started = Time.time, lastProgress = Time.time, previous = target.Current;
            while (target.IsAlive && Time.time - started < limit && session.MissionRemaining > 14f && !session.Ended)
            {
                Vector3 point = TargetPoint(drone);
                LookAt(point, 540f * Time.deltaTime);
                if (Quaternion.Angle(view.transform.rotation, Quaternion.LookRotation(point - view.transform.position)) < 3f)
                    weapon.TryFire();
                if (target.Current < previous - .001f) { previous = target.Current; lastProgress = Time.time; }
                if (Time.time - lastProgress > 2.5f)
                {
                    yield return CutTo(FindDroneSpot(drone, true), TargetPoint(drone));
                    lastProgress = Time.time;
                }
                yield return null;
            }
            if (target.IsAlive && !session.Ended)
            {
                // End a blocked encounter on camera, then let the ordinary death callbacks open the gate.
                LookAt(TargetPoint(drone), float.PositiveInfinity);
                weapon.TryFire();
                target.ApplyDamage(target.Current, player, "Showcase finishing shot");
                yield return new WaitForSeconds(.6f);
            }
        }

        private IEnumerator PlantInCheckpoint()
        {
            BombConsole bomb = FindAnyObjectByType<BombConsole>();
            caption = "CHECKPOINT  /  PLANT THE TIME BOMB";
            if (bomb != null)
            {
                yield return CutTo(FindConsoleSpot(bomb.GetComponent<Collider>()),
                    bomb.GetComponent<Collider>().bounds.center);
                yield return new WaitForSeconds(1f);
                bomb.Interact();
            }
            while (session.IsPlanting && !session.Ended) yield return null;
            if (!session.BombPlanted || session.Ended) yield break;
            yield return new WaitForSeconds(2f);
        }

        private IEnumerator FinishMission()
        {
            if (!session.BombPlanted || session.Ended) yield break;
            caption = "FINAL OBJECTIVE  /  REACH THE EXIT";
            ExitZone exit = FindAnyObjectByType<ExitZone>();
            if (exit != null)
            {
                Collider trigger = exit.GetComponent<Collider>();
                if (trigger != null)
                {
                    Vector3 approach = FindConsoleSpot(trigger);
                    yield return CutTo(approach, trigger.bounds.center + Vector3.up);
                    yield return WalkTo(trigger.bounds.center, null, 2f);
                }
            }
            if (!session.Won) session.TryExit();
            caption = session.Won ? "MISSION COMPLETE" : "SHOWCASE FINISHED";
        }

        private void CompleteRemainingObjectives()
        {
            for (int room = 0; room < 4; room++)
            {
                ConsoleSwitch console = FindConsole(room);
                if (console != null) console.Interact();
                else session.ConsoleActivated(room);
                DroneBrain drone = FindDrone(room);
                if (drone != null)
                {
                    Health target = drone.GetComponent<Health>();
                    if (target.IsAlive) target.ApplyDamage(target.Current, player, "Showcase time contingency");
                }
                else session.Progress.DefeatDrone(room);
            }
        }

        private IEnumerator Watch(DroneBrain drone, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until && session.MissionRemaining > 14f && !session.Ended)
            {
                if (drone != null) LookAt(TargetPoint(drone), 160f * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator Watch(Vector3 target, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until && session.MissionRemaining > 14f && !session.Ended)
            {
                LookAt(target, 160f * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator WalkTo(Vector3 destination, DroneBrain focus, float limit)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(destination, out NavMeshHit finish, 1.5f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(start.position, finish.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                yield break;
            float until = Time.time + limit;
            foreach (Vector3 corner in path.corners)
            {
                while (Time.time < until && !session.Ended)
                {
                    Vector3 direction = corner - player.transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude < .25f) break;
                    player.ShowcaseMove(direction.normalized, Time.deltaTime);
                    if (focus != null) LookAt(TargetPoint(focus), 160f * Time.deltaTime);
                    yield return null;
                }
                if (Time.time >= until) break;
            }
        }

        private IEnumerator CutTo(Vector3 position, Vector3 focus)
        {
            fade = 1f;
            yield return new WaitForSecondsRealtime(.16f);
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = position;
            body.enabled = true;
            player.ApplyCameraPose();
            LookAt(focus, float.PositiveInfinity);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.16f);
            fade = 0f;
        }

        private void LookAt(Vector3 point, float degrees)
        {
            Vector3 direction = point - view.transform.position;
            if (direction.sqrMagnitude < .01f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            view.transform.rotation = Quaternion.RotateTowards(view.transform.rotation, target, degrees);
        }

        private static Vector3 TargetPoint(DroneBrain drone)
        {
            CapsuleCollider body = drone.GetComponent<CapsuleCollider>();
            return body != null && body.enabled ? body.bounds.center : drone.transform.position + Vector3.up * 1.5f;
        }

        private Vector3 FindDroneSpot(DroneBrain drone, bool visible)
        {
            Vector3 anchor = drone.transform.position;
            Vector3 target = TargetPoint(drone);
            Vector3 fallback = player.transform.position;
            bool foundFallback = false;
            foreach (float radius in new[] { 6f, 8f, 4.5f })
            {
                for (int angle = 0; angle < 360; angle += 30)
                {
                    Vector3 probe = anchor + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                    if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) continue;
                    if ((hit.position - anchor).sqrMagnitude > 110f) continue;
                    if (!foundFallback) { fallback = hit.position; foundFallback = true; }
                    Vector3 from = hit.position + Vector3.up * 1.3f;
                    Vector3 ray = target - from;
                    bool sees = Physics.Raycast(from, ray.normalized, out RaycastHit block, ray.magnitude + .5f,
                        WeaponController.GameplayRaycastMask, QueryTriggerInteraction.Ignore) &&
                        block.collider.GetComponentInParent<DroneBrain>() == drone;
                    if (sees == visible) return hit.position;
                }
            }
            if (NavMesh.SamplePosition(fallback, out NavMeshHit safe, 1.5f, NavMesh.AllAreas)) return safe.position;
            return player.transform.position;
        }

        private Vector3 FindConsoleSpot(ConsoleSwitch console)
            => FindConsoleSpot(console.GetComponent<Collider>());

        private Vector3 FindConsoleSpot(Collider target)
        {
            Vector3 center = target.bounds.center;
            Vector3 fallback = center + Vector3.back * 2f;
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 probe = center + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.8f;
                probe.y = 0f;
                if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                fallback = hit.position;
                Vector3 eye = hit.position + Vector3.up * 1.3f;
                Vector3 ray = center - eye;
                if (Physics.Raycast(eye, ray.normalized, out RaycastHit seen, 3f,
                    WeaponController.GameplayRaycastMask, QueryTriggerInteraction.Ignore) &&
                    (seen.collider == target || seen.collider.transform.IsChildOf(target.transform)))
                    return hit.position;
            }
            if (NavMesh.SamplePosition(fallback, out NavMeshHit safe, 1.5f, NavMesh.AllAreas)) return safe.position;
            return player.transform.position;
        }

        private static DroneBrain FindDrone(int room)
        {
            foreach (DroneBrain drone in FindObjectsByType<DroneBrain>(FindObjectsSortMode.None))
                if (room == 0 && drone is ScoutBrain || room == 1 && drone is GuardBrain ||
                    room == 2 && drone is InterceptorBrain || room == 3 && drone is FlankerBrain) return drone;
            return null;
        }

        private static ConsoleSwitch FindConsole(int room)
        {
            foreach (ConsoleSwitch console in FindObjectsByType<ConsoleSwitch>(FindObjectsSortMode.None))
                if (console.GateId == room) return console;
            return null;
        }

        private static LabDoor FindGate(int room)
        {
            foreach (LabDoor gate in FindObjectsByType<LabDoor>(FindObjectsSortMode.None))
                if (gate.GateId == room) return gate;
            return null;
        }

        private void OnGUI()
        {
            if (session == null || !session.CanPlay) return;
            int oldDepth = GUI.depth;
            Color oldColor = GUI.color;
            GUI.depth = -11;
            if (fade > 0f)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            else if (!string.IsNullOrEmpty(caption))
            {
                GUI.color = new Color(.03f, .08f, .1f, .82f);
                GUI.DrawTexture(new Rect(22, 76, 450, 50), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(36, 80, 425, 22), "SHOWCASE RUN  /  " + caption);
                GUI.Label(new Rect(36, 102, 425, 18), "F3: AI decisions");
            }
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }
    }
}
