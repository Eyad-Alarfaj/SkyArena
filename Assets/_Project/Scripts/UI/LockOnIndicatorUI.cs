using SkyArena.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace SkyArena.UI
{
    /// <summary>
    /// Projects the local player's lock-on target onto the HUD.
    ///
    /// The reticle is amber while the lock is still building and red once the
    /// lock is confirmed and missiles can be fired. A thin radial ring shows
    /// how far through the lock timer the player is. The whole indicator hides
    /// itself whenever the target is behind the camera or off screen.
    /// </summary>
    public class LockOnIndicatorUI : MonoBehaviour
    {
        public static LockOnIndicatorUI Instance { get; private set; }

        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private RectTransform reticle;

        [Tooltip("Every bracket that makes up the reticle box; all are tinted together.")]
        [SerializeField] private Image[] reticleGraphics;

        [SerializeField] private Image lockProgressRing;

        [Tooltip("Leave empty for a Screen Space - Overlay canvas.")]
        [SerializeField] private Camera canvasCamera;

        [SerializeField] private Color trackingColor = new Color(1f, 0.78f, 0.2f);
        [SerializeField] private Color lockedColor = new Color(1f, 0.24f, 0.24f);

        private LockOnSystem lockOn;
        private Camera worldCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BindPlayer(LockOnSystem system)
        {
            lockOn = system;
            worldCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (lockOn == null || reticle == null || canvasRect == null)
            {
                Hide();
                return;
            }

            // Camera.main can change if the player respawns into a new rig.
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null)
            {
                Hide();
                return;
            }

            Transform tracked = lockOn.LockedTarget != null ? lockOn.LockedTarget : lockOn.CandidateTarget;
            if (tracked == null)
            {
                Hide();
                return;
            }

            Vector3 viewport = worldCamera.WorldToViewportPoint(tracked.position);
            bool visible = viewport.z > 0f &&
                           viewport.x > 0f && viewport.x < 1f &&
                           viewport.y > 0f && viewport.y < 1f;

            if (!visible)
            {
                Hide();
                return;
            }

            reticle.gameObject.SetActive(true);

            Vector2 screenPoint = worldCamera.WorldToScreenPoint(tracked.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPoint, canvasCamera, out Vector2 localPoint))
            {
                reticle.anchoredPosition = localPoint;
            }

            bool locked = lockOn.IsFullyLocked;
            Color tint = locked ? lockedColor : trackingColor;

            if (reticleGraphics != null)
            {
                for (int i = 0; i < reticleGraphics.Length; i++)
                {
                    if (reticleGraphics[i] != null) reticleGraphics[i].color = tint;
                }
            }

            if (lockProgressRing != null)
            {
                lockProgressRing.fillAmount = lockOn.LockProgress01;
                lockProgressRing.color = new Color(tint.r, tint.g, tint.b, 0.28f);
            }
        }

        private void Hide()
        {
            if (reticle != null) reticle.gameObject.SetActive(false);
        }
    }
}
