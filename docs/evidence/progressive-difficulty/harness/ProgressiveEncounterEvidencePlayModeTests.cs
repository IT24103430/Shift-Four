#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    // Optional diagnostic fixture installed ONLY in a disposable project by run_encounters.py.
    // Characterizes the current policies; no assertion claims the pending enhancements are present.
    public sealed class ProgressiveEncounterEvidencePlayModeTests
    {
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static FieldInfo Field(object o, string n)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly); if (f != null) return f; }
            throw new MissingFieldException(n);
        }
        private static object Call(object o, string n, params object[] a)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType) { var m = t.GetMethod(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly); if (m != null) return m.Invoke(o, a); }
            throw new MissingMethodException(n);
        }
        private static Component[] Find(string n) => Object.FindObjectsByType(T(n), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static string Out => Environment.GetEnvironmentVariable("SHIFTFOUR_ENCOUNTER_EVIDENCE") ?? "Logs/ProgressiveEncounters";
        private static string Row(params object[] values) => string.Join(",", values.Select(v => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"")) + "\n";
        private static void Place(Component player, Vector3 point)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = point; cc.enabled = true; Call(player, "ApplyCameraPose"); Physics.SyncTransforms();
        }
        private static bool PlayerPoint(Component brain, Component player, bool visible, int phase)
        {
            Vector3 original = player.transform.position;
            foreach (float distance in new[] { 6f, 8f, 4f, 10f }) for (int index = 0; index < 24; index++)
            {
                Vector3 point = brain.transform.position + Quaternion.Euler(0, index * 15 + phase * 120, 0) * Vector3.forward * distance;
                if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) continue;
                if (((IList)Call(Find("WaypointGraph").Single(), "FindRoute", brain.transform.position, hit.position)).Count == 0) continue;
                Place(player, hit.position);
                Vector3 muzzle = brain.transform.position + Vector3.up * 1.25f, eye = (Vector3)Get(player, "EyePosition");
                bool blocked = Physics.Raycast(muzzle, (eye - muzzle).normalized, out var obstruction, (eye - muzzle).magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                    obstruction.collider.GetComponentInParent(T("Health")) == null;
                if (visible)
                {
                    brain.transform.rotation = Quaternion.LookRotation(player.transform.position - brain.transform.position);
                    if ((bool)Call(brain, "CanSeePlayer")) return true;
                }
                else if (blocked) return true;
            }
            Place(player, original); return false;
        }
        private static bool EmitScoutSound(Component brain, out Vector3 estimate)
        {
            var hearing = Get(brain, "Hearing"); var body = brain.GetComponent(T("DroneBody"));
            for (int angle = 0; angle < 360; angle += 30)
            {
                Vector3 point = brain.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * 3f;
                if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas) || !(bool)Call(body, "CanOccupy", hit.position)) continue;
                T("SoundEvents").GetMethod("ReportShot").Invoke(null, new object[] { hit.position });
                object[] args = { null };
                if ((bool)hearing.GetType().GetMethod("TryGetLatest").Invoke(hearing, args))
                { estimate = (Vector3)args[0].GetType().GetField("Position").GetValue(args[0]); return true; }
            }
            estimate = default; return false;
        }
        [UnityTest] public IEnumerator FourRooms_ScriptedEncountersRecordTravelPlanningAndRecovery()
        {
            float capture = Time.captureDeltaTime; int rate = Application.targetFrameRate; var cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
            Directory.CreateDirectory(Out); Time.captureDeltaTime = 1f / 60; Application.targetFrameRate = 60;
            string summary = "agent,scenario_sim_seconds,travel_m,decision_switches,motor_plan_attempts,native_requests,blocked_entries,blocked_frames,player_damage,player_died,sound_received,min_distance_to_sound,cover_found,temporary_route_blocker\n";
            try
            {
                foreach (string name in new[] { "Scout", "Guard", "Interceptor", "Flanker" })
                {
                    yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
                    var player = Find("PlayerController").Single(); ((Behaviour)player).enabled = false;
                    foreach (var b in Find("DroneBrain")) { ((Behaviour)b).enabled = false; ((Behaviour)b.GetComponent(T("DroneMotor"))).enabled = false; b.GetComponent<NavMeshAgent>().isStopped = true; }
                    var brain = Find(name + "Brain").Single(); var motor = brain.GetComponent(T("DroneMotor")); var agent = brain.GetComponent<NavMeshAgent>();
                    Place(player, new Vector3(1000, 0, 1000));
                    Vector3 estimate = default, source = default; bool heard = false, cover = false;
                    if (name == "Scout") { heard = EmitScoutSound(brain, out estimate); Assert.That(heard, Is.True); }
                    else { Assert.That(PlayerPoint(brain, player, true, 0), Is.True, name); source = player.transform.position; }
                    ((Behaviour)brain).enabled = true; ((Behaviour)motor).enabled = true;
                    float began = Time.time, lastHP = (float)Get(player.GetComponent(T("Health")), "Current"), damage = 0, travel = 0, minSound = float.PositiveInfinity;
                    int changes = 0, plans = 0, requests = 0, blockedEntries = 0, blockedFrames = 0; string lastDecision = "", lastState = "";
                    float nextPlan = (float)Field(motor, "nextPlanAt").GetValue(motor); int requested = (int)Field(motor, "requestedFrame").GetValue(motor);
                    Vector3 last = brain.transform.position; bool moved = false, hidden = false, captured = false; GameObject blocker = null;
                    string csv = "time,phase,x,z,decision,awareness,motor_state,player_hp,actual_speed,goal_x,goal_z,native_status,plan_attempts,blocked_entries\n";
                    for (int frame = 0; frame < 840; frame++)
                    {
                        float elapsed = Time.time - began;
                        if (name == "Interceptor" && elapsed < 6)
                        {
                            float offset = elapsed < 3 ? elapsed * .6f : (6 - elapsed) * .6f;
                            Vector3 p = source + Vector3.right * offset;
                            if (NavMesh.SamplePosition(p, out var hit, .1f, NavMesh.AllAreas)) Place(player, hit.position);
                        }
                        else if (name != "Scout" && elapsed >= 3 && !moved) { PlayerPoint(brain, player, true, 1); moved = true; }
                        if (name == "Flanker" && elapsed >= 4.5f && blocker == null)
                        {
                            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name = "Temporary QA raised flank obstruction";
                            blocker.transform.position = (Vector3)Field(motor, "goal").GetValue(motor) + Vector3.up * 1.7f;
                            blocker.transform.localScale = new Vector3(1.2f, .5f, 1.2f); Physics.SyncTransforms();
                        }
                        if (name != "Scout" && elapsed >= 6 && !hidden)
                        {
                            cover = PlayerPoint(brain, player, false, 0); if (!cover) Place(player, new Vector3(1000, 0, 1000)); hidden = true;
                        }
                        yield return null;
                        travel += Vector3.Distance(last, brain.transform.position); last = brain.transform.position;
                        float hp = (float)Get(player.GetComponent(T("Health")), "Current"); damage += Mathf.Max(0, lastHP - hp); lastHP = hp;
                        string decision = (string)Get(brain, "CurrentDecision"), state = Field(motor, "state").GetValue(motor).ToString();
                        if (lastDecision != "" && decision != lastDecision) changes++; lastDecision = decision;
                        float currentPlan = (float)Field(motor, "nextPlanAt").GetValue(motor); if (currentPlan != nextPlan) { plans++; nextPlan = currentPlan; }
                        int currentRequest = (int)Field(motor, "requestedFrame").GetValue(motor); if (currentRequest != requested) { requests++; requested = currentRequest; }
                        if (state == "Blocked") { blockedFrames++; if (lastState != "Blocked") blockedEntries++; } lastState = state;
                        if (heard) minSound = Mathf.Min(minSound, Vector3.Distance(brain.transform.position, estimate));
                        if (frame % 6 == 0)
                        {
                            Vector3 goal = (Vector3)Field(motor, "goal").GetValue(motor);
                            csv += Row(Time.time - began, name == "Scout" ? "sound_then_expiry" : hidden ? cover ? "behind_room_cover" : "hidden_off_map_fallback" : elapsed < 3 ? "visible" : "relocation_or_reversal",
                                last.x, last.z, decision, Get(Get(brain, "Awareness"), "State"), state, hp, Get(motor, "ActualSpeed"), goal.x, goal.z, agent.pathStatus, plans, blockedEntries);
                        }
                        if (!captured && elapsed >= (name == "Scout" ? 1.5f : 5f)) { captured = true; yield return Capture(brain, name); }
                        if (!(bool)Get(player.GetComponent(T("Health")), "IsAlive")) break;
                    }
                    File.WriteAllText(Path.Combine(Out, name + "-trace.csv"), csv);
                    summary += Row(name, Time.time - began, travel, changes, plans, requests, blockedEntries, blockedFrames, damage,
                        !(bool)Get(player.GetComponent(T("Health")), "IsAlive"), heard, heard ? minSound.ToString(CultureInfo.InvariantCulture) : "not_applicable", cover, blocker != null);
                    if (name == "Scout") Assert.That(travel, Is.GreaterThan(.5f), "The unchanged active Scout must physically react, not only select a label");
                }
                File.WriteAllText(Path.Combine(Out, "encounter-summary.csv"), summary);
                File.WriteAllText(Path.Combine(Out, "context.txt"), "Four separate 14-second scripted MainLab windows, fresh scene each time. Normal target brain/motor/physics; other agents and player input paused. Scripted player placement/motion, not keyboard gameplay. Scout receives real hearing-bus noise. Guard and Flanker receive visible relocation; Interceptor receives forward motion/reversal. At six seconds player is placed behind reachable room geometry, or explicitly logged off-map if none exists. Flanker additionally receives a temporary raised obstruction on its current goal at 4.5 seconds. Plan/request counters count observed changes of motor scheduling fields, not all policy/hearing route queries. Blocked entries/frames are motor state observations, not unique failed paths. Player damage sums actual health decreases; no weapon, HP or cooldown tuning. These windows are not encounter completion times or a fairness/unavoidable-hit verdict.\n");
            }
            finally
            {
                var scene = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After encounter evidence"));
                SceneManager.UnloadSceneAsync(scene); Time.captureDeltaTime = capture; Application.targetFrameRate = rate; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
            }
        }
        private static IEnumerator Capture(Component brain, string name)
        {
            var go = new GameObject("Temporary encounter evidence camera"); var camera = go.AddComponent<Camera>(); camera.fieldOfView = 42; camera.nearClipPlane = .05f;
            Vector3 target = brain.transform.position + Vector3.up * 1.65f;
            camera.transform.position = brain.transform.position + new Vector3(3, 2.7f, -3);
            for (int angle = 0; angle < 360; angle += 45)
            {
                Vector3 p = brain.transform.position + Quaternion.Euler(0, angle, 0) * new Vector3(3, 0, -3) + Vector3.up * 2.7f;
                if (Physics.SphereCastAll(target, .3f, (p - target).normalized, (p - target).magnitude, ~0, QueryTriggerInteraction.Ignore).Any(h => !h.collider.transform.IsChildOf(brain.transform))) continue;
                camera.transform.position = p; break;
            }
            camera.transform.LookAt(target); var render = new RenderTexture(960, 540, 24); render.Create(); camera.targetTexture = render; yield return null;
            var previous = RenderTexture.active; RenderTexture.active = render; var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); texture.Apply();
                using var writer = new BinaryWriter(File.Create(Path.Combine(Out, name + "-encounter.tga")));
                writer.Write(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); writer.Write((short)960); writer.Write((short)540); writer.Write((byte)24); writer.Write((byte)0);
                foreach (var pixel in texture.GetPixels32()) { writer.Write(pixel.b); writer.Write(pixel.g); writer.Write(pixel.r); }
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; render.Release(); Object.Destroy(render); Object.Destroy(texture); Object.Destroy(go); }
        }
    }
}
#endif
