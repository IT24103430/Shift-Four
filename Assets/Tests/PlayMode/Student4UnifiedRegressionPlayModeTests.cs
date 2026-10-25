#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed partial class Student4UnifiedPlayModeTests
    {
        [UnityTest] public IEnumerator TacticalCache_ReusesOnlyCurrentDecision_RechecksMovedObstacleAndGate()
        {
            Call(interceptor,"Decide",false,Vector3.zero,100f);
            var validator=Get(interceptor,"TacticalValidator");
            Vector3 a=new Vector3(3,0,4.5f),b=new Vector3(3,0,15.5f);
            Call(validator,"BeginDecision");
            var first=Call(validator,"Route",a,b);Assert.That(first,Is.Not.Null);
            Assert.That(Call(validator,"Route",a,b),Is.SameAs(first));
            Assert.That(Get(validator,"CacheHits"),Is.EqualTo(1));
            Call(validator,"EndDecision");
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position=new Vector3(3,1.6f,10);obstacle.transform.localScale=new Vector3(.1f,.2f,.1f);
            Physics.SyncTransforms();Call(validator,"BeginDecision");
            Assert.That(Call(validator,"Route",a,b),Is.Null,"Next decision must see moved/created obstacles");
            Call(validator,"EndDecision");Object.Destroy(obstacle);yield return null;Physics.SyncTransforms();
            Assert.That(Call(validator,"Route",a,b),Is.Not.Null,"Queries outside a decision are always fresh");
            var progress=Get(session,"Progress");
            for(int i=0;i<4;i++){Call(progress,"DefeatDrone",i);Call(progress,"ActivateConsole",i);}
            yield return new WaitForSeconds(2f);
            a=new Vector3(-10,0,10);b=new Vector3(10,0,10);
            Call(validator,"BeginDecision");Assert.That(Call(validator,"PlayerRoute",a,b),Is.Not.Null);
            Call(graph,"SetGateOpen",1,false);
            Assert.That(Call(validator,"PlayerRoute",a,b),Is.Null,"Version changes invalidate even within the decision scope");
            Call(validator,"EndDecision");
        }

        [UnityTest] public IEnumerator BodyQueryOverflow_StillFindsExternalObstacleAmongManySelfColliders()
        {
            var agent=interceptor.GetComponent<NavMeshAgent>();agent.Warp(new Vector3(10,0,4.5f));
            var body=interceptor.GetComponent(T("DroneBody"));
            Vector3 position=interceptor.transform.position;
            Vector3 center=(Vector3)Get(body,"EnvelopeCenter");
            var fixtures=new List<GameObject>();
            try
            {
                for(int i=0;i<80;i++)
                {
                    var child=new GameObject("Overflow self collider "+i);child.transform.SetParent(interceptor.transform);
                    child.transform.position=position+center;child.AddComponent<BoxCollider>().size=Vector3.one*.02f;fixtures.Add(child);
                }
                Physics.SyncTransforms();Assert.That((bool)Call(body,"CanOccupy",position),Is.True);
                var external=GameObject.CreatePrimitive(PrimitiveType.Cube);fixtures.Add(external);
                external.transform.position=position+center;external.transform.localScale=Vector3.one*.1f;Physics.SyncTransforms();
                Assert.That((bool)Call(body,"CanOccupy",position),Is.False,"A full result buffer must not hide an external collider");
                external.transform.position=position+center+Vector3.forward*2f;Physics.SyncTransforms();
                Vector3 swept=(Vector3)Call(body,"Sweep",position,Vector3.forward*3f);
                Vector3 extents=(Vector3)Get(body,"EnvelopeExtents");
                float boundary=external.GetComponent<Collider>().bounds.min.z-extents.z-.039f;
                Assert.That(swept.z,Is.LessThanOrEqualTo(boundary),"All sweep hits, including overflow, must preserve the measured body clearance");
                Assert.That((bool)Call(body,"CanOccupy",swept),Is.True,"The stopped envelope must remain outside the obstacle");
            }
            finally {foreach(var fixture in fixtures)Object.Destroy(fixture);}
            yield return null;
        }

        [UnityTest] public IEnumerator ScriptedMission_AllFourRealConsoles_Gates_Exit_WinRestart_LossRestart()
        {
            // Scripted positioning and accepted damage calls, not a manual keyboard playthrough.
            string csv="step,gate,console,drone_defeated,physical_open,ended\n";
            var consoles=Find("ConsoleSwitch");
            string[] types={"ScoutBrain","GuardBrain","InterceptorBrain","FlankerBrain"};
            var progress=Get(session,"Progress");
            Assert.That((bool)Get(session,"MissionTimerActive"),Is.False,"No destruction timer before planting");
            var bomb=Find("BombConsole").Single();
            Assert.That(bomb.transform.position.x,Is.LessThan(0));
            Assert.That(bomb.transform.position.z,Is.LessThan(0));
            Call(bomb,"Interact");yield return new WaitForSeconds(5.2f);
            Assert.That((bool)Get(session,"MissionTimerActive"),Is.True);
            float startingTime=(float)Get(session,"MissionRemaining");
            for(int id=0;id<4;id++)
            {
                var drone=brains.Single(b=>b.GetType().Name==types[id]);
                var console=consoles.Single(c=>(int)Get(c,"GateId")==id);
                bool access=false;
                var target=console.GetComponent<Collider>().bounds.center;
                for(int angle=0;angle<360&&!access;angle+=15)
                {
                    Vector3 point=target+Quaternion.Euler(0,angle,0)*Vector3.forward*2f;point.y=0;
                    if(!NavMesh.SamplePosition(point,out var hit,.2f,NavMesh.AllAreas))continue;
                    if(Route((Vector3)Get(drone,"Home"),hit.position).Count==0)continue;
                    var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=hit.position;cc.enabled=true;Call(player,"ApplyCameraPose");Physics.SyncTransforms();
                    Vector3 eye=(Vector3)Get(player,"EyePosition");var ray=target-eye;
                    access=Physics.Raycast(eye,ray.normalized,out var seen,3f,~0,QueryTriggerInteraction.Ignore)&&seen.collider.GetComponentInParent(T("ConsoleSwitch"))==console;
                }
                Assert.That(access,Is.True,"Reachable console interaction ray "+id);
                Call(console,"Interact");yield return new WaitForSeconds(2f);
                Assert.That((bool)Call(graph,"IsGateOpen",id),Is.False,"Console alone cannot open gate");
                Call(drone.GetComponent(T("Health")),"TakeDamage",10000f);
                yield return new WaitForSeconds(3f);
                Assert.That((bool)Call(graph,"IsGateOpen",id),Is.True);
                Assert.That(Get(drone.GetComponentInChildren(T("DroneRotorVisuals")),"State").ToString(),Is.EqualTo("Disabled"));
                csv+=$"objective_complete,{id},{Call(progress,"ConsoleOnline",id)},{Call(progress,"DroneDefeated",id)},{Call(graph,"IsGateOpen",id)},{Get(session,"Ended")}\n";
                if(id<3){Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.False);}
            }
            Call(session,"EnterRoom",3);
            Assert.That((float)Get(session,"MissionRemaining"),Is.LessThan(startingTime-10f),
                "Entering Storage must not restart the whole-mission timer");
            Assert.That(Route(new Vector3(10,0,-10),new Vector3(22,0,-15)),Is.Not.Empty);
            Call(session,"TryExit");Assert.That((bool)Get(session,"Ended"),Is.True);
            csv+="win,3,True,True,True,True\n";
            Call(session,"Restart");yield return null;yield return null;
            session=Find("GameSession").Single();player=Find("PlayerController").Single();graph=Find("WaypointGraph").Single();
            Assert.That((bool)Get(session,"Ended"),Is.False);Assert.That(Get(session,"ConsolesOnline"),Is.EqualTo(0));
            Assert.That(Find("DroneWreckMotion"),Is.Empty);Assert.That(Get(session,"DronesRemaining"),Is.EqualTo(4));
            Call(player.GetComponent(T("Health")),"TakeDamage",10000f);Assert.That((bool)Get(session,"Ended"),Is.True);
            Call(session,"Restart");yield return null;yield return null;
            Assert.That((bool)Get(Find("GameSession").Single(),"Ended"),Is.False);
            Assert.That((float)Get(Find("PlayerController").Single().GetComponent(T("Health")),"Current"),Is.EqualTo(100f * PlayerWeaponTestActions.CombatMultiplier));
            session=Find("GameSession").Single();Assert.That((bool)Get(session,"MissionTimerActive"),Is.False);
            Call(Find("BombConsole").Single(),"Interact");yield return new WaitForSeconds(5.2f);
            Set(session,"detonationAt",Time.time-.1f);Call(session,"Update");
            Assert.That((bool)Get(session,"Ended"),Is.True);
            StringAssert.Contains("BOMB DETONATED",(string)Get(session,"Outcome"));
            csv+="win_restart_and_loss_restart,-1,False,False,False,False\n";Write("scripted-mission.csv",csv);
        }
    }
}
#endif
