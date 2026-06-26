namespace PrecisionBallistics.Core;

/// <summary>
/// Unit-conversion constants and helpers. The solver works internally in SI
/// (metres, metres/second, kilograms). These constants convert to/from the
/// imperial units common in ballistics (yards, feet/s, grains, inches, inHg).
/// </summary>
public static class Units
{
    // Length
    public const double Foot = 0.3048;          // m
    public const double Inch = 0.0254;          // m
    public const double Yard = 0.9144;          // m
    public const double Metre = 1.0;            // m

    // Mass
    public const double Grain = 6.479891e-5;    // kg
    public const double Pound = 0.45359237;     // kg

    // Pressure
    public const double InHgToPa = 3386.389;    // Pa per inHg
    public const double HpaToPa = 100.0;        // Pa per hPa/mbar

    // Ballistic coefficient: 1 lb/in^2 -> kg/m^2
    public const double BcLbIn2ToKgM2 = 703.069;

    // Physics
    public const double Gravity = 9.80665;      // m/s^2
    public const double OmegaEarth = 7.292115e-5; // rad/s (sidereal)

    // Angular
    public const double MoaPerRadian = 180.0 / System.Math.PI * 60.0;  // ~3437.75
    public const double MilPerRadian = 1000.0;                          // exact-mil (NATO mrad)

    // Energy
    public const double JouleToFtLb = 1.0 / 1.355817948;

    // Temperature
    public static double FahrenheitToCelsius(double f) => (f - 32.0) * 5.0 / 9.0;
    public static double CelsiusToFahrenheit(double c) => c * 9.0 / 5.0 + 32.0;
    public static double CelsiusToKelvin(double c) => c + 273.15;

    // Speed
    public static double FpsToMps(double fps) => fps * Foot;
    public static double MpsToFps(double mps) => mps / Foot;
    public static double MpsToKmh(double mps) => mps * 3.6;
    public static double MphToMps(double mph) => mph * 0.44704;
    public static double MpsToMph(double mps) => mps / 0.44704;

    // Distance
    public static double YardToM(double yd) => yd * Yard;
    public static double MToYard(double m) => m / Yard;
    public static double MToInch(double m) => m / Inch;
    public static double MToCm(double m) => m * 100.0;
    public static double MToFoot(double m) => m / Foot;

    // Angle of an offset (perpendicular metres) at a given range (metres)
    public static double Mil(double offsetM, double rangeM) =>
        rangeM <= 0 ? 0 : (offsetM / rangeM) * MilPerRadian;

    public static double Moa(double offsetM, double rangeM) =>
        rangeM <= 0 ? 0 : System.Math.Atan2(offsetM, rangeM) * MoaPerRadian;
}
