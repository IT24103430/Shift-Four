using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ShiftFour.Tests
{
    public sealed class CompactDroneGeometryEditModeTests
    {
        [TestCase("Scout")] [TestCase("Guard")] [TestCase("Interceptor")] [TestCase("Flanker")]
        public void CompactPrefab_MeshHitboxesAndFullRotorEnvelopeStayAligned(string name)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Agents/" + name + ".prefab");
            try
            {
                var visual = ((Component)root.GetComponentInChildren(Type.GetType("ShiftFour.DroneRotorVisuals, Assembly-CSharp", true), true)).transform;
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * .65f));
                var capsule = root.GetComponent<CapsuleCollider>();
                Assert.That(capsule.radius, Is.EqualTo(.325f).Within(.0001f));
                var body = new SerializedObject(root.GetComponent(Type.GetType("ShiftFour.DroneBody, Assembly-CSharp", true)));
                Vector3 extents = body.FindProperty("envelopeExtents").vector3Value;
                Vector3 center = body.FindProperty("envelopeCenter").vector3Value;
                float radius = 0f;
                var meshes = visual.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(meshes, Is.Not.Empty);
                foreach (var mesh in meshes)
                {
                    Assert.That(mesh.sharedMesh, Is.Not.Null);
                    var hitbox = mesh.GetComponent<MeshCollider>();
                    Assert.That(hitbox, Is.Not.Null, mesh.name);
                    Assert.That(hitbox.enabled && !hitbox.isTrigger, Is.True, mesh.name);
                    Assert.That(hitbox.sharedMesh, Is.SameAs(mesh.sharedMesh), mesh.name);
                    Vector3 rotorCenter = root.transform.InverseTransformPoint(mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center));
                    foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                    {
                        Vector3 point = root.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex));
                        float reach = new Vector2(point.x, point.z).magnitude;
                        if (mesh.name.StartsWith("Rotor"))
                            reach = new Vector2(rotorCenter.x, rotorCenter.z).magnitude + new Vector2(point.x - rotorCenter.x, point.z - rotorCenter.z).magnitude;
                        radius = Mathf.Max(radius, reach);
                        Assert.That(Mathf.Abs(point.y - center.y), Is.LessThanOrEqualTo(extents.y - .09f), mesh.name + " hover margin");
                    }
                }
                Assert.That(extents.x, Is.EqualTo(radius + .05f).Within(.0001f));
                Assert.That(extents.z, Is.EqualTo(extents.x));
                Assert.That(root.GetComponent<NavMeshAgent>().radius, Is.EqualTo(extents.x).Within(.0001f));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
