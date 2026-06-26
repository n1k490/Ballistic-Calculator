namespace PrecisionBallistics.Core;

/// <summary>
/// A complete, saveable shooting profile: a rifle, a cartridge/load, an optic,
/// and the zero range they were sighted in at.
/// </summary>
public sealed class Profile
{
    public string Name { get; set; } = "New profile";
    public Rifle Rifle { get; set; } = new();
    public Cartridge Cartridge { get; set; } = new();
    public Scope Scope { get; set; } = new();

    /// <summary>Zero range in metres.</summary>
    public double ZeroRangeM { get; set; } = 91.44; // 100 yd

    /// <summary>Atmosphere the rifle was zeroed in (used so re-zeroing tracks conditions). Optional.</summary>
    public Atmosphere ZeroAtmosphere { get; set; } = Atmosphere.Standard();

    public Profile DeepClone() => new()
    {
        Name = Name,
        Rifle = Rifle.Clone(),
        Cartridge = Cartridge.Clone(),
        Scope = Scope.Clone(),
        ZeroRangeM = ZeroRangeM,
        ZeroAtmosphere = new Atmosphere
        {
            TemperatureC = ZeroAtmosphere.TemperatureC,
            PressureInHg = ZeroAtmosphere.PressureInHg,
            HumidityPct = ZeroAtmosphere.HumidityPct,
            PressureIsSeaLevel = ZeroAtmosphere.PressureIsSeaLevel,
            AltitudeM = ZeroAtmosphere.AltitudeM
        }
    };
}
