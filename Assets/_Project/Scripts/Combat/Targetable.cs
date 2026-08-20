using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>
    /// Marks a plane as lockable and hittable, and maintains a static registry
    /// of every active target so the lock-on system and the radar can iterate a
    /// small list each frame instead of doing scene-wide searches.
    /// </summary>
    public class Targetable : MonoBehaviour
    {
        private static readonly List<Targetable> Registry = new List<Targetable>();

        [Tooltip("Set on bot prefabs. Lets AI prefer hunting humans over each other.")]
        [SerializeField] private bool isBot;

        /// <summary>Read-only view of every active target in the scene.</summary>
        public static IReadOnlyList<Targetable> All => Registry;

        public PhotonView OwnerView { get; private set; }
        public HealthSystem Health { get; private set; }

        /// <summary>Photon ViewID of the plane this target belongs to, or 0 if unowned.</summary>
        public int ViewID => OwnerView != null ? OwnerView.ViewID : 0;

        public bool IsAlive => Health == null || !Health.IsDead;

        /// <summary>True for AI-flown aircraft.</summary>
        public bool IsBot => isBot;

        private void Awake()
        {
            OwnerView = GetComponentInParent<PhotonView>();
            Health = GetComponentInParent<HealthSystem>();
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }
    }
}
