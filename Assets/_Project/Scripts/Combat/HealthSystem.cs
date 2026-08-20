using System;
using System.Collections;
using Photon.Pun;
using SkyArena.Core;
using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>
    /// Owner-authoritative health.
    ///
    /// Any client may call <see cref="RequestDamage"/> on any plane after a
    /// local hit test. The damage RPC is broadcast, but only the client that
    /// owns the PhotonView actually subtracts health — that client alone
    /// decides when it dies and when it respawns. The resulting alive/dead
    /// state is then replicated to everyone through <see cref="RpcSetAlive"/>,
    /// so remote clients always agree on whether a plane can be locked or hit.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class HealthSystem : MonoBehaviourPun
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float respawnDelay = 3f;
        [SerializeField] private Renderer[] visualRenderers;
        [SerializeField] private Collider[] hitColliders;

        private float currentHealth;
        private Coroutine respawnRoutine;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthNormalised => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        /// <summary>Replicated to every client, so remote planes report death correctly.</summary>
        public bool IsDead { get; private set; }

        /// <summary>(current, max). Raised on the owning client only.</summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>Raised on every client when this plane's alive state flips.</summary>
        public event Action<bool> OnAliveStateChanged;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        /// <summary>
        /// Called from the attacker's client. Damage is applied on the victim's
        /// owning client, which is the single source of truth for its health.
        /// </summary>
        public void RequestDamage(float amount, int attackerActorNumber)
        {
            if (amount <= 0f || IsDead) return;
            photonView.RPC(nameof(RpcApplyDamage), RpcTarget.All, amount, attackerActorNumber);
        }

        [PunRPC]
        private void RpcApplyDamage(float amount, int attackerActorNumber)
        {
            // Everyone receives this, only the owner acts on it.
            if (!photonView.IsMine || IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f) Die();
        }

        private void Die()
        {
            photonView.RPC(nameof(RpcSetAlive), RpcTarget.All, false);

            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GetSpawnPose(out Vector3 position, out Quaternion rotation);
                transform.SetPositionAndRotation(position, rotation);
            }

            // Give the plane a clean slate before it becomes solid again.
            FlightReset();

            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            photonView.RPC(nameof(RpcSetAlive), RpcTarget.All, true);

            respawnRoutine = null;
        }

        private void FlightReset()
        {
            // Optional dependency: a plane without a flight controller (should
            // never happen, but the null check keeps this component standalone).
            var flight = GetComponent<SkyArena.Flight.FlightController>();
            if (flight != null) flight.ResetFlight();
        }

        [PunRPC]
        private void RpcSetAlive(bool alive)
        {
            IsDead = !alive;

            if (visualRenderers != null)
            {
                for (int i = 0; i < visualRenderers.Length; i++)
                {
                    if (visualRenderers[i] != null) visualRenderers[i].enabled = alive;
                }
            }

            if (hitColliders != null)
            {
                for (int i = 0; i < hitColliders.Length; i++)
                {
                    if (hitColliders[i] != null) hitColliders[i].enabled = alive;
                }
            }

            OnAliveStateChanged?.Invoke(alive);
        }
    }
}
