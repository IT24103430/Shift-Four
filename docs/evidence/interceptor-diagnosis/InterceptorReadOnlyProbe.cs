#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Diagnostic observer installed only in the disposable copy AFTER the untouched run matrix.
// No policy inputs, production/test sources, transforms, physics settings or assets are written.
[InitializeOnLoad]
public static class InterceptorReadOnlyProbe
{
    static readonly BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_INTERCEPTOR_PROBE");
    static int frame = -1;
    static string previousTest = "", previousKey = "";
    static float previousTime = -100;
    static Type contextType, enumType;
    static InterceptorReadOnlyProbe()
    {
        if (string.IsNullOrEmpty(directory)) return;
        EditorApplication.update += Update;
        SceneManager.activeSceneChanged += (oldScene,newScene) => {
            if (oldScene.name == "MainLab" && newScene.name.StartsWith("After interceptor test")) Capture(true);
        };
    }
    static object Get(object target, string name)
    {
        if (target == null) return null;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var p = t.GetProperty(name, All); if (p != null) return p.GetValue(target);
            var f = t.GetField(name, All); if (f != null) return f.GetValue(target);
        }
        return null;
    }
    static object Call(object target, string name, params object[] args)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethod(name, All | BindingFlags.DeclaredOnly);
            if (m != null) return m.Invoke(target, args);
        }
        throw new MissingMethodException(name);
    }
    static Dictionary<string,object> Fields(object target, params string[] names)
    { var result = new Dictionary<string,object>(); foreach (string n in names) result[n] = Get(target,n); return result; }
    static Dictionary<string,object> Policy(object policy)
    {
        var d = Fields(policy,"State","Reason","LastObserved","LastObservedAt","Confidence","SelectedNode","DroneArrival","PlayerArrival","Replans","FailedAttempts","UsesMotionTiming","TimingSpeedLimit","TimingAcceleration","PlannedRoute");
        d["velocity"] = Get(policy,"velocity");
        d["history"] = (Get(policy,"History") as IEnumerable)?.Cast<object>().Select(o => Fields(o,"Time","Position","Speed","Direction")).ToArray();
        d["candidates"] = (Get(policy,"Candidates") as IEnumerable)?.Cast<object>().Select(o => Fields(o,"Node","Position","Score","DroneArrival","PlayerArrival","Confidence","PlayerRoute","DroneRoute")).ToArray();
        return d;
    }
    static void Update() => Capture(false);
    static void Capture(bool force)
    {
        if (!EditorApplication.isPlaying || (!force && frame == Time.frameCount)) return;
        frame = Time.frameCount;
        try
        {
            contextType ??= Type.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext, UnityEngine.TestRunner");
            enumType ??= Type.GetType("UnityEngine.TestTools.TestEnumerator, UnityEngine.TestRunner");
            object context = contextType?.GetProperty("CurrentContext",BindingFlags.Static|BindingFlags.Public)?.GetValue(null);
            object fixture = Get(context,"TestObject");
            if (fixture == null || fixture.GetType().Name != "InterceptorMainLabPlayModeTests") return;
            string test = Get(Get(context,"CurrentTest"),"FullName")?.ToString() ?? "unknown";
            if (!test.Contains("BaselineComparison") && !test.Contains("GenuineVisibleMotion")) return;
            var brain = Get(fixture,"brain") as Component;
            if (brain == null || !brain.gameObject.scene.isLoaded) return;
            var graph = Get(fixture,"graph"); var validator = Get(brain,"TacticalValidator");
            var agent = brain.GetComponent<NavMeshAgent>();
            if (validator == null || !agent.enabled || !agent.isOnNavMesh) return;
            object enumerator = enumType?.GetProperty("Enumerator",BindingFlags.Static|BindingFlags.Public)?.GetValue(null);
            var locals = new Dictionary<string,object>(); object improved = null;
            Vector3 start = default; Vector3[] targets = null;
            if (enumerator != null)
                foreach (var f in enumerator.GetType().GetFields(All))
                {
                    object v = f.GetValue(enumerator);
                    if (v != null && v.GetType().FullName == "ShiftFour.InterceptorPolicy") { improved = v; locals[f.Name] = Policy(v); }
                    else if (v is Vector3 || v is Vector3[] || v is float || v is int || v is bool || v is string) locals[f.Name] = v;
                    if (f.Name.StartsWith("<start>")) start = (Vector3)v;
                    if (f.Name.StartsWith("<targets>")) targets = v as Vector3[];
                }
            if (!force && locals.TryGetValue("<>1__state",out var state) && (int)state < 0) return;
            var policy = Get(brain,"Policy");
            string key = test + "/" + Get(policy,"LastObservedAt") + "/" + Get(policy,"Reason") + "/" + (improved != null) + "/" + agent.isStopped;
            if (!force && key == previousKey && Time.time - previousTime < .25f) return;
            bool changed = key != previousKey || test != previousTest;
            previousKey = key; previousTest = test; previousTime = Time.time;
            var player = Get(fixture,"player") as Component;
            var data = new Dictionary<string,object> {
                ["source"] = force ? "pre-unload" : "frame",["sceneHandle"] = brain.gameObject.scene.handle,["test"] = test,["frame"] = frame,["time"] = Time.time,["deltaTime"] = Time.deltaTime,["captureDeltaTime"] = Time.captureDeltaTime,["timeScale"] = Time.timeScale,["targetFrameRate"] = Application.targetFrameRate,
                ["scene"] = brain.gameObject.scene.name,["drone"] = brain.transform.position,["player"] = player != null ? player.transform.position : default,
                ["agent"] = new Dictionary<string,object>{{"speed",agent.speed},{"acceleration",agent.acceleration},{"velocity",agent.velocity},{"isStopped",agent.isStopped},{"hasPath",agent.hasPath},{"pathPending",agent.pathPending},{"pathStatus",agent.hasPath?agent.pathStatus.ToString():"NoPath"},{"remaining",agent.hasPath?agent.remainingDistance:0}},
                ["body"] = Fields(brain.GetComponent(Type.GetType("ShiftFour.DroneBody, Assembly-CSharp")),"MotionBlocked","EnvelopeCenter","EnvelopeExtents"),
                ["motor"] = Fields(brain.GetComponent(Type.GetType("ShiftFour.DroneMotor, Assembly-CSharp")),"enabled","state","goal","step","MovementMode","ActualVelocity"),
                ["policy"] = Policy(policy),["locals"] = locals,["graphVersion"] = Get(graph,"Version"),["awareness"] = Fields(Get(brain,"Awareness"),"State","Confidence"),
                ["actors"] = UnityEngine.Object.FindObjectsByType(Type.GetType("ShiftFour.DroneBrain, Assembly-CSharp"),FindObjectsSortMode.None).Cast<Component>().Select(b => new Dictionary<string,object>{{"name",b.name},{"position",b.transform.position},{"enabled",((Behaviour)b).enabled},{"motorEnabled",((Behaviour)b.GetComponent(Type.GetType("ShiftFour.DroneMotor, Assembly-CSharp"))).enabled}}).ToArray()
            };
            if (force && targets != null)
                data["comparisonRoutes"] = targets.Select(target => new Dictionary<string,object>{{"target",target},{"fromOriginalStart",Explain(brain,graph,validator,start,target)},{"fromCurrent",Explain(brain,graph,validator,brain.transform.position,target)}}).ToArray();
            if (force && test.Contains("GenuineVisibleMotion") && Get(policy,"History") is IEnumerable history && history.Cast<object>().Count() > 0)
            {
                var junctions = (IEnumerable)Get(brain,"junctions");
                Vector3 observed = (Vector3)Get(policy,"LastObserved");
                data["hypotheses"] = junctions.Cast<Vector3>().Select(point => new Dictionary<string,object> {
                    {"point",point},{"playerRoute",Call(validator,"PlayerRoute",observed,point)},
                    {"droneRoute",Call(brain,"ValidatedRoute",brain.transform.position,point)},
                    {"diagnostic",Explain(brain,graph,validator,brain.transform.position,point)} }).ToArray();
            }
            Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory,"frames.jsonl"),Json(data)+"\n");
        }
        catch (Exception ex) { Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory,"observer-errors.txt"),ex+"\n"); }
    }
    static object Explain(Component owner,object graph,object validator,Vector3 from,Vector3 to)
    {
        var agent = owner.GetComponent<NavMeshAgent>();
        Vector3 center = (Vector3)Get(validator,"Center"), extents = (Vector3)Get(validator,"Extents");
        Func<Collider,bool> external = c => c != null && !c.transform.IsChildOf(owner.transform) && !Physics.GetIgnoreLayerCollision(owner.gameObject.layer,c.gameObject.layer);
        Func<Collider,object> describe = c => new Dictionary<string,object>{{"name",c.name},{"path",PathOf(c.transform)},{"type",c.GetType().Name},{"layer",c.gameObject.layer},{"position",c.transform.position},{"boundsCenter",c.bounds.center},{"boundsExtents",c.bounds.extents}};
        Func<Vector3,object[]> occupants = p => Physics.OverlapBox(p+center,extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Where(external).Select(describe).ToArray();
        var logical = ((IEnumerable)Call(graph,"FindRoute",from,to)).Cast<Vector3>().ToArray();
        var result = new Dictionary<string,object>{{"from",from},{"to",to},{"logicalRoute",logical},{"startBlockers",occupants(from)},{"targetBlockers",occupants(to)}};
        var legs = new List<object>();
        if (NavMesh.SamplePosition(from,out var start,.25f,agent.areaMask))
        {
            Vector3 previous = start.position;
            foreach (var node in logical)
            {
                bool sampled = NavMesh.SamplePosition(node,out var end,.25f,agent.areaMask); var path = new NavMeshPath();
                bool found = sampled && NavMesh.CalculatePath(previous,end.position,agent.areaMask,path);
                var blockers = new List<object>(); Vector3 prior = previous;
                foreach (Vector3 corner in path.corners)
                {
                    Vector3 delta = corner-prior;
                    if (delta.magnitude > .000001f)
                        blockers.AddRange(Physics.BoxCastAll(prior+center,extents,delta.normalized,Quaternion.identity,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Where(h => external(h.collider)).Select(h => describe(h.collider)));
                    blockers.AddRange((object[])occupants(corner)); prior=corner;
                }
                legs.Add(new Dictionary<string,object>{{"sampled",sampled},{"calculated",found},{"status",path.status.ToString()},{"corners",path.corners},{"bodyBlockers",blockers}});
                previous = end.position;
            }
        }
        result["nativeLegs"] = legs;
        result["strictRoute"] = Call(validator,"Route",from,to);
        result["playerRoute"] = Call(validator,"PlayerRoute",from,to);
        return result;
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent)+"/"+t.name;
    static string Json(object value)
    {
        if (value == null) return "null";
        if (value is string s) return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t")+"\"";
        if (value is bool b) return b?"true":"false";
        if (value is Vector3 v) return "["+v.x.ToString("R",CultureInfo.InvariantCulture)+","+v.y.ToString("R",CultureInfo.InvariantCulture)+","+v.z.ToString("R",CultureInfo.InvariantCulture)+"]";
        if (value is IDictionary<string,object> d) return "{"+string.Join(",",d.Select(p => Json(p.Key)+":"+Json(p.Value)))+"}";
        if (value is IEnumerable list) return "["+string.Join(",",list.Cast<object>().Select(Json))+"]";
        if (value is Enum) return Json(value.ToString());
        if (value is float f && !float.IsFinite(f)) return Json(f.ToString(CultureInfo.InvariantCulture));
        if (value is double db && (double.IsInfinity(db)||double.IsNaN(db))) return Json(db.ToString(CultureInfo.InvariantCulture));
        if (value is IFormattable n) return n.ToString(null,CultureInfo.InvariantCulture);
        return Json(value.ToString());
    }
}
#endif
