using UnityEngine;

namespace ShiftFour
{
    // Wrist targets and elbow hints live in the same presentation space as the pistol.
    // Two-bone IK changes rotations only: no bone translation, scaling, physics, or camera writes.
    [DisallowMultipleComponent, RequireComponent(typeof(FirstPersonArmsRig))]
    public sealed class FirstPersonPistolGrip : MonoBehaviour
    {
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private Transform supportHandTarget;
        [SerializeField] private Transform rightElbowHint;
        [SerializeField] private Transform supportElbowHint;
        [SerializeField, Range(0, 60)] private float maximumWristDeviation = 40;
        private Arm dominant, support;
        private float configuredPronation;

        public Transform RightHandTarget => rightHandTarget;
        public Transform SupportHandTarget => supportHandTarget;
        public Transform RightElbowHint => rightElbowHint;
        public Transform SupportElbowHint => supportElbowHint;
        public float MaximumWristDeviation => maximumWristDeviation;
        public bool IsReady => dominant != null && support != null;

        public bool Initialize()
        {
            if (IsReady) return true;
            var rig = GetComponent<FirstPersonArmsRig>();
            if (rig == null || !rig.IsValid || !OwnTarget(rightHandTarget) || !OwnTarget(supportHandTarget) ||
                !OwnTarget(rightElbowHint) || !OwnTarget(supportElbowHint)) return false;
            dominant = new Arm(rig.RightUpperArm, rig.RightForearm, rig.RightHand);
            support = new Arm(rig.LeftUpperArm, rig.LeftForearm, rig.LeftHand);
            return true;
        }

        private bool OwnTarget(Transform target) => target != null && target.parent == transform;

        public bool ApplyConfiguration(FirstPersonWeaponGripConfiguration data)
        {
            if (data == null || !data.IsPrepared || !Initialize()) return false;
            transform.localPosition = data.ArmsRootPosition;
            rightHandTarget.localPosition = data.RightWristPosition - data.ArmsRootPosition;
            rightHandTarget.localRotation = data.RightWristRotation;
            supportHandTarget.localPosition = data.LeftWristPosition - data.ArmsRootPosition;
            supportHandTarget.localRotation = data.LeftWristRotation;
            rightElbowHint.localPosition = data.RightElbowHint - data.ArmsRootPosition;
            supportElbowHint.localPosition = data.LeftElbowHint - data.ArmsRootPosition;
            configuredPronation = data.ForearmPronation;
            ApplyPose(0, null, Vector3.zero); return true;
        }

        public void ApplyPose(float reloadProgress, Transform magazine, Vector3 magazineRestPosition)
        {
            if (!Initialize()) return;
            float reach = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .2f, reloadProgress)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, .97f, reloadProgress)));
            Vector3 supportOffset = transform.TransformVector(new Vector3(-.025f, -.015f, -.015f)) * reach;
            if (magazine != null && magazine.parent != null)
                supportOffset += (magazine.position - magazine.parent.TransformPoint(magazineRestPosition)) * reach;
            dominant.Solve(rightHandTarget.position, rightHandTarget.rotation, rightElbowHint.position, maximumWristDeviation, configuredPronation, configuredPronation > 0 ? 1 : 0);
            support.Solve(supportHandTarget.position + supportOffset, supportHandTarget.rotation, supportElbowHint.position, maximumWristDeviation, configuredPronation, configuredPronation > 0 ? 1 : 0);
        }

        public void ApplyMagazinePose(Transform magazineGrip, float contactWeight, float forearmTwistLimit, float handClearance)
        {
            if (!Initialize()) return;
            float weight = magazineGrip != null ? Mathf.Clamp01(contactWeight) : 0;
            dominant.Solve(rightHandTarget.position, rightHandTarget.rotation, rightElbowHint.position, maximumWristDeviation);
            Vector3 position = weight > 0 ? Vector3.Lerp(supportHandTarget.position, magazineGrip.position, weight) : supportHandTarget.position;
            position += transform.TransformVector(Vector3.left) * (handClearance * Mathf.Sin(weight * Mathf.PI));
            Quaternion rotation = weight > 0 ? Quaternion.Slerp(supportHandTarget.rotation, magazineGrip.rotation, weight) : supportHandTarget.rotation;
            support.Solve(position, rotation, supportElbowHint.position, maximumWristDeviation, forearmTwistLimit * weight, weight);
        }

        // Used by a separate planting rig, so the equipped weapon's authored pose is untouched.
        public void ApplyPlantingPose(Vector3 leftPosition, Quaternion leftRotation,
            Vector3 rightPosition, Quaternion rightRotation)
        {
            if (!Initialize()) return;
            support.Solve(leftPosition, leftRotation, supportElbowHint.position, 55f, 45f, 1f);
            dominant.Solve(rightPosition, rightRotation, rightElbowHint.position, 55f, 45f, 1f);
        }

        private sealed class Arm
        {
            private readonly Transform upper, forearm, hand;
            private readonly float upperLength, forearmLength;
            private readonly Quaternion neutralWrist;
            private readonly Quaternion upperRest, forearmRest;
            private readonly Vector3 restBend;

            public Arm(Transform upper, Transform forearm, Transform hand)
            {
                this.upper = upper; this.forearm = forearm; this.hand = hand;
                upperLength = Vector3.Distance(upper.position, forearm.position);
                forearmLength = Vector3.Distance(forearm.position, hand.position);
                neutralWrist = Quaternion.Inverse(forearm.rotation) * hand.rotation;
                upperRest = upper.localRotation; forearmRest = forearm.localRotation;
                restBend = upper.parent.InverseTransformDirection(forearm.position - upper.position);
            }

            public void Solve(Vector3 target, Quaternion rotation, Vector3 hint, float wristLimit, float twistLimit = 0, float orientationWeight = 0)
            {
                // Solve from the same authored orientation each time. Incremental FromTo rotations
                // otherwise retain path-dependent roll after a reload, even when the wrist returns.
                upper.localRotation = upperRest; forearm.localRotation = forearmRest; hand.localRotation = neutralWrist;
                Vector3 shoulder = upper.position, delta = target - shoulder;
                if (upperLength < .0001f || forearmLength < .0001f || delta.sqrMagnitude < .000001f) return;
                Vector3 axis = delta.normalized;
                // Targets outside the anatomical reach stop at the reachable boundary; bones never stretch.
                float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - forearmLength) + .001f,
                    upperLength + forearmLength - .001f);
                target = shoulder + axis * distance;
                if (orientationWeight > 0)
                {
                    Quaternion desiredForearm = rotation * Quaternion.Inverse(neutralWrist);
                    Vector3 desiredElbow = target - desiredForearm * hand.localPosition.normalized * forearmLength;
                    hint = Vector3.Lerp(hint, desiredElbow, Mathf.Clamp01(orientationWeight));
                }
                Vector3 bend = Vector3.ProjectOnPlane(hint - shoulder, axis);
                if (bend.sqrMagnitude < .000001f)
                    bend = Vector3.ProjectOnPlane(upper.parent.TransformDirection(restBend), axis);
                if (bend.sqrMagnitude < .000001f) return;
                float along = (upperLength * upperLength - forearmLength * forearmLength + distance * distance) / (2 * distance);
                float height = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
                Vector3 elbow = shoulder + axis * along + bend.normalized * height;
                upper.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder) * upper.rotation;
                forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, target - forearm.position) * forearm.rotation;
                Quaternion neutral = forearm.rotation * neutralWrist;
                if (twistLimit > 0)
                {
                    // Pronation about the elbow-to-wrist axis preserves the wrist position and bone length.
                    Vector3 axisOfForearm = (hand.position - forearm.position).normalized;
                    Quaternion difference = rotation * Quaternion.Inverse(neutral);
                    Vector3 projected = Vector3.Project(new Vector3(difference.x, difference.y, difference.z), axisOfForearm);
                    float angle = 2 * Mathf.Atan2(Vector3.Dot(projected, axisOfForearm), difference.w) * Mathf.Rad2Deg;
                    angle = Mathf.DeltaAngle(0, angle);
                    forearm.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -twistLimit, twistLimit), axisOfForearm) * forearm.rotation;
                    neutral = forearm.rotation * neutralWrist;
                }
                hand.rotation = Quaternion.RotateTowards(neutral, rotation, wristLimit);
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawTarget(rightHandTarget, new Color(.9f, .65f, .2f));
            DrawTarget(supportHandTarget, new Color(.2f, .8f, .9f));
        }

        private static void DrawTarget(Transform target, Color color)
        {
            if (target == null) return;
            Gizmos.color = color; Gizmos.DrawWireSphere(target.position, .009f);
            Gizmos.DrawLine(target.position, target.position + target.forward * .045f);
            Gizmos.DrawLine(target.position, target.position + target.up * .025f);
        }
    }
}
