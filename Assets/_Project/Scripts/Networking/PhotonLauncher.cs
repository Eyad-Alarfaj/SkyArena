using Photon.Pun;
using Photon.Realtime;
using SkyArena.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SkyArena.Networking
{
    /// <summary>
    /// Owns the whole connection lifecycle for the arena.
    ///
    /// The game is deliberately a single scene: every client connects, joins
    /// the same named room and spawns its own plane in place. There is no
    /// lobby scene and no networked level loading, which removes an entire
    /// class of scene-sync bugs from the prototype.
    ///
    /// If the Photon cloud cannot be reached (no internet, missing App ID,
    /// region blocked) the launcher falls back to PUN's offline mode so the
    /// game is still fully playable solo instead of hanging on a black screen.
    /// </summary>
    public class PhotonLauncher : MonoBehaviourPunCallbacks
    {
        [Header("Room")]
        [SerializeField] private string roomName = "SkyArena";
        [SerializeField] private byte maxPlayersPerRoom = 8;

        [Tooltip("Clients must share a game version to see each other. Bump it after breaking changes.")]
        [SerializeField] private string gameVersion = "1.0";

        [Header("Resilience")]
        [SerializeField] private bool fallBackToOfflineOnFailure = true;
        [SerializeField] private float offlineFallbackDelay = 1.5f;

        [Header("UI")]
        [SerializeField] private Text statusText;

        private bool hasJoinedRoom;
        private bool offlineFallbackScheduled;

        private void Awake()
        {
            // Single scene: nothing to sync, so keep PUN out of scene loading.
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.GameVersion = gameVersion;

            if (string.IsNullOrEmpty(PhotonNetwork.NickName))
            {
                PhotonNetwork.NickName = $"Pilot{Random.Range(100, 999)}";
            }
        }

        private void Start()
        {
            Connect();
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnected)
            {
                SetStatus("Joining room...");
                JoinArenaRoom();
                return;
            }

            SetStatus("Connecting to Photon...");
            PhotonNetwork.ConnectUsingSettings();
        }

        /// <summary>Plays solo with no server. Also used as the automatic failure fallback.</summary>
        public void GoOffline()
        {
            if (hasJoinedRoom) return;

            SetStatus("Offline mode - solo flight");
            PhotonNetwork.OfflineMode = true; // immediately raises OnConnectedToMaster
        }

        private void JoinArenaRoom()
        {
            RoomOptions options = new RoomOptions
            {
                MaxPlayers = maxPlayersPerRoom,
                IsVisible = true,
                IsOpen = true
            };

            // JoinOrCreate guarantees that every client lands in the same room,
            // which is what makes two devices meet without any lobby UI.
            PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default);
        }

        public override void OnConnectedToMaster()
        {
            SetStatus(PhotonNetwork.OfflineMode ? "Starting offline arena..." : "Connected. Joining room...");
            JoinArenaRoom();
        }

        public override void OnJoinedRoom()
        {
            hasJoinedRoom = true;
            CancelInvoke(nameof(GoOffline));

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SpawnLocalPlayer();
            }
            else
            {
                Debug.LogError("[SkyArena] No GameManager in the scene, so no plane can be spawned.");
            }

            ReportRoomState();
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            SetStatus($"Join failed ({returnCode}). Retrying...");
            ScheduleOfflineFallback();
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            SetStatus($"Room creation failed ({returnCode}).");
            ScheduleOfflineFallback();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (hasJoinedRoom)
            {
                SetStatus($"Disconnected: {cause}");
                return;
            }

            SetStatus($"Could not reach Photon ({cause})");
            ScheduleOfflineFallback();
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer) => ReportRoomState();

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer) => ReportRoomState();

        private void ScheduleOfflineFallback()
        {
            if (!fallBackToOfflineOnFailure || hasJoinedRoom || offlineFallbackScheduled) return;

            offlineFallbackScheduled = true;
            Invoke(nameof(GoOffline), offlineFallbackDelay);
        }

        private void ReportRoomState()
        {
            if (PhotonNetwork.CurrentRoom == null) return;

            if (PhotonNetwork.OfflineMode)
            {
                SetStatus("OFFLINE - solo flight");
                return;
            }

            int count = PhotonNetwork.CurrentRoom.PlayerCount;
            SetStatus($"{PhotonNetwork.CurrentRoom.Name}  -  {count}/{maxPlayersPerRoom} pilots");
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[SkyArena] {message}");
        }
    }
}
