using Photon.Pun;
using SkyArena.Combat;
using SkyArena.Flight;
using SkyArena.Inputs;
using UnityEngine;

namespace SkyArena.AI
{
    /// <summary>
    /// A bot pilot. Produces exactly the same five inputs a human thumb does,
    /// so it flies the same physics and fires the same guns and missiles as a
    /// player - it simply decides where to point.
    ///
    /// Only the client that owns this PhotonView thinks. Every other client
    /// sees the bot purely through NetworkTransformSync, which means the bot
    /// cannot desync: there is exactly one brain per bot in the whole room.
    ///
    /// Behaviour is priority-ordered rather than a state machine, because at
    /// this size an explicit ordering (do not hit the ground, do not leave the
    /// arena, run if hurt, otherwise fight) is easier to follow than a graph of
    /// transitions.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(FlightController))]
    public class AiPilot : MonoBehaviourPun, IFlightInput
    {
        [Header("Difficulty")]
        [SerializeField] private AiDifficulty difficulty = AiDifficulty.Normal;

        [Tooltip("Ignore the difficulty preset and use the values below instead.")]
        [SerializeField] private bool useCustomProfile;
        [SerializeField] private AiProfile customProfile = new AiProfile();

        [Header("Airmanship")]
        [Tooltip("The bot pulls up hard below this altitude.")]
        [SerializeField] private float safeAltitude = 90f;
        [SerializeField] private float patrolAltitude = 240f;
        [SerializeField] private float arenaRadius = 1300f;
        [SerializeField] private float targetRefreshInterval = 0.5f;

        [Tooltip("How sharply the bot converts an off-nose target into stick deflection.")]
        [SerializeField] private float steerGain = 2.6f;

        private AiProfile profile;
        private FlightController flight;
        private HealthSystem health;
        private LockOnSystem lockOn;

        private Targetable target;
        private Vector3 lastTargetPosition;
        private Vector3 targetVelocity;

        private float nextTargetSearch;
        private float evadeUntil;
        private float noiseSeed;

        private bool isActive;
        private bool hadLock;
        private bool committedToMissile;

        private float roll;
        private float pitch;
        private float throttle;
        private bool gunHeld;
        private bool missileHeld;

        public AiDifficulty Difficulty => difficulty;
        public Transform CurrentTarget => target != null ? target.transform : null;

        // ------------------------------------------------------- IFlightInput

        float IFlightInput.Roll => isActive ? roll : 0f;
        float IFlightInput.Pitch => isActive ? pitch : 0f;
        float IFlightInput.Throttle => isActive ? throttle : 0f;
        bool IFlightInput.GunHeld => isActive && gunHeld;
        bool IFlightInput.MissileHeld => isActive && missileHeld;

        // ----------------------------------------------------------- Lifetime

        private void Awake()
        {
            flight = GetComponent<FlightController>();
            health = GetComponent<HealthSystem>();
            lockOn = GetComponent<LockOnSystem>();
            noiseSeed = Random.Range(0f, 1000f);
            profile = AiProfile.For(difficulty);
        }

        private void Start()
        {
            // The spawner passes difficulty through Photon so every client
            // agrees on which bot this is, even though only the owner acts.
            object[] data = photonView.InstantiationData;
            if (data != null && data.Length > 0 && data[0] is int packed)
            {
                difficulty = (AiDifficulty)Mathf.Clamp(packed, 0, 2);
            }

            profile = useCustomProfile ? customProfile : AiProfile.For(difficulty);

            isActive = photonView.IsMine;
            gameObject.name = $"Bot [{difficulty}]";

            if (!isActive) return;

            flight.ApplyHandlingProfile(
                profile.pitchRate, profile.rollRate, profile.turnAssistRate,
                profile.minSpeed, profile.maxSpeed, profile.cruiseSpeed);
        }

        /// <summary>Lets a spawner set difficulty on a locally created bot.</summary>
        public void SetDifficulty(AiDifficulty value)
        {
            difficulty = value;
            profile = useCustomProfile ? customProfile : AiProfile.For(value);
        }

        private void Update()
        {
            if (!isActive) return;

            if (health != null && health.IsDead)
            {
                Relax();
                return;
            }

            RefreshTarget();
            TrackTargetVelocity();
            Decide();
        }

        private void Relax()
        {
            roll = 0f;
            pitch = 0f;
            throttle = 0f;
            gunHeld = false;
            missileHeld = false;
            target = null;
            hadLock = false;
        }

        // ------------------------------------------------------ Target picking

        private void RefreshTarget()
        {
            // Drop a target that died or despawned between searches.
            if (target != null && (!target.IsAlive || target.OwnerView == null)) target = null;

            if (Time.time < nextTargetSearch) return;
            nextTargetSearch = Time.time + targetRefreshInterval;

            Targetable best = null;
            float bestScore = float.MaxValue;
            Vector3 position = transform.position;

            var candidates = Targetable.All;
            for (int i = 0; i < candidates.Count; i++)
            {
                Targetable candidate = candidates[i];
                if (candidate == null || candidate.OwnerView == null) continue;
                if (candidate.ViewID == photonView.ViewID) continue;
                if (!candidate.IsAlive) continue;

                float distance = Vector3.Distance(position, candidate.transform.position);
                if (distance > profile.engageRange) continue;

                // Bots are scored as if they were further away, so a bot always
                // prefers to hunt a human when both are in range.
                float score = candidate.IsBot ? distance * 2.5f : distance;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best != target)
            {
                target = best;
                lastTargetPosition = best != null ? best.transform.position : Vector3.zero;
                targetVelocity = Vector3.zero;
                hadLock = false;
            }
        }

        private void TrackTargetVelocity()
        {
            if (target == null) return;

            Vector3 position = target.transform.position;
            if (Time.deltaTime > 0f)
            {
                Vector3 sample = (position - lastTargetPosition) / Time.deltaTime;
                targetVelocity = Vector3.Lerp(targetVelocity, sample, 0.25f);
            }
            lastTargetPosition = position;
        }

        // ----------------------------------------------------------- Decisions

        private void Decide()
        {
            Vector3 position = transform.position;

            gunHeld = false;
            missileHeld = false;

            Vector3 desired;
            float wantedSpeed;

            if (target == null)
            {
                desired = PatrolDirection(position);
                wantedSpeed = profile.cruiseSpeed;
            }
            else
            {
                Vector3 toTarget = target.transform.position - position;
                float distance = toTarget.magnitude;

                if (ShouldEvade())
                {
                    // Run, and climb while running: altitude is escape options.
                    desired = (-toTarget.normalized + Vector3.up * 0.35f).normalized;
                    wantedSpeed = profile.maxSpeed;
                }
                else if (distance < profile.breakOffDistance)
                {
                    // Too close to turn onto them; fly through and come around.
                    desired = transform.forward;
                    wantedSpeed = profile.maxSpeed;
                }
                else
                {
                    desired = ApplyAimError((PredictAimPoint(distance) - position).normalized);

                    float angleToTarget = Vector3.Angle(transform.forward, toTarget);
                    gunHeld = distance <= profile.gunRange && angleToTarget <= profile.gunConeDegrees;
                    missileHeld = WantsMissile();

                    wantedSpeed = distance > profile.gunRange ? profile.maxSpeed : profile.cruiseSpeed;
                }
            }

            desired = ApplySafetyOverrides(position, desired);
            SteerToward(desired);
            throttle = TrimToSpeed(wantedSpeed);
        }

        private bool ShouldEvade()
        {
            if (Time.time < evadeUntil) return true;
            if (health == null) return false;
            if (health.HealthNormalised > profile.evadeHealthFraction) return false;

            evadeUntil = Time.time + profile.evadeDuration;
            return true;
        }

        /// <summary>
        /// Rolls the dice once per lock rather than once per frame, so
        /// willingness reads as "this bot decided to take the shot" instead of
        /// a stutter of half-pressed triggers.
        /// </summary>
        private bool WantsMissile()
        {
            if (lockOn == null) return false;

            if (!lockOn.IsFullyLocked)
            {
                hadLock = false;
                return false;
            }

            if (!hadLock)
            {
                hadLock = true;
                committedToMissile = Random.value < profile.missileWillingness;
            }

            return committedToMissile;
        }

        private Vector3 PredictAimPoint(float distance)
        {
            Vector3 aimPoint = target.transform.position;
            if (profile.leadPrediction <= 0f) return aimPoint;

            float closingSpeed = Mathf.Max(20f, flight != null ? flight.CurrentSpeed : 50f);
            float timeToReach = distance / closingSpeed;
            return aimPoint + targetVelocity * timeToReach * profile.leadPrediction;
        }

        private Vector3 ApplyAimError(Vector3 direction)
        {
            if (profile.aimErrorDegrees <= 0f) return direction;

            float t = Time.time * profile.aimErrorSpeed;
            float yawError = (Mathf.PerlinNoise(t, noiseSeed) - 0.5f) * 2f * profile.aimErrorDegrees;
            float pitchError = (Mathf.PerlinNoise(noiseSeed, t) - 0.5f) * 2f * profile.aimErrorDegrees;

            return Quaternion.Euler(pitchError, yawError, 0f) * direction;
        }

        /// <summary>Ground and arena limits outrank whatever the bot wanted.</summary>
        private Vector3 ApplySafetyOverrides(Vector3 position, Vector3 desired)
        {
            if (position.y < safeAltitude)
            {
                float urgency = Mathf.InverseLerp(safeAltitude, safeAltitude * 0.4f, position.y);
                desired = Vector3.Slerp(desired, Vector3.up, urgency);
            }

            Vector2 horizontal = new Vector2(position.x, position.z);
            if (horizontal.magnitude > arenaRadius)
            {
                Vector3 towardCentre = new Vector3(-position.x, 0f, -position.z).normalized;
                desired = Vector3.Slerp(desired, towardCentre, 0.7f);
            }

            return desired.sqrMagnitude < 0.0001f ? transform.forward : desired.normalized;
        }

        private Vector3 PatrolDirection(Vector3 position)
        {
            Vector3 towardCentre = new Vector3(-position.x, 0f, -position.z);
            if (towardCentre.sqrMagnitude < 1f) return transform.forward;

            float distanceOut = towardCentre.magnitude;
            towardCentre /= distanceOut;

            // Circle the arena, pulling inward harder the further out it drifts.
            Vector3 tangent = Vector3.Cross(Vector3.up, towardCentre);
            float inwardPull = Mathf.InverseLerp(arenaRadius * 0.5f, arenaRadius, distanceOut);

            Vector3 direction = Vector3.Slerp(tangent, towardCentre, inwardPull);
            direction.y = Mathf.Clamp((patrolAltitude - position.y) * 0.004f, -0.4f, 0.4f);
            return direction.normalized;
        }

        // ------------------------------------------------------------- Control

        /// <summary>
        /// Converts a world-space heading into stick deflection, then eases the
        /// stick toward it so the bot has believable reaction lag.
        /// </summary>
        private void SteerToward(Vector3 worldDirection)
        {
            Vector3 local = transform.InverseTransformDirection(worldDirection);

            float wantedRoll;
            float wantedPitch;

            if (local.z < 0f)
            {
                // Target is behind us. Nudging the stick would trace a lazy arc,
                // so commit to a full-authority turn until the nose comes round.
                wantedRoll = local.x >= 0f ? 1f : -1f;
                wantedPitch = Mathf.Clamp(local.y * 2f, -1f, 1f);
            }
            else
            {
                wantedRoll = Mathf.Clamp(local.x * steerGain, -1f, 1f);
                wantedPitch = Mathf.Clamp(local.y * steerGain, -1f, 1f);
            }

            float responsiveness = profile.reactionTime <= 0.001f
                ? 1f
                : 1f - Mathf.Exp(-Time.deltaTime / profile.reactionTime);

            roll = Mathf.Lerp(roll, wantedRoll, responsiveness);
            pitch = Mathf.Lerp(pitch, wantedPitch, responsiveness);
        }

        private float TrimToSpeed(float wantedSpeed)
        {
            if (flight == null) return 0f;

            float difference = wantedSpeed - flight.CurrentSpeed;
            if (difference > 1.5f) return 1f;
            if (difference < -1.5f) return -1f;
            return 0f;
        }
    }
}
