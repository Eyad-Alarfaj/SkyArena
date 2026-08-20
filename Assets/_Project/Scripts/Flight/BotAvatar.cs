using Photon.Pun;
using UnityEngine;

namespace SkyArena.Flight
{
    /// <summary>
    /// The bot equivalent of <see cref="PlayerAvatar"/>: paints the airframe so
    /// bots read as enemies at a glance.
    ///
    /// Deliberately does NOT claim the chase camera or bind the HUD. A bot is
    /// owned by the master client, so <c>photonView.IsMine</c> is true there -
    /// reusing PlayerAvatar would make the host's camera snap to a bot instead
    /// of to their own plane.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class BotAvatar : MonoBehaviourPun
    {
        [SerializeField] private Renderer[] bodyRenderers;
        [SerializeField] private Material botMaterial;

        private void Start()
        {
            if (botMaterial == null || bodyRenderers == null) return;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null) bodyRenderers[i].sharedMaterial = botMaterial;
            }
        }
    }
}
