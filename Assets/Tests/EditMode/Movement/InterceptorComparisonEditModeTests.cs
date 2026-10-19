using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    // Controlled observations and route fixtures isolate policy differences. These measurements
    // are NOT a substitute for MainLab physical navigation or player capture benchmarks.
    public sealed class InterceptorComparisonEditModeTests
    {
        private readonly Vector3[] nodes = { Vector3.right * 8, Vector3.right * 10, Vector3.right * 12, Vector3.right * 16 };
        private readonly Vector3 home = new Vector3(9, 0, 2);
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object p, string name) => p.GetType().GetProperty(name).GetValue(p);
        private static Vector3 Position(string scenario, float t)
        {
            if (scenario == "reversal" && t > 2.1f) return Vector3.right * (4.2f - (t - 2.1f) * 2f);
            if (scenario == "stop" && t > 2.1f) return Vector3.right * 4.2f;
            if (scenario == "sharp_turn" && t > 2.1f) return new Vector3(4.2f, 0, (t - 2.1f) * 2f);
            return Vector3.right * (scenario == "locked_gate" ? Mathf.Min(t * 2f, 6f) : t * 2f);
        }
        [Test] public void CompareFrozenBaselineAndPredictivePolicy_WithIdenticalObservationsAndRoutes()
        {
            string csv = "scenario,policy,intercept_ticks,feasible_intercept_ticks,mean_intercept_path_distance,goal_changes,junction_switches,replans,invalid_route_goals,first_fallback_seconds\n";
            foreach (string scenario in new[] { "straight", "reversal", "stop", "sharp_turn", "cover_loss", "locked_gate", "gate_opens", "long_detour" })
            for (int variant = 0; variant < 2; variant++)
            {
                var settings = Activator.CreateInstance(T("InterceptorSettings"));
                object policy = Activator.CreateInstance(T("InterceptorPolicy"), settings);
                Vector3 last = home, velocity = Vector3.zero, previous = Vector3.zero, lastGoal = home;
                float seenAt = float.NegativeInfinity, previousTime = 0f, sumDistance = 0f, fallback = -1f;
                int attempts = 0, feasible = 0, switches = 0, errors = 0;
                for (int step = 0; step <= 36; step++)
                {
                    float now = step * .3f; Vector3 position = Position(scenario, now);
                    bool visible = scenario != "cover_loss" || now <= 2.1f;
                    bool open = scenario != "locked_gate" && (scenario != "gate_opens" || now >= 2.1f);
                    Func<Vector3, Vector3, Vector3[]> route = (a, b) => !open && ((a.x < 8f) != (b.x < 8f)) ? null :
                        scenario == "long_detour" && a == home ? new[] { a, new Vector3(9, 0, 100), b } : new[] { a, b };
                    if (visible)
                    {
                        float elapsed = now - previousTime;
                        if (elapsed > .1f && elapsed < 3f) velocity = (position - previous) / elapsed;
                        previous = last = position; previousTime = seenAt = now;
                    }
                    Vector3 goal; bool intercept;
                    if (variant == 0)
                    {
                        goal = home; float best = 10f; intercept = false;
                        if (now - seenAt < 4f)
                        {
                            goal = last; best = 34f;
                            if (velocity.sqrMagnitude > .3f)
                            foreach (var node in nodes)
                            {
                                float ahead = Vector3.Dot(node - last, velocity.normalized);
                                if (ahead < 1f || route(home, node) == null) continue;
                                float score = 42f + Mathf.Min(ahead, 15f) * 1.5f - Vector3.Distance(home, node) * .3f;
                                if (score > best) { best = score; goal = node; intercept = true; }
                            }
                        }
                        if (visible && Vector3.Distance(home, position) < 3.5f) { goal = position; intercept = false; }
                    }
                    else
                    {
                        object decision = policy.GetType().GetMethod("Step").Invoke(policy, new object[] {
                            now, visible, visible ? position : Vector3.one * 9999, home, home, 3.5f,
                            open ? 1 : 0, nodes, route });
                        goal = (Vector3)decision.GetType().GetField("Destination").GetValue(decision);
                        intercept = Get(policy, "State").ToString() == "InterceptAhead";
                    }
                    if (Vector3.Distance(goal, lastGoal) > .1f) switches++;
                    lastGoal = goal;
                    Vector3[] chosenRoute = route(home, goal);
                    if (chosenRoute == null) errors++;
                    if (intercept)
                    {
                        attempts++; float length = Length(chosenRoute); sumDistance += length;
                        Vector3[] playerRoute = route(last, goal);
                        float playerTime = Length(playerRoute) / Mathf.Max(.1f, velocity.magnitude) - (now - seenAt);
                        if (chosenRoute != null && playerRoute != null && playerTime > 0f && length / 3.5f <= playerTime + .6f) feasible++;
                    }
                    if (now > 2.1f && !intercept && fallback < 0 &&
                        (scenario == "reversal" || scenario == "stop" || scenario == "sharp_turn" || scenario == "cover_loss")) fallback = now - 2.1f;
                }
                Assert.That(float.IsNaN(sumDistance), Is.False);
                csv += string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F3},{5},{6},{7},{8},{9:F3}\n",
                    scenario, variant == 0 ? "baseline" : "predictive", attempts, feasible,
                    attempts > 0 ? sumDistance / attempts : 0f, switches, variant == 0 ? -1 : (int)Get(policy, "Switches"),
                    variant == 0 ? -1 : (int)Get(policy, "Replans"), errors, fallback);
            }
            string directory = Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE") ?? "Logs/InterceptorEvidence";
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "controlled-comparison.csv"), csv);
            TestContext.Out.WriteLine(csv);
        }
        [Test] public void RecordComparablePredictionAndFallbackTrace()
        {
            string csv="scenario,time,visible,observed_x,observed_z,goal_x,goal_z,state,confidence,selected_node,replans,failures,reason\n";
            foreach(string scenario in new[]{"straight","stop","reversal","sharp_turn","cover_loss","no_intercept","missed_window","locked_gate","gate_opens"})
            {
                object policy=Activator.CreateInstance(T("InterceptorPolicy"),Activator.CreateInstance(T("InterceptorSettings")));
                for(int step=0;step<=36;step++)
                {
                    float now=step*.3f;bool visible=(scenario!="cover_loss"&&scenario!="missed_window")||now<=2.1f;
                    Vector3 position=Position(scenario,now);
                    bool open=scenario!="locked_gate"&&(scenario!="gate_opens"||now>=2.1f);
                    Func<Vector3,Vector3,Vector3[]> route=(a,b)=>!open&&((a.x<8f)!=(b.x<8f))?null:
                        scenario=="no_intercept"&&a==home?new[]{a,new Vector3(9,0,100),b}:new[]{a,b};
                    Vector3 drone=home;
                    if(scenario=="missed_window"&&now>3f&&(int)Get(policy,"SelectedNode")>=0)drone=nodes[(int)Get(policy,"SelectedNode")];
                    var decision=policy.GetType().GetMethod("Step").Invoke(policy,new object[]{now,visible,visible?position:Vector3.one*9999,drone,home,3.5f,open?1:0,nodes,route});
                    Vector3 goal=(Vector3)decision.GetType().GetField("Destination").GetValue(decision);
                    Assert.That(float.IsNaN(goal.sqrMagnitude)||float.IsInfinity(goal.sqrMagnitude),Is.False);
                    csv+=string.Format(CultureInfo.InvariantCulture,"{0},{1:F1},{2},{3:F3},{4:F3},{5:F3},{6:F3},{7},{8:F3},{9},{10},{11},\"{12}\"\n",
                        scenario,now,visible,visible?position.x:float.NaN,visible?position.z:float.NaN,goal.x,goal.z,Get(policy,"State"),Get(policy,"Confidence"),Get(policy,"SelectedNode"),Get(policy,"Replans"),Get(policy,"FailedAttempts"),Get(policy,"Reason"));
                }
            }
            string directory=Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE")??"Logs/InterceptorEvidence";Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"prediction-trace.csv"),csv);
        }
        private static float Length(Vector3[] path)
        {
            if (path == null) return float.PositiveInfinity;
            float value = 0; for (int i = 1; i < path.Length; i++) value += Vector3.Distance(path[i - 1], path[i]);
            return value;
        }
    }
}
