using Photon.Pun;
using UnityEngine;

namespace SkyArena.Core
{
    /// <summary>
    /// Per-client scene director. Owns the spawn point registry and creates
    /// the local player's networked plane once the room has been joined.
    ///
    /// It never spawns anything for remote players — Photon replicates their
    /// planes automatically from their own PhotonNetwork.Instantiate calls.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private string playerPrefabName = "PlayerPlane";
        [SerializeField] private int targetFrameRate = 60;

        private SpawnPoint[] spawnPoints;
        private GameObject localPlayer;

        public GameObject LocalPlayer => localPlayer;
        public bool HasLocalPlayer => localPlayer != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            spawnPoints = FindObjectsByType<SpawnPoint>();

            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called by <c>PhotonLauncher</c> after OnJoinedRoom. Safe to call
        /// more than once — a second call is ignored while a plane is alive.
        /// </summary>
        public void SpawnLocalPlayer()
        {
            if (localPlayer != null) return;
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[SkyArena] SpawnLocalPlayer called while not in a room. Ignored.");
                return;
            }

            GetSpawnPose(out Vector3 position, out Quaternion rotation);
            localPlayer = PhotonNetwork.Instantiate(playerPrefabName, position, rotation);
        }

        /// <summary>Picks a random spawn pose, falling back to a safe default if none exist.</summary>
        public void GetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                position = new Vector3(0f, 150f, 0f);
                rotation = Quaternion.identity;
                return;
            }

            Transform chosen = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;
            position = chosen.position;
            rotation = chosen.rotation;
        }
    }
}
