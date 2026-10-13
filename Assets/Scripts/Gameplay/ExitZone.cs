using UnityEngine;

namespace ShiftFour
{
    public sealed class ExitZone : MonoBehaviour
    {
        private void OnTriggerStay(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                GameSession.Instance?.TryExit();
        }
    }
}
