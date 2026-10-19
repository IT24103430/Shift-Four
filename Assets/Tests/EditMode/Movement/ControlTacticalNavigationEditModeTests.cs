using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class ControlTacticalNavigationEditModeTests
    {
        private object settings, navigation;
        private Type type;
        private readonly Vector3 home=new Vector3(10,0,10);
        private Func<Vector3,Vector3,Vector3[]> route;
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private object Get(string n)=>type.GetProperty(n).GetValue(navigation);
        private static object Field(object o,string n)=>o.GetType().GetField(n).GetValue(o);
        private void Set(string n,object v)=>settings.GetType().GetField(n).SetValue(settings,v);
        private Vector3[] Points=>((IEnumerable)Get("Points")).Cast<Vector3>().ToArray();
        private object Build(int entry,int target,int direction)=>type.GetMethod("Build").Invoke(navigation,new object[]{entry,target,direction,Points[entry],route});
        private bool Decide(float now=1f,float observedAt=1f,Vector3? observed=null,Vector3? velocity=null,Vector3? drone=null,int version=0)
        {
            object[] args={now,observedAt,observed??new Vector3(3,0,10),velocity??Vector3.forward*2,
                drone??new Vector3(10,0,15.5f),home,3.5f,1f,version,route,route,null};
            return (bool)type.GetMethod("TryDecide").Invoke(navigation,args);
        }
        [SetUp] public void SetUp()
        {
            settings=Activator.CreateInstance(T("ControlTacticalSettings")); type=T("ControlTacticalNavigation");
            navigation=Activator.CreateInstance(type,settings); type.GetMethod("SetAnchor").Invoke(navigation,new object[]{home});
            route=(a,b)=>new[]{a,b};
        }
        [Test] public void Coordinates_AreAnchoredToControlHome()
        {
            CollectionAssert.AreEqual(new[]{new Vector3(10,0,4.5f),new Vector3(3,0,4.5f),new Vector3(3,0,15.5f),
                new Vector3(10,0,15.5f),new Vector3(17,0,13.5f),new Vector3(17,0,4.5f)},Points);
        }
        [Test] public void LeftAndRight_AreDifferentSimpleArcs()
        {
            var left=Build(0,3,1); var right=Build(0,3,-1);
            CollectionAssert.AreEqual(new[]{0,1,2,3},(int[])Field(left,"Junctions"));
            CollectionAssert.AreEqual(new[]{0,5,4,3},(int[])Field(right,"Junctions"));
            Assert.That((float)Field(left,"Distance"),Is.EqualTo(25f).Within(.001f));
            Assert.That((float)Field(right,"Distance"),Is.EqualTo(16f+Mathf.Sqrt(53f)).Within(.001f));
        }
        [Test] public void EveryArc_HasAtMostSixDistinctStops()
        {
            for(int a=0;a<6;a++) for(int b=0;b<6;b++) foreach(int direction in new[]{-1,1})
            {
                var plan=Build(a,b,direction);var stops=(int[])Field(plan,"Junctions");
                Assert.That(stops.Length,Is.LessThanOrEqualTo(6)); Assert.That(stops.Distinct().Count(),Is.EqualTo(stops.Length));
                Assert.That(stops[stops.Length-1],Is.EqualTo(b));
            }
        }
        [Test] public void BlockedLeftLeg_DoesNotMakeRightRouteUnreachable()
        {
            route=(a,b)=>a.x==3 && b.x==3 ? null:new[]{a,b};
            Assert.That(Build(0,3,1),Is.Null);Assert.That(Build(0,3,-1),Is.Not.Null);
        }
        [Test] public void SelectsAheadOnLeft_WithFeasibleArrivalTime()
        {
            Assert.That(Decide(),Is.True);var plan=Get("Plan");
            Assert.That(Field(plan,"Target"),Is.EqualTo(2));Assert.That(Field(plan,"Side"),Is.EqualTo("Left"));
            Assert.That((float)Field(plan,"DroneEta"),Is.LessThanOrEqualTo((float)Field(plan,"PlayerEta")+.6f));
        }
        [Test] public void SelectsAheadOnRight_WhenMotionAndStartingSideDiffer()
        {
            Assert.That(Decide(observed:new Vector3(17,0,8),drone:new Vector3(10,0,15.5f)),Is.True);
            Assert.That(Field(Get("Plan"),"Target"),Is.EqualTo(4));
        }
        [Test] public void LongPhysicalDetour_CannotWinOnEuclideanProximity()
        {
            route=(a,b)=>new[]{a,a+Vector3.forward*100,b}; Assert.That(Decide(),Is.False);
        }
        [Test] public void NoConnector_ReturnsExplicitNoPlan()
        { route=(a,b)=>null; Assert.That(Decide(),Is.False);Assert.That(Get("Plan"),Is.Null); }
        [Test] public void RepeatedUpdates_KeepSameTargetAndRouteDirection()
        {
            Assert.That(Decide(),Is.True); var plan=Get("Plan");
            for(int i=0;i<20;i++) Assert.That(Decide(),Is.True);
            Assert.That(Get("Plan"),Is.SameAs(plan));Assert.That(Get("Switches"),Is.EqualTo(0));
        }
        [Test] public void Reversal_InvalidatesCommitment()
        {
            Assert.That(Decide(),Is.True);Assert.That(Decide(now:1.3f,observedAt:1.3f,velocity:Vector3.back*2),Is.False);
            Assert.That(Get("Plan"),Is.Null);Assert.That((int)Get("Replans"),Is.GreaterThan(0));
        }
        [Test] public void StationaryOrExpiredObservation_DropsLocalPlan()
        {
            Assert.That(Decide(),Is.True);Assert.That(Decide(velocity:Vector3.zero),Is.False);
            Assert.That(Decide(now:8f,observedAt:1f),Is.False);
        }
        [Test] public void GateVersion_RequestsReplanWithoutChangingTopology()
        {
            Assert.That(Decide(),Is.True);Assert.That(Decide(now:1.3f,observedAt:1.3f,version:1),Is.True);
            Assert.That((int)Get("Replans"),Is.GreaterThan(0));Assert.That(Points.Length,Is.EqualTo(6));
        }
        [Test] public void BlockedCommittedRoute_BypassesMinimumCommitment()
        {
            Set("minimumCommitment",100f);Assert.That(Decide(),Is.True);route=(a,b)=>null;
            Assert.That(Decide(now:1.1f,observedAt:1.1f),Is.False);Assert.That(Get("Plan"),Is.Null);
        }
        [Test] public void ReachedPrediction_IsNotRepeatedWithoutNewEvidence()
        {
            Assert.That(Decide(),Is.True);
            Assert.That(Decide(now:4.5f,drone:new Vector3(3,0,15.5f)),Is.False);
            Assert.That(Get("Plan"),Is.Null);
        }
        [Test] public void EarlyArrival_HoldsTheCommittedInterceptionPoint()
        {
            Assert.That(Decide(),Is.True);
            Assert.That(Decide(now:1.1f,drone:new Vector3(3,0,15.5f)),Is.True);
            Assert.That(Field(Get("Plan"),"Target"),Is.EqualTo(2));
            Assert.That(Get("Reason").ToString(),Does.Contain("Holding"));
        }
        [Test] public void DisabledLayer_LeavesReviewedFallbackAvailable()
        { Set("enabled",false);Assert.That(Decide(),Is.False); }
        [Test] public void OutsideControl_DoesNotCreateCrossRoomLocalShortcuts()
        { Assert.That(Decide(drone:new Vector3(-10,0,10)),Is.False);Assert.That(Decide(observed:new Vector3(10,0,-10)),Is.False); }
        [Test] public void InvalidRoutePoints_AreRejected()
        {
            route=(a,b)=>new[]{a,new Vector3(float.NaN,0,0),b};Assert.That(Build(0,3,1),Is.Null);
        }
        [Test] public void InvalidRingConfiguration_FailsClosed()
        {
            Set("offsets",new Vector3[5]);type.GetMethod("SetAnchor").Invoke(navigation,new object[]{home});
            Assert.That(Build(0,3,1),Is.Null);Assert.That(Decide(),Is.False);
        }
        [Test] public void PredictionAdapter_PreservesRemainingArcWhenAnotherConnectorBecomesNearer()
        {
            var method=type.GetMethod("RouteTo");
            var first=method.Invoke(navigation,new object[]{3,Points[0],route});
            type.GetMethod("Follow").Invoke(navigation,new object[]{first,Points[0],1f});
            var stops=(int[])Field(first,"Junctions");
            Vector3 moved=Points[stops[1]];
            type.GetMethod("Follow").Invoke(navigation,new object[]{first,moved,1f});
            var next=method.Invoke(navigation,new object[]{3,moved,route});
            CollectionAssert.AreEqual(stops,(int[])Field(next,"Junctions"));
            Assert.That(((Vector3[])Field(next,"Route")).Length,Is.GreaterThan(1));
            route=(a,b)=>null;
            Assert.That(method.Invoke(navigation,new object[]{3,moved,route}),Is.Null);
        }
    }
}
