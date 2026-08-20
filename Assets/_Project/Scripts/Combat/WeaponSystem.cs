using Photon.Pun;
using SkyArena.Inputs;
using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>
    /// Hitscan machine gun.
    ///
    /// The owning client raycasts from the muzzle while the gun button is held
    /// and asks the victim's client to apply damage. The tracer is drawn
    /// immediately for the shooter and replicated to everyone else, so enemy
    /// gunfire is visible without costing the local player a network round trip.
    ///
    /// This component stays enabled on remote planes so it can receive the
    /// tracer RPC and time out the line renderer; all firing logic is gated on
    /// <see cref="isLocal"/> instead of on the enabled flag.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class WeaponSystem : MonoBehaviourPun
    {
        private const int MaxHitsPerShot = 16;

        [SerializeField] private Transform muzzle;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private float damage = 7f;
        [SerializeField] private float range = 600f;
        [SerializeField] private float shotsPerSecond = 8f;
        [SerializeField] private float tracerDuration = 0.05f;
        [SerializeField] private LayerMask hitMask = ~0;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[MaxHitsPerShot];

        private HealthSystem health;
        private IFlightInput pilot;
        private bool isLocal;
        private float nextFireTime;
        private float tracerHideTime;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            pilot = GetComponent<IFlightInput>();
            if (tracer != null)
            {
                tracer.positionCount = 2;
                tracer.useWorldSpace = true;
                tracer.enabled = false;
            }
        }

        private void Start()
        {
            isLocal = photonView.IsMine;
        }

        private void Update()
        {
            if (tracer != null && tracer.enabled && Time.time >= tracerHideTime)
            {
                tracer.enabled = false;
            }

            if (!isLocal || muzzle == null) return;
            if (health != null && health.IsDead) return;

            bool wantsToFire = pilot != null && pilot.GunHeld;
            if (!wantsToFire || Time.time < nextFireTime) return;

            nextFireTime = Time.time + (shotsPerSecond > 0f ? 1f / shotsPerSecond : 0.1f);
            Fire();
        }

        private void Fire()
        {
            Vector3 origin = muzzle.position;
            Vector3 direction = muzzle.forward;
            Vector3 endPoint = origin + direction * range;

            if (TryFindClosestValidHit(origin, direction, out RaycastHit hit))
            {
                endPoint = hit.point;

                Targetable victim = hit.collider.GetComponentInParent<Targetable>();
                if (victim != null && victim.Health != null && victim.IsAlive)
                {
                    victim.Health.RequestDamage(damage, PhotonNetwork.LocalPlayer.ActorNumber);
                }
            }

            ShowTracer(origin, endPoint);
            photonView.RPC(nameof(RpcShowTracer), RpcTarget.Others, origin, endPoint);
        }

        /// <summary>
        /// Returns the nearest hit along the muzzle ray that does not belong to
        /// this plane. Without that filter the shooter's own hull would always
        /// be the first thing the ray touches and no shot would ever land.
        /// </summary>
        private bool TryFindClosestValidHit(Vector3 origin, Vector3 direction, out RaycastHit closest)
        {
            closest = default;

            int count = Physics.RaycastNonAlloc(
                origin, direction, hitBuffer, range, hitMask, QueryTriggerInteraction.Ignore);

            bool found = false;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = hitBuffer[i];
                if (candidate.collider == null) continue;

                PhotonView hitView = candidate.collider.GetComponentInParent<PhotonView>();
                if (hitView != null && hitView.ViewID == photonView.ViewID) continue;

                if (candidate.distance < closestDistance)
                {
                    closestDistance = candidate.distance;
                    closest = candidate;
                    found = true;
                }
            }

            return found;
        }

        [PunRPC]
        private void RpcShowTracer(Vector3 start, Vector3 end)
        {
            ShowTracer(start, end);
        }

        private void ShowTracer(Vector3 start, Vector3 end)
        {
            if (tracer == null) return;
            tracer.enabled = true;
            tracer.SetPosition(0, start);
            tracer.SetPosition(1, end);
            tracerHideTime = Time.time + tracerDuration;
        }
    }
}
