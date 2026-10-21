using UnityEngine;
namespace ShiftFour
{
    // One world camera, normal URP depth testing. These offsets affect presentation only.
    [DisallowMultipleComponent]
    public sealed class FirstPersonViewmodelFraming : MonoBehaviour
    {
        [SerializeField, Range(0,.03f)] private float cuffLift = 0f;
        [SerializeField, Range(.1f,.24f)] private float maximumRetreat = .18f;
        [SerializeField, Range(0,70)] private float highReadyDegrees = 50;
        [SerializeField] private Vector3 rotationAnchor = new Vector3(.03f,-.022f,-.148f);
        [SerializeField] private Vector3 nearCoverPosition = new Vector3(-.06f,.07f,.10f);
        public Vector3 PositionOffset => Vector3.up * cuffLift;
        public float MaximumRetreat => maximumRetreat;
        private float CoverWeight(float retreat)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(.06f,maximumRetreat,retreat));
        // Rotate the complete assembly about the authored dominant wrist, then raise it into view.
        public Vector3 CoverPositionOffset(float retreat)=>rotationAnchor-ObstructionRotation(retreat)*rotationAnchor+nearCoverPosition*CoverWeight(retreat);
        public Quaternion ObstructionRotation(float retreat)=>Quaternion.Euler(-highReadyDegrees*CoverWeight(retreat),0,0);
    }
}
