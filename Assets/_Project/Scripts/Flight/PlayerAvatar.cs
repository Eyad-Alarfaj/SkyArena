using Photon.Pun;
using SkyArena.Combat;
using SkyArena.UI;
using UnityEngine;

namespace SkyArena.Flight
{
    /// <summary>
    /// Runs once per spawned plane on every client and decides what this copy
    /// of the plane is: the local player's aircraft, or a remote opponent.
    ///
    /// The local plane claims the chase camera and binds the scene HUD to
    /// itself. Remote planes just get the enemy paint job. Every other
    /// component guards its own ownership, so this class only handles the
    /// scene-level wiring that has to happen exactly once.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class PlayerAvatar : MonoBehaviourPun
    {
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Material localPlayerMaterial;
        [SerializeField] private Material remotePlayerMaterial;

        private HealthSystem health;
        private FlightController flight;
        private LockOnSystem lockOn;
        private MissileLauncher missiles;

        public bool IsLocalPlayer => photonView.IsMine;

        private void Awake()
        {
            health = GetComponent<HealthSystem>();
            flight = GetComponent<FlightController>();
            lockOn = GetComponent<LockOnSystem>();
            missiles = GetComponent<MissileLauncher>();
        }

        private void Start()
        {
            ApplyPaint(photonView.IsMine ? localPlayerMaterial : remotePlayerMaterial);
            SetNetworkName();

            if (!photonView.IsMine) return;

            ClaimCamera();
            BindHud();
        }

        private void ApplyPaint(Material material)
        {
            if (material == null || bodyRenderers == null) return;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null) bodyRenderers[i].sharedMaterial = material;
            }
        }

        private void SetNetworkName()
        {
            string owner = photonView.Owner != null ? photonView.Owner.NickName : "Offline";
            if (string.IsNullOrEmpty(owner)) owner = $"Pilot {photonView.OwnerActorNr}";
            gameObject.name = photonView.IsMine ? $"Plane [YOU] {owner}" : $"Plane {owner}";
        }

        private void ClaimCamera()
        {
            CameraFollow chaseCamera = null;

            if (Camera.main != null) chaseCamera = Camera.main.GetComponent<CameraFollow>();
            if (chaseCamera == null) chaseCamera = FindAnyObjectByType<CameraFollow>();

            if (chaseCamera == null)
            {
                Debug.LogError("[SkyArena] No CameraFollow found in the scene. The chase camera will not track the player.");
                return;
            }

            chaseCamera.SetTarget(transform);
        }

        private void BindHud()
        {
            if (HUDController.Instance != null) HUDController.Instance.BindPlayer(health, flight, missiles);
            if (LockOnIndicatorUI.Instance != null) LockOnIndicatorUI.Instance.BindPlayer(lockOn);
            if (RadarController.Instance != null) RadarController.Instance.BindLocalPlayer(transform, photonView.ViewID);
        }
    }
}
