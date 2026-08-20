using UnityEngine;

namespace SkyArena.Inputs
{
    /// <summary>
    /// Feeds the touch HUD into an aircraft. Sits on the player prefab and is
    /// the only thing on that prefab which knows a Canvas exists.
    ///
    /// Remote copies of a plane still carry this component, but nothing reads
    /// it there: their flight components are inert and the transform is driven
    /// by network snapshots instead.
    /// </summary>
    public class HumanPilot : MonoBehaviour, IFlightInput
    {
        private static MobileInputController Hud => MobileInputController.Instance;

        public float Roll => Hud != null ? Hud.Roll : 0f;

        public float Pitch => Hud != null ? Hud.Pitch : 0f;

        public float Throttle => Hud != null ? Hud.Throttle : 0f;

        public bool GunHeld => Hud != null && Hud.GunHeld;

        public bool MissileHeld => Hud != null && Hud.MissileHeld;
    }
}
