using UnityEngine;

namespace SkyArena.Core
{
    /// <summary>
    /// Marker for a place where a plane can enter the arena.
    /// <see cref="GameManager"/> collects every instance in the scene.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 6f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 25f);
        }
    }
}
