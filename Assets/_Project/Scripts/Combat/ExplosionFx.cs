using UnityEngine;

namespace SkyArena.Combat
{
    /// <summary>
    /// Purely cosmetic impact puff. Spawned locally on every client, never
    /// networked — each client creates its own copy when a missile is
    /// destroyed, which costs nothing in bandwidth.
    /// </summary>
    public class ExplosionFx : MonoBehaviour
    {
        [SerializeField] private float duration = 0.35f;
        [SerializeField] private float startScale = 1f;
        [SerializeField] private float endScale = 14f;

        private float elapsed;

        private void Update()
        {
            elapsed += Time.deltaTime;

            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float scale = Mathf.Lerp(startScale, endScale, t);
            transform.localScale = new Vector3(scale, scale, scale);

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
