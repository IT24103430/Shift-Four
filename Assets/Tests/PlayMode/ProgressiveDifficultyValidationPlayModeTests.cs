#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    // Controlled instrumented fixtures, not human playtests or encounter completion times.
    public sealed class ProgressiveDifficultyValidationPlayModeTests
    {
        private Component[] brains;
        private Component player;
        private float capture;
        private int rate;
        private CursorLockMode cursor;
        private bool cursorVisible;
        private static Type T(string n) => Type.GetType("ShiftFour." + n + ", Assembly-CSharp", true);
        private static object Get(object o, string n) => o.GetType().GetProperty(n).GetValue(o);
        private static FieldInfo Field(object o, string n)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new MissingFieldException(n);
        }
        private static object Call(object o, string n, params object[] args)
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (m != null) return m.Invoke(o, args);
            }
            throw new MissingMethodException(n);
        }
        private static Component[] Find(string n) => Object.FindObjectsByType(T(n), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static string Output => Environment.GetEnvironmentVariable("SHIFTFOUR_FINAL_EVIDENCE") ?? "Logs/ProgressiveDifficulty";
        private static void Write(string name, string text) { Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, name), text); }
        private static string Row(params object[] cells) => string.Join(",", cells.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture))) + "\n";
        private void PositionPlayer(Vector3 point)
        {
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = point; cc.enabled = true;
            Call(player, "ApplyCameraPose"); Physics.SyncTransforms();
        }
        private bool ShootingPosition(Component brain)
        {
            var target = brain.GetComponent<Collider>().bounds.center;
            foreach (float distance in new[] { 4f, 6f, 8f }) for (int angle = 0; angle < 360; angle += 15)
            {
                Vector3 point = brain.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(brain.transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                PositionPlayer(hit.position);
                Vector3 eye = (Vector3)Get(player, "EyePosition"); Vector3 direction = target - eye;
                if (Physics.Raycast(eye, direction.normalized, out var shot, direction.magnitude + .1f, ~0, QueryTriggerInteraction.Ignore) &&
                    shot.collider.GetComponentInParent(T("Health")) == brain.GetComponent(T("Health")))
                {
                    brain.transform.rotation = Quaternion.LookRotation(player.transform.position - brain.transform.position);
                    return true;
                }
            }
            return false;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; rate = Application.targetFrameRate; cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            Time.captureDeltaTime = 1f / 60; Application.targetFrameRate = 60;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            brains = Find("DroneBrain").OrderBy(b => b.name).ToArray(); player = Find("PlayerController").Single();
            ((Behaviour)player).enabled = false;
            foreach (var brain in brains)
            {
                ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = brain.GetComponent<NavMeshAgent>(); agent.isStopped = true; agent.ResetPath();
            }
            PositionPlayer(new Vector3(1000, 0, 1000)); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After progressive difficulty validation"));
            yield return SceneManager.UnloadSceneAsync(old);
            Time.captureDeltaTime = capture; Application.targetFrameRate = rate; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        [UnityTest] public IEnumerator AuthoredRooms_ActualPlayerHitscanMeasuresShotsToDefeatAtUnchangedCadence()
        {
            string csv = "agent,initial_hp,weapon_damage,accepted_shots,elapsed_sim_seconds,player_damage,fixture\n";
            float damage = (float)Field(player, "gunDamage").GetValue(player), cadence = (float)Field(player, "fireInterval").GetValue(player);
            Assert.That(damage, Is.EqualTo(25f)); Assert.That(cadence, Is.EqualTo(.24f));
            foreach (var brain in brains)
            {
                Assert.That(ShootingPosition(brain), Is.True, brain.name + " requires a reachable clear shot in its authored room");
                var health = brain.GetComponent(T("Health")); float hp = (float)Get(health, "Current");
                float playerHP = (float)Get(player.GetComponent(T("Health")), "Current"), began = Time.time; int shots = 0;
                while ((bool)Get(health, "IsAlive") && shots < 10)
                {
                    if (shots > 0) yield return new WaitForSeconds(cadence);
                    Vector3 origin = (Vector3)Get(player, "EyePosition"), target = brain.GetComponent<Collider>().bounds.center;
                    float before = (float)Get(health, "Current"); Call(player, "Shoot", new Ray(origin, (target - origin).normalized)); shots++;
                    Assert.That((float)Get(health, "Current"), Is.EqualTo(Mathf.Max(0, before - damage)), "Each counted shot must be an accepted production hitscan");
                }
                Assert.That(Get(health, "IsAlive"), Is.False); Assert.That(shots, Is.EqualTo(Mathf.CeilToInt(hp / damage)));
                csv += Row(brain.name, hp, damage, shots, Time.time - began, playerHP - (float)Get(player.GetComponent(T("Health")), "Current"), "stationary_targets_enemy_decisions_paused");
                yield return null;
            }
            Write("shots-to-defeat.csv", csv);
        }

        [UnityTest] public IEnumerator AuthoredRooms_VisiblePolicyAndCoverDisengagementCharacterization()
        {
            string csv = "agent,phase,sim_seconds,decision,state,player_damage,has_route,route_points,nav_status\n";
            foreach (var brain in brains)
            {
                Assert.That(ShootingPosition(brain), Is.True, brain.name);
                Field(brain, "nextShot").SetValue(brain, 0f);
                float hp = (float)Get(player.GetComponent(T("Health")), "Current"), start = Time.time;
                Field(brain, "nextDecision").SetValue(brain, 0f); Call(brain, "Update");
                Assert.That(Get(Get(brain, "Awareness"), "HasVisualConfirmation"), Is.True);
                string visibleDecision = (string)Get(brain, "CurrentDecision");
                csv += Trace(brain, "genuine_visible_observation", start, hp);
                // Find a walkable point behind real room geometry, with native route attachment.
                bool covered = false;
                foreach (float distance in new[] { 4f, 6f, 8f, 10f })
                {
                    for (int angle = 0; angle < 360 && !covered; angle += 15)
                    {
                        Vector3 point = brain.transform.position + Quaternion.Euler(0, angle, 0) * Vector3.forward * distance;
                        if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) continue;
                        var path = new NavMeshPath();
                        if (!NavMesh.CalculatePath(player.transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                        PositionPlayer(hit.position);
                        Vector3 muzzle = brain.transform.position + Vector3.up * 1.25f, eye = (Vector3)Get(player, "EyePosition");
                        covered = Physics.Raycast(muzzle, (eye - muzzle).normalized, out var obstruction, (eye - muzzle).magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                            !obstruction.collider.transform.IsChildOf(brain.transform) && obstruction.collider.GetComponentInParent(T("Health")) == null;
                    }
                    if (covered) break;
                }
                Assert.That(covered, Is.True, brain.name + " requires actual cover, not just facing outside its field of view");
                float coveredHP = (float)Get(player.GetComponent(T("Health")), "Current");
                for (int i = 0; i < 6; i++)
                {
                    Field(brain, "nextDecision").SetValue(brain, 0f); Call(brain, "Update"); Call(brain, "TryShoot");
                    Assert.That(Get(Get(brain, "Awareness"), "HasVisualConfirmation"), Is.False);
                    Assert.That(Get(player.GetComponent(T("Health")), "Current"), Is.EqualTo(coveredHP), "Solid cover must prevent accepted damage");
                    yield return new WaitForSeconds(.3f);
                    csv += Trace(brain, "solid_cover_enemy_motion_paused", start, hp);
                }
                Assert.That(visibleDecision, Is.Not.Empty);
                PositionPlayer(new Vector3(1000, 0, 1000));
            }
            Write("policy-cover-characterization.csv", csv);
        }
        private string Trace(Component brain, string phase, float began, float hp)
        {
            var motor = brain.GetComponent(T("DroneMotor")); var agent = brain.GetComponent<NavMeshAgent>();
            return Row(brain.name, phase, Time.time - began, Get(brain, "CurrentDecision"), Get(Get(brain, "Awareness"), "State"),
                hp - (float)Get(player.GetComponent(T("Health")), "Current"), Get(motor, "HasRoute"), ((IList)Field(motor, "route").GetValue(motor)).Count, agent.pathStatus);
        }

        [UnityTest] public IEnumerator NaturalDecisionCadence_MeasuresHearingInvestigationAndVisualResponse()
        {
            foreach (var brain in brains) ((Behaviour)brain).enabled = true;
            // Native motion is paused, but normal brain Update scheduling and all sensory checks run.
            yield return new WaitForSeconds(11f);
            string csv = "agent,hearing_latency_sim_s,investigation_latency_sim_s,visual_latency_sim_s,player_damage,fixture\n";
            foreach (var brain in brains)
            {
                var awareness = Get(brain, "Awareness"); var hearing = Get(brain, "Hearing");
                Assert.That(Get(awareness, "State").ToString(), Is.EqualTo("Patrol"));
                bool heard = false; float emitted = Time.time;
                for (int i = 0; i < 12 && !heard; i++)
                {
                    var point = brain.transform.position + Quaternion.Euler(0, i * 30, 0) * Vector3.forward * .6f;
                    if (!NavMesh.SamplePosition(point, out var hit, .1f, NavMesh.AllAreas)) continue;
                    emitted = Time.time; T("SoundEvents").GetMethod("ReportShot").Invoke(null, new object[] { hit.position });
                    object[] args = { null }; heard = (bool)hearing.GetType().GetMethod("TryGetLatest").Invoke(hearing, args);
                    if (!heard) yield return null;
                }
                Assert.That(heard, Is.True, brain.name);
                float deadline = Time.time + .5f;
                while ((float)Get(awareness, "LastHeardAt") != emitted && Time.time < deadline) yield return null;
                Assert.That((float)Get(awareness, "LastHeardAt"), Is.EqualTo(emitted));
                float hearingLatency = Time.time - emitted;
                Assert.That(hearingLatency, Is.LessThanOrEqualTo(.35f));
                deadline = Time.time + 1f;
                while (Get(awareness, "State").ToString() != "Investigate" && Time.time < deadline) yield return null;
                Assert.That(Get(awareness, "State").ToString(), Is.EqualTo("Investigate"));
                float investigationLatency = Time.time - emitted;
                Assert.That(ShootingPosition(brain), Is.True);
                float visibleAt = Time.time, hp = (float)Get(player.GetComponent(T("Health")), "Current"); deadline = Time.time + .5f;
                while (!(bool)Get(awareness, "HasVisualConfirmation") && Time.time < deadline) yield return null;
                Assert.That(Get(awareness, "HasVisualConfirmation"), Is.True);
                Assert.That(Time.time - visibleAt, Is.LessThanOrEqualTo(.35f));
                csv += Row(brain.name, hearingLatency, investigationLatency, Time.time - visibleAt,
                    hp - (float)Get(player.GetComponent(T("Health")), "Current"), "scripted_placement_normal_brain_schedule_motion_paused");
                PositionPlayer(new Vector3(1000, 0, 1000));
            }
            Write("natural-response-latencies.csv", csv);
        }

        [UnityTest] public IEnumerator LiveSensingProfile_RealDispatchAndInclusiveNavigationCosts()
        {
            bool wasEnabled = UnityEngine.Profiling.Profiler.enabled;
            bool scriptsEnabled = UnityEngine.Profiling.Profiler.IsCategoryEnabled(ProfilerCategory.Scripts);
            UnityEngine.Profiling.Profiler.enabled = true; UnityEngine.Profiling.Profiler.SetCategoryEnabled(ProfilerCategory.Scripts, true);
            string[] names = { "Hearing", "FindRoute", "DroneBody", "Decision", "DroneAnimation", "Effects" };
            var recorders = names.Select(n => ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "ShiftFour." + n, 1024,
                ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame)).ToArray();
            var previous = new int[names.Length];
            string csv = "frame,wall_ms,emissions,dispatch_ms," + string.Join(",", names.SelectMany(n => new[] { n + "_ms", n + "_calls" })) + "\n";
            try
            {
                foreach (var brain in brains) { ((Behaviour)brain).enabled = true; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = true; }
                double before = Time.realtimeSinceStartupAsDouble;
                for (int frame = 0; frame < 240; frame++)
                {
                    int emissions = frame % 12 == 0 ? 1 : 0; double dispatch = 0;
                    if (emissions > 0)
                    {
                        var brain = brains[(frame / 12) % 4];
                        var category = Enum.Parse(T("NoiseCategory"), new[] { "Footstep", "Sprint", "Gunshot", "ConsoleInteraction" }[(frame / 48) % 4]);
                        float intensity = (float)T("SoundEvents").GetMethod("DefaultIntensity").Invoke(null, new[] { category });
                        var noise = Activator.CreateInstance(T("NoiseEvent"), brain.transform.position + Vector3.forward * .6f, category, intensity, Time.time, null);
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        T("SoundEvents").GetMethod("ReportNoise", new[] { T("NoiseEvent") }).Invoke(null, new[] { noise });
                        timer.Stop(); dispatch = timer.Elapsed.TotalMilliseconds;
                    }
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    csv += Row(frame, (now - before) * 1000, emissions, dispatch).TrimEnd('\n'); before = now;
                    for (int m = 0; m < names.Length; m++)
                    {
                        long nanoseconds = 0, calls = 0;
                        Assert.That(recorders[m].WrappedAround, Is.False);
                        for (int s = previous[m]; s < recorders[m].Count; s++) { var sample = recorders[m].GetSample(s); nanoseconds += sample.Value; calls += sample.Count; }
                        previous[m] = recorders[m].Count; csv += "," + (nanoseconds / 1000000.0).ToString("F6", CultureInfo.InvariantCulture) + "," + calls;
                    }
                    csv += "\n";
                }
                Write("sensing-profile-frames.csv", csv);
                Write("sensing-profile-availability.csv", "marker,valid,samples\n" + string.Concat(names.Select((n, i) => Row(n, recorders[i].Valid, recorders[i].Count))));
                Write("sensing-profile-context.txt", $"Unity={Application.unityVersion}; GPU={SystemInfo.graphicsDeviceName}; CPU={SystemInfo.processorType}; captureDeltaTime={Time.captureDeltaTime}; targetFrameRate={Application.targetFrameRate}\n240 rendered Editor frames; fixed simulation step; stationary hidden player; real active brains/motors; 20 scripted dispatcher emissions near drones, four categories. Wall times include pacing, Editor/render and test harness. Dispatch Stopwatch includes reflection overhead. Inclusive nested markers must not be summed. This is not a standalone-player or human encounter benchmark.\n");
                Write("hearing-counters.csv", "agent,evaluations,physics_queries,native_paths,graph_queries,budget_rejections\n" + string.Concat(brains.Select(b =>
                {
                    var h = Get(b, "Hearing"); return Row(b.name, Get(h, "EvaluationCount"), Get(h, "PhysicsQueryCount"), Get(h, "NativePathQueryCount"), Get(h, "GraphQueryCount"), Get(h, "BudgetRejections"));
                })));
                string anchors = "gate,console,source_x,source_y,source_z,strict_navigation_attachment\n";
                foreach (var console in Find("ConsoleSwitch"))
                {
                    var position = console.transform.position; var floor = new Vector3(position.x, 0, position.z);
                    bool attached = NavMesh.SamplePosition(floor, out var hit, .35f, NavMesh.AllAreas) &&
                        Vector2.Distance(new Vector2(floor.x, floor.z), new Vector2(hit.position.x, hit.position.z)) <= .05f;
                    anchors += Row(Get(console, "GateId"), console.name, position.x, position.y, position.z, attached);
                }
                Write("console-sound-anchors.csv", anchors);
                Write("authored-post-clearance.csv", "agent,home_x,home_y,home_z,full_body_can_occupy\n" + string.Concat(brains.Select(b =>
                {
                    var home = (Vector3)Get(b, "Home"); return Row(b.name, home.x, home.y, home.z, Call(b.GetComponent(T("DroneBody")), "CanOccupy", home));
                })));
            }
            finally
            {
                foreach (var recorder in recorders) recorder.Dispose();
                UnityEngine.Profiling.Profiler.SetCategoryEnabled(ProfilerCategory.Scripts, scriptsEnabled); UnityEngine.Profiling.Profiler.enabled = wasEnabled;
            }
        }
    }
}
#endif
