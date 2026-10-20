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
        [UnityTest] public IEnumerator AllFour_MovingPlayerStandoff_ProtectsBodyCameraAndCanResume()
        {
            var cc=player.GetComponent<CharacterController>();var camera=player.GetComponentInChildren<Camera>();
            string csv="agent,frames,max_penetration,drone_travel,resumed\n";
            // The authored Interceptor starts in this test corridor. Move every inactive
            // fixture actor to its own room home before bringing one subject into Control.
            foreach(var other in brains)Assert.That(other.GetComponent<NavMeshAgent>().Warp((Vector3)Get(other,"Home")),Is.True);
            foreach(var brain in brains.OrderBy(b=>b.name))
            {
                var agent=brain.GetComponent<NavMeshAgent>();var motor=brain.GetComponent(T("DroneMotor"));
                Vector3 home=brain.transform.position;agent.Warp(new Vector3(10,0,5.5f));
                cc.enabled=false;player.transform.position=new Vector3(10,0,2.5f);cc.enabled=true;Call(player,"ApplyCameraPose");
                Physics.SyncTransforms();
                Assert.That((bool)Call(brain.GetComponent(T("DroneBody")),"CanOccupy",brain.transform.position),Is.True,"Fixture must start outside all other actors and props");
                ((Behaviour)motor).enabled=true;
                Vector3 previous=brain.transform.position;float travel=0,depth=0;
                for(int frame=0;frame<360;frame++)
                {
                    // Continuous CharacterController movement, including two direction reversals.
                    float direction=frame<90?1f:frame<270?-1f:1f;
                    cc.Move(Vector3.right*(direction*1.8f*Time.deltaTime));Call(player,"ApplyCameraPose");
                    Call(motor,"GoToCombat",player.transform.position,player,true);
                    yield return null;Physics.SyncTransforms();
                    travel+=Vector3.Distance(previous,brain.transform.position);previous=brain.transform.position;
                    var physical=brain.GetComponent<Collider>();
                    if(Physics.ComputePenetration(physical,physical.transform.position,physical.transform.rotation,cc,cc.transform.position,cc.transform.rotation,out _,out float penetration))depth=Mathf.Max(depth,penetration);
                    foreach(var renderer in brain.GetComponentsInChildren<MeshRenderer>())
                    foreach(var uv in new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one})
                        Assert.That(renderer.bounds.Contains(camera.ViewportToWorldPoint(new Vector3(uv.x,uv.y,camera.nearClipPlane))),Is.False,brain.name+" model envelope must not contain the camera near plane");
                    Assert.That(((Behaviour)motor).enabled,Is.True,"Proximity must not terminally stop the motor");
                }
                TestContext.Out.WriteLine($"{brain.name} start fixture: player={player.transform.position}, drone={brain.transform.position}, travel={travel}, motor={Field(motor,"state")}, combatGoal={Field(motor,"combatGoal")}, hasCombatGoal={Field(motor,"hasCombatGoal")}");
                var body=brain.GetComponent(T("DroneBody"));
                foreach(var hit in Physics.OverlapBox(brain.transform.position+(Vector3)Get(body,"EnvelopeCenter"),(Vector3)Get(body,"EnvelopeExtents"),Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                    if(!hit.transform.IsChildOf(brain.transform))TestContext.Out.WriteLine("Body overlap diagnostic: "+hit.name+" "+hit.bounds);
                Assert.That(depth,Is.LessThanOrEqualTo(.005f));Assert.That(travel,Is.GreaterThan(.2f),brain.name+" must remain responsive to moving target");
                cc.enabled=false;player.transform.position=new Vector3(-10,0,-10);cc.enabled=true;Call(player,"ApplyCameraPose");
                Vector3 goal=new Vector3(10,0,4.5f);Call(motor,"GoTo",goal);
                float until=Time.time+8f;while(Time.time<until&&Field(motor,"state").ToString()!="Arrived")yield return null;
                bool resumed=Field(motor,"state").ToString()=="Arrived";Assert.That(resumed,Is.True);
                csv+=$"{brain.name},360,{depth},{travel},{resumed}\n";Write("moving-player-standoff.csv",csv);
                ((Behaviour)motor).enabled=false;agent.isStopped=true;agent.ResetPath();agent.Warp(home);
            }
        }
    }
}
#endif
