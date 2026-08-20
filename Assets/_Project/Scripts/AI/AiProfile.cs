using UnityEngine;

namespace SkyArena.AI
{
    public enum AiDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// The complete personality of an AI pilot.
    ///
    /// Difficulty is not a single multiplier: an Easy bot is slower AND turns
    /// lazily AND reacts late AND aims badly AND never reaches for a missile.
    /// Separating those knobs means you can tune "hard but fair" (accurate,
    /// still slow) rather than only "fast and unhittable".
    /// </summary>
    [System.Serializable]
    public class AiProfile
    {
        [Header("Speed")]
        [Tooltip("Speed the bot settles at when it is not chasing anyone.")]
        public float cruiseSpeed = 52f;
        public float maxSpeed = 85f;
        public float minSpeed = 25f;

        [Header("Manoeuvring")]
        [Tooltip("Degrees per second the bot can pitch and roll. Higher = tighter turns.")]
        public float pitchRate = 50f;
        public float rollRate = 110f;
        public float turnAssistRate = 28f;

        [Tooltip("Seconds the bot takes to move the stick to where it wants it. Higher = sluggish.")]
        public float reactionTime = 0.28f;

        [Header("Accuracy")]
        [Tooltip("Peak aiming error in degrees. The bot steers at a wandering offset from the true target.")]
        public float aimErrorDegrees = 5f;

        [Tooltip("How quickly the aiming error wanders. Higher = jittery, lower = a slow drift.")]
        public float aimErrorSpeed = 0.6f;

        [Tooltip("0 aims where the target is now, 1 aims where it will be. Only affects pursuit steering.")]
        [Range(0f, 1f)] public float leadPrediction = 0.6f;

        [Header("Aggression")]
        [Tooltip("Distance at which the bot starts hunting a target.")]
        public float engageRange = 1100f;

        [Tooltip("The bot only pulls the trigger inside this distance.")]
        public float gunRange = 420f;

        [Tooltip("The bot only pulls the trigger when the target is within this angle of its nose.")]
        public float gunConeDegrees = 9f;

        [Tooltip("Closer than this and the bot breaks off rather than ramming.")]
        public float breakOffDistance = 70f;

        [Tooltip("Chance from 0 to 1 that the bot takes a missile shot once it has a lock.")]
        [Range(0f, 1f)] public float missileWillingness = 0.45f;

        [Header("Self preservation")]
        [Tooltip("Below this fraction of health the bot disengages and runs.")]
        [Range(0f, 1f)] public float evadeHealthFraction = 0.3f;

        [Tooltip("Seconds the bot keeps running once it decides to evade.")]
        public float evadeDuration = 4f;

        /// <summary>Returns the tuned preset for a difficulty setting.</summary>
        public static AiProfile For(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy:
                    return new AiProfile
                    {
                        cruiseSpeed = 40f, maxSpeed = 62f, minSpeed = 25f,
                        pitchRate = 34f, rollRate = 70f, turnAssistRate = 18f,
                        reactionTime = 0.55f,
                        aimErrorDegrees = 13f, aimErrorSpeed = 0.9f, leadPrediction = 0f,
                        engageRange = 850f, gunRange = 300f, gunConeDegrees = 13f,
                        breakOffDistance = 95f, missileWillingness = 0f,
                        evadeHealthFraction = 0.45f, evadeDuration = 6f
                    };

                case AiDifficulty.Hard:
                    return new AiProfile
                    {
                        cruiseSpeed = 64f, maxSpeed = 108f, minSpeed = 30f,
                        pitchRate = 62f, rollRate = 135f, turnAssistRate = 36f,
                        reactionTime = 0.10f,
                        aimErrorDegrees = 1.8f, aimErrorSpeed = 0.35f, leadPrediction = 1f,
                        engageRange = 1500f, gunRange = 520f, gunConeDegrees = 7f,
                        breakOffDistance = 50f, missileWillingness = 0.9f,
                        evadeHealthFraction = 0.2f, evadeDuration = 2.5f
                    };

                default: // Normal
                    return new AiProfile();
            }
        }
    }
}
