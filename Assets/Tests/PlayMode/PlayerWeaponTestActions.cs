#if UNITY_EDITOR
using System;
using UnityEngine;

namespace ShiftFour.Tests
{
    // Existing combat fixtures used to call the unchecked private PlayerController.Shoot(ray).
    // Aim the real player camera and call the checked production API, without bypassing cooldown.
    internal static class PlayerWeaponTestActions
    {
        internal static Component Controller(Component player)
            => player.GetComponent(Type.GetType("ShiftFour.WeaponController, Assembly-CSharp", true));
        internal static object State(Component player) => Controller(player).GetType().GetProperty("ActiveWeapon").GetValue(Controller(player));
        internal static object Definition(Component player) => State(player).GetType().GetProperty("Definition").GetValue(State(player));
        internal static float Data(Component player, string property)
            => (float)Definition(player).GetType().GetProperty(property).GetValue(Definition(player));
        internal static float NextShotTime(Component player)
            => (float)State(player).GetType().GetProperty("NextShotTime").GetValue(State(player));

        internal static bool Fire(Component player, Ray aim)
        {
            var camera = (Camera)player.GetType().GetProperty("ViewCamera").GetValue(player);
            if (camera == null)
            {
                camera = new GameObject("Combat fixture camera").AddComponent<Camera>();
                camera.enabled = false; camera.transform.SetParent(player.transform, false);
                player.GetType().GetMethod("Configure").Invoke(player, new object[] { camera });
            }
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            bool enabled = ((Behaviour)player).enabled;
            try
            {
                ((Behaviour)player).enabled = true;
                camera.transform.SetPositionAndRotation(aim.origin, Quaternion.LookRotation(aim.direction));
                var weapons = Controller(player);
                return (bool)weapons.GetType().GetMethod("TryFire").Invoke(weapons, null);
            }
            finally
            {
                ((Behaviour)player).enabled = enabled;
                camera.transform.SetPositionAndRotation(position, rotation);
            }
        }
    }
}
#endif
