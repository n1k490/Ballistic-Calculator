using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrecisionBallistics.Core;

/// <summary>
/// Saves and loads profiles as JSON (using the built-in System.Text.Json — no
/// external dependencies), and supplies a small library of starter profiles.
/// </summary>
public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(IEnumerable<Profile> profiles, string path)
    {
        var json = JsonSerializer.Serialize(new List<Profile>(profiles), Options);
        File.WriteAllText(path, json);
    }

    public static List<Profile> Load(string path)
    {
        if (!File.Exists(path)) return new List<Profile>();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<Profile>>(json, Options) ?? new List<Profile>();
    }

    /// <summary>Save a single profile to its own JSON file (for sharing).</summary>
    public static void SaveOne(Profile profile, string path)
    {
        var json = JsonSerializer.Serialize(profile, Options);
        File.WriteAllText(path, json);
    }

    /// <summary>Load a single profile from a JSON file.</summary>
    public static Profile? LoadOne(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Profile>(json, Options);
    }

    /// <summary>Default application data path for profiles.</summary>
    public static string DefaultPath()
    {
        var dir = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "PrecisionBallistics");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "profiles.json");
    }

    /// <summary>A few sensible starter profiles, including a 6.5 Creedmoor match load.</summary>
    public static List<Profile> DefaultLibrary() => new()
    {
        new Profile
        {
            Name = "6.5 Creedmoor — 140gr ELD-M",
            Rifle = new Rifle { Name = "Savage 110 Elite Precision", TwistRateIn = 8.0, TwistDirection = TwistDirection.Right, SightHeightIn = 2.0 },
            Cartridge = new Cartridge
            {
                Name = "Hornady 140gr ELD Match",
                DiameterIn = 0.264, WeightGr = 140, LengthIn = 1.345,
                BallisticCoefficient = 0.315, DragModel = DragModel.G7,
                MuzzleVelocityFps = 2710, MvTempSensitivityFpsPerC = 0, MvReferenceTempC = 15
            },
            Scope = new Scope { Name = "Nightforce ATACR 7-35x56 F1", Unit = AngularUnit.Mil, ClickValue = 0.1, BaseMountMoa = 20 },
            ZeroRangeM = 100
        },
        new Profile
        {
            Name = "308 Win — 175gr SMK",
            Rifle = new Rifle { Name = "308 rifle", TwistRateIn = 10.0, SightHeightIn = 1.9 },
            Cartridge = new Cartridge
            {
                Name = "Sierra 175gr MatchKing",
                DiameterIn = 0.308, WeightGr = 175, LengthIn = 1.240,
                BallisticCoefficient = 0.243, DragModel = DragModel.G7, MuzzleVelocityFps = 2600
            },
            Scope = new Scope { Unit = AngularUnit.Mil, ClickValue = 0.1, BaseMountMoa = 20 },
            ZeroRangeM = 91.44
        },
        new Profile
        {
            Name = "223 Rem — 77gr SMK",
            Rifle = new Rifle { Name = "223 rifle", TwistRateIn = 7.7, SightHeightIn = 2.6 },
            Cartridge = new Cartridge
            {
                Name = "Sierra 77gr MatchKing",
                DiameterIn = 0.224, WeightGr = 77, LengthIn = 0.990,
                BallisticCoefficient = 0.200, DragModel = DragModel.G7, MuzzleVelocityFps = 2750
            },
            Scope = new Scope { Unit = AngularUnit.Mil, ClickValue = 0.1, BaseMountMoa = 20 },
            ZeroRangeM = 91.44
        }
    };
}
