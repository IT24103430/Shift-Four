#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace ShiftFour.Tests
{
    public sealed partial class Student4UnifiedPlayModeTests
    {
        [UnityTest] public IEnumerator RenderedAllFour_ActualMotorTurningAndBraking()
        {
            string csv="agent,turn_seen,brake_seen,arrived,maximum_bank,maximum_pitch,travel_metres\n";
            foreach(var brain in brains.OrderBy(b=>b.name))
            {
                var motor=brain.GetComponent(T("DroneMotor"));var agent=brain.GetComponent<NavMeshAgent>();
                var visual=brain.GetComponentInChildren(T("DroneRotorVisuals"));var body=brain.GetComponent(T("DroneBody"));
                Vector3 home=brain.transform.position;
                Assert.That(agent.Warp(new Vector3(3,0,4.5f)),Is.True);brain.transform.rotation=Quaternion.LookRotation(Vector3.right);
                yield return null;Physics.SyncTransforms();
                Assert.That((bool)Call(body,"CanOccupy",brain.transform.position),Is.True);
                ((Behaviour)motor).enabled=true;Call(motor,"GoTo",new Vector3(3,0,15.5f));
                bool turned=false,braked=false;float bank=0,pitch=0,travel=0;float until=Time.time+14f;
                Vector3 previous=brain.transform.position;
                while(Time.time<until)
                {
                    yield return null;
                    travel+=Vector3.Distance(previous,brain.transform.position);previous=brain.transform.position;
                    Assert.That((bool)Call(body,"CanOccupy",brain.transform.position),Is.True,brain.name+" movement clearance");
                    var pose=Field(visual,"pose");bank=Mathf.Max(bank,Mathf.Abs((float)Get(pose,"Bank")));pitch=Mathf.Max(pitch,Mathf.Abs((float)Get(pose,"Pitch")));
                    string state=Get(visual,"State").ToString();
                    if(!turned&&state=="Turning"&&bank>.1f){turned=true;yield return Capture(brain,brain.name+"-turning");}
                    if(!braked&&state=="Braking"){braked=true;yield return Capture(brain,brain.name+"-braking");}
                    if(Field(motor,"state").ToString()=="Arrived")break;
                }
                bool arrived=Field(motor,"state").ToString()=="Arrived";
                csv+=$"{brain.name},{turned},{braked},{arrived},{bank},{pitch},{travel}\n";
                Write("actual-motion-states.csv",csv);
                Assert.That(turned,Is.True,brain.name+" actual root turn must bank");
                Assert.That(braked,Is.True,brain.name+" actual arrival must brake");Assert.That(arrived,Is.True);
                Assert.That(travel,Is.InRange(9f,14f),"No repeated route loops");
                ((Behaviour)motor).enabled=false;agent.isStopped=true;agent.ResetPath();agent.Warp(home);
            }
        }
    }
}
#endif
