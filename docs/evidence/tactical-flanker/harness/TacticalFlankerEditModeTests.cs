using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class TacticalFlankerEditModeTests
    {
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
        private static object F(object o,string n)=>o.GetType().GetField(n).GetValue(o);
        private static object New()=>Activator.CreateInstance(T("FlankerPolicy"),Activator.CreateInstance(T("FlankerSettings")));
        private static Vector3 Target=new Vector3(0,0,10),Home=new Vector3(0,0,-10);
        private static Vector3[] Direct(Vector3 a,Vector3 b)=>new[]{a,b};
        private static bool Clear(Vector3 a,Vector3 b)=>true;
        private static object Sound(float start=0,float until=5)=>Activator.CreateInstance(T("SoundObservation"),new Vector3(4,0,2),Enum.Parse(T("NoiseCategory"),"Gunshot"),.9f,1.5f,start,until);
        private static object Tick(object p,float now,bool visible=true,Vector3? observation=null,
            Func<Vector3,Vector3,Vector3[]> route=null,Func<Vector3,Vector3,bool> line=null,object sound=null,Vector3? drone=null)
            =>p.GetType().GetMethod("Step").Invoke(p,new object[]{now,visible,observation??Target,drone??Vector3.zero,Home,sound!=null,sound??Activator.CreateInstance(T("SoundObservation")),route??Direct,line??Clear});
        private static int Side(object p)=>(int)Get(p,"SelectedSide");
        [Test] public void BothSidesReachable_PreservesLeftRightAndExplainableOwnerFormula()
        {
            var p=New();Tick(p,0);Assert.That(Side(p),Is.EqualTo(-1));Assert.That(((IList)Get(p,"Candidates")).Count,Is.EqualTo(2));
            foreach(object c in (IEnumerable)Get(p,"Candidates"))
            {
                float expected=42+30*(float)F(c,"Lateral")+12*(float)F(c,"Range")+((bool)F(c,"ClearLine")?14:0)-1.15f*(float)F(c,"Cost");
                Assert.That(F(c,"Score"),Is.EqualTo(expected).Within(.0001f));
            }
        }
        [TestCase(-1)] [TestCase(1)] public void OneSideUnreachable_SelectsOtherSide(int permitted)
        {
            var p=New();Tick(p,0,route:(a,b)=>Mathf.Sign(b.x)==permitted?Direct(a,b):null);Assert.That(Side(p),Is.EqualTo(permitted));
        }
        [Test] public void RouteCostAndCoverQuality_CanPreferLongerEuclideanSide()
        {
            var p=New();Tick(p,0,route:(a,b)=>b.x<0?new[]{a,a+Vector3.back*20,b}:Direct(a,b));Assert.That(Side(p),Is.EqualTo(1));
            var cover=New();Tick(cover,0,line:(a,b)=>a.x>0);Assert.That(Side(cover),Is.EqualTo(1));
        }
        [Test] public void BothFlanksUnreachable_UsesRememberedStandoffThenSafeStorageHold()
        {
            var p=New();var d=Tick(p,0,route:(a,b)=>Mathf.Abs(b.x)<.01f?Direct(a,b):null);
            Assert.That(Side(p),Is.Zero);Assert.That(F(d,"Name").ToString(),Does.Contain("standoff"));
            Assert.That(Vector3.Distance((Vector3)F(d,"Destination"),Target),Is.GreaterThanOrEqualTo(2.5f));
            d=Tick(p,.3f,route:(a,b)=>null);Assert.That(F(d,"Name").ToString(),Does.Contain("Hold clear"));Assert.That(F(d,"Destination"),Is.EqualTo(Vector3.zero));
        }
        [Test] public void VisibleFallback_ApproachesCombatStandoffInsteadOfHoldingAtFlankRange()
        {
            var p=New();Vector3 drone=new Vector3(0,0,4.5f);
            var d=Tick(p,0,drone:drone,route:(a,b)=>Mathf.Abs(b.x)<.01f?Direct(a,b):null);
            Assert.That(F(d,"Name").ToString(),Does.Contain("standoff"));
            Assert.That(Vector3.Distance((Vector3)F(d,"Destination"),drone),Is.GreaterThan(1.1f));
            Assert.That(Vector3.Distance((Vector3)F(d,"Destination"),Target),Is.EqualTo(3.5f).Within(.001f));
        }
        [Test] public void CommitAndSwitchMargin_PreventScoreOscillationButPermitUsefulSideChange()
        {
            var p=New();Tick(p,0);Tick(p,.3f,line:(a,b)=>a.x>0);Assert.That(Side(p),Is.EqualTo(-1));
            Tick(p,1.5f,line:(a,b)=>a.x>0);Assert.That(Side(p),Is.EqualTo(1));
            for(int i=0;i<10;i++)Tick(p,1.6f+i*.03f);Assert.That(Side(p),Is.EqualTo(1));Assert.That(Get(p,"Switches"),Is.EqualTo(1));
        }
        [Test] public void InvalidCommittedRoute_OverridesCommitAndCoolsFailedSide()
        {
            var p=New();Tick(p,0);Tick(p,.3f,route:(a,b)=>b.x<0?null:Direct(a,b));
            Assert.That(Side(p),Is.EqualTo(1));Assert.That(Get(p,"FailedAttempts"),Is.EqualTo(1));
            for(int i=0;i<10;i++)Tick(p,.4f+i*.1f);Assert.That(Side(p),Is.EqualTo(1));Assert.That(Get(p,"FailedAttempts"),Is.EqualTo(1));
        }
        [Test] public void StuckFlank_ChangesSideAndCannotRepeatUntilCooldownExpires()
        {
            var p=New();Tick(p,0);Tick(p,3);Assert.That(Side(p),Is.EqualTo(1));Assert.That(Get(p,"FailedAttempts"),Is.EqualTo(1));
            Tick(p,6);Assert.That(Side(p),Is.Zero);Assert.That(Get(p,"FailedAttempts"),Is.EqualTo(2));
            Tick(p,7.1f);Assert.That(Side(p),Is.EqualTo(-1));Assert.That((int)Get(p,"FailureCount"),Is.LessThanOrEqualTo(8));
        }
        [Test] public void ReachedButObstructedFlank_IsRetiredForOtherSide()
        {
            var p=New();Tick(p,0);object c=((IEnumerable)Get(p,"Candidates")).Cast<object>().First(x=>(int)F(x,"Side")==-1);
            var position=(Vector3)F(c,"Position");Tick(p,.3f,line:(a,b)=>false,drone:position);Tick(p,1.9f,line:(a,b)=>false,drone:position);
            Assert.That(Get(p,"FailedAttempts"),Is.EqualTo(1));Assert.That(Side(p),Is.EqualTo(1));
        }
        [Test] public void GenuineReversal_ReplansFromVisibleHistoryOnly()
        {
            var p=New();Tick(p,0);Tick(p,.3f,observation:Target+Vector3.forward);int replans=(int)Get(p,"Replans");Tick(p,.6f,observation:Target);
            Assert.That((int)Get(p,"Replans"),Is.GreaterThan(replans));Assert.That(((Vector3)Get(p,"VisualVelocity")).z,Is.LessThan(0));
        }
        [Test] public void SoundOnly_UsesUncertainPointWithoutVisualHistoryVelocityOrExactTarget()
        {
            var p=New();var d=Tick(p,1,false,Vector3.one*9999,sound:Sound());
            Assert.That(Get(p,"UsingSound"),Is.True);Assert.That(Get(p,"VisualObservations"),Is.Zero);
            Assert.That(Get(p,"LastVisualAt"),Is.EqualTo(float.NegativeInfinity));Assert.That(Get(p,"VisualVelocity"),Is.EqualTo(Vector3.zero));
            Assert.That(F(d,"Destination"),Is.EqualTo(new Vector3(4,0,2)));Assert.That(Get(p,"HeardPosition"),Is.EqualTo(F(d,"Destination")));
            d=Tick(p,5.1f,false,Vector3.one*-9999,sound:Sound());Assert.That(Get(p,"UsingSound"),Is.False);Assert.That(F(d,"Name").ToString(),Does.Contain("Storage post"));
        }
        [Test] public void VisibleMemory_TakesPriorityAndNoiseCannotRefreshIt()
        {
            var p=New();Tick(p,0);Tick(p,1,false,Vector3.one*9999,sound:Sound(.5f,8));Assert.That(Get(p,"UsingSound"),Is.False);
            Assert.That(Get(p,"LastVisualAt"),Is.EqualTo(0));Assert.That(Get(p,"LastVisual"),Is.EqualTo(Target));
            Tick(p,5.1f,false,Vector3.one*-9999,sound:Sound(.5f,8));Assert.That(Get(p,"UsingSound"),Is.True);Assert.That(Get(p,"LastVisualAt"),Is.EqualTo(0));
        }
        [Test] public void HiddenInputs_CannotRefreshVisualMemoryVelocityOrTacticalGoal()
        {
            var p=New();Tick(p,0);Tick(p,.3f,observation:Target+Vector3.forward);
            var last=Get(p,"LastVisual");var velocity=Get(p,"VisualVelocity");var count=Get(p,"VisualObservations");
            var a=Tick(p,.6f,false,new Vector3(float.NaN,float.PositiveInfinity,9999));var b=Tick(p,.6f,false,Vector3.one*-9999);
            Assert.That(F(a,"Destination"),Is.EqualTo(F(b,"Destination")));Assert.That(Get(p,"LastVisual"),Is.EqualTo(last));
            Assert.That(Get(p,"VisualVelocity"),Is.EqualTo(velocity));Assert.That(Get(p,"VisualObservations"),Is.EqualTo(count));
            var d=Tick(p,5.31f,false,Vector3.one*9999);Assert.That(F(d,"Name").ToString(),Does.Contain("Storage post"));
        }
    }
}
