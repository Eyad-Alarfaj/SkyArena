using UnityEngine;
using UnityEngine.EventSystems;

namespace SkyArena.Inputs
{
    /// <summary>
    /// Left-side touch joystick. Outputs a normalised [-1, 1] vector based on
    /// how far the finger has been dragged from the background's centre.
    ///
    /// Implemented purely with uGUI pointer events, so it behaves identically
    /// under the legacy Input Manager, the new Input System, or both — which
    /// matters because this project ships with "Active Input Handling" set to
    /// the new Input System only.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float handleRange = 110f;
        [SerializeField] private float deadZone = 0.06f;

        private Vector2 inputVector;

        /// <summary>Roll axis. -1 = full left, +1 = full right.</summary>
        public float Horizontal => inputVector.x;

        /// <summary>Pitch axis. -1 = stick pulled down, +1 = stick pushed up.</summary>
        public float Vertical => inputVector.y;

        public Vector2 Value => inputVector;

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null || handleRange <= 0f) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            localPoint = Vector2.ClampMagnitude(localPoint, handleRange);
            handle.anchoredPosition = localPoint;

            Vector2 raw = localPoint / handleRange;
            inputVector = raw.magnitude < deadZone ? Vector2.zero : raw;
        }

        public void OnPointerUp(PointerEventData eventData) => ResetStick();

        private void OnDisable() => ResetStick();

        private void ResetStick()
        {
            inputVector = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}
