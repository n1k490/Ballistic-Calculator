using System;

namespace PrecisionBallistics.Core;

/// <summary>
/// Atmospheric conditions and the derived air density / speed of sound used by
/// the trajectory solver. Density is computed from temperature, station
/// pressure and relative humidity using the partial-pressure (humid air) model.
/// </summary>
public sealed class Atmosphere
{
    /// <summary>Air temperature, degrees Celsius.</summary>
    public double TemperatureC { get; set; } = 15.0;

    /// <summary>Station (absolute) pressure in inches of mercury. NOT sea-level corrected.</summary>
    public double PressureInHg { get; set; } = 29.92;

    /// <summary>Relative humidity, percent (0..100).</summary>
    public double HumidityPct { get; set; } = 50.0;

    /// <summary>
    /// True if <see cref="PressureInHg"/> is a sea-level (corrected/altimeter)
    /// value that must be reduced to station pressure using <see cref="AltitudeM"/>.
    /// </summary>
    public bool PressureIsSeaLevel { get; set; } = false;

    /// <summary>Shooter altitude in metres (used only when correcting sea-level pressure).</summary>
    public double AltitudeM { get; set; } = 0.0;

    /// <summary>ICAO standard sea-level conditions.</summary>
    public static Atmosphere Standard() => new()
    {
        TemperatureC = 15.0,
        PressureInHg = 29.92,
        HumidityPct = 0.0,
        PressureIsSeaLevel = false,
        AltitudeM = 0.0
    };

    /// <summary>Station pressure in pascals (applies altitude reduction if the input was sea-level).</summary>
    public double StationPressurePa()
    {
        double p = PressureInHg * Units.InHgToPa;
        if (PressureIsSeaLevel && AltitudeM != 0.0)
        {
            // Barometric reduction to station altitude (ISA troposphere).
            const double L = 0.0065, T0 = 288.15, g = 9.80665, M = 0.0289644, R = 8.3144598;
            p *= Math.Pow(1.0 - L * AltitudeM / T0, g * M / (R * L));
        }
        return p;
    }

    /// <summary>Air density in kg/m^3 for humid air (Tetens saturation model).</summary>
    public double DensityKgM3()
    {
        double tC = TemperatureC;
        double tK = Units.CelsiusToKelvin(tC);
        double p = StationPressurePa();
        double pSat = 610.78 * Math.Exp(17.27 * tC / (tC + 237.3)); // Pa
        double pv = Math.Clamp(HumidityPct, 0, 100) / 100.0 * pSat;
        double pd = p - pv;
        return pd / (287.058 * tK) + pv / (461.495 * tK);
    }

    /// <summary>Speed of sound in m/s for the current temperature (dry-air approximation).</summary>
    public double SpeedOfSoundMps() => Math.Sqrt(1.4 * 287.058 * Units.CelsiusToKelvin(TemperatureC));

    /// <summary>
    /// Density altitude in feet — a single number that captures the combined
    /// effect of pressure, temperature and humidity on air density. Useful for
    /// quick field comparison and for "truing" a profile to conditions.
    /// </summary>
    public double DensityAltitudeFt()
    {
        double rho = DensityKgM3();
        const double rho0 = 1.225;           // ISA sea-level density
        const double L = 0.0065, T0 = 288.15;
        const double exponent = 1.0 / 4.2558797; // 1/(g*M/(R*L) - 1)
        double ratio = rho / rho0;
        double hMeters = T0 / L * (1.0 - Math.Pow(ratio, exponent));
        return hMeters / Units.Foot;
    }
}
