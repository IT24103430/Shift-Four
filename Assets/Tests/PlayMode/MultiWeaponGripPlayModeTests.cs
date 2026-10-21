#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShiftFour.Tests
{
    public sealed class MultiWeaponGripPlayModeTests
    {
        private PistolGripPlayModeTests fixture;private Component player,mount;private GameObject preview;
        private static Type T(string n)=>Type.GetType("ShiftFour."+n+", Assembly-CSharp",true);
        private static object Get(object o,string n)=>o.GetType().GetProperty(n).GetValue(o);
        private static object Call(object o,string n,params object[]a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(o,a);
        private static string Evidence=>Environment.GetEnvironmentVariable("SHIFTFOUR_MULTI_GRIP_EVIDENCE")??Path.Combine(Application.temporaryCachePath,"MultiWeaponGrip");
        [UnitySetUp]public IEnumerator Setup()
        {
            fixture=new PistolGripPlayModeTests();yield return fixture.Setup();player=Object.FindAnyObjectByType(T("PlayerController"))as Component;
            mount=player.GetComponent(T("FirstPersonWeaponMount"));Time.timeScale=1;Call(mount,"RequestUnequip");
            while(Get(mount,"TransitionState").ToString()!="Hidden")yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(preview!=null)Object.Destroy(preview);yield return fixture.Cleanup();}
        [UnityTest]public IEnumerator MP5_UsesItsForeendProfileWithLocalSharedRigBindings(){yield return Check("HKMP5");}
        [UnityTest]public IEnumerator M16_UsesItsHandguardProfileWithLocalSharedRigBindings(){yield return Check("M16A1");}
        [UnityTest]public IEnumerator AK47_UsesItsHandguardProfileWithLocalSharedRigBindings(){yield return Check("AK47");}
        [UnityTest]public IEnumerator Barrett_UsesItsForeendProfileWithLocalSharedRigBindings(){yield return Check("BarrettM82A1");}
        private IEnumerator Check(string id)
        {
            var camera=(Camera)Get(player,"ViewCamera");var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FPS/GripPreviews/"+id+"GripPreview.prefab");
            preview=Object.Instantiate(prefab,camera.transform,false);yield return null;yield return null;
            var binding=preview.GetComponent(T("FirstPersonWeaponGripBinding"));Assert.That(Get(binding,"IsBound"),Is.EqualTo(true));
            var rig=preview.GetComponentInChildren(T("FirstPersonArmsRig"));var grip=rig.GetComponent(T("FirstPersonPistolGrip"));var skin=(SkinnedMeshRenderer)Get(rig,"Skin");
            var original=Resources.Load<GameObject>("Weapons/FirstPersonArmsRig").GetComponent(T("FirstPersonArmsRig"));var sourceSkin=(SkinnedMeshRenderer)Get(original,"Skin");
            Assert.That(skin.sharedMesh,Is.SameAs(sourceSkin.sharedMesh));CollectionAssert.AreEqual(sourceSkin.bones.Select(b=>b.localPosition),skin.bones.Select(b=>b.localPosition));
            CollectionAssert.AreEqual(sourceSkin.bones.Select(b=>b.localScale),skin.bones.Select(b=>b.localScale));
            Assert.That(preview.GetComponentsInChildren(T("FirstPersonArmsRig"),true).Length,Is.EqualTo(1));
            foreach(string name in new[]{"RightHandTarget","SupportHandTarget","RightElbowHint","SupportElbowHint"})Assert.That(((Transform)Get(grip,name)).parent,Is.SameAs(rig.transform));
            Assert.That(preview.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),Is.True);
            var targets=new[]{((Transform)Get(grip,"RightHandTarget")).localPosition,((Transform)Get(grip,"SupportHandTarget")).localPosition};
            var wrong=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/FPS/WeaponGrips/FNFiveSeven.asset");Assert.That(Call(binding,"TryApplyConfiguration",wrong),Is.EqualTo(false));
            CollectionAssert.AreEqual(targets,new[]{((Transform)Get(grip,"RightHandTarget")).localPosition,((Transform)Get(grip,"SupportHandTarget")).localPosition});
            float right=Vector3.Distance(((Transform)Get(rig,"RightHand")).position,((Transform)Get(grip,"RightHandTarget")).position);
            float left=Vector3.Distance(((Transform)Get(rig,"LeftHand")).position,((Transform)Get(grip,"SupportHandTarget")).position);
            string folder=Path.Combine(Evidence,id);Directory.CreateDirectory(folder);
            if(!Application.isBatchMode)
            {
                var viewType=TypeCache.GetTypesDerivedFrom<EditorWindow>().First(t=>t.FullName=="UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);view.Show();view.Focus();
                for(int frame=0;frame<8;frame++){view.Repaint();yield return null;}
                string saved=Environment.GetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE");
                try{Environment.SetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE",folder);typeof(FirstPersonReloadPlayModeTests).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{view,"draft-preview-game-view"});}
                finally{Environment.SetEnvironmentVariable("SHIFTFOUR_RELOAD_EVIDENCE",saved);}
            }
File.WriteAllText(Path.Combine(folder,"binding-and-reach.txt"),"Prepared preview, not playable. Right target distance(m)="+right+", support="+left+". Shared mesh/bone offsets/scales retained; mismatch rejected. Finger-surface acceptance is reported by separate baked-geometry audit.\n");
            var fields=new[]{"model","pivot","skin"};var values=new object[]{binding,(Transform)Get(binding,"PosePivot"),skin};var previous=fields.Select(n=>typeof(PistolGripPlayModeTests).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(fixture)).ToArray();
            string old=Environment.GetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE");
            try
            {
                for(int i=0;i<fields.Length;i++)typeof(PistolGripPlayModeTests).GetField(fields[i],BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fixture,values[i]);
                Environment.SetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE",folder);Call(fixture,"ExportGeometry");
            }
            finally{for(int i=0;i<fields.Length;i++)typeof(PistolGripPlayModeTests).GetField(fields[i],BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fixture,previous[i]);Environment.SetEnvironmentVariable("SHIFTFOUR_GRIP_EVIDENCE",old);}
        }
        [UnityTest]public IEnumerator PistolBinding_RejectsAnUnrelatedProfileWithoutReassigningHands()
        {
            Call(mount,"RequestEquip");yield return PlayerWeaponTestActions.WaitForReady(player);
            var model=(Component)Get(mount,"Instance");var binding=model.GetComponent(T("FirstPersonWeaponGripBinding"));Assert.That(Get(binding,"IsBound"),Is.EqualTo(true));
            var before=Get(binding,"Configuration");var wrong=AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/FPS/WeaponGrips/HKMP5.asset");
            Assert.That(Call(binding,"TryApplyConfiguration",wrong),Is.EqualTo(false));Assert.That(Get(binding,"Configuration"),Is.SameAs(before));
            Assert.That(player.GetComponentsInChildren(T("FirstPersonViewmodel"),true).Length,Is.EqualTo(1));
        }
    }
}
#endif
