using UnityEngine;

namespace SkyArena.Inputs
{
    /// <summary>
    /// Scene-level input hub. Gameplay scripts (flight, guns, missiles) read
    /// from <see cref="Instance"/> instead of holding their own Canvas
    /// references, which keeps the spawned player prefab completely decoupled
    /// from the UI layout.
    ///
    /// Every value is polled, never event-driven, so there is no ordering
    /// dependency between the EventSystem and gameplay Update calls and no
    /// possibility of a dropped or double-consumed input.
    /// </summary>
    public class MobileInputController : MonoBehaviour
    {
        public static MobileInputController Instance { get; private set; }

        [SerializeField] private VirtualJoystick flightJoystick;
        [SerializeField] private HoldButton throttleUpButton;
        [SerializeField] private HoldButton throttleDownButton;
        [SerializeField] private HoldButton gunButton;
        [SerializeField] private HoldButton missileButton;

        /// <summary>Roll input from the joystick, -1 (left) to +1 (right).</summary>
        public float Roll => flightJoystick != null ? flightJoystick.Horizontal : 0f;

        /// <summary>Pitch input from the joystick, -1 (nose up) to +1 (nose down).</summary>
        public float Pitch => flightJoystick != null ? flightJoystick.Vertical : 0f;

        public bool GunHeld => gunButton != null && gunButton.IsPressed;

        public bool MissileHeld => missileButton != null && missileButton.IsPressed;

        /// <summary>+1 while accelerating, -1 while braking, 0 otherwise.</summary>
        public float Throttle
        {
            get
            {
                bool up = throttleUpButton != null && throttleUpButton.IsPressed;
                bool down = throttleDownButton != null && throttleDownButton.IsPressed;
                if (up == down) return 0f;
                return up ? 1f : -1f;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
