using Photon.Pun;
using SkyArena.Inputs;
using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>What the missile button can do right now. Drives the HUD label.</summary>
    public enum MissileStatus
    {
        Unavailable,
        Reloading,
        NoLock,
        Unguided,
        Locked
    }

    /// <summary>
    /// Fires a networked homing missile while the missile button is held.
    ///
    /// The launcher deliberately never fails silently. A held button with no
    /// lock either fires an unguided missile (the default) or reports NoLock to
    /// the HUD - it does not simply do nothing, which is indistinguishable from
    /// a broken button.
    ///
    /// The button is polled rather than subscribed to, so there is no ordering
    /// dependency between the EventSystem and gameplay Update.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(LockOnSystem))]
    public class MissileLauncher : MonoBehaviourPun
    {
        [SerializeField] private string missilePrefabName = "Missile";
        [SerializeField] private Transform launchPoint;
        [SerializeField] private float damage = 34f;
        [SerializeField] private float cooldown = 2.5f;

        [Tooltip("Fire straight ahead when nothing is locked, instead of refusing to fire.")]
        [SerializeField] private bool allowUnguidedFire = true;

        private HealthSystem health;
        private LockOnSystem lockOn;
        private IFlightInput pilot;
        private bool isLocal;
        private float nextFireTime;

        /// <summary>1 immediately after firing, 0 when reloaded. Drives the cooldown ring.</summary>
        public float CooldownRemaining01 =>
            cooldown <= 0f ? 0f : Mathf.Clamp01((nextFireTime - Time.time) / cooldown);

        public MissileStatus Status
        {
            get
            {
                if (health != null && health.IsDead) return MissileStatus.Unavailable;
                if (Time.time < nextFireTime) return MissileStatus.Reloading;
                if (lockOn != null && lockOn.IsFullyLocked) return MissileStatus.Locked;
                return allowUnguidedFire ? MissileStatus.Unguided : MissileStatus.NoLock;
            }
        }

        public bool CanFire
        {
            get
            {
                MissileStatus status = Status;
                return isLocal && (status == MissileStatus.Locked || status == MissileStatus.Unguided);
            }
        }

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            lockOn = GetComponent<LockOnSystem>();
            pilot = GetComponent<IFlightInput>();
        }

        private void Start()
        {
            isLocal = photonView.IsMine;

            if (!isLocal)
            {
                // Remote copies never fire; their missiles arrive over the network.
                enabled = false;
                return;
            }

            if (pilot == null)
            {
                Debug.LogError(
                    $"[SkyArena] {name} has no IFlightInput component (HumanPilot or AiPilot), " +
                    "so the missile button can never reach it.");
            }
        }

        private void Update()
        {
            if (!isLocal || pilot == null) return;
            if (!pilot.MissileHeld) return;
            if (!CanFire) return;

            FireMissile(lockOn != null ? lockOn.LockedTarget : null);
        }

        private void FireMissile(Transform lockedTarget)
        {
            nextFireTime = Time.time + cooldown;

            Transform origin = launchPoint != null ? launchPoint : transform;
            GameObject missileObject =
                PhotonNetwork.Instantiate(missilePrefabName, origin.position, origin.rotation);

            if (missileObject == null)
            {
                Debug.LogError(
                    $"[SkyArena] PhotonNetwork.Instantiate('{missilePrefabName}') returned null. " +
                    "The prefab must live inside a folder named Resources.");
                return;
            }

            MissileController missile = missileObject.GetComponent<MissileController>();
            if (missile == null)
            {
                Debug.LogError($"[SkyArena] Missile prefab '{missilePrefabName}' has no MissileController.");
                return;
            }

            // A null target is legal: the missile then flies straight.
            missile.Initialize(lockedTarget, PhotonNetwork.LocalPlayer.ActorNumber, damage);
        }
    }
}
