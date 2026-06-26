using System;

namespace PrecisionBallistics.Core;

/// <summary>Gyroscopic-stability calculations (Miller stability formula).</summary>
public static class Stability
{
    /// <summary>
    /// Miller gyroscopic stability factor (SG), corrected for muzzle velocity
    /// and atmosphere. SG &gt; 1.4 is comfortably stable; 1.0–1.4 marginal; &lt; 1.0 unstable.
    /// </summary>
    public static double MillerSg(Cartridge c, Rifle r, double muzzleVelocityFps, Atmosphere atm)
    {
        if (c.LengthIn <= 0 || c.DiameterIn <= 0 || r.TwistRateIn <= 0)
            return 0;

        double t = r.TwistRateIn / c.DiameterIn;   // twist in calibres
        double l = c.LengthIn / c.DiameterIn;      // length in calibres

        double sg = 30.0 * c.WeightGr /
                    (t * t * Math.Pow(c.DiameterIn, 3) * l * (1.0 + l * l));

        // Velocity correction (normalised to 2800 fps).
        sg *= Math.Pow(muzzleVelocityFps / 2800.0, 1.0 / 3.0);

        // Atmosphere correction to standard (59 F, 29.92 inHg).
        double tempF = Units.CelsiusToFahrenheit(atm.TemperatureC);
        double pressInHg = atm.StationPressurePa() / Units.InHgToPa;
        sg *= (tempF + 460.0) / 519.0;
        sg *= 29.92 / pressInHg;

        return sg;
    }
}
