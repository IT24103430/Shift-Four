using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class InterceptorMotionTimingEditModeTests
    {
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static object Get(object o,string name) => o.GetType().GetProperty(name).GetValue(o);
        private static object Value(object o,string name) => o.GetType().GetField(name).GetValue(o);
        private static float Eta(float distance,float cap,float start,float acceleration)
            => (float)T("InterceptorPolicy").GetMethod("EstimateTravelSeconds").Invoke(null,new object[]{distance,cap,start,acceleration});
        private static float RouteEta(Vector3[] path,float cap,Vector3 ownVelocity,float acceleration)
            => (float)T("InterceptorPolicy").GetMethod("EstimateRouteSeconds").Invoke(null,new object[]{path,cap,ownVelocity,acceleration});
        private static readonly Vector3 Drone=new Vector3(9,0,2), Home=new Vector3(15,0,2);
        private static readonly Vector3[] Nodes={new Vector3(10,0,0),new Vector3(12,0,0)};
        private static readonly Func<Vector3,Vector3,Vector3[]> Direct=(a,b)=>new[]{a,b};
        private static object NewPolicy() => Activator.CreateInstance(T("InterceptorPolicy"),Activator.CreateInstance(T("InterceptorSettings")));
        private static object Tick(object p,float now,bool visible,Vector3 seen,float cap=3.7f,Vector3? velocity=null,float acceleration=8f,int version=0,
            Func<Vector3,Vector3,Vector3[]> route=null)
            => p.GetType().GetMethod("StepWithMotion").Invoke(p,new object[]{now,visible,seen,Drone,Home,cap,velocity??Vector3.zero,acceleration,version,Nodes,route??Direct,Direct});
        private static void Train(object p,float cap=3.7f,Vector3? velocity=null,float acceleration=8f)
        {for(int i=0;i<=6;i++)Tick(p,i*.3f,true,Vector3.right*i*.6f,cap,velocity,acceleration);}
        [Test] public void RestArrival_IncludesAccelerationAndBraking()
        {
            Assert.That(Eta(10f,3.7f,0f,8f),Is.EqualTo(10f/3.7f+3.7f/8f).Within(.0001f));
            Assert.That(Eta(.5f,3.7f,0f,8f),Is.EqualTo(.5f).Within(.0001f));
            Assert.That(Eta(.5f,3.7f,0f,8f),Is.GreaterThan(.5f/3.7f));
        }
        [Test] public void RouteCorners_AreConservativeStopsAndUseActualLengths()
        {
            var direct=new[]{Vector3.zero,Vector3.right*10f};
            var bent=new[]{Vector3.zero,new Vector3(0,0,5),new Vector3(5,0,5),new Vector3(10,0,0)};
            Assert.That(RouteEta(bent,3.7f,Vector3.zero,8f),Is.GreaterThan(RouteEta(direct,3.7f,Vector3.zero,8f)));
            Assert.That(RouteEta(bent,3.7f,Vector3.zero,8f),Is.GreaterThan((5f+5f+Mathf.Sqrt(50f))/3.7f));
        }
        [Test] public void CurrentOwnHeading_ReceivesNoFreeVelocityTowardOppositeGoal()
        {
            var route=new[]{Vector3.zero,Vector3.right*10f};
            float aligned=RouteEta(route,3.7f,Vector3.right*3.7f,8f);
            Assert.That(RouteEta(route,3.7f,Vector3.left*3.7f,8f),Is.GreaterThan(aligned));
            Assert.That(RouteEta(route,3.7f,Vector3.forward*3.7f,8f),Is.GreaterThan(aligned));
        }
        [Test] public void InvalidMotion_CannotProduceOptimisticArrival()
        {
            Assert.That(Eta(1,0,0,8),Is.EqualTo(float.PositiveInfinity));
            Assert.That(Eta(1,3.7f,0,0),Is.EqualTo(float.PositiveInfinity));
            Assert.That(Eta(1,float.NaN,0,8),Is.EqualTo(float.PositiveInfinity));
            Assert.That(Eta(1,3.7f,float.NaN,8),Is.EqualTo(float.PositiveInfinity));
            Assert.That(RouteEta(null,3.7f,Vector3.zero,8),Is.EqualTo(float.PositiveInfinity));
            Assert.That(RouteEta(new[]{Vector3.zero,Vector3.right},float.NaN,Vector3.zero,8),Is.EqualTo(float.PositiveInfinity));
            Assert.That(Eta(0,3.7f,0,8),Is.Zero);
        }
        [Test] public void ConfiguredSpeedAndAcceleration_ChangeCandidateEta()
        {
            object slow=NewPolicy(),fast=NewPolicy(),lowAcceleration=NewPolicy();
            Train(slow,2.5f);Train(fast,3.7f);Train(lowAcceleration,3.7f,null,4f);
            Assert.That(Get(fast,"State").ToString(),Is.EqualTo("InterceptAhead"));
            Assert.That((float)Get(slow,"DroneArrival"),Is.GreaterThan((float)Get(fast,"DroneArrival")));
            Assert.That((float)Get(lowAcceleration,"DroneArrival"),Is.GreaterThan((float)Get(fast,"DroneArrival")));
            Assert.That(Get(fast,"UsesMotionTiming"),Is.True);Assert.That(Get(fast,"TimingSpeedLimit"),Is.EqualTo(3.7f));
        }
        [Test] public void StraightMotion_KeepsAdvancedFeasiblePredictionAndScores()
        {
            var p=NewPolicy();Train(p);Assert.That(Get(p,"State").ToString(),Is.EqualTo("InterceptAhead"));
            foreach(var c in (IEnumerable)Get(p,"Candidates"))
            {
                float score=30f*(float)Value(c,"Timing")+20f*(float)Value(c,"Forward")+20f*(float)Value(c,"Positioning")+
                    25f*(float)Value(c,"Confidence")+8f*(float)Value(c,"Stability");
                Assert.That(Value(c,"Score"),Is.EqualTo(score).Within(.0001f));
                Assert.That((float)Value(c,"DroneArrival"),Is.LessThanOrEqualTo((float)Value(c,"PlayerArrival")+.6f));
            }
        }
        [Test] public void StationaryAndSuddenReversal_PreserveObservationInvalidation()
        {
            var stopped=NewPolicy();for(int i=0;i<12;i++)Tick(stopped,i*.3f,true,Vector3.zero);
            Assert.That(Get(stopped,"SelectedNode"),Is.EqualTo(-1));
            var reversed=NewPolicy();Train(reversed);Tick(reversed,2.1f,true,Vector3.right*3f);
            Assert.That(Get(reversed,"SelectedNode"),Is.EqualTo(-1));Assert.That(Get(reversed,"Reason").ToString(),Does.Contain("reversed"));
        }
        [Test] public void LostSight_HiddenArgumentsCannotUpdateHistoryVelocityOrDeadline()
        {
            var p=NewPolicy();Train(p);Tick(p,2.1f,false,Vector3.one*99999);
            var last=Get(p,"LastObserved");float time=(float)Get(p,"LastObservedAt");
            object velocity=p.GetType().GetField("velocity",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p);
            var deadline=p.GetType().GetField("predictionDeadline",BindingFlags.NonPublic|BindingFlags.Instance);object until=deadline.GetValue(p);
            var first=Tick(p,2.1f,false,new Vector3(float.NaN,float.PositiveInfinity,-99999));
            var second=Tick(p,2.1f,false,Vector3.one*-99999);
            Assert.That(Value(first,"Destination"),Is.EqualTo(Value(second,"Destination")));
            Assert.That(Get(p,"LastObserved"),Is.EqualTo(last));Assert.That(Get(p,"LastObservedAt"),Is.EqualTo(time));
            Assert.That(p.GetType().GetField("velocity",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p),Is.EqualTo(velocity));
            Assert.That(deadline.GetValue(p),Is.EqualTo(until));
        }
        [Test] public void GateChangesAndFailedArrival_RetainCooldownAndNeedNewEvidence()
        {
            var p=NewPolicy();Train(p);int node=(int)Get(p,"SelectedNode");Assert.That(node,Is.GreaterThanOrEqualTo(0));
            float until=(float)p.GetType().GetField("predictionDeadline",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(p);
            Tick(p,until+.7f,false,Vector3.one*1000);
            Assert.That((int)Get(p,"FailedAttempts"),Is.GreaterThan(0));
            Tick(p,until+1f,false,Vector3.one*-1000);Assert.That(Get(p,"SelectedNode"),Is.EqualTo(-1));
            Tick(p,until+10f,false,Vector3.zero);Assert.That(Get(p,"State").ToString(),Is.EqualTo("ReturnHome"));
            var gate=NewPolicy();Train(gate);int replans=(int)Get(gate,"Replans");
            Tick(gate,2.1f,false,Vector3.zero,3.7f,null,8f,1,(a,b)=>null);
            Assert.That(Get(gate,"SelectedNode"),Is.EqualTo(-1));Assert.That((int)Get(gate,"Replans"),Is.GreaterThan(replans));
        }
        [Test] public void RepeatedTicks_DoNotSwitchOrExtendPredictionWindow()
        {
            var p=NewPolicy();Train(p);int node=(int)Get(p,"SelectedNode");
            var f=p.GetType().GetField("predictionDeadline",BindingFlags.NonPublic|BindingFlags.Instance);object deadline=f.GetValue(p);
            for(int i=0;i<20;i++)Tick(p,1.81f+i*.001f,false,Vector3.one*(i%2==0?99999:-99999));
            Assert.That(Get(p,"SelectedNode"),Is.EqualTo(node));Assert.That(Get(p,"Switches"),Is.EqualTo(0));Assert.That(f.GetValue(p),Is.EqualTo(deadline));
            Assert.That(((IList)Get(p,"History")).Count,Is.LessThanOrEqualTo(12));
        }
        [Test] public void BeforeAfter_IdenticalAdvancedPolicyWithCruiseAndMotionTiming()
        {
            string csv="timing,time,visible,state,node,confidence,drone_eta,player_eta,goal_x,goal_z,reason\n";
            foreach(bool motion in new[]{false,true})
            {
                var p=NewPolicy();
                for(int i=0;i<=32;i++)
                {
                    float now=i*.3f;bool visible=i<=9;Vector3 observed=Vector3.right*(i<=7?i*.6f:4.2f-(i-7)*.6f);
                    object decision=motion?Tick(p,now,visible,visible?observed:Vector3.one*99999):
                        p.GetType().GetMethod("StepWithRoutes").Invoke(p,new object[]{now,visible,visible?observed:Vector3.one*99999,Drone,Home,3.7f,0,Nodes,Direct,Direct});
                    Vector3 goal=(Vector3)Value(decision,"Destination");
                    csv+=string.Format(CultureInfo.InvariantCulture,"{0},{1:F3},{2},{3},{4},{5:F4},{6:F4},{7:F4},{8:F3},{9:F3},\"{10}\"\n",
                        motion?"acceleration_aware":"prior_cruise_contract",now,visible,Get(p,"State"),Get(p,"SelectedNode"),Get(p,"Confidence"),Get(p,"DroneArrival"),Get(p,"PlayerArrival"),goal.x,goal.z,Get(p,"Reason"));
                }
            }
            string folder=Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE")??"Logs/InterceptorEvidence";
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"motion-before-after.csv"),csv);
        }
    }
}
