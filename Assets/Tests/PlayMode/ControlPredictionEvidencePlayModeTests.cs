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
    public sealed partial class ControlPredictionMainLabPlayModeTests
    {
        [UnityTest] public IEnumerator RenderedPrediction_VisibleMotion_Reversal_CoverAndMemorySearch()
        {
            yield return OpenGates();Scenario(out var source,out var heading);
            brain.GetComponent<NavMeshAgent>().isStopped=true;
            ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled=false;
            var cc=player.GetComponent<CharacterController>();
            string directory=Environment.GetEnvironmentVariable("SHIFTFOUR_EVIDENCE")??"Logs/InterceptorEvidence";
            Directory.CreateDirectory(directory);
            string csv="phase,state,selected,confidence,last_observed,reason\n";
            for(int i=0;i<=7;i++)
            {
                cc.enabled=false;player.transform.position=source+heading*i*.36f;cc.enabled=true;Call(player,"ApplyCameraPose");
                brain.transform.rotation=Quaternion.LookRotation(player.transform.position-brain.transform.position);Physics.SyncTransforms();
                Assert.That((bool)Call(brain,"CanSeePlayer"),Is.True);
                Call(brain,"Decide",true,player.transform.position,Vector3.Distance(brain.transform.position,player.transform.position));
                if(i<7)yield return new WaitForSeconds(.3f);
            }
            var policy=Get(brain,"Policy");Assert.That(Get(policy,"State").ToString(),Is.EqualTo("InterceptAhead"));
            Action<string> row=phase=>csv+=$"{phase},{Get(policy,"State")},{Get(policy,"SelectedNode")},{Get(policy,"Confidence")},\"{Get(policy,"LastObserved")}\",\"{Get(policy,"Reason")}\"\n";
            row("visible_forward");yield return PredictionFrame(directory,"prediction-forward",policy);
            yield return new WaitForSeconds(.3f);
            cc.enabled=false;player.transform.position-=heading*.6f;cc.enabled=true;Call(player,"ApplyCameraPose");Physics.SyncTransforms();
            Assert.That((bool)Call(brain,"CanSeePlayer"),Is.True);Call(brain,"Decide",true,player.transform.position,4f);
            Assert.That(Get(policy,"SelectedNode"),Is.EqualTo(-1));row("observed_reversal");
            yield return PredictionFrame(directory,"prediction-reversal",policy);
            Vector3 last=(Vector3)Get(policy,"LastObserved");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary QA cover";
            Vector3 sight=player.transform.position-brain.transform.position;sight.y=0;
            wall.transform.position=(brain.transform.position+player.transform.position)*.5f+Vector3.up*1.5f;
            wall.transform.rotation=Quaternion.LookRotation(sight);wall.transform.localScale=new Vector3(8,4,.2f);Physics.SyncTransforms();
            Assert.That((bool)Call(brain,"CanSeePlayer"),Is.False);Call(brain,"Decide",false,Vector3.one*99999,100f);
            cc.enabled=false;player.transform.position=new Vector3(-10,0,-10);cc.enabled=true;Call(player,"ApplyCameraPose");Physics.SyncTransforms();
            yield return new WaitForSeconds(2.2f);Call(brain,"Decide",false,Vector3.one*-99999,100f);
            Assert.That((Vector3)Get(policy,"LastObserved"),Is.EqualTo(last));
            Assert.That(Get(policy,"State").ToString(),Is.EqualTo("SearchLastSeenArea"));row("cover_hidden_moved_search");
            yield return PredictionFrame(directory,"prediction-search",policy);
            Object.Destroy(wall);File.WriteAllText(Path.Combine(directory,"rendered-prediction.csv"),csv);
        }

        private IEnumerator PredictionFrame(string directory,string name,object policy)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)Assert.Ignore("Rendered evidence needs graphics");
            var objects=new List<GameObject>();var materials=new List<Material>();
            Action<Vector3[],Color> draw=(points,color)=>
            {
                if(points.Length<2)return;
                var go=new GameObject("Temporary measured route overlay");objects.Add(go);
                var line=go.AddComponent<LineRenderer>();var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetColor("_BaseColor",color);materials.Add(material);line.sharedMaterial=material;
                line.positionCount=points.Length;line.widthMultiplier=.09f;line.useWorldSpace=true;
                line.SetPositions(points.Select(v=>v+Vector3.up*.15f).ToArray());
            };
            var ring=((IEnumerable)Get(Get(brain,"TacticalNavigation"),"Points")).Cast<Vector3>().ToArray();
            draw(new[]{ring[0],ring[1],ring[2],ring[3]},Color.cyan);
            draw(new[]{ring[0],ring[5],ring[4],ring[3]},Color.magenta);
            draw((Vector3[])Get(policy,"PredictedTrajectory"),Color.yellow);
            draw((Vector3[])Get(policy,"PlannedRoute"),Color.green);
            var cameraObject=new GameObject("Temporary Control overview camera");objects.Add(cameraObject);
            var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=11;camera.nearClipPlane=.05f;
            camera.transform.position=new Vector3(10,2.8f,10);camera.transform.rotation=Quaternion.Euler(90,0,0);
            var render=new RenderTexture(960,960,24);render.Create();camera.targetTexture=render;
            yield return null;
            var previous=RenderTexture.active;RenderTexture.active=render;var pixels=new Texture2D(960,960,TextureFormat.RGB24,false);
            try
            {
                pixels.ReadPixels(new Rect(0,0,960,960),0,0);pixels.Apply();
                using var writer=new BinaryWriter(File.Create(Path.Combine(directory,name+".tga")));
                writer.Write(new byte[]{0,0,2,0,0,0,0,0,0,0,0,0});writer.Write((short)960);writer.Write((short)960);writer.Write((byte)24);writer.Write((byte)0);
                foreach(Color32 pixel in pixels.GetPixels32()){writer.Write(pixel.b);writer.Write(pixel.g);writer.Write(pixel.r);}
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;render.Release();Object.Destroy(render);Object.Destroy(pixels);
                foreach(var go in objects)Object.Destroy(go);foreach(var material in materials)Object.Destroy(material);
            }
        }
    }
}
#endif
