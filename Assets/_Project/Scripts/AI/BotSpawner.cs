using System.Collections.Generic;
using Photon.Pun;
using SkyArena.Core;
using UnityEngine;

namespace SkyArena.AI
{
    /// <summary>
    /// Keeps the arena stocked with bots.
    ///
    /// Only the master client ever spawns them, so a room with four players
    /// still contains exactly <see cref="botCount"/> bots rather than four
    /// times that. If the master leaves, Photon removes the objects it created
    /// and whichever client is promoted re-stocks the arena from scratch.
    /// </summary>
    public class BotSpawner : MonoBehaviourPunCallbacks
    {
        [SerializeField] private bool spawnBots = true;
        [SerializeField] private string botPrefabName = "EnemyBot";
        [SerializeField] private AiDifficulty difficulty = AiDifficulty.Normal;

        [Tooltip("How many bots should be alive in the arena at once.")]
        [SerializeField] private int botCount = 2;

        [Tooltip("Seconds between checks that the arena still has enough bots.")]
        [SerializeField] private float restockInterval = 3f;

        private readonly List<GameObject> bots = new List<GameObject>();
        private float nextRestockTime;

        public AiDifficulty Difficulty => difficulty;
        public int ActiveBotCount => bots.Count;

        public override void OnJoinedRoom() => Restock();

        public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient) => Restock();

        private void Update()
        {
            if (Time.time < nextRestockTime) return;
            nextRestockTime = Time.time + restockInterval;
            Restock();
        }

        /// <summary>Adds or removes bots until the arena holds exactly the wanted number.</summary>
        public void Restock()
        {
            if (!spawnBots || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

            // Photon destroys a bot when it dies out of the room or its owner
            // leaves, which leaves null holes in this list.
            bots.RemoveAll(bot => bot == null);

            while (bots.Count > botCount) RemoveOneBot();
            while (bots.Count < botCount) SpawnOneBot();
        }

        /// <summary>Changes difficulty and rebuilds the flight so it takes effect immediately.</summary>
        public void SetDifficulty(AiDifficulty value)
        {
            if (difficulty == value) return;
            difficulty = value;

            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

            // Difficulty is baked in at spawn time, so existing bots are
            // replaced rather than reconfigured mid-flight.
            for (int i = bots.Count - 1; i >= 0; i--) RemoveOneBot();
            Restock();
        }

        public void SetBotCount(int value)
        {
            botCount = Mathf.Max(0, value);
            Restock();
        }

        private void SpawnOneBot()
        {
            Vector3 position;
            Quaternion rotation;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.GetSpawnPose(out position, out rotation);
            }
            else
            {
                position = new Vector3(Random.Range(-500f, 500f), 240f, Random.Range(-500f, 500f));
                rotation = Quaternion.identity;
            }

            // Sent to every client so remote copies can label themselves, even
            // though only the owner acts on the difficulty.
            object[] instantiationData = { (int)difficulty };

            GameObject bot = PhotonNetwork.Instantiate(botPrefabName, position, rotation, 0, instantiationData);
            if (bot == null)
            {
                Debug.LogError($"[SkyArena] Could not spawn '{botPrefabName}'. Is the prefab inside a Resources folder?");
                return;
            }

            bots.Add(bot);
        }

        private void RemoveOneBot()
        {
            int last = bots.Count - 1;
            if (last < 0) return;

            GameObject bot = bots[last];
            bots.RemoveAt(last);

            if (bot != null) PhotonNetwork.Destroy(bot);
        }
    }
}
