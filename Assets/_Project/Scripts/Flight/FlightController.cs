using Photon.Pun;
using SkyArena.Combat;
using SkyArena.Inputs;
using UnityEngine;

namespace SkyArena.Flight
{
    /// <summary>
    /// Arcade flight model.
    ///
    /// The plane always flies forward; the throttle only trims its speed
    /// between a minimum and a maximum, so it can never stall or fall out of
    /// the sky. Roll and pitch come straight from the joystick, with a yaw
    /// turn-assist that makes banked turns curve the way players expect
    /// without any real aerodynamics.
    ///
    /// Runs on the owning client only. Remote copies of this plane are moved
    /// entirely by NetworkTransformSync.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Rigidbody))]
    public class FlightController : MonoBehaviourPun
    {
        [Header("Speed (metres per second)")]
        [SerializeField] private float minSpeed = 25f;
        [SerializeField] private float maxSpeed = 110f;
        [SerializeField] private float cruiseSpeed = 55f;
        [SerializeField] private float throttleAcceleration = 30f;

        [Header("Handling (degrees per second)")]
        [SerializeField] private float pitchRate = 55f;
        [SerializeField] private float rollRate = 120f;
        [SerializeField] private float turnAssistRate = 30f;

        [Tooltip("On: pull the stick down to climb (classic flight sim). Off: push up to climb.")]
        [SerializeField] private bool invertPitch = false;

        [Header("Arena bounds")]
        [SerializeField] private float minAltitude = 20f;
        [SerializeField] private float maxAltitude = 900f;
        [SerializeField] private float arenaRadius = 1400f;
        [SerializeField] private float boundarySteerRate = 45f;

        private Rigidbody body;
        private HealthSystem health;
        private IFlightInput pilot;
        private float currentSpeed;

        public float CurrentSpeed => currentSpeed;

        /// <summary>Speed shown on the HUD, converted to km/h for readability.</summary>
        public int SpeedKmh => Mathf.RoundToInt(currentSpeed * 3.6f);

        public int AltitudeMetres => Mathf.RoundToInt(transform.position.y);

        /// <summary>0 at minimum speed, 1 at maximum speed. Drives the throttle gauge.</summary>
        public float ThrottleNormalised =>
            maxSpeed > minSpeed ? Mathf.Clamp01((currentSpeed - minSpeed) / (maxSpeed - minSpeed)) : 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            health = GetComponent<HealthSystem>();

            // Whoever is flying this airframe: HumanPilot on the player prefab,
            // AiPilot on a bot. The flight model does not care which.
            pilot = GetComponent<IFlightInput>();
            currentSpeed = cruiseSpeed;
        }

        /// <summary>
        /// Retunes the airframe at runtime. Used by <c>AiPilot</c> so that a
        /// Hard bot genuinely out-turns and outruns an Easy one, rather than
        /// just holding the stick differently.
        /// </summary>
        public void ApplyHandlingProfile(
            float newPitchRate, float newRollRate, float newTurnAssistRate,
            float newMinSpeed, float newMaxSpeed, float newCruiseSpeed)
        {
            pitchRate = newPitchRate;
            rollRate = newRollRate;
            turnAssistRate = newTurnAssistRate;

            minSpeed = newMinSpeed;
            maxSpeed = Mathf.Max(newMaxSpeed, newMinSpeed);
            cruiseSpeed = Mathf.Clamp(newCruiseSpeed, minSpeed, maxSpeed);

            currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
        }

        private void Start()
        {
            if (!photonView.IsMine)
            {
                enabled = false;
                return;
            }

            if (pilot == null)
            {
                Debug.LogError(
                    $"[SkyArena] {name} has no IFlightInput component. Add HumanPilot (player) or " +
                    "AiPilot (bot) to the prefab, or run SkyArena > Build Everything to regenerate it. " +
                    "Without one the aircraft will fly straight and ignore all controls.");
            }
        }

        /// <summary>Restores cruise speed. Called by <see cref="HealthSystem"/> on respawn.</summary>
        public void ResetFlight()
        {
            currentSpeed = cruiseSpeed;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // A dead plane coasts straight and level until it respawns.
            bool isDead = health != null && health.IsDead;

            float roll = 0f;
            float pitch = 0f;
            float throttle = 0f;

            if (pilot != null && !isDead)
            {
                roll = pilot.Roll;
                pitch = pilot.Pitch;
                throttle = pilot.Throttle;
            }

            currentSpeed = Mathf.Clamp(
                currentSpeed + throttle * throttleAcceleration * dt, minSpeed, maxSpeed);

            Quaternion rotation = ApplySteering(body.rotation, roll, pitch, dt);
            rotation = ApplyBoundarySteering(rotation, body.position, dt);
            body.MoveRotation(rotation);

            Vector3 nextPosition = body.position + (rotation * Vector3.forward) * currentSpeed * dt;
            nextPosition.y = Mathf.Clamp(nextPosition.y, minAltitude, maxAltitude);
            body.MovePosition(nextPosition);
        }

        /// <summary>
        /// Unity's axes: +X pitches the nose down, +Y yaws right, +Z rolls left.
        /// The sign flips below convert joystick space into that convention.
        /// </summary>
        private Quaternion ApplySteering(Quaternion rotation, float roll, float pitch, float dt)
        {
            float pitchSign = invertPitch ? 1f : -1f;

            float pitchDelta = pitchSign * pitch * pitchRate * dt;
            float rollDelta = -roll * rollRate * dt;
            float yawDelta = roll * turnAssistRate * dt;

            return rotation * Quaternion.Euler(pitchDelta, yawDelta, rollDelta);
        }

        /// <summary>
        /// Gently turns the plane back toward the middle of the arena once it
        /// strays past the boundary, instead of hard-clamping its position.
        /// </summary>
        private Quaternion ApplyBoundarySteering(Quaternion rotation, Vector3 position, float dt)
        {
            Vector2 horizontal = new Vector2(position.x, position.z);
            if (horizontal.magnitude <= arenaRadius) return rotation;

            Vector3 towardCentre = new Vector3(-position.x, 0f, -position.z).normalized;
            if (towardCentre.sqrMagnitude < 0.0001f) return rotation;

            Quaternion facingCentre = Quaternion.LookRotation(towardCentre, Vector3.up);
            return Quaternion.RotateTowards(rotation, facingCentre, boundarySteerRate * dt);
        }
    }
}
