using System.Collections.Generic;
using SkyArena.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SkyArena.UI
{
    /// <summary>
    /// Flat top-down radar.
    ///
    /// Every other plane is projected onto a circular panel relative to the
    /// local player, rotated so that the top of the radar is always the
    /// direction the player is flying. Contacts beyond the radar range are
    /// pinned to the rim rather than disappearing, so the player can always
    /// tell which way the fight is.
    ///
    /// Blips are pooled by Photon ViewID and created, reused and removed as
    /// players join, die and leave.
    /// </summary>
    public class RadarController : MonoBehaviour
    {
        public static RadarController Instance { get; private set; }

        [SerializeField] private RectTransform radarPanel;
        [SerializeField] private RectTransform blipTemplate;
        [SerializeField] private float radarWorldRange = 900f;
        [SerializeField] private float radarUIRadius = 105f;
        [SerializeField] private Color enemyColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.3f, 0.3f, 0.45f);

        private readonly Dictionary<int, RectTransform> activeBlips = new Dictionary<int, RectTransform>();
        private readonly List<int> seenThisFrame = new List<int>();
        private readonly List<int> toRemove = new List<int>();

        private Transform localPlayer;
        private int localViewId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (blipTemplate != null) blipTemplate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BindLocalPlayer(Transform player, int viewId)
        {
            localPlayer = player;
            localViewId = viewId;
        }

        private void LateUpdate()
        {
            if (localPlayer == null || radarPanel == null || blipTemplate == null) return;
            if (radarWorldRange <= 0f) return;

            seenThisFrame.Clear();

            // Counter-rotating by the player's heading keeps "up" = "forward".
            float headingRadians = -localPlayer.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Cos(headingRadians);
            float sin = Mathf.Sin(headingRadians);

            Vector3 origin = localPlayer.position;

            IReadOnlyList<Targetable> targets = Targetable.All;
            for (int i = 0; i < targets.Count; i++)
            {
                Targetable target = targets[i];
                if (target == null || target.OwnerView == null) continue;

                int viewId = target.ViewID;
                if (viewId == 0 || viewId == localViewId) continue;
                if (!target.IsAlive) continue;

                Vector3 relative = target.transform.position - origin;
                Vector2 flat = new Vector2(relative.x, relative.z);

                bool outOfRange = flat.magnitude > radarWorldRange;
                if (outOfRange) flat = flat.normalized * radarWorldRange;

                Vector2 rotated = new Vector2(
                    flat.x * cos - flat.y * sin,
                    flat.x * sin + flat.y * cos);

                RectTransform blip = GetOrCreateBlip(viewId);
                blip.anchoredPosition = (rotated / radarWorldRange) * radarUIRadius;

                Image blipImage = blip.GetComponent<Image>();
                if (blipImage != null) blipImage.color = outOfRange ? outOfRangeColor : enemyColor;

                seenThisFrame.Add(viewId);
            }

            PruneStaleBlips();
        }

        private RectTransform GetOrCreateBlip(int viewId)
        {
            if (activeBlips.TryGetValue(viewId, out RectTransform existing) && existing != null)
            {
                return existing;
            }

            RectTransform blip = Instantiate(blipTemplate, radarPanel);
            blip.gameObject.SetActive(true);
            blip.name = $"Blip_{viewId}";
            activeBlips[viewId] = blip;
            return blip;
        }

        private void PruneStaleBlips()
        {
            toRemove.Clear();

            foreach (KeyValuePair<int, RectTransform> entry in activeBlips)
            {
                if (!seenThisFrame.Contains(entry.Key)) toRemove.Add(entry.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                int viewId = toRemove[i];
                if (activeBlips.TryGetValue(viewId, out RectTransform blip) && blip != null)
                {
                    Destroy(blip.gameObject);
                }
                activeBlips.Remove(viewId);
            }
        }
    }
}
