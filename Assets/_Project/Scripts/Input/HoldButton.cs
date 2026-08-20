using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SkyArena.Inputs
{
    /// <summary>
    /// Hold-to-act touch button used for throttle, guns and missiles.
    /// <see cref="IsPressed"/> stays true for as long as the finger is held
    /// down on this element.
    ///
    /// uGUI guarantees that the object which received OnPointerDown also
    /// receives OnPointerUp, even if the finger is released somewhere else on
    /// screen, so the button can never get stuck "held" by dragging off it.
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic tintTarget;
        [SerializeField] private Color pressedTint = new Color(1f, 1f, 1f, 1f);

        private Color idleTint = Color.white;
        private bool hasIdleTint;

        public bool IsPressed { get; private set; }

        private void Awake() => CacheIdleTint();

        private void CacheIdleTint()
        {
            if (hasIdleTint || tintTarget == null) return;
            idleTint = tintTarget.color;
            hasIdleTint = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            ApplyTint(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
            ApplyTint(false);
        }

        private void OnDisable()
        {
            // Defensive: a button hidden mid-press must not leave the input latched.
            IsPressed = false;
            ApplyTint(false);
        }

        private void ApplyTint(bool pressed)
        {
            CacheIdleTint();
            if (tintTarget == null) return;
            tintTarget.color = pressed ? pressedTint : idleTint;
        }
    }
}
