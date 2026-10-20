using UnityEngine;

namespace ShiftFour
{
    // Runtime camera wiring keeps scene and imported prefab assets untouched.
    [DisallowMultipleComponent]
    public sealed class FirstPersonWeaponMount : MonoBehaviour
    {
        private WeaponController weapons;
        private PlayerController player;
        private WeaponDefinition displayed;
        public Transform Socket { get; private set; }
        public FirstPersonViewmodel Instance { get; private set; }

        public void Initialize(WeaponController controller, PlayerController actor)
        {
            if (weapons != null) return;
            weapons = controller; player = actor;
            weapons.WeaponEquipped += OnEquipped;
            Refresh();
        }

        private void OnEquipped(WeaponDefinition _) => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (weapons == null || player == null) return;
            Camera camera = player.ViewCamera;
            WeaponDefinition definition = weapons.ActiveWeapon?.Definition;
            if (camera == null) { if (Socket != null) Socket.gameObject.SetActive(false); return; }
            if (Socket == null && definition != null && definition.ViewmodelPrefab != null)
            {
                Socket = new GameObject("WeaponSocket").transform;
                Socket.gameObject.layer = 2;
            }
            if (Socket == null) return;
            if (Socket.parent != camera.transform)
            {
                Socket.SetParent(camera.transform, false);
                Socket.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                Socket.localScale = Vector3.one;
            }
            if (displayed != definition || (Instance == null && definition != null && definition.ViewmodelPrefab != null))
            {
                if (Instance != null)
                {
                    Instance.gameObject.SetActive(false);
                    Destroy(Instance.gameObject);
                }
                Instance = null; displayed = definition;
                if (definition != null && definition.ViewmodelPrefab != null)
                {
                    Instance = Instantiate(definition.ViewmodelPrefab, Socket, false);
                    Instance.name = definition.ViewmodelPrefab.name;
                    Instance.Bind(weapons);
                }
            }
            Socket.gameObject.SetActive(isActiveAndEnabled && weapons.isActiveAndEnabled && player.CanAct);
        }

        private void OnDisable() { if (Socket != null) Socket.gameObject.SetActive(false); }
        private void OnDestroy()
        {
            if (weapons != null) weapons.WeaponEquipped -= OnEquipped;
            if (Socket != null) Destroy(Socket.gameObject);
        }
    }
}
