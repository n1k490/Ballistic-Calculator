namespace PrecisionBallistics.Core;

/// <summary>
/// Conditions for a particular firing solution: atmosphere, wind, target range,
/// inclination, and geographic data used for the Coriolis correction.
/// </summary>
public sealed class ShotConditions
{
    public Atmosphere Atmosphere { get; set; } = Atmosphere.Standard();

    /// <summary>Wind speed in metres/second.</summary>
    public double WindSpeedMps { get; set; } = 0.0;

    /// <summary>
    /// Direction the wind is coming FROM, in degrees relative to the line of fire
    /// (clock/compass style): 0 = headwind (from target), 90 = from shooter's
    /// left to right, 180 = tailwind, 270 = from the right.
    /// </summary>
    public double WindFromDeg { get; set; } = 90.0;

    /// <summary>Maximum range to compute, in metres.</summary>
    public double MaxRangeM { get; set; } = 914.4; // 1000 yd

    /// <summary>Range increment for the trajectory table, in metres.</summary>
    public double RangeStepM { get; set; } = 91.44; // 100 yd

    /// <summary>Line-of-sight (look) angle to the target in degrees (+up / -down). Drives the rifleman's-rule correction.</summary>
    public double LookAngleDeg { get; set; } = 0.0;

    // --- Coriolis geometry (optional) ---
    public bool ApplyCoriolis { get; set; } = false;

    /// <summary>Shooter latitude in degrees (+N / -S).</summary>
    public double LatitudeDeg { get; set; } = 0.0;

    /// <summary>Azimuth of fire in degrees, clockwise from true north (0 = N, 90 = E).</summary>
    public double AzimuthDeg { get; set; } = 0.0;

    public bool ApplySpinDrift { get; set; } = true;
    public bool ApplyAeroJump { get; set; } = true;
}
