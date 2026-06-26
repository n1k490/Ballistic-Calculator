using System.Collections.Generic;

namespace PrecisionBallistics.Core;

/// <summary>A single computed point along the trajectory at a specific range.</summary>
public sealed class TrajectoryPoint
{
    public double RangeM { get; init; }
    public double RangeYd => Units.MToYard(RangeM);

    /// <summary>Vertical position of the bullet relative to the line of sight, metres (negative = below LOS / drop).</summary>
    public double DropM { get; init; }
    public double DropInch => Units.MToInch(DropM);
    public double DropCm => Units.MToCm(DropM);

    /// <summary>Total horizontal deflection (wind + spin drift + Coriolis), metres (positive = right).</summary>
    public double WindageM { get; init; }
    public double WindageInch => Units.MToInch(WindageM);
    public double WindageCm => Units.MToCm(WindageM);

    public double WindDeflectionM { get; init; }
    public double SpinDriftM { get; init; }
    public double CoriolisHorizM { get; init; }
    public double CoriolisVertM { get; init; }
    public double AeroJumpM { get; init; }

    /// <summary>Elevation correction to dial, MIL (positive = come up).</summary>
    public double ElevationMil { get; init; }
    public double ElevationMoa { get; init; }

    /// <summary>Windage correction to dial, MIL (positive = right).</summary>
    public double WindageMil { get; init; }
    public double WindageMoa { get; init; }

    public double VelocityMps { get; init; }
    public double VelocityFps => Units.MpsToFps(VelocityMps);
    public double Mach { get; init; }

    /// <summary>Remaining kinetic energy, joules.</summary>
    public double EnergyJ { get; init; }
    public double EnergyFtLb => EnergyJ * Units.JouleToFtLb;

    public double TimeOfFlightS { get; init; }
    public bool Supersonic => Mach >= 1.0;
}

/// <summary>Full trajectory solution plus the metadata that produced it.</summary>
public sealed class TrajectorySolution
{
    public List<TrajectoryPoint> Points { get; } = new();
    public double ZeroAngleRad { get; init; }
    public double ZeroAngleMoa { get; init; }
    public double AirDensityKgM3 { get; init; }
    public double SpeedOfSoundMps { get; init; }
    public double DensityAltitudeFt { get; init; }
    public double StabilityFactor { get; init; }
    public double EffectiveMuzzleVelocityFps { get; init; }

    /// <summary>Range at which the bullet first drops below Mach 1 (metres), or 0 if it stays supersonic.</summary>
    public double TransonicRangeM { get; init; }
}
