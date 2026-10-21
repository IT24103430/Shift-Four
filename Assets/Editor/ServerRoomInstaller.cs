using UnityEditor;
using UnityEngine;

// Owns Server cosmetics only. Existing cover colliders and console stay in place.
public static class ServerRoomInstaller
{
    private const string RackPath = "Assets/Prefabs/Sections/ServerRack.prefab";
    [MenuItem("Tools/Shift Four/Polish Server Room")]
    public static void Install()
    {
        GameObject rack = AssetDatabase.LoadAssetAtPath<GameObject>(RackPath);
        if (rack == null) rack = BuildRack();
        const string path = "Assets/Prefabs/Sections/Server.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith("Fixed cover")) continue;
                if (child.Find("Server rack visual") != null) continue;
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(rack);
                visual.name = "Server rack visual";
                visual.transform.SetParent(child, false);
                // Rack mesh is 2 x 2.2 x 1.5m, matching original solid cover exactly.
                visual.transform.localScale = new Vector3(1f / child.localScale.x, 1f / child.localScale.y, 1f / child.localScale.z);
                var old = child.GetComponent<MeshRenderer>();
                if (old != null) old.enabled = false;
            }
            if (root.transform.Find("Server finish") == null)
            {
                GameObject finish = new GameObject("Server finish");
                finish.transform.SetParent(root.transform, false);
                var steel = Material("Server_Steel", new Color(.12f, .16f, .19f));
                var trim = Material("Server_Trim", new Color(.30f, .35f, .38f));
                var cyan = Material("Server_Cyan", new Color(.08f, .65f, .75f), true);
                Cube("Ceiling", finish.transform, new Vector3(0f, 3.3f, 0f), new Vector3(19.8f, .25f, 19.8f), steel, true);
                for (int i = -1; i <= 1; i++)
                {
                    Cube("Ceiling beam " + i, finish.transform, new Vector3(i * 6f, 3.08f, 0f), new Vector3(.18f, .18f, 19.5f), trim);
                    Cube("Light strip " + i, finish.transform, new Vector3(i * 6f, 2.97f, 0f), new Vector3(.12f, .04f, 8f), cyan);
                    GameObject lamp = new GameObject("Server fill " + i);
                    lamp.transform.SetParent(finish.transform, false);
                    lamp.transform.localPosition = new Vector3(i * 6f, 2.7f, 0f);
                    Light light = lamp.AddComponent<Light>();
                    light.type = LightType.Point; light.color = new Color(.58f, .77f, .85f);
                    light.range = 9f; light.intensity = 1.1f; light.shadows = LightShadows.None;
                }
                // Broken conduit/maintenance strips suggest abandonment without cluttering routes.
                for (int i = 0; i < 6; i++)
                    Cube("Maintenance floor marking " + i, finish.transform, new Vector3(-6f + i * 2f, .006f, -5f), new Vector3(1f, .01f, .07f), trim);
                Cube("North service band", finish.transform, new Vector3(0f, 2.5f, 9.65f), new Vector3(19f, .4f, .08f), steel);
                Cube("West service band", finish.transform, new Vector3(-9.65f, 2.5f, 0f), new Vector3(.08f, .4f, 19f), steel);
                foreach (Vector3 position in new[] { new Vector3(-4f, 0f, 2f), new Vector3(4f, 0f, 2f) })
                {
                    GameObject cover = new GameObject("Guard cover destination");
                    cover.transform.SetParent(finish.transform, false); cover.transform.localPosition = position;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    private static GameObject BuildRack()
    {
        GameObject root = new GameObject("ServerRack");
        var steel = Material("Server_Steel", new Color(.12f, .16f, .19f));
        var trim = Material("Server_Trim", new Color(.30f, .35f, .38f));
        var black = Material("Server_Vent", new Color(.025f, .035f, .04f));
        var cyan = Material("Server_Cyan", new Color(.08f, .65f, .75f), true);
        Cube("Enclosure", root.transform, Vector3.zero, new Vector3(2f, 2.2f, 1.5f), steel);
        for (int i = 0; i < 7; i++)
        {
            float y = -.85f + i * .28f;
            Cube("Blade " + i, root.transform, new Vector3(0f, y, -.76f), new Vector3(1.72f, .22f, .035f), trim);
            Cube("Vent " + i, root.transform, new Vector3(-.14f, y, -.785f), new Vector3(1.22f, .12f, .025f), black);
            Cube("Status " + i, root.transform, new Vector3(.72f, y, -.805f), new Vector3(.06f, .06f, .025f), cyan);
        }
        // Combine decorative geometry by material: four renderer draws per reusable rack.
        foreach (Material material in new[] { steel, trim, black, cyan })
        {
            var combines = new System.Collections.Generic.List<CombineInstance>();
            var obsolete = new System.Collections.Generic.List<GameObject>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.GetComponent<Renderer>().sharedMaterial != material) continue;
                combines.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                obsolete.Add(filter.gameObject);
            }
            Mesh mesh = new Mesh { name = "Rack_" + material.name };
            mesh.CombineMeshes(combines.ToArray());
            string meshPath = "Assets/Models/" + mesh.name + ".asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            foreach (GameObject old in obsolete) Object.DestroyImmediate(old);
            GameObject part = new GameObject(material.name);
            part.transform.SetParent(root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RackPath);
        Object.DestroyImmediate(root);
        return prefab;
    }
    private static void Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool solid = false)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name; obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position; obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Object.DestroyImmediate(obj.GetComponent<Collider>());
    }
    private static Material Material(string name, Color color, bool emission = false)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        material.SetColor("_BaseColor", color);
        if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.5f); }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
