using System;
using System.IO;
using System.Linq;
using ShiftFour;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Targeted art refresh. Never calls baseline generation, layout migration or NavMesh baking.
public static class ShiftFourVisualUpgrade
{
    const string ScenePath = "Assets/Scenes/MainLab.unity";
    static readonly string[] Types = { "Scout", "Guard", "Interceptor", "Flanker" };
    static Material steel, recess, ivory, amber, cyan;
    [MenuItem("Tools/Shift Four/Refresh Visual Upgrade")]
    public static void Refresh()
    {
        if (!Application.isBatchMode && (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() ||
            !EditorUtility.DisplayDialog("Refresh visual assets", "Refresh only named door/drone art and Visual polish children in MainLab and section prefabs. Files are backed up under Library/VisualUpgradeBackup before the first refresh.", "Refresh", "Cancel"))) return;
        foreach (string type in Types)
            if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SF_" + type + "Drone_GV.fbx")) throw new Exception("Missing upgraded " + type);
        if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SF_HeavySecurityDoor_GV.fbx")) throw new Exception("Missing door FBX");
        Backup(ScenePath);
        ConfigureRendering();
        steel = Mat("Steel", new Color(.28f,.34f,.38f), .65f);
        recess = Mat("Recess", new Color(.045f,.06f,.075f), .2f);
        ivory = Mat("Ivory", new Color(.64f,.68f,.66f), .15f);
        amber = Mat("Amber", new Color(.8f,.46f,.13f), .15f);
        cyan = Mat("Practical", new Color(.62f,.78f,.83f), .1f, true);
        foreach (string type in Types)
        {
            string path = "Assets/Prefabs/Agents/" + type + ".prefab"; Backup(path);
            string originalYaml=File.ReadAllText(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Drone(root, type); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PreserveGameplayYaml(originalYaml,path);
        }
        foreach (string room in new[]{"Checkpoint","Server","Control","Storage"})
        {
            string path="Assets/Prefabs/Sections/"+room+".prefab"; Backup(path);
            var root=PrefabUtility.LoadPrefabContents(path);
            try { Room(root.transform,room); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Scene scene=SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene=EditorSceneManager.OpenScene(ScenePath);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var door in root.GetComponentsInChildren<LabDoor>(true))
            {
                string stamp=Stamp("Assets/Models/SF_HeavySecurityDoor_GV.fbx");
                if(door.transform.Find("Door assembly/"+stamp))continue;
                // Stage a complete new assembly before deleting the old one.
                var staging=new GameObject("Visual upgrade staging");
                var next=ShiftFourModelInstaller.AddDoorVisual(staging.transform);
                var old=door.transform.Find("Door assembly");
                Vector3 p=old ? old.localPosition : Vector3.zero;
                Quaternion q=old ? old.localRotation : Quaternion.identity;
                Vector3 s=old ? old.localScale : Vector3.one;
                if(old) Object.DestroyImmediate(old.gameObject);
                next.transform.SetParent(door.transform,false);
                next.transform.localPosition=p;next.transform.localRotation=q;next.transform.localScale=s;
                Object.DestroyImmediate(staging);
                Group(next.transform,stamp);
                GateSurround(door.transform);
            }
            foreach(var brain in root.GetComponentsInChildren<DroneBrain>(true))
            {
                string type=brain is ScoutBrain ? "Scout" : brain is GuardBrain ? "Guard" : brain is InterceptorBrain ? "Interceptor" : "Flanker";
                Drone(brain.gameObject,type);
            }
        }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("VISUAL UPGRADE: four gates, four fleet prefabs/scene instances, four section prefabs refreshed.");
    }
    internal static void PreserveGameplayYaml(string before,string path,bool keepCombatOrigin=false)
    {
        // Unity drops serialized fields absent from this branch's C# definition.
        // Preserve teammate/forward-compatible brain and Health data verbatim.
        string after=File.ReadAllText(path);
        foreach(string block in System.Text.RegularExpressions.Regex.Split(before,@"(?=^--- !u!)",System.Text.RegularExpressions.RegexOptions.Multiline))
        {
            var script=System.Text.RegularExpressions.Regex.Match(block,@"m_Script:.*guid: (\w+)");
            if(!script.Success)continue;
            string source=AssetDatabase.GUIDToAssetPath(script.Groups[1].Value);
            if(!source.StartsWith("Assets/Scripts/") || source.EndsWith("DroneMotor.cs") || source.EndsWith("DroneRotorVisuals.cs") || source.EndsWith("DroneBody.cs"))continue;
            string header=block.Split('\n')[0];
            string pattern=System.Text.RegularExpressions.Regex.Escape(header)+@"\n.*?(?=^--- !u!|\z)";
            string preserved=block;
            if(keepCombatOrigin)
            {
                var generated=System.Text.RegularExpressions.Regex.Match(after,pattern,System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline);
                var origin=System.Text.RegularExpressions.Regex.Match(generated.Value,@"^  combatOriginHeight:.*$",System.Text.RegularExpressions.RegexOptions.Multiline);
                if(origin.Success)
                {
                    if(System.Text.RegularExpressions.Regex.IsMatch(preserved,@"^  combatOriginHeight:.*$",System.Text.RegularExpressions.RegexOptions.Multiline))
                        preserved=System.Text.RegularExpressions.Regex.Replace(preserved,@"^  combatOriginHeight:.*$",_=>origin.Value,System.Text.RegularExpressions.RegexOptions.Multiline);
                    else preserved=preserved.TrimEnd('\r','\n')+"\n"+origin.Value+"\n";
                }
            }
            after=System.Text.RegularExpressions.Regex.Replace(after,pattern,_=>preserved,System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline);
        }
        if(after!=File.ReadAllText(path)){File.WriteAllText(path,after);AssetDatabase.ImportAsset(path);}
    }
    static void ConfigureRendering()
    {
        var pipeline=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/ShiftFour_URP.asset");
        if(!pipeline)throw new Exception("Expected existing ShiftFour URP asset");
        Backup("ProjectSettings/GraphicsSettings.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline=pipeline;
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Materials"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m.shader.name!="Standard")continue;
            Backup(path);Color color=m.color;
            float metallic=m.GetFloat("_Metallic"), smooth=m.GetFloat("_Glossiness");
            m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);
            m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);
        }
    }
    static void Backup(string path)
    {
        string dest="Library/VisualUpgradeBackup/"+path;
        if(File.Exists(dest))return;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(path,dest);
    }
    static string Stamp(string path) => "Visual upgrade v1 " + AssetDatabase.GetAssetDependencyHash(path);
    static void Drone(GameObject root,string type)
    {
        var previous=root.transform.Cast<Transform>().Where(t=>t.name=="Drone visual - "+type).ToArray();
        var old=previous.FirstOrDefault();
        string stamp=Stamp("Assets/Models/SF_"+type+"Drone_GV.fbx");
        if(previous.Length==1 && old.Find(stamp)) { BindDroneVisual(root,old); return; }
        var next=ShiftFourModelInstaller.AddDroneVisual(root.transform,type);
        if(!next)throw new Exception("Cannot stage "+type);
        if(old)
        {
            next.transform.localPosition=old.localPosition;
            next.transform.localRotation=old.localRotation;
            next.transform.localScale=old.localScale;
            foreach(var item in previous)Object.DestroyImmediate(item.gameObject);
        }
        BindDroneVisual(root,next.transform);
        Group(next.transform,stamp);
    }
    static void BindDroneVisual(GameObject root,Transform visual)
    {
        var motor=root.GetComponent<DroneMotor>();
        motor.Configure(visual);
        if(PrefabUtility.IsPartOfPrefabInstance(motor))
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
    }
    static Material Mat(string name,Color color,float metallic,bool emission=false)
    {
        const string folder="Assets/Materials/VisualUpgrade";
        if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Materials","VisualUpgrade");
        string path=folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m)return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.32f);
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.65f);}
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static Transform Group(Transform parent,string name)
    {
        var old=parent.Find(name);if(old)Object.DestroyImmediate(old.gameObject);
        var o=new GameObject(name);o.transform.SetParent(parent,false);return o.transform;
    }
    static void Box(Transform parent,string name,Vector3 p,Vector3 size,Material mat)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);
        o.transform.localPosition=p;o.transform.localScale=size;
        Object.DestroyImmediate(o.GetComponent<Collider>());o.GetComponent<Renderer>().sharedMaterial=mat;
        o.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    static void GateSurround(Transform parent)
    {
        var group=Group(parent,"Visual polish - gate");
        for(int side=-1;side<=1;side+=2)
        {
            Box(group,"Threshold edge",new Vector3(0,.012f,side*.46f),new Vector3(3.8f,.018f,.08f),steel);
            for(int i=0;i<6;i++) Box(group,"Approach warning",new Vector3(-1.55f+i*.62f,.014f,side*.65f),new Vector3(.27f,.018f,.16f),amber);
        }
    }
    static void Room(Transform parent,string room)
    {
        if(parent.Find("Visual polish/Revision 6"))return;
        var group=Group(parent,"Visual polish");
        Group(group,"Revision 6");
        foreach(float x in new[]{-5f,5f}) foreach(float z in new[]{-5f,5f})
            Box(group,"Ceiling cassette",new Vector3(x,3.48f,z),new Vector3(9.94f,.12f,9.94f),steel);
        if(room=="Control")
        {
            var emergency=Mat("Emergency",new Color(.6f,.10f,.065f),.1f,true);
            Box(group,"Emergency fixture",new Vector3(9.58f,2.6f,3),new Vector3(.08f,.15f,.65f),emergency);
        }
        // Room local floor spans +/-10m. Keep decoration overhead or flush to surfaces.
        for(int i=-1;i<=1;i++)
        {
            Box(group,"Ceiling service beam",new Vector3(i*6,3.31f,0),new Vector3(.18f,.16f,18.8f),steel);
            Box(group,"Cable tray",new Vector3(i*6+.23f,3.28f,0),new Vector3(.16f,.08f,18.8f),recess);
        }
        for(int seam=-2;seam<=2;seam++)
        {
            Box(group,"Floor panel seam",new Vector3(seam*3,.006f,0),new Vector3(.018f,.008f,18.8f),steel);
            Box(group,"Floor panel seam",new Vector3(0,.006f,seam*3),new Vector3(18.8f,.008f,.018f),steel);
        }
        for(int side=-1;side<=1;side+=2)
        {
            Box(group,"Upper wall fascia",new Vector3(0,3.27f,side*9.83f),new Vector3(20,.34f,.16f),steel);
            Box(group,"Upper wall fascia",new Vector3(side*9.83f,3.27f,0),new Vector3(.16f,.34f,20),steel);
            // Break perimeter trim around central door openings.
            foreach(float offset in new[]{-6f,6f})
            {
                Box(group,"Skirting",new Vector3(offset,.14f,side*9.73f),new Vector3(7.1f,.20f,.07f),steel);
                if(room=="Storage" && side==1 && offset<0)
                    Box(group,"Skirting",new Vector3(side*9.73f,.14f,-8.375f),new Vector3(.07f,.20f,2.35f),steel);
                else Box(group,"Skirting",new Vector3(side*9.73f,.14f,offset),new Vector3(.07f,.20f,7.1f),steel);
            }
            for(int i=-1;i<=1;i++)
            {
                if(i==0 || (room=="Storage" && side==1 && i==-1))continue; // central gates and offset Storage exit
                Box(group,"Wall pilaster",new Vector3(side*9.70f,1.7f,i*6),new Vector3(.09f,2.3f,.16f),steel);
                Box(group,"Wall service panel",new Vector3(side*9.64f,2.15f,i*6+.9f),new Vector3(.07f,.5f,1.2f),ivory);
                for(int vent=0;vent<4;vent++)Box(group,"Vent slit",new Vector3(side*9.59f,2.0f+vent*.09f,i*6+.9f),new Vector3(.02f,.025f,.9f),recess);
            }
            Box(group,"Floor route seam",new Vector3(side*8,.008f,0),new Vector3(.045f,.012f,18),steel);
            Box(group,"Practical housing",new Vector3(side*5,2.85f,0),new Vector3(2.8f,.15f,.65f),recess);
            Box(group,"Practical diffuser",new Vector3(side*5,2.76f,0),new Vector3(2.5f,.025f,.43f),cyan);
            var lamp=new GameObject("Neutral practical fill");lamp.transform.SetParent(group,false);lamp.transform.localPosition=new Vector3(side*5,2.55f,0);
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=12;light.intensity=1.5f;
            light.color=room=="Storage" ? new Color(1,.86f,.68f) : room=="Server" ? new Color(.73f,.89f,1) : new Color(.9f,.94f,1);
            light.shadows=LightShadows.None;
        }
        // Dress existing solid cover and consoles, preserving their colliders and silhouettes.
        foreach(Transform child in parent.Cast<Transform>().ToArray())
        {
            if(child==group || child.name=="Floor")continue;
            var renderer=child.GetComponent<Renderer>();if(!renderer)continue;
            var b=renderer.bounds;var local=parent.InverseTransformPoint(b.center);
            if(child.name.ToLowerInvariant().Contains("console"))
            {
                Box(group,"Console monitor hood",local+new Vector3(0,b.extents.y+.06f,0),new Vector3(b.size.x+.08f,.10f,b.size.z+.06f),steel);
                continue;
            }
            if(!child.name.Contains("cover") && !child.name.Contains("rack") && !child.name.Contains("Rack"))continue;
            float front=local.z-b.extents.z-.012f;
            Box(group,room+" equipment face",new Vector3(local.x,local.y,front),new Vector3(b.size.x*.83f,b.size.y*.7f,.025f),recess);
            for(int i=0;i<4;i++)Box(group,room=="Storage" ? "Cargo band" : "Equipment vent",new Vector3(local.x,local.y-b.extents.y*.45f+i*b.size.y*.14f,front-.02f),new Vector3(b.size.x*.72f,.045f,.025f),room=="Storage"?amber:steel);
        }
        // Room identity band remains above eye height and out of all routes.
        Box(group,room+" identity stripe",new Vector3(4.5f,2.75f,-9.65f),new Vector3(3,.12f,.06f),room=="Control"?amber:ivory);
    }
}
