#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShiftFour.Tests
{
    public sealed class FirstPersonViewmodelFramingPlayModeTests
    {
        private PistolGripPlayModeTests fixture;
        private Component player,mount,model,presenter,rig,grip;
        private Camera camera; private GameObject wall;
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
        private static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
        private static string Evidence=>Environment.GetEnvironmentVariable("SHIFTFOUR_FRAMING_EVIDENCE")??Path.Combine(Application.temporaryCachePath,"ViewmodelFraming");
        [UnitySetUp] public IEnumerator Setup()
        {
            fixture=new PistolGripPlayModeTests();yield return fixture.Setup();
            player=Object.FindAnyObjectByType(T("PlayerController"))as Component;mount=player.GetComponent(T("FirstPersonWeaponMount"));
            model=(Component)Get(mount,"Instance");presenter=model.GetComponent(T("PistolPresentation"));rig=model.GetComponentInChildren(T("FirstPersonArmsRig"));grip=rig.GetComponent(T("FirstPersonPistolGrip"));
            camera=(Camera)Get(player,"ViewCamera");Directory.CreateDirectory(Evidence);
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(wall!=null)Object.Destroy(wall);yield return fixture.Cleanup();}
        private void Attached()
        {
            foreach(var pair in new[]{("RightHand","RightHandTarget"),("LeftHand","SupportHandTarget")})
                Assert.That(Vector3.Distance(((Transform)Get(rig,pair.Item1)).position,((Transform)Get(grip,pair.Item2)).position),Is.LessThan(.0002f));
        }
        [UnityTest] public IEnumerator DesktopAspectsAndExtremePitchYaw_KeepWristsFramedWithoutChangingAimOrCapsule()
        {
            var capsule=player.GetComponent<CharacterController>();float height=capsule.height,radius=capsule.radius,near=camera.nearClipPlane;
            int cameras=Object.FindObjectsByType<Camera>().Length;Vector3 eye=camera.transform.localPosition;
            float originalAspect=camera.aspect;Quaternion root=player.transform.rotation,view=camera.transform.localRotation;
            var trace=new System.Text.StringBuilder("aspect,yaw,pitch,right_x,right_y,right_depth,left_x,left_y,left_depth\n");
            try
            {
                foreach(float aspect in new[]{16f/9,16f/10,4f/3,21f/9})foreach(float yaw in new[]{-135f,0,135f})foreach(float pitch in new[]{-80f,0,80f})
                {
                    camera.aspect=aspect;player.transform.rotation=Quaternion.Euler(0,yaw,0);camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
                    Vector3 aim=camera.transform.forward;yield return null;Attached();
                    var r=camera.WorldToViewportPoint(((Transform)Get(rig,"RightHand")).position);var l=camera.WorldToViewportPoint(((Transform)Get(rig,"LeftHand")).position);
                    foreach(var p in new[]{r,l}){Assert.That(p.x,Is.InRange(.02f,.98f));Assert.That(p.y,Is.InRange(.02f,.98f));Assert.That(p.z,Is.GreaterThan(near+.01f));}
                    Assert.That(Vector3.Angle(aim,camera.transform.forward),Is.LessThan(.001f));Assert.That(camera.transform.localPosition,Is.EqualTo(eye));
                    Assert.That(capsule.height,Is.EqualTo(height));Assert.That(capsule.radius,Is.EqualTo(radius));Assert.That(camera.nearClipPlane,Is.EqualTo(near));
                    trace.AppendLine(FormattableString.Invariant($"{aspect},{yaw},{pitch},{r.x},{r.y},{r.z},{l.x},{l.y},{l.z}"));
                }
                Assert.That(Object.FindObjectsByType<Camera>().Length,Is.EqualTo(cameras));
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials){Assert.That(material,Is.Not.Null);Assert.That(material.shader.name,Does.Not.Contain("InternalErrorShader"));}
                File.WriteAllText(Path.Combine(Evidence,"aspects-pitch-yaw.csv"),trace.ToString());
            }
            finally{camera.aspect=originalAspect;player.transform.rotation=root;camera.transform.localRotation=view;}
        }
        [UnityTest] public IEnumerator CloseCover_HighReadyKeepsWristsBeyondNearPlaneAndMuzzleBehindWallDuringRecoil()
        {
            // Isolated real physics wall, preserving the actual MainLab camera and controller.
            player.GetComponent<CharacterController>().enabled=false;player.transform.SetPositionAndRotation(new Vector3(100,0,100),Quaternion.identity);player.GetComponent<CharacterController>().enabled=true;
            camera.transform.localRotation=Quaternion.identity;
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Framing clearance wall";
            wall.transform.position=camera.transform.position+Vector3.forward*.40f;wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();
            yield return null;yield return null;
            Assert.That((float)Get(mount,"PresentationRetreat"),Is.EqualTo(.18f).Within(.00001f));
            var aim=camera.transform.forward;var eye=camera.transform.position;
            Assert.That(Call(player.GetComponent(T("WeaponController")),"TryFire"),Is.EqualTo(true));
            float minimum=float.PositiveInfinity;float oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/120;Time.timeScale=1;
            try
            {
                for(int i=0;i<100;i++)
                {
                    yield return null;Attached();
                    foreach(string hand in new[]{"RightHand","LeftHand"}){float depth=camera.transform.InverseTransformPoint(((Transform)Get(rig,hand)).position).z;minimum=Mathf.Min(minimum,depth);Assert.That(depth,Is.GreaterThan(camera.nearClipPlane+.01f));var screen=camera.WorldToViewportPoint(((Transform)Get(rig,hand)).position);if(i<2)TestContext.Out.WriteLine("Cover diagnostic "+hand+" screen="+screen+" cameraLocal="+camera.transform.InverseTransformPoint(((Transform)Get(rig,hand)).position)+" pivot="+((Transform)Get(presenter,"RecoilPivot")).localPosition+" angles="+((Transform)Get(presenter,"RecoilPivot")).localEulerAngles);Assert.That(screen.x,Is.InRange(.03f,.97f));Assert.That(screen.y,Is.InRange(.03f,.97f));}
                    var muzzle=(Transform)Get(presenter,"MuzzlePoint");Assert.That(muzzle.position.z,Is.LessThan(wall.transform.position.z-.05f));
                    Assert.That(camera.transform.position,Is.EqualTo(eye));Assert.That(Vector3.Angle(aim,camera.transform.forward),Is.LessThan(.001f));
                }
                File.WriteAllText(Path.Combine(Evidence,"close-cover.txt"),"Single normal world camera; retreat 0.18m, high-ready 50deg, leftward shift 0.06m. Minimum measured wrist depth="+minimum+"m; near plane="+camera.nearClipPlane+"m. Muzzle stays behind 0.35m wall face through accepted-shot recoil. No camera/capsule edits.\n");
                Object.Destroy(wall);wall=null;Physics.SyncTransforms();
                for(int i=0;i<120;i++)yield return null;
                Assert.That((float)Get(mount,"PresentationRetreat"),Is.Zero);
            }
            finally{Time.captureDeltaTime=oldCapture;}
        }
        [UnityTest] public IEnumerator CloseCoverReload_RecordsActualSkinnedNearPlaneSegmentsAtAllDesktopAspects()
        {
            var capsule=player.GetComponent<CharacterController>();capsule.enabled=false;player.transform.SetPositionAndRotation(new Vector3(100,0,100),Quaternion.identity);capsule.enabled=true;camera.transform.localRotation=Quaternion.identity;
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=camera.transform.position+Vector3.forward*.40f;wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();yield return null;yield return null;
            var weapons=player.GetComponent(T("WeaponController"));Assert.That(Call(weapons,"TryFire"),Is.EqualTo(true));Assert.That(Call(weapons,"RequestReload"),Is.EqualTo(true));
            var skin=(SkinnedMeshRenderer)Get(rig,"Skin");var mesh=new Mesh();int[] triangles=skin.sharedMesh.triangles;float oldStep=Time.captureDeltaTime,oldAspect=camera.aspect;Time.captureDeltaTime=1f/60;Time.timeScale=1;
            var trace=new System.Text.StringBuilder("frame,progress,aspect,visible_near_plane_segments\n");int maximum=0;
            try
            {
                for(int frame=0;frame<110;frame++)
                {
                    yield return null;skin.BakeMesh(mesh,true);var vertices=mesh.vertices.Select(v=>camera.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
                    foreach(float aspect in new[]{4f/3,16f/10,16f/9,21f/9})
                    {
                        camera.aspect=aspect;float near=camera.nearClipPlane,y=near*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f),x=y*aspect;int count=0;
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                            if((a.z<near)==(b.z<near)&&(b.z<near)==(c.z<near))continue;
                            Vector3 one=default,two=default;int found=0;
                            Segment(a,b);Segment(b,c);Segment(c,a);
                            if(found==2&&!((one.x< -x&&two.x< -x)||(one.x>x&&two.x>x)||(one.y< -y&&two.y< -y)||(one.y>y&&two.y>y)))count++;
                            void Segment(Vector3 v,Vector3 w){if((v.z<near)==(w.z<near))return;var point=Vector3.Lerp(v,w,(near-v.z)/(w.z-v.z));if(found++==0)one=point;else two=point;}
                        }
                        maximum=Mathf.Max(maximum,count);trace.AppendLine(FormattableString.Invariant($"{frame},{Get(model,"ReloadProgress")},{aspect},{count}"));
                    }
                }
                File.WriteAllText(Path.Combine(Evidence,"close-cover-reload-near-plane.csv"),trace.ToString());
                Assert.That(maximum,Is.Zero,"A skinned triangle intersects the near plane inside the viewport; inspect captured framing before acceptance.");
            }
            finally{camera.aspect=oldAspect;Time.captureDeltaTime=oldStep;Object.Destroy(mesh);}
        }
        [UnityTest] public IEnumerator GameViewEvidence_CapturesFourActualDesktopResolutionsAndCloseCover()
        {
            if(Application.isBatchMode)Assert.Ignore("Actual Game-view resolution evidence requires the graphical Editor run.");
            Type type=TypeCache.GetTypesDerivedFrom<EditorWindow>().First(t=>t.FullName=="UnityEditor.GameView");var view=EditorWindow.GetWindow(type);view.Show();view.Focus();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var sizesType=type.Assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Static|BindingFlags.Public);var sizes=singleton.GetValue(null);
            var groupType=type.Assembly.GetType("UnityEditor.GameViewSizeGroupType");var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var sizeType=type.Assembly.GetType("UnityEditor.GameViewSize");var sizeKind=type.Assembly.GetType("UnityEditor.GameViewSizeType");var selected=type.GetProperty("selectedSizeIndex",flags);int previous=(int)selected.GetValue(view);
            try
            {
                foreach(var resolution in new[]{(1280,720),(1280,800),(1024,768),(1680,720)})
                {
                    string name=resolution.Item1+"x"+resolution.Item2;int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
                    var size=Activator.CreateInstance(sizeType,new[]{Enum.Parse(sizeKind,"FixedResolution"),(object)resolution.Item1,resolution.Item2,"FPS QA "+name});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});selected.SetValue(view,count);
                    for(int i=0;i<12;i++){view.Repaint();yield return null;}
                    Capture(view,"idle-"+name);
                    var surface=(RenderTexture)type.GetField("m_RenderTexture",flags).GetValue(view);Assert.That(surface.width,Is.EqualTo(resolution.Item1));Assert.That(surface.height,Is.EqualTo(resolution.Item2));
                    // Actual editor resolution selection drives camera projection, not PNG resizing.
                    foreach(string hand in new[]{"RightHand","LeftHand"}){var p=camera.WorldToViewportPoint(((Transform)Get(rig,hand)).position);Assert.That(p.x,Is.InRange(0f,1f));Assert.That(p.y,Is.InRange(0f,1f));}
                }
                camera.transform.localRotation=Quaternion.Euler(-80,0,0);for(int i=0;i<6;i++)yield return null;Capture(view,"looking-up");
                camera.transform.localRotation=Quaternion.Euler(80,0,0);for(int i=0;i<6;i++)yield return null;Capture(view,"looking-down");
                camera.transform.localRotation=Quaternion.identity;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=camera.transform.position+camera.transform.forward*.40f;wall.transform.rotation=camera.transform.rotation;wall.transform.localScale=new Vector3(3,3,.1f);Physics.SyncTransforms();
                for(int i=0;i<8;i++)yield return null;Capture(view,"close-cover-high-ready");
            }
            finally{selected.SetValue(view,previous);}
        }
        private static void Capture(EditorWindow view,string name)
        {
            string old=Environment.GetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE");
            try{Environment.SetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE",Evidence);typeof(FirstPersonReloadPlayModeTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,name});}
            finally{Environment.SetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE",old);}
        }
    }
}
#endif
