using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour
{
    [Serializable]
    public sealed class ControlTacticalSettings
    {
        public bool enabled = true;
        [Min(.1f)] public float horizon = 4f;
        [Min(0f)] public float minimumCommitment = 1.2f;
        [Min(0f)] public float switchingThreshold = 8f;
        [Min(.1f)] public float failureCooldown = 2f;
        [Min(.1f)] public float arrivalSlack = .6f;
        [Min(.01f)] public float clearanceMargin = .08f;
        [Min(.1f)] public float roomHalfExtent = 8.5f;
        // Clockwise perimeter around the actual two-cover layout. Home is the Control anchor.
        public Vector3[] offsets = {
            new Vector3(0,0,-5.5f), new Vector3(-7,0,-5.5f), new Vector3(-7,0,5.5f),
            new Vector3(0,0,5.5f), new Vector3(7,0,3.5f), new Vector3(7,0,-5.5f)
        };
    }

    public sealed class ControlTacticalPlan
    {
        public int Target, Direction;
        public int[] Junctions;
        public float Distance, Score, DroneEta, PlayerEta;
        public string Side;
        public Vector3[] Route;
    }

    // This is a private tactical ring, NOT a second global waypoint graph. Each leg still
    // requests the existing gate-aware A* graph and a complete native path through a delegate.
    public sealed class ControlTacticalNavigation
    {
        public static readonly string[] Labels = { "South", "WestSouth", "WestNorth", "Forward", "EastNorth", "EastSouth" };
        private readonly ControlTacticalSettings settings;
        private readonly Dictionary<int, Vector2> failures = new Dictionary<int, Vector2>();
        private ControlTacticalPlan plan;
        private int step, version = -1;
        private float committedAt, deadline, evidenceAt = float.NegativeInfinity;
        private Vector3 committedHeading;
        private Vector3[] points = Array.Empty<Vector3>();
        public IReadOnlyList<Vector3> Points => points;
        public ControlTacticalPlan Plan => plan;
        public int Step => step;
        public int Switches { get; private set; }
        public int Replans { get; private set; }
        public string Reason { get; private set; } = "No tactical plan";
        public ControlTacticalNavigation(ControlTacticalSettings config) { settings = config; }
        private static Vector3 Flat(Vector3 v) { v.y=0; return v; }
        public bool InRoom(Vector3 p, Vector3 home) => Mathf.Abs(p.x-home.x) <= settings.roomHalfExtent &&
            Mathf.Abs(p.z-home.z) <= settings.roomHalfExtent;
        public void SetAnchor(Vector3 home)
        {
            if (settings.offsets==null) { points=Array.Empty<Vector3>(); return; }
            if (points.Length != settings.offsets.Length) points = new Vector3[settings.offsets.Length];
            for (int i=0;i<points.Length;i++) points[i] = home + settings.offsets[i];
        }
        public void Clear(string reason)
        { if (plan != null) Replans++; plan=null; step=0; Reason=reason; }

        // Enumerate the two simple arcs; no node is revisited and no shared topology changes.
        public ControlTacticalPlan Build(int entry, int target, int direction, Vector3 origin,
            Func<Vector3, Vector3, Vector3[]> route)
        {
            if (points.Length != 6 || entry < 0 || entry >= 6 || target < 0 || target >= 6 ||
                (direction != -1 && direction != 1)) return null;
            var stops = new List<int>(); var corners = new List<Vector3>(); Vector3 previous=origin;
            int at=entry;
            for (int count=0;count<points.Length;count++)
            {
                Vector3[] leg=route(previous,points[at]);
                if (!Valid(leg)) return null;
                stops.Add(at); Append(corners,leg); previous=points[at];
                if (at==target) break;
                at=(at+direction+points.Length)%points.Length;
            }
            if (at!=target) return null;
            return new ControlTacticalPlan { Target=target, Direction=direction, Junctions=stops.ToArray(),
                Route=corners.ToArray(), Distance=Length(corners.ToArray()), Side=Side(stops) };
        }
        // Prediction selects the target. Reuse the committed arc's remaining stops so
        // attachment to a newly nearer junction cannot reverse the drone on every tick.
        public ControlTacticalPlan RouteTo(int target, Vector3 origin, Func<Vector3, Vector3, Vector3[]> route)
        {
            if (points.Length != 6 || target < 0 || target >= points.Length) return null;
            if (plan != null && plan.Target == target)
            {
                var remaining = new List<Vector3>(); Vector3 previous = origin;
                for (int i = step; i < plan.Junctions.Length; i++)
                {
                    var leg = route(previous, points[plan.Junctions[i]]);
                    if (!Valid(leg)) return null;
                    Append(remaining, leg); previous = points[plan.Junctions[i]];
                }
                if (remaining.Count == 0) return null;
                return new ControlTacticalPlan { Target=target, Direction=plan.Direction, Junctions=plan.Junctions,
                    Route=remaining.ToArray(), Distance=Length(remaining.ToArray()), Side=plan.Side };
            }
            int entry=-1; float nearest=float.PositiveInfinity;
            for (int i=0; i<points.Length; i++)
            {
                var leg=route(origin, points[i]); if (!Valid(leg)) continue;
                float length=Length(leg); if (length>=nearest) continue;
                nearest=length; entry=i;
            }
            if (entry<0) return null;
            var left=Build(entry,target,1,origin,route); var right=Build(entry,target,-1,origin,route);
            return left==null ? right : right==null || left.Distance<=right.Distance ? left : right;
        }
        public Vector3 Follow(ControlTacticalPlan selected, Vector3 origin, float stoppingDistance)
        {
            if (plan==null || plan.Target!=selected.Target || plan.Direction!=selected.Direction)
            { plan=selected; step=0; }
            else
            {
                plan.Route=selected.Route; plan.Distance=selected.Distance;
                plan.Score=selected.Score; plan.DroneEta=selected.DroneEta; plan.PlayerEta=selected.PlayerEta;
            }
            while (step<plan.Junctions.Length-1 && Flat(origin-points[plan.Junctions[step]]).magnitude<=stoppingDistance+.1f) step++;
            Reason="Prediction selected a validated " + plan.Side + " arc";
            return points[plan.Junctions[step]];
        }
        private static string Side(List<int> stops)
        {
            foreach (int index in stops)
            {
                if (index==1 || index==2) return "Left";
                if (index==4 || index==5) return "Right";
            }
            return "Forward";
        }
        private static int Key(ControlTacticalPlan value) => value.Target*2 + (value.Direction>0?1:0);
        private void Fail(float now, float observedAt, string reason, bool wholeTarget=false)
        {
            if (plan!=null)
            {
                var failure=new Vector2(now+settings.failureCooldown,observedAt);
                failures[Key(plan)]=failure;
                if (wholeTarget) { failures[plan.Target*2]=failure; failures[plan.Target*2+1]=failure; }
            }
            Clear(reason);
        }
        public bool TryDecide(float now, float observedAt, Vector3 observed, Vector3 velocity, Vector3 drone,
            Vector3 home, float droneSpeed, float stoppingDistance, int graphVersion,
            Func<Vector3, Vector3, Vector3[]> route, Func<Vector3, Vector3, Vector3[]> playerRoute, out DecisionOption decision)
        {
            decision=default; SetAnchor(home);
            float age=now-observedAt; velocity=Flat(velocity);
            if (!settings.enabled || age<0f || age>settings.horizon || !InRoom(drone,home) || !InRoom(observed,home) ||
                velocity.magnitude<.35f || droneSpeed<=0f || points.Length!=6)
            { Clear("No fresh, moving in-room observation"); return false; }
            bool graphChanged=version>=0 && graphVersion!=version; version=graphVersion;
            if (graphChanged) Clear("Gate version changed");
            if (plan!=null && Vector3.Angle(committedHeading,velocity)>55f)
                Fail(now,observedAt,"Observed heading changed");
            if (plan!=null && now>deadline+settings.arrivalSlack)
                Fail(now,observedAt,"Predicted arrival window expired",true);
            if (plan!=null)
            {
                while (step<plan.Junctions.Length && Flat(drone-points[plan.Junctions[step]]).magnitude<=stoppingDistance+.1f) step++;
                if (step>=plan.Junctions.Length)
                {
                    plan.Route=route(drone,points[plan.Target]); plan.Distance=0f;
                    if (!Valid(plan.Route) || !Score(plan,observed,velocity,age,droneSpeed,playerRoute))
                        Clear("Reached point no longer supported by observations");
                    else
                    {
                        if (observedAt>evidenceAt) { deadline=now+plan.PlayerEta; evidenceAt=observedAt; }
                        Reason="Holding justified interception point";
                        decision=new DecisionOption("Control hold "+Labels[plan.Target],points[plan.Target],plan.Score);
                        step=plan.Junctions.Length-1;
                        return true;
                    }
                }
                else if (!Valid(route(drone,points[plan.Junctions[step]]))) Fail(now,observedAt,"Current tactical leg blocked");
            }
            ControlTacticalPlan best=null;
            int entry=-1; float nearest=float.PositiveInfinity;
            for (int i=0;i<points.Length;i++)
            {
                var path=route(drone,points[i]); if (!Valid(path)) continue;
                float distance=Length(path); if (distance>=nearest) continue;
                nearest=distance; entry=i;
            }
            if (entry<0) { Clear("No reachable local connector"); return false; }
            for (int target=0;target<points.Length;target++)
            for (int direction=-1;direction<=1;direction+=2)
            {
                var candidate=Build(entry,target,direction,drone,route); if (candidate==null) continue;
                if (failures.TryGetValue(Key(candidate),out var failure) && (now<failure.x || observedAt<=failure.y)) continue;
                if (!Score(candidate,observed,velocity,age,droneSpeed,playerRoute)) continue;
                if (best==null || candidate.Score>best.Score) best=candidate;
            }
            if (plan!=null)
            {
                // Re-score remaining legs, not a new attachment that could turn the drone back.
                var remaining=new List<Vector3>(); Vector3 previous=drone;
                for (int i=step;i<plan.Junctions.Length;i++)
                {
                    var leg=route(previous,points[plan.Junctions[i]]);
                    if (!Valid(leg)) { remaining.Clear(); break; }
                    Append(remaining,leg); previous=points[plan.Junctions[i]];
                }
                if (remaining.Count==0) Fail(now,observedAt,"Remaining tactical route blocked");
                else
                {
                    plan.Route=remaining.ToArray(); plan.Distance=Length(plan.Route);
                    if (!Score(plan,observed,velocity,age,droneSpeed,playerRoute)) Clear("Arrival advantage no longer justified");
                }
            }
            if (best!=null && (plan==null || (now-committedAt>=settings.minimumCommitment &&
                best.Score>plan.Score+settings.switchingThreshold && best.Target!=plan.Target)))
            {
                if (plan!=null) Switches++;
                plan=best; step=0; committedAt=now; committedHeading=velocity;
                deadline=now+best.PlayerEta; evidenceAt=observedAt;
                Reason="Reachable "+best.Side+" arc to "+Labels[best.Target]+" with arrival advantage";
                while (step<plan.Junctions.Length-1 && Flat(drone-points[plan.Junctions[step]]).magnitude<=stoppingDistance+.1f) step++;
            }
            if (plan==null) { Reason="No feasible local interception; use existing policy fallback"; return false; }
            if (observedAt>evidenceAt) { deadline=now+plan.PlayerEta; evidenceAt=observedAt; }
            decision=new DecisionOption("Control "+plan.Side+" -> "+Labels[plan.Target],points[plan.Junctions[step]],plan.Score);
            return true;
        }
        private bool Score(ControlTacticalPlan p, Vector3 observed, Vector3 velocity, float age, float speed,
            Func<Vector3, Vector3, Vector3[]> route)
        {
            Vector3 ahead=Flat(points[p.Target]-observed); float forward=Vector3.Dot(ahead,velocity.normalized);
            if (forward<1f) return false;
            var playerRoute=route(observed,points[p.Target]); if (!Valid(playerRoute)) return false;
            float playerDistance=Length(playerRoute);
            p.PlayerEta=playerDistance/Mathf.Max(.1f,velocity.magnitude)-age;
            p.DroneEta=p.Distance/speed;
            if (p.PlayerEta<=0f || p.PlayerEta+age>settings.horizon || p.DroneEta>p.PlayerEta+settings.arrivalSlack) return false;
            float confidence=Mathf.Exp(-age*.7f)*Mathf.Clamp01(ahead.magnitude/Mathf.Max(.1f,playerDistance));
            if (confidence<.38f) return false;
            p.Score=42f+Mathf.Min(forward,15f)*1.5f + confidence*15f +
                Mathf.Clamp01((p.PlayerEta-p.DroneEta+settings.arrivalSlack)/Mathf.Max(.1f,p.PlayerEta))*15f-p.Distance*.3f;
            return true;
        }
        public static bool Valid(Vector3[] route)
        {
            if (route==null || route.Length==0) return false;
            foreach (var p in route) if (float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude)) return false;
            return true;
        }
        public static float Length(Vector3[] points)
        { float result=0f; for (int i=1;i<points.Length;i++) result+=Vector3.Distance(points[i-1],points[i]); return result; }
        private static void Append(List<Vector3> target, Vector3[] source)
        { foreach (var p in source) if (target.Count==0 || Vector3.Distance(target[target.Count-1],p)>.001f) target.Add(p); }
    }

    // Use the same raised envelope as movement. A renderer-derived fallback supports isolated
    // fixtures without DroneBody; it never replaces the live movement guard.
    public sealed class ControlTacticalRouteValidator
    {
        private readonly Transform owner;
        private readonly NavMeshAgent agent;
        private readonly WaypointGraph graph;
        // Cache only a single synchronous decision. Moving actors and gates are queried afresh
        // on the next decision; callers outside that scope always get a fresh physical query.
        private readonly Dictionary<(Vector3 from, Vector3 to, bool clearance), Vector3[]> routes = new();
        private readonly Dictionary<Vector3, bool> occupancy = new();
        private bool evaluating;
        private int cachedVersion;
        public int RouteQueries { get; private set; }
        public int CacheHits { get; private set; }
        public void BeginDecision()
        {
            routes.Clear(); occupancy.Clear(); evaluating=true;
            cachedVersion=graph!=null?graph.Version:-1; RouteQueries=CacheHits=0;
        }
        public void EndDecision() { evaluating=false; routes.Clear(); occupancy.Clear(); }
        private void CheckVersion()
        {
            int version=graph!=null?graph.Version:-1;
            if(cachedVersion==version)return;
            routes.Clear(); occupancy.Clear(); cachedVersion=version;
        }
        public Vector3 Center { get; }
        public Vector3 Extents { get; }
        public ControlTacticalRouteValidator(Component brain, WaypointGraph waypoints, float margin)
        {
            owner=brain.transform; agent=brain.GetComponent<NavMeshAgent>(); graph=waypoints;
            var body=brain.GetComponent<DroneBody>();
            if (body!=null)
            {
                Center=body.EnvelopeCenter;
                Extents=body.EnvelopeExtents + Vector3.one*Mathf.Max(0f,margin);
                return;
            }
            Bounds bounds=new Bounds(Vector3.up,Vector3.one*.1f);
            foreach (var renderer in brain.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.localBounds;
                for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
                    bounds.Encapsulate(owner.InverseTransformPoint(renderer.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)))));
            }
            foreach (var collider in brain.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                var b=collider.bounds;
                for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
                    bounds.Encapsulate(owner.InverseTransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
            }
            float top=bounds.max.y+margin, bottom=Mathf.Max(.05f,bounds.min.y-margin);
            float radius=Mathf.Max(agent.radius,Mathf.Sqrt(bounds.extents.x*bounds.extents.x+bounds.extents.z*bounds.extents.z))+margin;
            Center=new Vector3(0f,(top+bottom)*.5f,0f); Extents=new Vector3(radius,Mathf.Max(.05f,(top-bottom)*.5f),radius);
        }
        private bool External(Collider collider) => collider!=null && !collider.transform.IsChildOf(owner) &&
            !Physics.GetIgnoreLayerCollision(owner.gameObject.layer,collider.gameObject.layer);
        public bool CanOccupy(Vector3 point)
        {
            if(evaluating) { CheckVersion(); if(occupancy.TryGetValue(point,out bool cached))return cached; }
            bool clear=true;
            foreach (var c in Physics.OverlapBox(point+Center,Extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                if (External(c)) { clear=false; break; }
            if(evaluating)occupancy[point]=clear;
            return clear;
        }
        private bool Clear(Vector3 a, Vector3 b)
        {
            if (!CanOccupy(a) || !CanOccupy(b)) return false;
            Vector3 delta=b-a;
            if (delta.sqrMagnitude<.000001f) return true;
            foreach (var hit in Physics.BoxCastAll(a+Center,Extents,delta.normalized,Quaternion.identity,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if (External(hit.collider)) return false;
            return true;
        }
        public Vector3[] Route(Vector3 from, Vector3 to) => CheckedRoute(from,to,true);
        public Vector3[] PlayerRoute(Vector3 from, Vector3 to) => CheckedRoute(from,to,false);
        private Vector3[] CheckedRoute(Vector3 from, Vector3 to, bool clearance)
        {
            if(evaluating)
            {
                CheckVersion();
                if(routes.TryGetValue((from,to,clearance),out var cached)) { CacheHits++; return cached; }
            }
            RouteQueries++;
            var result=RouteCore(from,to,clearance);
            if(evaluating)routes[(from,to,clearance)]=result;
            return result;
        }
        private Vector3[] RouteCore(Vector3 from, Vector3 to, bool clearance)
        {
            if (graph==null || agent==null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return null;
            var nodes=graph.FindRoute(from,to); if (nodes.Count==0) return null;
            if (!NavMesh.SamplePosition(from,out var start,.25f,agent.areaMask)) return null;
            if (clearance && !CanOccupy(start.position)) return null;
            var result=new List<Vector3> { start.position }; Vector3 previous=start.position;
            foreach (var node in nodes)
            {
                if (!NavMesh.SamplePosition(node,out var end,.25f,agent.areaMask)) return null;
                var path=new NavMeshPath();
                if (!NavMesh.CalculatePath(previous,end.position,agent.areaMask,path) || path.status!=NavMeshPathStatus.PathComplete) return null;
                foreach (var p in path.corners)
                {
                    if (clearance && !Clear(result[result.Count-1],p)) return null;
                    if (Vector3.Distance(result[result.Count-1],p)>.001f) result.Add(p);
                }
                previous=end.position;
            }
            return result.ToArray();
        }
    }
}
