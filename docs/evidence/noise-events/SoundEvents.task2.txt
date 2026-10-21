using System;
using UnityEngine;

namespace ShiftFour
{
    public static class SoundEvents
    {
        public static event Action<Vector3> ShotFired;
        public static void ReportShot(Vector3 position) => ShotFired?.Invoke(position);
    }
}
