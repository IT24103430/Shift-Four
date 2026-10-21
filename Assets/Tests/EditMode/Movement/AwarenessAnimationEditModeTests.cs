using System;
using NUnit.Framework;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class AwarenessAnimationEditModeTests
    {
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object New()=>Activator.CreateInstance(T("DroneAnimationPose"));
        private static object Get(object p,string n)=>p.GetType().GetProperty(n).GetValue(p);
        private static float V(object p,string n)=>(float)Get(p,n);
        private static void Call(object p,string n,params object[] args)=>p.GetType().GetMethod(n).Invoke(p,args);
        private static void Cue(object p,string n)=>Call(p,"SetAwareness",Enum.Parse(T("DroneAwarenessState"),n));
        private static void Step(object p,float dt,Vector3 velocity,float yaw=0,bool alive=true)=>Call(p,"Step",dt,velocity,yaw,alive);
        [TestCase("Patrol",false)] [TestCase("Suspicious",true)] [TestCase("Investigate",true)]
        [TestCase("Search",true)] [TestCase("Combat",true)] [TestCase("ReturnToPost",false)]
        public void AwarenessCue_IsSeparateFromMotionAndWeaponAuthorization(string state,bool alert)
        {
            var p=New();Cue(p,state);
            for(int i=0;i<120;i++)Step(p,1f/60,Vector3.zero);
            Assert.That(V(p,"AlertBlend")>0,Is.EqualTo(alert));Assert.That(V(p,"AlertBlend"),Is.LessThanOrEqualTo(.281f));
            Assert.That(Get(p,"State").ToString(),Is.EqualTo("Idle"));Assert.That(V(p,"FireFlash"),Is.Zero);Assert.That(V(p,"HitFlash"),Is.Zero);
            if(state=="Suspicious") {var c=(Color)Get(p,"AlertColor");Assert.That(c.r,Is.GreaterThan(c.g));Assert.That(c.g,Is.GreaterThan(c.b));Assert.That(V(p,"AlertBlend"),Is.LessThan(.11f));}
            if(state=="Combat") {var c=(Color)Get(p,"AlertColor");Assert.That(c.r,Is.GreaterThan(c.g*4));Assert.That(V(p,"AlertBlend"),Is.GreaterThan(.17f));}
        }
        [Test] public void CombatPulse_IsFasterAndStrongerThanAmberSuspicion()
        {
            var calm=New();var combat=New();Cue(calm,"Suspicious");Cue(combat,"Combat");
            int calmPeaks=0,combatPeaks=0;float ca=0,cb=0,pa=0,pb=0;
            for(int i=0;i<600;i++)
            {
                Step(calm,1f/60,Vector3.zero);Step(combat,1f/60,Vector3.zero);float a=V(calm,"AlertBlend"),b=V(combat,"AlertBlend");
                if(i>120){if(pa>ca&&pa>a)calmPeaks++;if(pb>cb&&pb>b)combatPeaks++;Assert.That(b,Is.GreaterThan(a));}
                ca=pa;cb=pb;pa=a;pb=b;
            }
            Assert.That(calmPeaks,Is.InRange(2,4));Assert.That(combatPeaks,Is.InRange(14,18));
        }
        [TestCase("Investigate")] [TestCase("Search")]
        public void Scan_IsSlowBoundedAndSuppressedDuringMovement(string state)
        {
            var p=New();Cue(p,state);float maximum=0,old=0;int crossings=0;
            for(int i=0;i<600;i++)
            {Step(p,1f/60,Vector3.zero);float yaw=V(p,"ScanYaw");maximum=Mathf.Max(maximum,Mathf.Abs(yaw));if(i>120&&Mathf.Sign(yaw)!=Mathf.Sign(old))crossings++;old=yaw;}
            Assert.That(maximum,Is.InRange(.9f,1.201f));Assert.That(crossings,Is.InRange(4,6));
            Step(p,1f/60,Vector3.forward);Assert.That(V(p,"ScanYaw"),Is.Zero);
        }
        [Test] public void Acceleration_AddsSmallPitchAndRotorEffort_ThenBrakesToLevel()
        {
            var accelerated=New();var reference=New();Call(accelerated,"SetAcceleration",1f);
            for(int i=0;i<30;i++){Step(accelerated,1f/60,Vector3.forward*.5f);Step(reference,1f/60,Vector3.forward*.5f);}
            Assert.That(V(accelerated,"Pitch"),Is.LessThan(V(reference,"Pitch")-.3f));Assert.That(V(accelerated,"RotorSpeed"),Is.GreaterThan(V(reference,"RotorSpeed")+.03f));
            Call(accelerated,"SetAcceleration",0f);Step(accelerated,1f/60,Vector3.zero);Assert.That(Get(accelerated,"State").ToString(),Is.EqualTo("Braking"));
            for(int i=0;i<240;i++)Step(accelerated,1f/60,Vector3.zero);
            Assert.That(Mathf.Abs(V(accelerated,"Pitch")),Is.LessThan(.001f));Assert.That(V(accelerated,"RotorSpeed"),Is.EqualTo(.8f).Within(.001f));
        }
        [Test] public void TurningBank_IsPreservedWhileAccelerationAndAlertAreActive()
        {
            var p=New();var reference=New();Cue(p,"Search");Call(p,"SetAcceleration",1f);
            for(int i=0;i<60;i++){Step(p,1f/60,Vector3.forward,75);Step(reference,1f/60,Vector3.forward,75);}
            Assert.That(V(p,"Bank"),Is.EqualTo(V(reference,"Bank")).Within(.00001f));Assert.That(V(p,"ScanYaw"),Is.Zero);
        }
        [Test] public void FireHitAndFatalShutdown_HavePriorityOverScanningAndAlert()
        {
            var p=New();Cue(p,"Search");for(int i=0;i<60;i++)Step(p,1f/60,Vector3.zero);
            Assert.That(Mathf.Abs(V(p,"ScanYaw")),Is.GreaterThan(.5f));Call(p,"Fire");Step(p,.001f,Vector3.zero);Assert.That(Get(p,"State").ToString(),Is.EqualTo("Firing"));Assert.That(Mathf.Abs(V(p,"ScanYaw")),Is.LessThan(.02f));
            Call(p,"Hit",false);Step(p,.001f,Vector3.zero);Assert.That(Get(p,"State").ToString(),Is.EqualTo("Damaged"));
            Call(p,"Hit",true);Assert.That(V(p,"AlertBlend"),Is.Zero);Assert.That(V(p,"ScanYaw"),Is.Zero);Assert.That(V(p,"RotorSpeed"),Is.GreaterThan(0));
            Cue(p,"Combat");Call(p,"SetAcceleration",1f);Call(p,"Fire");
            for(int i=0;i<180;i++)Step(p,1f/60,Vector3.forward,90,false);
            foreach(string n in new[]{"AlertBlend","ScanYaw","ForwardAcceleration","FireFlash","HitFlash","RotorSpeed"})Assert.That(V(p,n),Is.Zero,n);
        }
        [Test] public void ReturningToPatrol_FadesAlertAndScanWithoutChangingHoverHeight()
        {
            var p=New();Cue(p,"Search");for(int i=0;i<120;i++)Step(p,1f/60,Vector3.zero);Cue(p,"Patrol");
            for(int i=0;i<240;i++){Step(p,1f/60,Vector3.zero);Assert.That(Mathf.Abs(V(p,"Hover")),Is.LessThanOrEqualTo(.02001f));}
            Assert.That(V(p,"AlertBlend"),Is.Zero);Assert.That(Mathf.Abs(V(p,"ScanYaw")),Is.LessThan(.0001f));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void MixedAwarenessAndProgressiveFlight_AgreeAcrossCadences(int fps)
        {
            object p=New(),reference=New();float actual=Run(p,fps),expected=Run(reference,240);
            foreach(string n in new[]{"Pitch","Bank","RotorSpeed","AlertBlend","ScanYaw"})Assert.That(V(p,n),Is.EqualTo(V(reference,n)).Within(.025f),n);
            Assert.That(actual,Is.EqualTo(expected).Within(.025f));
        }
        private static float Run(object p,int fps)
        {
            float travel=0;
            for(int i=0;i<fps*6;i++)
            {
                float time=(i+1f)/fps;Cue(p,time<=1?"Suspicious":time<=4?"Combat":"Search");
                float speed=time<=1?time:time<=3?1:Mathf.Clamp01(4-time);
                Call(p,"SetAcceleration",time<=1?1f:0f);Step(p,1f/fps,Vector3.forward*speed,time>1&&time<=3?60:0);travel+=V(p,"RotorTravel");
            }
            return travel;
        }
        [Test] public void InvalidAccelerationAndPausedTime_CannotAdvanceOrPoisonPose()
        {
            var p=New();Cue(p,"Combat");Call(p,"SetAcceleration",float.NaN);Step(p,0,Vector3.forward);
            foreach(string n in new[]{"AlertBlend","ScanYaw","ForwardAcceleration","RotorTravel"})Assert.That(V(p,n),Is.Zero);
            Call(p,"SetAcceleration",float.PositiveInfinity);Step(p,1f/60,Vector3.zero);Assert.That(V(p,"ForwardAcceleration"),Is.Zero);
        }
    }
}
