#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShiftFour.Tests
{
    public sealed class GVFinishPlayModeTests
    {
        private Component player, session;
        private Camera camera;
        private CursorLockMode cursor;
        private bool visible;
        private float capture, timeScale;
        private GameObject wall;
        private static Type T(string name) => Type.GetType("ShiftFour." + name + ", Assembly-CSharp", true);
        private static Component Find(string name) => Object.FindAnyObjectByType(T(name)) as Component;
        private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        [UnitySetUp] public IEnumerator Setup()
        {
            cursor = Cursor.lockState; visible = Cursor.visible; capture = Time.captureDeltaTime; timeScale = Time.timeScale;
            Time.captureDeltaTime = 0; Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainLab.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            player = Find("PlayerController"); session = Find("GameSession"); camera = (Camera)Get(player, "ViewCamera");
            foreach (Component brain in Object.FindObjectsByType(T("DroneBrain")))
            {
                ((Behaviour)brain).enabled = false; ((Behaviour)brain.GetComponent(T("DroneMotor"))).enabled = false;
                var agent = brain.GetComponent<NavMeshAgent>(); if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            }
            Cursor.lockState = CursorLockMode.None;
            yield return PlayerWeaponTestActions.WaitForReady(player);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (wall != null) Object.Destroy(wall);
            Time.timeScale = timeScale; Time.captureDeltaTime = capture;
            var old = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(SceneManager.CreateScene("After GV fixture " + Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
            Cursor.lockState = cursor; Cursor.visible = visible;
        }

        [UnityTest] public IEnumerator CloseWall_RetractsOnlyViewmodel_AndRestoresItAfterMovingAway()
        {
            var capsule = player.GetComponent<CharacterController>(); capsule.enabled = false;
            player.transform.position = new Vector3(500, 0, 500); capsule.enabled = true;
            Call(player, "ApplyCameraPose"); camera.transform.rotation = Quaternion.identity;
            Vector3 eye = camera.transform.position;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = eye + Vector3.forward * .38f; wall.transform.localScale = new Vector3(3, 3, .04f);
            Physics.SyncTransforms(); yield return null; yield return null;
            var mount = player.GetComponent(T("FirstPersonWeaponMount"));
            Assert.That((float)Get(mount, "PresentationRetreat"), Is.GreaterThan(.1f));
            var model = (Component)Get(mount, "Instance");
            var muzzle = (Transform)Get(model.GetComponent(T("PistolPresentation")), "MuzzlePoint");
            Assert.That(muzzle.position.z, Is.LessThan(wall.GetComponent<Collider>().bounds.min.z));
            Assert.That(camera.transform.position, Is.EqualTo(eye));
            Object.Destroy(wall); yield return null; yield return new WaitForSeconds(.35f);
            Assert.That((float)Get(mount, "PresentationRetreat"), Is.EqualTo(0).Within(.0001f));
        }

        [UnityTest] public IEnumerator LiveStartup_HasSkinnedArmsAndNoPrematureDeathPose()
        {
            var death = player.GetComponent(T("PlayerDeathPresentation"));
            Assert.That(Get(death, "IsActive"), Is.False);
            Assert.That(camera.transform.position.y - player.transform.position.y, Is.InRange(1.2f, 1.4f));
            Assert.That(camera.transform.localEulerAngles.z, Is.EqualTo(0).Within(.01f));
            var arms = player.GetComponentInChildren(T("FirstPersonArms"), true) as Component;
            Assert.That(arms, Is.Not.Null); Assert.That(Get(arms, "IsReady"), Is.True);
            var skin = arms.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skin.sharedMesh.vertexCount, Is.GreaterThan(1000));
            Assert.That(skin.bones.Length, Is.GreaterThan(30));
            Assert.That(arms.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
            var pivot = (Transform)Get(arms.GetComponent(T("PistolPresentation")), "RecoilPivot");
            Directory.CreateDirectory("Logs/GVFinish");
            File.WriteAllText("Logs/GVFinish/arms-bindings.txt", string.Join("\n", arms.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "hand.R" || t.name == "hand.L").Select(t => t.name + " " + pivot.InverseTransformPoint(t.position).ToString("F4"))));
            var authored = Resources.Load<GameObject>("Weapons/FirstPersonArmsRig");
            Assert.That(authored, Is.Not.Null);
            foreach (var hand in arms.GetComponentsInChildren<Transform>(true).Where(t => t.name == "hand.R" || t.name == "hand.L"))
            {
                var reference = authored.GetComponentsInChildren<Transform>(true).Single(t => t.name == hand.name);
                Assert.That(Vector3.Distance(pivot.InverseTransformPoint(hand.position), authored.transform.InverseTransformPoint(reference.position)),
                    Is.LessThan(.0002f), "Wrist must retain the fitted rig's authored grip.");
            }
            yield return new WaitForSeconds(.1f);
            Assert.That(Get(death, "IsActive"), Is.False);
        }

        [UnityTest] public IEnumerator Death_LowersCameraInUnscaledTime_WithoutMovingPhysicsRoot_AndRestartClearsIt()
        {
            var weapons = Get(player, "Weapons");
            Assert.That(Call(weapons, "TryFire"), Is.True); Assert.That(Call(weapons, "RequestReload"), Is.True);
            Vector3 root = player.transform.position, eye = camera.transform.position;
            Quaternion rotation = camera.transform.localRotation;
            Call(player.GetComponent(T("Health")), "TakeDamage", 10000f);
            Assert.That(Get(session, "Ended"), Is.True); Assert.That(Get(player, "CanAct"), Is.False);
            Assert.That(Call(weapons, "TryFire"), Is.False);
            Assert.That(Get(Get(weapons, "ActiveWeapon"), "ReloadState").ToString(), Is.EqualTo("Idle"));
            Time.timeScale = 0; yield return new WaitForSecondsRealtime(.8f);
            Assert.That(player.transform.position, Is.EqualTo(root));
            Assert.That(eye.y - camera.transform.position.y, Is.InRange(.4f, .65f));
            Assert.That(Quaternion.Angle(rotation, camera.transform.localRotation), Is.InRange(10f, 25f));
            Assert.That(Get(player.GetComponent(T("PlayerDeathPresentation")), "Progress"), Is.EqualTo(1f));
            Call(session, "Restart"); yield return null; yield return null;
            player = Find("PlayerController"); session = Find("GameSession"); camera = (Camera)Get(player, "ViewCamera");
            Cursor.lockState = CursorLockMode.None;
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Get(player.GetComponent(T("PlayerDeathPresentation")), "IsActive"), Is.False);
            Assert.That(Get(session, "Ended"), Is.False);
            Assert.That(Get(Get(Get(player, "Weapons"), "ActiveWeapon"), "RoundsInMagazine"), Is.EqualTo(12));
            Assert.That(player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SupportForearm_FollowsReloadWithoutStretchingBoneLength_AndCancelsCleanly()
        {
            var arms = player.GetComponentInChildren(T("FirstPersonArms"), true) as Component;
            var transforms = arms.GetComponentsInChildren<Transform>(true);
            var elbow = transforms.Single(t => t.name == "forearm.L");
            var wrist = transforms.Single(t => t.name == "hand.L");
            Vector3 rest = elbow.localPosition, worldRest = elbow.position;
            float length = Vector3.Distance(elbow.position, wrist.position);
            var weapons = Get(player, "Weapons"); Call(weapons, "TryFire");
            Assert.That(Call(weapons, "RequestReload"), Is.True);
            yield return new WaitForSeconds(.7f);
            Assert.That(Vector3.Distance(elbow.position, worldRest), Is.GreaterThan(.01f));
            Assert.That(Vector3.Distance(elbow.localPosition, rest), Is.LessThan(.000001f), "Reload must solve the arm with rotations rather than moving the elbow joint.");
            Assert.That(Vector3.Distance(elbow.position, wrist.position), Is.EqualTo(length).Within(.001f));
            Call(weapons, "CancelReload"); yield return null;
            Assert.That(Vector3.Distance(elbow.localPosition, rest), Is.LessThan(.0001f));
        }

        [UnityTest] public IEnumerator Storage_AuditAuthoredPostAndTwoSideRoutes()
        {
            var flanker = Find("FlankerBrain"); var body = flanker.GetComponent(T("DroneBody"));
            Vector3 home = (Vector3)Get(flanker, "Home");
            string report = "Home=" + home + ",clear=" + Call(body, "CanOccupy", home) + "\n";
            for (int radius = 1; radius <= 5; radius++) for (int x = -radius; x <= radius; x++) for (int z = -radius; z <= radius; z++)
            {
                if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius) continue;
                Vector3 candidate = home + new Vector3(x, 0, z);
                if ((bool)Call(body, "CanOccupy", candidate) && NavMesh.SamplePosition(candidate, out var hit, .15f, NavMesh.AllAreas))
                    report += "Clear post=" + hit.position.ToString("F2") + "\n";
            }
            Directory.CreateDirectory("Logs/GVFinish"); File.WriteAllText("Logs/GVFinish/storage-clearance.txt", report);
            Assert.That((bool)Call(body, "CanOccupy", home), Is.True, "Authored Storage post must fit the complete drone body.");
            Assert.That(Path(new Vector3(10, 0, -17), new Vector3(4, 0, -10)), Is.True);
            Assert.That(Path(new Vector3(4, 0, -10), new Vector3(10, 0, -3)), Is.True);
            Assert.That(Path(new Vector3(10, 0, -17), new Vector3(16, 0, -10)), Is.True);
            Assert.That(Path(new Vector3(16, 0, -10), new Vector3(10, 0, -3)), Is.True);
            var motor = flanker.GetComponent(T("DroneMotor"));
            ((Behaviour)motor).enabled = true; flanker.GetComponent<NavMeshAgent>().isStopped = false;
            float until = Time.time + 8f;
            while (Vector3.Distance(flanker.transform.position, home) > .7f && Time.time < until)
            { Call(motor, "GoTo", home); yield return null; }
            Assert.That(Vector3.Distance(flanker.transform.position, home), Is.LessThan(.7f), "Real motor must reach the corrected Storage post.");
            Assert.That((bool)Call(body, "CanOccupy", flanker.transform.position), Is.True);
        }
        private static bool Path(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out var a, 1, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 1, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath(); return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
#endif
