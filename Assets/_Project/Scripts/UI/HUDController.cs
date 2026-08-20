using SkyArena.Combat;
using SkyArena.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace SkyArena.UI
{
    /// <summary>
    /// Minimal combat HUD: health, speed, altitude, throttle and the missile
    /// cooldown ring.
    ///
    /// The HUD lives in the scene and is bound to whichever plane turns out to
    /// be the local player, so the networked prefab never has to know that a
    /// Canvas exists.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [Header("Health")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Color healthyColor = new Color(0.25f, 0.85f, 0.45f);
        [SerializeField] private Color criticalColor = new Color(0.9f, 0.25f, 0.25f);

        [Header("Flight")]
        [SerializeField] private Text speedText;
        [SerializeField] private Text altitudeText;
        [SerializeField] private Image throttleFill;

        [Header("Weapons")]
        [SerializeField] private Image missileCooldownFill;
        [SerializeField] private Text weaponStatusText;
        [SerializeField] private Color statusLockedColor = new Color(1f, 0.24f, 0.24f);
        [SerializeField] private Color statusReadyColor = new Color(1f, 0.78f, 0.2f);
        [SerializeField] private Color statusBlockedColor = new Color(0.65f, 0.7f, 0.78f);

        [Header("Death")]
        [SerializeField] private GameObject deathOverlay;

        private HealthSystem health;
        private FlightController flight;
        private MissileLauncher missiles;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (deathOverlay != null) deathOverlay.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by <c>PlayerAvatar</c> once the local plane exists.</summary>
        public void BindPlayer(HealthSystem healthSystem, FlightController flightController, MissileLauncher missileLauncher)
        {
            Unsubscribe();

            health = healthSystem;
            flight = flightController;
            missiles = missileLauncher;

            if (health != null)
            {
                health.OnHealthChanged += HandleHealthChanged;
                health.OnAliveStateChanged += HandleAliveStateChanged;
                HandleHealthChanged(health.CurrentHealth, health.MaxHealth);
                HandleAliveStateChanged(!health.IsDead);
            }
        }

        private void Unsubscribe()
        {
            if (health == null) return;
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnAliveStateChanged -= HandleAliveStateChanged;
        }

        private void Update()
        {
            if (flight != null)
            {
                if (speedText != null) speedText.text = $"{flight.SpeedKmh} km/h";
                if (altitudeText != null) altitudeText.text = $"ALT {flight.AltitudeMetres} m";
                if (throttleFill != null) throttleFill.fillAmount = flight.ThrottleNormalised;
            }

            if (missiles != null)
            {
                if (missileCooldownFill != null) missileCooldownFill.fillAmount = missiles.CooldownRemaining01;
                ShowWeaponStatus(missiles.Status);
            }
        }

        /// <summary>
        /// Tells the player why the missile button will or will not fire. A
        /// held button that silently does nothing is indistinguishable from a
        /// broken one, which is exactly how the missile bug presented.
        /// </summary>
        private void ShowWeaponStatus(MissileStatus status)
        {
            if (weaponStatusText == null) return;

            switch (status)
            {
                case MissileStatus.Locked:
                    weaponStatusText.text = "MSL LOCKED";
                    weaponStatusText.color = statusLockedColor;
                    break;

                case MissileStatus.Unguided:
                    weaponStatusText.text = "MSL UNGUIDED";
                    weaponStatusText.color = statusReadyColor;
                    break;

                case MissileStatus.NoLock:
                    weaponStatusText.text = "NO LOCK";
                    weaponStatusText.color = statusBlockedColor;
                    break;

                case MissileStatus.Reloading:
                    weaponStatusText.text = "RELOADING";
                    weaponStatusText.color = statusBlockedColor;
                    break;

                default:
                    weaponStatusText.text = string.Empty;
                    break;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            float normalised = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (healthFill != null)
            {
                healthFill.fillAmount = normalised;
                healthFill.color = Color.Lerp(criticalColor, healthyColor, normalised);
            }

            if (healthText != null) healthText.text = Mathf.CeilToInt(current).ToString();
        }

        private void HandleAliveStateChanged(bool alive)
        {
            if (deathOverlay != null) deathOverlay.SetActive(!alive);
        }
    }
}
