using Photon.Pun;
using UnityEngine;

namespace SkyArena.Networking
{
    /// <summary>
    /// Replicates position and rotation for any networked object.
    ///
    /// The owning client is the source of truth — its transform is driven
    /// directly by the flight or missile controller. Every other client
    /// receives snapshots and smooths toward them. Because snapshots arrive a
    /// few hundred milliseconds late at aircraft speeds, the received position
    /// is extrapolated along the sender's velocity by the measured lag before
    /// it is used as the interpolation goal, which removes most of the rubber
    /// banding you would otherwise see on fast movers.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class NetworkTransformSync : MonoBehaviourPun, IPunObservable
    {
        [SerializeField] private float positionLerpSpeed = 14f;
        [SerializeField] private float rotationLerpSpeed = 14f;
        [SerializeField] private float teleportDistance = 200f;
        [SerializeField] private bool extrapolateForLag = true;

        private Vector3 networkPosition;
        private Quaternion networkRotation = Quaternion.identity;
        private Vector3 lastSentPosition;
        private Vector3 estimatedVelocity;

        private void Awake()
        {
            networkPosition = transform.position;
            networkRotation = transform.rotation;
            lastSentPosition = transform.position;
        }

        private void Update()
        {
            if (photonView.IsMine) return;

            // A large gap means a respawn or a join in progress: snap, do not slide.
            if ((transform.position - networkPosition).sqrMagnitude > teleportDistance * teleportDistance)
            {
                transform.SetPositionAndRotation(networkPosition, networkRotation);
                return;
            }

            transform.position = Vector3.Lerp(
                transform.position, networkPosition, 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(
                transform.rotation, networkRotation, 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime));
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                Vector3 position = transform.position;

                // Velocity is derived from consecutive sends rather than from
                // the Rigidbody, because these bodies are kinematic and so
                // always report a velocity of zero.
                float interval = PhotonNetwork.SerializationRate > 0 ? 1f / PhotonNetwork.SerializationRate : 0.1f;
                Vector3 velocity = (position - lastSentPosition) / interval;
                lastSentPosition = position;

                stream.SendNext(position);
                stream.SendNext(transform.rotation);
                stream.SendNext(velocity);
            }
            else
            {
                networkPosition = (Vector3)stream.ReceiveNext();
                networkRotation = (Quaternion)stream.ReceiveNext();
                estimatedVelocity = (Vector3)stream.ReceiveNext();

                if (extrapolateForLag)
                {
                    float lag = Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime));
                    networkPosition += estimatedVelocity * lag;
                }
            }
        }
    }
}
