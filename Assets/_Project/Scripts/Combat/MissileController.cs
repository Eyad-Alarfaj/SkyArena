using Photon.Pun;
using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>
    /// Homing missile.
    ///
    /// Only the client that fired it simulates guidance and detects impacts;
    /// every other client sees it move purely through NetworkTransformSync.
    /// A short arming delay plus an explicit owner check stop the missile from
    /// detonating on the plane that launched it.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody))]
    public class MissileController : MonoBehaviourPun
    {
        [SerializeField] private float speed = 110f;
        [SerializeField] private float turnRateDegPerSec = 130f;
        [SerializeField] private float lifeTime = 8f;
        [SerializeField] private float armingDelay = 0.15f;
        [SerializeField] private float damage = 34f;
        [SerializeField] private GameObject explosionPrefab;

        private Rigidbody body;
        private Transform target;
        private HealthSystem targetHealth;
        private int ownerActorNumber = -1;
        private float armedAtTime;
        private bool isLocal;
        private bool hasExploded;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        /// <summary>
        /// Called by <see cref="MissileLauncher"/> on the firing client only,
        /// immediately after PhotonNetwork.Instantiate.
        /// </summary>
        public void Initialize(Transform lockedTarget, int firingActorNumber, float overrideDamage)
        {
            target = lockedTarget;
            targetHealth = lockedTarget != null ? lockedTarget.GetComponentInParent<HealthSystem>() : null;
            ownerActorNumber = firingActorNumber;
            if (overrideDamage > 0f) damage = overrideDamage;
        }

        private void Start()
        {
            isLocal = photonView.IsMine;
            armedAtTime = Time.time + armingDelay;

            if (isLocal) Invoke(nameof(SelfDestruct), lifeTime);
        }

        private void FixedUpdate()
        {
            if (!isLocal || hasExploded) return;

            // Stop chasing a target that died mid-flight; fly straight instead.
            if (targetHealth != null && targetHealth.IsDead)
            {
                target = null;
                targetHealth = null;
            }

            Vector3 desiredDirection = transform.forward;
            if (target != null)
            {
                Vector3 toTarget = target.position - body.position;
                if (toTarget.sqrMagnitude > 0.0001f) desiredDirection = toTarget.normalized;
            }

            Quaternion desired = Quaternion.LookRotation(desiredDirection, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, desired, turnRateDegPerSec * Time.fixedDeltaTime));
            body.MovePosition(body.position + transform.forward * speed * Time.fixedDeltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isLocal || hasExploded) return;
            if (Time.time < armedAtTime) return;

            Targetable victim = other.GetComponentInParent<Targetable>();
            if (victim != null)
            {
                PhotonView victimView = victim.OwnerView;

                // Never detonate on the plane that fired this missile.
                if (victimView != null && victimView.Owner != null &&
                    victimView.Owner.ActorNumber == ownerActorNumber)
                {
                    return;
                }

                if (victim.Health != null && victim.IsAlive)
                {
                    victim.Health.RequestDamage(damage, ownerActorNumber);
                }
            }

            Explode();
        }

        private void SelfDestruct() => Explode();

        private void Explode()
        {
            if (hasExploded) return;
            hasExploded = true;

            CancelInvoke(nameof(SelfDestruct));
            PhotonNetwork.Destroy(gameObject);
        }

        /// <summary>
        /// Runs on every client when the networked missile is removed, which is
        /// exactly when the impact puff should appear for everyone.
        /// </summary>
        private void OnDestroy()
        {
            if (explosionPrefab == null) return;
            if (!Application.isPlaying) return;

            // Skip the effect while the scene itself is being torn down.
            if (!gameObject.scene.isLoaded) return;

            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }
    }
}
