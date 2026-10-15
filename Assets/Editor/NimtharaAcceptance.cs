using System;
using System.IO;
using System.Linq;
using ShiftFour;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// No extra test package required. Run on a disposable project copy: modifies scene/prefab assets.
[InitializeOnLoad]
public static class NimtharaAcceptance
{
    private const string Running = "NimtharaQA.Running";
    private const string Phase = "NimtharaQA.Phase";
    private static float next;
    private static int count;
    private static Vector3[] positions;
    private static Quaternion[] rotations;
    private static Vector3 guardStart;
    private static Transform[] rotorPivots;
    private static Quaternion[] rotorAngles;
    static NimtharaAcceptance()
    {
        EditorApplication.update += Tick;
        SceneManager.sceneLoaded += (_, __) =>
        {
            if (SessionState.GetBool(Running, false) && SessionState.GetInt(Phase, 0) >= 5)
                foreach (DroneBrain drone in Object.FindObjectsByType<DroneBrain>()) drone.enabled = false;
        };
    }
    private static void Check(bool value, string description)
    {
        if (!value) throw new Exception("FAIL: " + description);
        count++;
        Debug.Log("NIMTHARA PASS: " + description);
    }
    public static void VerifyUntouchedPrefabs()
    {
        string[] paths = new[] { "Scout", "Guard", "Interceptor", "Flanker" }.Select(n => "Assets/Prefabs/Agents/" + n + ".prefab").ToArray();
        byte[][] before = paths.Select(File.ReadAllBytes).ToArray();
        ShiftFourModelInstaller.Install();
        for (int i = 0; i < paths.Length; i++) Check(before[i].SequenceEqual(File.ReadAllBytes(paths[i])), "unchanged agent prefab bytes: " + paths[i]);
    }
    public static void InspectAssets()
    {
        foreach (string path in new[] { "Assets/Models/SF_GuardDrone_GV.fbx", "Assets/Models/SF_HeavySecurityDoor_GV.fbx" })
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                Debug.Log("GV IMPORT " + part.name + " position=" + part.localPosition + " rotation=" + part.localEulerAngles + " scale=" + part.localScale);
        }
    }
    public static void RunAll()
    {
        try
        {
            for (int gate = 0; gate < 4; gate++)
            for (int order = 0; order < 2; order++)
            {
                var state = new MissionProgress();
                Check(!state.CanOpen(gate), "gate starts locked " + gate);
                if (order == 0) state.ActivateConsole(gate); else state.DefeatDrone(gate);
                Check(!state.CanOpen(gate), "one prerequisite cannot open " + gate);
                if (order == 0) state.DefeatDrone(gate); else state.ActivateConsole(gate);
                Check(state.CanOpen(gate), "AND works in order " + order + " for " + gate);
                Check(!state.ActivateConsole(gate) && !state.DefeatDrone(gate), "duplicates ignored");
                Check(state.ConsolesOnline == 1 && state.DronesDefeated == 1 && !state.Complete, "identity counts");
                for (int other = 0; other < 4; other++)
                    if (other != gate) Check(!state.CanOpen(other), "unrelated gate unchanged");
            }
            GraphTests();
            NimtharaServerInstaller.Install();
            ShiftFourModelInstaller.Install();
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SF_HeavySecurityDoor_GV.fbx") != null) ShiftFourModelInstaller.ApplyGVArt();
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainLab.unity");
            int before = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            var graph = Object.FindAnyObjectByType<WaypointGraph>();
            Vector3 original = graph.NodePosition(0);
            ShiftFourModelInstaller.UpdateSceneLayout(scene);
            NimtharaServerInstaller.Install();
            int after = scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Length);
            Check(before == after && graph.NodePosition(0) == original, "migration rerun preserves hierarchy and graph");
            Check(Object.FindObjectsByType<LabDoor>().Length == 4 && graph.NodeCount == 5, "four doors and exit graph node");
            Check(Object.FindObjectsByType<ConsoleSwitch>().Select(c => c.GateId).Distinct().Count() == 4, "four console identities");
            Check(Object.FindObjectsByType<RoomZone>().Length == 4, "room zones installed");
            Check(GameObject.Find("Sealed checkpoint-storage wall") != null, "legacy shortcut sealed");
            Check(GameObject.Find("Sliding shortcut door") == null, "legacy moving shortcut removed");
            Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "no missing scripts in scene hierarchy");
            Bake();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(Running, true);
            SessionState.SetInt(Phase, 0);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Fail(error); }
    }
    private static void GraphTests()
    {
        GameObject root = new GameObject("QA graph");
        var graph = root.AddComponent<WaypointGraph>();
        var nodes = new Transform[5];
        for (int i = 0; i < 5; i++)
        {
            nodes[i] = new GameObject("node").transform;
            nodes[i].SetParent(root.transform); nodes[i].position = new Vector3(i * 10, 0, 0);
        }
        graph.Configure(nodes, Enumerable.Range(0, 4).Select(i => new GraphEdge(i, i + 1, (SecurityGate)i)).ToArray());
        Check(graph.FindRoute(nodes[0].position, nodes[4].position).Count == 0, "closed A* returns no route");
        for (int i = 0; i < 4; i++)
        {
            int version = graph.Version;
            graph.SetGateOpen(i, true);
            Check(graph.Version == version + 1, "gate invalidates route");
            graph.SetGateOpen(i, true);
            Check(graph.Version == version + 1, "duplicate gate event stable version");
            Check(graph.FindRoute(nodes[i].position, nodes[i + 1].position).Count > 0, "open edge routes");
            if (i < 3) Check(graph.FindRoute(nodes[0].position, nodes[4].position).Count == 0, "later gate still blocks route");
        }
        Check(graph.FindRoute(nodes[0].position, nodes[4].position).Count > 0, "all gates route to exit");
        graph.SetGateOpen(2, false);
        Check(graph.FindRoute(nodes[0].position, nodes[4].position).Count == 0, "reclosing invalidates route");
        Object.DestroyImmediate(root);
    }
    private static void Bake()
    {
        var surface = Object.FindAnyObjectByType<NavMeshSurface>();
        var stored = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/Scenes/MainLab_NavMesh.asset");
        surface.BuildNavMesh();
        var generated = surface.navMeshData;
        surface.RemoveData();
        if (stored != null) { EditorUtility.CopySerialized(generated, stored); surface.navMeshData = stored; EditorUtility.SetDirty(stored); }
        else AssetDatabase.CreateAsset(generated, "Assets/Scenes/MainLab_NavMesh.asset");
        surface.AddData();
        EditorUtility.SetDirty(surface);
    }
    private static LabDoor[] Gates() => Object.FindObjectsByType<LabDoor>().OrderBy(g => g.GateId).ToArray();
    private static ConsoleSwitch Console(int id) => Object.FindObjectsByType<ConsoleSwitch>().Single(c => c.GateId == id);
    private static DroneBrain Drone(int id) => Object.FindObjectsByType<DroneBrain>().Single(d =>
        id == 0 ? d is ScoutBrain : id == 1 ? d is GuardBrain : id == 2 ? d is InterceptorBrain : d is FlankerBrain);
    private static void Later(int phase, double delay)
    {
        SessionState.SetInt(Phase, phase); next = Time.time + (float)delay;
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Running, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < next) return;
        try
        {
            var session = GameSession.Instance;
            if (session == null || Object.FindObjectsByType<LabDoor>().Length != 4) return;
            int phase = SessionState.GetInt(Phase, 0);
            if (phase == 0)
            {
                rotorPivots = Object.FindObjectsByType<DroneRotorVisuals>().SelectMany(v => v.GetComponentsInChildren<Transform>()).Where(t => t.name.StartsWith("Animated Rotor")).ToArray();
                Check(rotorPivots.Length == 16, "all four drones have four runtime rotor pivots");
                rotorAngles = rotorPivots.Select(t => t.localRotation).ToArray();
                Later(1, 1); return;
            }
            if (phase == 1)
            {
                foreach (DroneBrain drone in Object.FindObjectsByType<DroneBrain>()) drone.enabled = false;
                session.Player.enabled = false;
                Check(session.ConsolesOnline == 0 && session.DronesRemaining == 4 && !session.Ended, "fresh runtime state");
                for (int i=0;i<rotorPivots.Length;i++) Check(Quaternion.Angle(rotorAngles[i],rotorPivots[i].localRotation) > .1f,"independent live rotor spin " + i);
                guardStart = Drone(1).transform.position;
                Drone(1).GetComponent<DroneMotor>().GoTo(new Vector3(-14, 0, 12));
                Check(Drone(1).GetComponentsInChildren<MeshFilter>().Count(f => f.name.StartsWith("Rotor")) == 4, "four imported Guard rotors");
                positions = Gates().Select(g => g.transform.position).ToArray();
                rotations = Gates().Select(g => g.transform.rotation).ToArray();
                foreach (LabDoor gate in Gates())
                {
                    CheckStatus(gate,new Color(.95f,.12f,.06f));
                    Check(!gate.IsOpen && gate.GetComponent<Collider>().enabled && !session.Graph.IsGateOpen(gate.GateId), "initial barrier " + gate.GateId);
                    var panel = gate.transform.Find("Door assembly/LeftPanel");
                    Bounds bounds = new Bounds(new Vector3(-1, 1.5f, 0), Vector3.zero);
                    foreach (MeshFilter filter in panel.GetComponentsInChildren<MeshFilter>())
                    {
                        Bounds mesh = filter.sharedMesh.bounds;
                        for (int mask = 0; mask < 8; mask++)
                        {
                            Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((mask & 1) == 0 ? -1 : 1, (mask & 2) == 0 ? -1 : 1, (mask & 4) == 0 ? -1 : 1));
                            bounds.Encapsulate(gate.transform.InverseTransformPoint(filter.transform.TransformPoint(corner)));
                        }
                    }
                    Check(bounds.size.x > 1.9f && bounds.size.x < 2.2f && bounds.size.y > 2.9f && bounds.size.y < 3.2f, "imported leaf metre scale " + gate.GateId);
                    Check(Physics.Raycast(gate.transform.TransformPoint(new Vector3(0, 1.4f, -1)), gate.transform.forward, 2f, ~0, QueryTriggerInteraction.Ignore), "closed gate blocks physical ray " + gate.GateId);
                    Check(!PassageRoute(gate), "closed gate blocks NavMesh " + gate.GateId);
                }
                session.TryExit(); Check(!session.Ended, "premature exit denied");
                foreach (int id in Enumerable.Range(0, 4))
                {
                    if (id % 2 == 1) Console(id).Interact(); else Drone(id).GetComponent<Health>().TakeDamage(10000);
                }
                rotorAngles = rotorPivots.Select(t => t.localRotation).ToArray();
                Later(2, .5); return;
            }
            if (phase == 2)
            {
                for (int i=0;i<rotorPivots.Length;i++)
                    if (!rotorPivots[i].GetComponentInParent<Health>().IsAlive) Check(Quaternion.Angle(rotorAngles[i],rotorPivots[i].localRotation)<.01f,"dead rotor stopped " + i);
                Check(Vector3.Distance(guardStart, Drone(1).transform.position) > .05f, "Guard follows reachable route");
                Check(Gates().All(g => !g.IsOpen && g.Status == "LOCKED"), "one condition leaves all doors locked");
                foreach (int id in Enumerable.Range(0, 4))
                {
                    if (id % 2 == 1) Drone(id).GetComponent<Health>().TakeDamage(10000); else Console(id).Interact();
                    Console(id).Interact(); Drone(id).GetComponent<Health>().TakeDamage(10000);
                }
                Check(session.ConsolesOnline == 4 && session.DronesRemaining == 0, "real events and duplicate immunity");
                Later(3, .15); return;
            }
            if (phase == 3)
            {
                foreach (LabDoor gate in Gates())
                {
                    CheckStatus(gate,new Color(1f,.65f,.08f));
                    Check(gate.GetComponent<Collider>().enabled && !session.Graph.IsGateOpen(gate.GateId), "barrier retained while bolts retract");
                    Check(Mathf.Approximately(gate.transform.Find("Door assembly/LeftPanel").localPosition.x, -1f), "panel waits for bolts");
                }
                Later(4, 2); return;
            }
            if (phase == 4)
            {
                foreach (LabDoor gate in Gates())
                {
                    Check(PassageRoute(gate), "open gate NavMesh route " + gate.GateId);
                    var controller = session.Player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    session.Player.transform.position = gate.transform.TransformPoint(new Vector3(0, .05f, -1.5f));
                    controller.enabled = true;
                    Physics.SyncTransforms();
                    for (int step = 0; step < 20; step++) controller.Move(gate.transform.forward * .15f);
                    Check(Vector3.Distance(session.Player.transform.position, gate.transform.TransformPoint(new Vector3(0, .05f, 1.5f))) < .3f, "player capsule traverses gate " + gate.GateId);
                    CheckStatus(gate,new Color(.08f,.85f,.45f));
                    Check(gate.IsOpen && !gate.GetComponent<Collider>().enabled && !gate.GetComponent<NavMeshObstacle>().enabled && session.Graph.IsGateOpen(gate.GateId), "physical/navigation release " + gate.GateId);
                    Check(gate.transform.position == positions[gate.GateId] && gate.transform.rotation == rotations[gate.GateId], "stationary root " + gate.GateId);
                    Check(gate.transform.Find("Door assembly/LeftPanel").localPosition.x < -3f && gate.transform.Find("Door assembly/RightPanel").localPosition.x > 3f, "outward local panels " + gate.GateId);
                    Vector3 start = gate.transform.TransformPoint(new Vector3(0f, 1.4f, -1f));
                    Check(!Physics.Raycast(start, gate.transform.forward, 2f, ~0, QueryTriggerInteraction.Ignore), "no invisible passage collider " + gate.GateId);
                }
                Check(session.Graph.FindRoute(new Vector3(-10, 0, -10), new Vector3(22, 0, -15)).Count > 0, "scene A* reaches exit");
                foreach (Vector3 point in new[] { new Vector3(-14, 0, 12), new Vector3(-6, 0, 12) })
                {
                    Check(NavMesh.SamplePosition(point, out NavMeshHit hit, .5f, NavMesh.AllAreas), "Guard cover on NavMesh");
                    var path = new NavMeshPath();
                    Check(NavMesh.CalculatePath(new Vector3(-7, 0, 13), hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "Guard cover reachable");
                }
                Check(Physics.Raycast(new Vector3(-12, 1.2f, 8), Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore), "server rack blocks LOS");
                session.TryExit(); Check(session.Ended, "victory after all objectives");
                session.Restart(); Later(5, 0); next = 1f; return;
            }
            if (phase == 5)
            {
                Check(!session.Ended && session.ConsolesOnline == 0 && session.DronesRemaining == 4 && Gates().All(g => !g.IsOpen), "restart after victory resets objectives/gates");
                var health = session.Player.GetComponent<Health>();
                Check(Mathf.Approximately(health.Current, health.Maximum), "restart restores maximum health");
                health.TakeDamage(10000); Check(session.Ended, "player death ends mission");
                session.Restart(); Later(6, 0); next = 1f; return;
            }
            if (phase == 6)
            {
                Check(!session.Ended && session.ConsolesOnline == 0 && session.DronesRemaining == 4, "restart after defeat resets session");
                SessionState.SetBool(Running, false);
                Debug.Log("NIMTHARA ACCEPTANCE COMPLETE — all executed assertions passed");
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Fail(error); }
    }
    private static void CheckStatus(LabDoor gate, Color expected)
    {
        foreach(string face in new[]{"FrontStatus","RearStatus"})
        {
            var lights=gate.GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains(face)).ToArray();
            Check(lights.Length==2,"two " + face + " indicators gate " + gate.GateId);
            foreach(var light in lights)
            {
                var block=new MaterialPropertyBlock();light.GetPropertyBlock(block);
                Check(block.GetColor("_BaseColor")==expected,"synchronized " + face + " " + gate.Status);
            }
        }
    }
    private static bool PassageRoute(LabDoor gate)
    {
        Vector3 a = gate.transform.TransformPoint(new Vector3(0, 0, -1.5f));
        Vector3 b = gate.transform.TransformPoint(new Vector3(0, 0, 1.5f));
        if (!NavMesh.SamplePosition(a, out NavMeshHit start, .7f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(b, out NavMeshHit end, .7f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    private static void Fail(Exception error)
    {
        SessionState.SetBool(Running, false);
        Debug.LogException(error);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
