#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Profiling;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class CombatAuditPlayModeTests
    {
        private static readonly Vector3 Arena = new Vector3(1000f, 0f, 1000f);
        private Component player, session, health;
        private Component[] brains;
        private CursorLockMode cursorLock;
        private bool cursorVisible;
        private int frameRate;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Component[] Find(string name) => Object.FindObjectsByType(T(name), FindObjectsSortMode.None).Cast<Component>().ToArray();
        private static FieldInfo Field(Component c, string name)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }
        private static void Set(Component c, string name, object value) => Field(c, name).SetValue(c, value);
        private static object Get(Component c, string name) => Field(c, name).GetValue(c);
        private static object Prop(object c, string name) => c.GetType().GetProperty(name).GetValue(c);
        private static object Record(Component c, string name)
        {
            object record = Prop(c, "LastDamage");
            return record.GetType().GetField(name).GetValue(record);
        }
        private static object Call(Component c, string name, params object[] args)
        {
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                var method = t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null) return method.Invoke(c, args);
            }
            throw new MissingMethodException(name);
        }
        private float HP => (float)Prop(health, "Current");
        private bool Ended => (bool)Prop(session, "Ended");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            frameRate = Application.targetFrameRate;
            Application.targetFrameRate = 60;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            player = Find("PlayerController").Single(); session = Find("GameSession").Single();
            health = player.GetComponent(T("Health")); brains = Find("DroneBrain");
            ((Behaviour)player).enabled = false;
            foreach (var brain in brains) ((Behaviour)brain).enabled = false;
            yield return new WaitForSeconds(.2f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("After combat audit"));
            if (scene.path == "Assets/Scenes/MainLab.unity") yield return SceneManager.UnloadSceneAsync(scene);
            Application.targetFrameRate = frameRate;
            Time.timeScale = 1f;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
        }

        // Actual MainLab components, with movement suspended and actors outside scene geometry.
        // This isolates damage rules from tactical movement, cover placement and user input.
        private void CombatLayout(int attackers)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false; player.transform.position = Arena; cc.enabled = true;
            Call(player, "ApplyCameraPose");
            Call(health, "Configure", 100f, true);
            for (int i = 0; i < brains.Length; i++)
            {
                var brain = brains[i];
                ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                brain.GetComponent<NavMeshAgent>().enabled = false;
                brain.transform.position = Arena + (i < attackers
                    ? Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward * 5f
                    : Vector3.right * (40f + i * 20f));
                brain.transform.LookAt(new Vector3(Arena.x, brain.transform.position.y, Arena.z));
                Set(brain, "nextDecision", 0f); Set(brain, "nextShot", 0f);
            }
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator SustainedCombat_ValidHitsCauseDeathAndRestartRestoresMainLab()
        {
            CombatLayout(1);
            var attacker = brains[0];
            Assert.That((bool)Call(attacker, "CanSeePlayer"), Is.True);
            ((Behaviour)attacker).enabled = true; // Real Update and its 0.3 s decisions, no timer overrides.
            float started = Time.time, previousHit = float.NegativeInfinity, previousHP = HP;
            int hits = 0;
            while (!Ended && Time.time - started < 16f)
            {
                yield return null;
                if (HP == previousHP) continue;
                Assert.That(previousHP - HP, Is.EqualTo(10f).Within(.001f));
                float hitTime = (float)Record(health, "Time");
                Assert.That(hitTime - previousHit, Is.GreaterThanOrEqualTo(1.099f));
                Assert.That(Record(health, "Attacker"), Is.SameAs(attacker));
                previousHit = hitTime; previousHP = HP; hits++;
            }
            Assert.That(Ended, Is.True); Assert.That(HP, Is.Zero); Assert.That(hits, Is.EqualTo(10));
            Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(9.9f));
            Assert.That(Get(session, "outcome").ToString(), Does.Contain("SYSTEM FAILURE"));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            int deathFrame = Time.frameCount;
            yield return new WaitForSeconds(1.3f);
            Assert.That(Time.frameCount, Is.GreaterThan(deathFrame));
            Assert.That((float)Record(health, "Time"), Is.EqualTo(previousHit));
            Assert.That(HP, Is.Zero);
            Call(session, "Restart");
            yield return null; yield return null;
            Assert.That((float)Prop(Find("Health").Single(h => h.GetComponent(T("PlayerController")) != null), "Current"), Is.EqualTo(100f));
            Assert.That((bool)Prop(Find("GameSession").Single(), "Ended"), Is.False);
            Assert.That(Find("DroneBrain").All(b => (bool)Prop(b.GetComponent(T("Health")), "IsAlive")), Is.True);
            Assert.That(Find("LabDoor").All(d => !(bool)Prop(d, "IsOpen")), Is.True);
        }

        [UnityTest]
        public IEnumerator MultipleAttackers_EachKeepsOwnCooldownAndSessionEndsOnlyAtZeroHP()
        {
            CombatLayout(4);
            float[] previousHit = Enumerable.Repeat(float.NegativeInfinity, 4).ToArray();
            int[] hits = new int[4];
            float start = Time.time;
            // Call each real attack path every frame, including duplicate attempts in the same frame.
            while (!Ended && Time.time - start < 6f)
            {
                for (int i = 0; i < 4; i++)
                {
                    float before = HP;
                    Call(brains[i], "TryShoot"); Call(brains[i], "TryShoot");
                    if (HP == before) continue;
                    Assert.That(before - HP, Is.EqualTo(10f));
                    Assert.That(Time.time - previousHit[i], Is.GreaterThanOrEqualTo(1.099f));
                    Assert.That(Record(health, "Attacker"), Is.SameAs(brains[i]));
                    previousHit[i] = Time.time; hits[i]++;
                    Assert.That(Ended, Is.EqualTo(HP == 0f));
                }
                yield return null;
            }
            Assert.That(hits.Sum(), Is.EqualTo(10));
            Assert.That(hits.All(n => n >= 2), Is.True, "All four must contribute valid attacks.");
            Assert.That(Ended, Is.True); Assert.That(HP, Is.Zero);
            TestContext.WriteLine($"Four attackers: {Time.time - start:0.000}s to death, hit counts [{string.Join(",", hits)}].");
        }

        [UnityTest]
        public IEnumerator AllFourAttackers_RejectCoverEmbeddedOriginRangeDeadAttackerAndDeadTarget()
        {
            CombatLayout(0);
            foreach (var attacker in brains)
            {
                attacker.transform.position = Arena + Vector3.forward * 5f;
                attacker.transform.rotation = Quaternion.LookRotation(Vector3.back);
                Call(health, "Configure", 100f, false);
                Set(attacker, "nextShot", 0f); Physics.SyncTransforms();
                Assert.That((bool)Call(attacker, "CanSeePlayer"), Is.True, attacker.name);
                Call(attacker, "TryShoot"); Call(attacker, "TryShoot");
                Assert.That(HP, Is.EqualTo(90f), attacker.name + " immediate duplicate");
                Set(attacker, "nextShot", 0f);
                var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.transform.position = Arena + new Vector3(0f, 1.5f, 2.5f);
                cover.transform.localScale = new Vector3(4f, 3f, .2f);
                Physics.SyncTransforms(); Call(attacker, "TryShoot");
                Assert.That(HP, Is.EqualTo(90f), attacker.name + " cover");
                cover.transform.position = (Vector3)T("DroneBrain").GetProperty("CombatOrigin",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attacker);
                Physics.SyncTransforms(); Call(attacker, "TryShoot");
                Assert.That(HP, Is.EqualTo(90f), attacker.name + " embedded origin");
                Object.Destroy(cover); yield return null;
                attacker.transform.position = Arena + Vector3.forward * 13f;
                Physics.SyncTransforms(); Call(attacker, "TryShoot");
                Assert.That(HP, Is.EqualTo(90f), attacker.name + " range");
                attacker.transform.position = Arena + Vector3.forward * 5f;
                Call(attacker.GetComponent(T("Health")), "TakeDamage", 1000f);
                Physics.SyncTransforms(); Call(attacker, "TryShoot");
                Assert.That(HP, Is.EqualTo(90f), attacker.name + " dead attacker");
                attacker.transform.position = Arena + Vector3.right * 100f;
            }
            Call(health, "TakeDamage", 1000f);
            Assert.That(Ended, Is.True); Assert.That(HP, Is.Zero);
            foreach (var attacker in brains) Call(attacker, "TryShoot");
            Assert.That(HP, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OptionalDiagnostics_ReportsAttributionAndFatalReason()
        {
            CombatLayout(1);
            Set(health, "logDamageDiagnostics", true);
            LogAssert.Expect(LogType.Log, new Regex(@"\[Damage\].*attacker=.*damage=40 HP=100->60 time=.*reason=Audit hit death=none"));
            Call(health, "ApplyDamage", 40f, brains[0], "Audit hit");
            LogAssert.Expect(LogType.Log, new Regex(@"\[Damage\].*damage=60 HP=60->0 time=.*reason=Audit fatal hit death=Audit fatal hit"));
            Call(health, "ApplyDamage", 60f, brains[0], "Audit fatal hit");
            Assert.That(Record(health, "Attacker"), Is.SameAs(brains[0]));
            Assert.That(Record(health, "Reason"), Is.EqualTo("Audit fatal hit"));
            Assert.That(Ended, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainLab_RaisedVisualsStayWithinSweptEnvelopeAtEveryYawAndHoverExtreme()
        {
            var vertices = new Dictionary<Mesh, Vector3[]>();
            foreach (var brain in brains)
            {
                var body = brain.GetComponent(T("DroneBody"));
                var motor = brain.GetComponent(T("DroneMotor"));
                ((Behaviour)motor).enabled = false;
                brain.GetComponent<NavMeshAgent>().enabled = false;
                var visual = (Transform)Get(motor, "visual");
                // This fixture independently checks the legacy hover budget. Restore rest tilt
                // before applying its manual +/- .08 m offsets; maximum animated poses have a separate sweep test.
                ((Behaviour)visual.GetComponent(T("DroneRotorVisuals"))).enabled = false;
                var meshes = visual.GetComponentsInChildren<MeshFilter>();
                foreach (var filter in meshes)
                {
                    if (vertices.ContainsKey(filter.sharedMesh)) continue;
                    // Editor read-only access leaves the imported Read/Write setting unchanged.
                    using var data = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                    using var positions = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp);
                    data[0].GetVertices(positions);
                    vertices[filter.sharedMesh] = positions.ToArray();
                }
                var rest = (Vector3)Get(motor, "visualRestPosition");
                var envelope = new Bounds(brain.transform.position + (Vector3)Get(body, "envelopeCenter"), (Vector3)Get(body, "envelopeExtents") * 2f);
                envelope.Expand(.02f);
                for (int yaw = 0; yaw < 360; yaw += 15)
                foreach (float hover in new[] { -.08f, .08f })
                {
                    brain.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    visual.localPosition = rest + Vector3.up * hover;
                    foreach (var mesh in meshes)
                    {
                        // Rotated Renderer.bounds includes empty AABB corners. Check real vertices,
                        // matching the envelope authoring utility, rather than those empty corners.
                        bool contained = vertices[mesh.sharedMesh].All(v => envelope.Contains(mesh.transform.TransformPoint(v)));
                        Assert.That(contained, Is.True, brain.name + "/" + mesh.name + " yaw=" + yaw + " hover=" + hover);
                    }
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllFourRaisedModels_BlockPlayerWalkingIntoTheirVisibleMeshes()
        {
            CombatLayout(0);
            var cc = player.GetComponent<CharacterController>();
            var proxyObject = new GameObject("Inner player capsule for penetration query");
            SceneManager.MoveGameObjectToScene(proxyObject, SceneManager.GetActiveScene());
            var proxy = proxyObject.AddComponent<CapsuleCollider>();
            proxy.enabled = false;
            proxy.center = cc.center;
            proxy.height = cc.height - 2f * cc.skinWidth;
            proxy.radius = cc.radius - cc.skinWidth;
            foreach (var brain in brains)
            {
                brain.transform.position = Arena;
                for (int yaw = 0; yaw < 360; yaw += 45)
                {
                    brain.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    foreach (Vector3 heading in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                    {
                        cc.enabled = false;
                        player.transform.position = Arena + heading * 4f;
                        cc.enabled = true;
                        Physics.SyncTransforms();
                        cc.Move(-heading * 5f);
                        Assert.That(Vector3.Distance(player.transform.position, Arena), Is.GreaterThan(.7f), brain.name + " must block player movement");
                        foreach (var mesh in brain.GetComponentsInChildren<MeshCollider>())
                        {
                            bool penetrating = Physics.ComputePenetration(proxy, player.transform.position, player.transform.rotation,
                                mesh, mesh.transform.position, mesh.transform.rotation, out _, out float depth);
                            Assert.That(penetrating && depth > .01f, Is.False, brain.name + "/" + mesh.name + " penetration=" + depth + " yaw=" + yaw);
                        }
                    }
                }
                brain.transform.position = Arena + Vector3.right * 100f;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainLab_ProfileNavigationQueriesAndPhysicsSynchronization()
        {
            Call(health, "Configure", 10000f, true); // Measurement fixture only; serialized balance stays untouched.
            foreach (var brain in brains) ((Behaviour)brain).enabled = true;
            yield return new WaitForSeconds(1f);
            bool profilerEnabled = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;
            bool scriptsEnabled = UnityEngine.Profiling.Profiler.IsCategoryEnabled(ProfilerCategory.Scripts);
            UnityEngine.Profiling.Profiler.SetCategoryEnabled(ProfilerCategory.Scripts, true);
            string[] names = { "ShiftFour.FindRoute", "ShiftFour.CanConnect", "ShiftFour.GoToCombat", "ShiftFour.ClearShot", "ShiftFour.DroneBody", "ShiftFour.SyncTransforms" };
            string[] types = { "WaypointGraph", "WaypointGraph", "DroneMotor", "DroneBrain", "DroneBody", "DroneBody" };
            string[] fields = { "RouteMarker", "ConnectMarker", "CombatGoalMarker", "ShotQueryMarker", "BodyMarker", "SyncMarker" };
            var recorders = names.Select((n, i) => ProfilerRecorder.StartNew(
                (ProfilerMarker)T(types[i]).GetField(fields[i], BindingFlags.NonPublic | BindingFlags.Static).GetValue(null),
                524288, ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.CollectOnlyOnCurrentThread)).ToArray();
            var previousCounts = new int[names.Length];
            var maxCalls = new long[names.Length];
            var maxFrameMs = new double[names.Length];
            var frames = new List<float>();
            var interceptor = brains.Single(b => b.GetType().Name == "InterceptorBrain");
            float start = Time.time;
            try
            {
                while (Time.time - start < 12f)
                {
                    // Second half: exercise the real Interceptor's recent-velocity branch while
                    // retaining MainLab graph/gates/native paths. This is an explicit stress fixture.
                    if (Time.time - start > 6f)
                    {
                        Set(interceptor, "lastSeenAt", Time.time);
                        Set(interceptor, "lastKnown", interceptor.transform.position + Vector3.back * 3f);
                        Set(interceptor, "observedVelocity", Vector3.forward * 5f);
                    }
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime * 1000f);
                    for (int i = 0; i < recorders.Length; i++)
                    {
                        int count = recorders[i].Count;
                        long calls = 0, ns = 0;
                        for (int j = previousCounts[i]; j < count; j++)
                        { var value = recorders[i].GetSample(j); calls += value.Count; ns += value.Value; }
                        previousCounts[i] = count;
                        maxCalls[i] = Math.Max(maxCalls[i], calls);
                        maxFrameMs[i] = Math.Max(maxFrameMs[i], ns / 1e6);
                    }
                }
                frames.Sort();
                TestContext.WriteLine($"PROFILE: n={frames.Count} frame_ms p50={frames[frames.Count / 2]:0.000} p95={frames[(int)(frames.Count * .95f)]:0.000} max={frames.Last():0.000} (Editor batch, target 60 fps, includes pacing). ");
                for (int i = 0; i < names.Length; i++)
                {
                    Assert.That(recorders[i].Valid, Is.True, names[i] + " recorder");
                    Assert.That(recorders[i].WrappedAround, Is.False, names[i] + " must retain the full measurement window");
                    var samples = new List<ProfilerRecorderSample>(); recorders[i].CopyTo(samples);
                    Assert.That(samples.Count, Is.GreaterThan(0), names[i] + " must capture actual samples");
                    TestContext.WriteLine($"PROFILE: {names[i]} samples={samples.Count} calls={samples.Sum(s => s.Count)} max_calls_frame={maxCalls[i]} total_ms={samples.Sum(s => s.Value) / 1e6:0.000} max_ms_frame={maxFrameMs[i]:0.000} max_ms_call={(samples.Count > 0 ? samples.Max(s => s.Value) / 1e6 : 0):0.000}");
                }
            }
            finally
            {
                foreach (var recorder in recorders) recorder.Dispose();
                UnityEngine.Profiling.Profiler.SetCategoryEnabled(ProfilerCategory.Scripts, scriptsEnabled);
                UnityEngine.Profiling.Profiler.enabled = profilerEnabled;
            }
        }
    }
}
#endif
