namespace SkyArena.Inputs
{
    /// <summary>
    /// Everything a pilot can ask an aircraft to do.
    ///
    /// Flight and weapon components read this interface off their own
    /// GameObject instead of reaching for the touch UI, which is what lets a
    /// human-flown plane and an AI-flown plane share the exact same physics,
    /// guns and missiles. The only difference between the two prefabs is which
    /// component supplies these five values.
    /// </summary>
    public interface IFlightInput
    {
        /// <summary>-1 rolls left, +1 rolls right. Yaw follows the bank.</summary>
        float Roll { get; }

        /// <summary>-1 pushes the nose down, +1 pulls it up.</summary>
        float Pitch { get; }

        /// <summary>+1 accelerates, -1 brakes, 0 holds the current speed.</summary>
        float Throttle { get; }

        bool GunHeld { get; }

        bool MissileHeld { get; }
    }
}
