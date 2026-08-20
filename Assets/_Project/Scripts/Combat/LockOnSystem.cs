using Photon.Pun;
using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>
    /// Local-only target acquisition. Each frame it picks the living enemy
    /// closest to the nose of the plane (smallest angle) that sits inside
    /// <see cref="lockRange"/> and <see cref="lockAngle"/>. That candidate must
    /// stay in the cone for <see cref="timeToLock"/> seconds before it becomes
    /// a confirmed <see cref="LockedTarget"/> that missiles can be fired at.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class LockOnSystem : MonoBehaviourPun
    {
        [SerializeField] private float lockRange = 700f;
        [SerializeField] private float lockAngle = 25f;
        [SerializeField] private float timeToLock = 1.0f;

        [Tooltip("Seconds a target may slip out of the cone before the lock resets.")]
        [SerializeField] private float lockGracePeriod = 0.4f;

        private HealthSystem health;
        private Transform candidate;
        private float lockProgress;
        private float lastSeenTime;

        /// <summary>Non-null only once a candidate has been held for the full lock time.</summary>
        public Transform LockedTarget { get; private set; }

        /// <summary>The target currently being tracked, locked or not.</summary>
        public Transform CandidateTarget => candidate;

        public bool IsFullyLocked => LockedTarget != null;

        public float LockProgress01 => timeToLock <= 0f ? 1f : Mathf.Clamp01(lockProgress / timeToLock);

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
        }

        private void Start()
        {
            // Lock-on is a client-side aiming aid; remote copies never need it.
            if (!photonView.IsMine) enabled = false;
        }

        private void Update()
        {
            if (health != null && health.IsDead)
            {
                ClearLock();
                return;
            }

            Transform best = FindBestTarget();

            if (best != null)
            {
                if (best != candidate)
                {
                    candidate = best;
                    lockProgress = 0f;
                }

                lastSeenTime = Time.time;
                lockProgress += Time.deltaTime;
            }
            else
            {
                // A target weaving across the edge of the cone should not wipe
                // a lock that was almost complete, so hold it briefly.
                bool withinGrace = candidate != null && Time.time - lastSeenTime <= lockGracePeriod;
                if (!withinGrace || !IsCandidateAlive())
                {
                    ClearLock();
                    return;
                }
            }

            LockedTarget = lockProgress >= timeToLock ? candidate : null;
        }

        private bool IsCandidateAlive()
        {
            if (candidate == null) return false;

            HealthSystem candidateHealth = candidate.GetComponentInParent<HealthSystem>();
            return candidateHealth == null || !candidateHealth.IsDead;
        }

        private void ClearLock()
        {
            candidate = null;
            LockedTarget = null;
            lockProgress = 0f;
        }

        private Transform FindBestTarget()
        {
            Transform best = null;
            float bestAngle = lockAngle;
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;

            var targets = Targetable.All;
            for (int i = 0; i < targets.Count; i++)
            {
                Targetable target = targets[i];
                if (target == null || target.OwnerView == null) continue;
                if (target.ViewID == photonView.ViewID) continue;
                if (!target.IsAlive) continue;

                Vector3 toTarget = target.transform.position - origin;
                float distance = toTarget.magnitude;
                if (distance < 1f || distance > lockRange) continue;

                float angle = Vector3.Angle(forward, toTarget);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = target.transform;
                }
            }

            return best;
        }
    }
}
