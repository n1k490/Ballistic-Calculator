namespace PrecisionBallistics.Core;

/// <summary>Direction of barrel rifling twist (affects spin-drift sign).</summary>
public enum TwistDirection
{
    Right,
    Left
}

/// <summary>Output adjustment unit for elevation/windage solutions.</summary>
public enum AngularUnit
{
    Mil,
    Moa
}

/// <summary>
/// Cartridge / projectile definition. Diameter, weight and length are used both
/// for the drag scaling (via BC) and for the Miller gyroscopic-stability factor.
/// </summary>
public sealed class Cartridge
{
    public string Name { get; set; } = "Custom load";

    /// <summary>Bullet diameter (calibre) in inches, e.g. 0.264 for 6.5 mm.</summary>
    public double DiameterIn { get; set; } = 0.264;

    /// <summary>Bullet mass in grains.</summary>
    public double WeightGr { get; set; } = 140.0;

    /// <summary>Bullet overall length in inches (for stability calc; optional but recommended).</summary>
    public double LengthIn { get; set; } = 1.345;

    /// <summary>Ballistic coefficient value (in the convention of <see cref="DragModel"/>), lb/in^2.</summary>
    public double BallisticCoefficient { get; set; } = 0.315;

    /// <summary>Reference drag model for <see cref="BallisticCoefficient"/>.</summary>
    public DragModel DragModel { get; set; } = DragModel.G7;

    /// <summary>Custom drag curve Mach values (paired with <see cref="CustomCd"/>). Used when <see cref="DragModel"/> is Custom.</summary>
    public double[]? CustomMach { get; set; }

    /// <summary>Custom drag curve Cd values (paired with <see cref="CustomMach"/>).</summary>
    public double[]? CustomCd { get; set; }

    /// <summary>True if a usable custom drag curve is present.</summary>
    public bool HasCustomCurve =>
        CustomMach != null && CustomCd != null &&
        CustomMach.Length >= 2 && CustomMach.Length == CustomCd.Length;

    /// <summary>Muzzle velocity in feet per second.</summary>
    public double MuzzleVelocityFps { get; set; } = 2710.0;

    /// <summary>Muzzle-velocity temperature sensitivity (fps per degree C). 0 disables MV temp correction.</summary>
    public double MvTempSensitivityFpsPerC { get; set; } = 0.0;

    /// <summary>Temperature at which <see cref="MuzzleVelocityFps"/> was measured (deg C).</summary>
    public double MvReferenceTempC { get; set; } = 15.0;

    public Cartridge Clone() => (Cartridge)MemberwiseClone();
}

/// <summary>Rifle definition: barrel twist and optic mounting geometry.</summary>
public sealed class Rifle
{
    public string Name { get; set; } = "Custom rifle";

    /// <summary>Barrel twist rate in inches per turn (e.g. 8.0 for 1:8").</summary>
    public double TwistRateIn { get; set; } = 8.0;

    public TwistDirection TwistDirection { get; set; } = TwistDirection.Right;

    /// <summary>Height of the optical axis above the bore axis, inches.</summary>
    public double SightHeightIn { get; set; } = 2.0;

    public Rifle Clone() => (Rifle)MemberwiseClone();
}

/// <summary>Optic / scope definition.</summary>
public sealed class Scope
{
    public string Name { get; set; } = "Custom optic";

    /// <summary>Preferred adjustment unit shown for dial values.</summary>
    public AngularUnit Unit { get; set; } = AngularUnit.Mil;

    /// <summary>Turret click value in the chosen unit (e.g. 0.1 mil, or 0.25 moa).</summary>
    public double ClickValue { get; set; } = 0.1;

    /// <summary>Optional fixed scope-mount cant, in MOA (e.g. 20 for a 20 MOA rail). Informational.</summary>
    public double BaseMountMoa { get; set; } = 20.0;

    public Scope Clone() => (Scope)MemberwiseClone();
}
