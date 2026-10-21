using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShiftFour.Tests
{
    public sealed class TacticalArmsMaterialsEditModeTests
    {
        private const string Folder = "Assets/Materials/FPS/Arms/";
        private static readonly string[] Names = { "GloveShell", "SleeveRipstop", "CuffElastic", "GloveReinforcement", "KnuckleRubber" };
        private static Material Surface(string name) => AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat");

        [Test] public void AuthoredSurfaces_PreserveSupportedBaseAndNormalMaps()
        {
            foreach (string name in Names)
            {
                Material mat = Surface(name);
                Assert.That(mat, Is.Not.Null);
                Assert.That(mat.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(mat.shader.isSupported, Is.True);
                bool fabric = name == "SleeveRipstop" || name == "CuffElastic";
                string family = fabric ? "Ripstop" : "Leather";
                Assert.That(mat.GetTexture("_BaseMap"), Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + family + "Base.tga")));
                var normal = mat.GetTexture("_BumpMap");
                Assert.That(normal, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + family + "Normal.tga")));
                Assert.That(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal))).textureType, Is.EqualTo(TextureImporterType.NormalMap));
                Assert.That(mat.IsKeywordEnabled("_NORMALMAP"), Is.True);
                Assert.That(mat.GetFloat("_BumpScale"), Is.InRange(.05f, .25f));
            }
        }

        [Test] public void Smoothness_IsNonmetallicBoundedAndUsesTheMaskAlpha()
        {
            foreach (string name in Names)
            {
                Material mat = Surface(name);
                Assert.That(mat.GetFloat("_Metallic"), Is.Zero);
                Assert.That(mat.GetFloat("_Smoothness"), Is.InRange(.07f, .28f));
                if (name == "SleeveRipstop" || name == "CuffElastic") Assert.That(mat.GetFloat("_Smoothness"), Is.LessThan(.16f));
                var mask = mat.GetTexture("_MetallicGlossMap");
                Assert.That(mask, Is.Not.Null);
                Assert.That(mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
                Assert.That(mat.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"), Is.False);
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mask));
                Assert.That(importer.sRGBTexture, Is.False);
                Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
                byte[] raw = File.ReadAllBytes(AssetDatabase.GetAssetPath(mask));
                Assert.That(raw[2], Is.EqualTo(2)); Assert.That(raw[16], Is.EqualTo(32));
                byte min = 255, max = 0;
                for (int i = 18; i < raw.Length; i += 4)
                {
                    Assert.That(raw[i] | raw[i + 1] | raw[i + 2], Is.Zero, "Metallic RGB must stay zero.");
                    min = (byte)Mathf.Min(min, raw[i + 3]); max = (byte)Mathf.Max(max, raw[i + 3]);
                }
                Assert.That(min, Is.GreaterThanOrEqualTo(209));
                Assert.That(max, Is.EqualTo(255));
                Assert.That(min, Is.LessThan(max), "The supplied grain should modulate the smoothness cap.");
            }
        }

        [Test] public void Palette_DistinguishesDarkGlovesFabricAndElastic()
        {
            Color glove = Surface("GloveShell").GetColor("_BaseColor");
            Color sleeve = Surface("SleeveRipstop").GetColor("_BaseColor");
            Color cuff = Surface("CuffElastic").GetColor("_BaseColor");
            float Luma(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
            Assert.That(Luma(sleeve), Is.GreaterThan(Luma(glove) * 1.6f));
            Assert.That(Luma(cuff), Is.LessThan(Luma(glove) * .7f));
            Assert.That(sleeve.b, Is.GreaterThan(sleeve.r), "The dark fabric should fit the cool laboratory palette.");
            Assert.That(Surface("CuffElastic").GetFloat("_Smoothness"), Is.LessThan(Surface("GloveShell").GetFloat("_Smoothness")));
        }

        [Test] public void Prefab_AssignsTheFivePersistentAssetsAndHasTangentSpaceData()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Weapons/FirstPersonArmsRig.prefab");
            var skin = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
            CollectionAssert.AreEquivalent(Names, skin.sharedMaterials.Select(m => m.name));
            Assert.That(skin.sharedMaterials.All(AssetDatabase.Contains), Is.True);
            foreach (Material mat in skin.sharedMaterials) Assert.That(mat, Is.SameAs(Surface(mat.name)));
            Mesh mesh = skin.sharedMesh;
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.tangents.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv.All(p => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y)), Is.True);
            Assert.That(mesh.tangents.All(t => !float.IsNaN(t.x) && !float.IsNaN(t.y) && !float.IsNaN(t.z) && !float.IsNaN(t.w)), Is.True);
        }
    }
}
