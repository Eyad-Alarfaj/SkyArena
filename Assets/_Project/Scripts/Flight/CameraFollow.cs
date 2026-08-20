using UnityEngine;

namespace SkyArena.Flight
{
    /// <summary>
    /// Chase camera that lives in the scene rather than inside the player
    /// prefab, so <c>Camera.main</c> is valid from the very first frame and
    /// there is never more than one AudioListener.
    ///
    /// While no plane is assigned (during connection) it slowly orbits the
    /// arena so the player is not looking at a frozen frame.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Chase")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 7f, -26f);
        [SerializeField] private float positionSmoothing = 8f;
        [SerializeField] private float rotationSmoothing = 6f;
        [SerializeField] private float lookAheadDistance = 40f;

        [Header("Idle orbit (no plane assigned)")]
        [SerializeField] private Vector3 idleCentre = new Vector3(0f, 220f, 0f);
        [SerializeField] private float idleRadius = 420f;
        [SerializeField] private float idleOrbitSpeed = 6f;

        private Transform target;
        private float idleAngle;

        public bool HasTarget => target != null;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target == null) return;

            // Snap into place so the camera does not sweep in from the idle orbit.
            transform.position = target.TransformPoint(followOffset);
            transform.rotation = Quaternion.LookRotation(
                target.position + target.forward * lookAheadDistance - transform.position, target.up);
        }

        public void ClearTarget() => target = null;

        private void LateUpdate()
        {
            if (target == null)
            {
                OrbitIdle();
                return;
            }

            Vector3 desiredPosition = target.TransformPoint(followOffset);
            transform.position = Vector3.Lerp(
                transform.position, desiredPosition, 1f - Mathf.Exp(-positionSmoothing * Time.deltaTime));

            Vector3 lookTarget = target.position + target.forward * lookAheadDistance;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, target.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime));
        }

        private void OrbitIdle()
        {
            idleAngle += idleOrbitSpeed * Time.deltaTime;
            float radians = idleAngle * Mathf.Deg2Rad;

            Vector3 position = idleCentre + new Vector3(
                Mathf.Cos(radians) * idleRadius, 0f, Mathf.Sin(radians) * idleRadius);

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(idleCentre - position, Vector3.up);
        }
    }
}
