using UnityEngine;

namespace ShiftFour
{
    public enum FirstPersonWeaponClass { Pistol, SubmachineGun, Rifle, SniperRifle }
    public enum FirstPersonReloadPresentation { NonePrepared, FiveSevenProcedural, AuthoredClip }

    [CreateAssetMenu(menuName = "Shift Four/First Person/Weapon Grip Configuration")]
    public sealed class FirstPersonWeaponGripConfiguration : ScriptableObject
    {
        [SerializeField] private string weaponId;
        [SerializeField] private FirstPersonWeaponClass weaponClass;
        [SerializeField] private GameObject sourceModel;
        [SerializeField] private Mesh bodyMesh;
        [SerializeField] private Vector3 modelPosition;
        [SerializeField] private Vector3 modelEuler;
        [SerializeField] private Vector3 armsRootPosition;
        [SerializeField] private Vector3 rightWristPosition, leftWristPosition, rightElbowHint, leftElbowHint;
        [SerializeField] private Quaternion rightWristRotation = Quaternion.identity, leftWristRotation = Quaternion.identity;
        [SerializeField] private Vector3 dominantGripCenter, supportGripCenter;
        [SerializeField, Range(0, 80)] private float forearmPronation;
        [SerializeField, Range(6, 24)] private float breathsPerMinute = 12;
        [SerializeField, Range(0, .003f)] private float idleAmplitude = .0008f;
        [SerializeField] private AnimationClip idleClip, reloadClip;
        [SerializeField] private FirstPersonReloadPresentation reloadPresentation;
        [SerializeField, TextArea] private string validationNotes;
        public string WeaponId => weaponId;
        public FirstPersonWeaponClass WeaponClass => weaponClass;
        public GameObject SourceModel => sourceModel;
        public Mesh BodyMesh => bodyMesh;
        public Vector3 ModelPosition => modelPosition;
        public Quaternion ModelRotation => Quaternion.Euler(modelEuler);
        public Vector3 ArmsRootPosition => armsRootPosition;
        public Vector3 RightWristPosition => rightWristPosition;
        public Quaternion RightWristRotation => rightWristRotation;
        public Vector3 LeftWristPosition => leftWristPosition;
        public Quaternion LeftWristRotation => leftWristRotation;
        public Vector3 RightElbowHint => rightElbowHint;
        public Vector3 LeftElbowHint => leftElbowHint;
        public Vector3 DominantGripCenter => dominantGripCenter;
        public Vector3 SupportGripCenter => supportGripCenter;
        public float ForearmPronation => forearmPronation;
        public float BreathsPerMinute => breathsPerMinute;
        public float IdleAmplitude => idleAmplitude;
        public AnimationClip IdleClip => idleClip;
        public AnimationClip ReloadClip => reloadClip;
        public FirstPersonReloadPresentation ReloadPresentation => reloadPresentation;
        public string ValidationNotes => validationNotes;
        // This asset carries no gameplay definition, ammo, damage or bone Transform references.
        public bool IsPrepared => !string.IsNullOrWhiteSpace(weaponId) && sourceModel != null && bodyMesh != null;
    }
}
