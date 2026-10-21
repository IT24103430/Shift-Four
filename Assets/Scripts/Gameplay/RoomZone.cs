using UnityEngine;

namespace ShiftFour
{
    public sealed class RoomZone : MonoBehaviour
    {
        [SerializeField] private SecurityGate room;
        public void Configure(SecurityGate id) => room = id;
        private void OnTriggerStay(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                GameSession.Instance?.EnterRoom((int)room);
        }
    }
}
