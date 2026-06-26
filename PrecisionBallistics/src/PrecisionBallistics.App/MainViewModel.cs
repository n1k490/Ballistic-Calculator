using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    /// <summary>A single formatted row in the trajectory output grid.</summary>
    public sealed class RowDisplay
    {
        public string Range { get; init; } = "";
        public string Elevation { get; init; } = "";
        public string Windage { get; init; } = "";
        public string Drop { get; init; } = "";
        public string Velocity { get; init; } = "";
        public string Energy { get; init; } = "";
        public string Mach { get; init; } = "";
        public string Tof { get; init; } = "";
        public bool Supersonic { get; init; } = true;
    }

    public sealed class MainViewModel : ViewModelBase
    {
        private readonly BallisticSolver _solver = new();

        public MainViewModel()
        {
            foreach (var p in ProfileStore.DefaultLibrary())
                Profiles.Add(p);
            _selectedProfile = Profiles.Count > 0 ? Profiles[0] : new Profile();

            SolveCommand = new RelayCommand(Solve);
            SaveCommand = new RelayCommand(Save);
            LoadCommand = new RelayCommand(Load);
            NewProfileCommand = new RelayCommand(NewProfile);
            DeleteProfileCommand = new RelayCommand(DeleteProfile, () => Profiles.Count > 1);

            Solve();
        }

        // ---- Profiles ----
        public ObservableCollection<Profile> Profiles { get; } = new();

        private Profile _selectedProfile;
        public Profile SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (Set(ref _selectedProfile, value))
                {
                    OnPropertyChanged(nameof(SelectedProfile));
                    OnPropertyChanged(nameof(ZeroRangeDisplay));
                    OnPropertyChanged(nameof(MuzzleVelocityDisplay));
                    Solve();
                }
            }
        }

        // ---- Shot conditions ----
        public ShotConditions Conditions { get; } = new()
        {
            Atmosphere = new Atmosphere { TemperatureC = 15, PressureInHg = 29.92, HumidityPct = 50 },
            WindSpeedMps = 4.0,
            WindFromDeg = 90,
            MaxRangeM = 1200,
            RangeStepM = 100,
            ApplySpinDrift = true,
            ApplyAeroJump = true
        };

        // ---- Enum sources for combo boxes ----
        public Array DragModels => Enum.GetValues(typeof(DragModel));
        public Array TwistDirections => Enum.GetValues(typeof(TwistDirection));
        public Array AngularUnits => Enum.GetValues(typeof(AngularUnit));

        // ---- Unit toggles ----
        private bool _useMoa;
        public bool UseMoa
        {
            get => _useMoa;
            set { if (Set(ref _useMoa, value)) { OnPropertyChanged(nameof(AngularUnitLabel)); Solve(); } }
        }

        private bool _useMetric = true;
        public bool UseMetric
        {
            get => _useMetric;
            set
            {
                if (Set(ref _useMetric, value))
                {
                    OnPropertyChanged(nameof(MaxRangeDisplay));
                    OnPropertyChanged(nameof(RangeStepDisplay));
                    OnPropertyChanged(nameof(ZeroRangeDisplay));
                    OnPropertyChanged(nameof(RangeUnitLabel));
                    OnPropertyChanged(nameof(WindSpeedDisplay));
                    OnPropertyChanged(nameof(WindUnitLabel));
                    OnPropertyChanged(nameof(WindMinDisplay));
                    OnPropertyChanged(nameof(WindMaxDisplay));
                    Solve();
                }
            }
        }

        public string AngularUnitLabel => UseMoa ? "MOA" : "MIL";
        public string RangeUnitLabel => UseMetric ? "m" : "yd";

        private bool _useMvMetric;
        public bool UseMvMetric
        {
            get => _useMvMetric;
            set
            {
                if (Set(ref _useMvMetric, value))
                {
                    OnPropertyChanged(nameof(MuzzleVelocityDisplay));
                    OnPropertyChanged(nameof(MvUnitLabel));
                }
            }
        }

        public string MvUnitLabel => UseMvMetric ? "m/s" : "fps";

        /// <summary>Muzzle velocity shown in the chosen unit (storage is always fps).</summary>
        public double MuzzleVelocityDisplay
        {
            get => UseMvMetric
                ? Units.FpsToMps(SelectedProfile.Cartridge.MuzzleVelocityFps)
                : SelectedProfile.Cartridge.MuzzleVelocityFps;
            set
            {
                SelectedProfile.Cartridge.MuzzleVelocityFps =
                    UseMvMetric ? Units.MpsToFps(value) : value;
                OnPropertyChanged();
            }
        }

        // Unit-aware proxies for the range fields.
        public double MaxRangeDisplay
        {
            get => UseMetric ? Conditions.MaxRangeM : Units.MToYard(Conditions.MaxRangeM);
            set { Conditions.MaxRangeM = UseMetric ? value : Units.YardToM(value); OnPropertyChanged(); }
        }

        public double RangeStepDisplay
        {
            get => UseMetric ? Conditions.RangeStepM : Units.MToYard(Conditions.RangeStepM);
            set { Conditions.RangeStepM = UseMetric ? value : Units.YardToM(value); OnPropertyChanged(); }
        }

        public double ZeroRangeDisplay
        {
            get => UseMetric ? SelectedProfile.ZeroRangeM : Units.MToYard(SelectedProfile.ZeroRangeM);
            set { SelectedProfile.ZeroRangeM = UseMetric ? value : Units.YardToM(value); OnPropertyChanged(); }
        }

        // ---- Wind unit (m/s metric, mph imperial) ----
        public string WindUnitLabel => UseMetric ? "m/s" : "mph";

        public double WindSpeedDisplay
        {
            get => UseMetric ? Conditions.WindSpeedMps : Units.MpsToMph(Conditions.WindSpeedMps);
            set { Conditions.WindSpeedMps = UseMetric ? value : Units.MphToMps(value); OnPropertyChanged(); }
        }

        // ---- Wind bracket ----
        private bool _windBracket;
        public bool WindBracket
        {
            get => _windBracket;
            set { if (Set(ref _windBracket, value)) Solve(); }
        }

        private double _windMinMps = 2.0;
        private double _windMaxMps = 6.0;

        public double WindMinDisplay
        {
            get => UseMetric ? _windMinMps : Units.MpsToMph(_windMinMps);
            set { _windMinMps = UseMetric ? value : Units.MphToMps(value); OnPropertyChanged(); }
        }

        public double WindMaxDisplay
        {
            get => UseMetric ? _windMaxMps : Units.MpsToMph(_windMaxMps);
            set { _windMaxMps = UseMetric ? value : Units.MphToMps(value); OnPropertyChanged(); }
        }

        // ---- Environment presets ----
        public string[] EnvironmentPresets { get; } =
        {
            "— preset —",
            "ICAO standard (15°C, 29.92)",
            "Cold (−10°C)",
            "Hot (35°C)",
            "High altitude (2000 m)",
            "Sea level humid (25°C, 80%)"
        };

        private string _selectedEnvironmentPreset = "— preset —";
        public string SelectedEnvironmentPreset
        {
            get => _selectedEnvironmentPreset;
            set { if (Set(ref _selectedEnvironmentPreset, value)) ApplyPreset(value); }
        }

        private void ApplyPreset(string name)
        {
            var a = Conditions.Atmosphere;
            switch (name)
            {
                case "ICAO standard (15°C, 29.92)":
                    a.TemperatureC = 15; a.PressureInHg = 29.92; a.HumidityPct = 0; a.AltitudeM = 0; a.PressureIsSeaLevel = false; break;
                case "Cold (−10°C)":
                    a.TemperatureC = -10; a.PressureInHg = 30.10; a.HumidityPct = 60; break;
                case "Hot (35°C)":
                    a.TemperatureC = 35; a.PressureInHg = 29.70; a.HumidityPct = 40; break;
                case "High altitude (2000 m)":
                    a.TemperatureC = 5; a.PressureInHg = 29.92; a.HumidityPct = 40; a.AltitudeM = 2000; a.PressureIsSeaLevel = true; break;
                case "Sea level humid (25°C, 80%)":
                    a.TemperatureC = 25; a.PressureInHg = 29.92; a.HumidityPct = 80; a.AltitudeM = 0; a.PressureIsSeaLevel = false; break;
                default:
                    return;
            }
            OnPropertyChanged(nameof(Conditions));
            Solve();
        }

        // ---- Results ----
        public ObservableCollection<RowDisplay> Rows { get; } = new();

        private string _zeroAngleText = "";
        public string ZeroAngleText { get => _zeroAngleText; set => Set(ref _zeroAngleText, value); }

        private string _densityAltitudeText = "";
        public string DensityAltitudeText { get => _densityAltitudeText; set => Set(ref _densityAltitudeText, value); }

        private string _stabilityText = "";
        public string StabilityText { get => _stabilityText; set => Set(ref _stabilityText, value); }

        private string _transonicText = "";
        public string TransonicText { get => _transonicText; set => Set(ref _transonicText, value); }

        private string _muzzleVelocityText = "";
        public string MuzzleVelocityText { get => _muzzleVelocityText; set => Set(ref _muzzleVelocityText, value); }

        private string _airDensityText = "";
        public string AirDensityText { get => _airDensityText; set => Set(ref _airDensityText, value); }

        // Latest solution exposed for the reticle view.
        public TrajectorySolution? LatestSolution { get; private set; }
        public bool LatestUseMoa { get; private set; }
        public event Action? Solved;

        // ---- Commands ----
        public ICommand SolveCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand NewProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }

        /// <summary>Apply a trued muzzle velocity (fps) to the current load and recompute.</summary>
        public void ApplyTruedMv(double fps)
        {
            if (SelectedProfile == null) return;
            SelectedProfile.Cartridge.MuzzleVelocityFps = fps;
            OnPropertyChanged(nameof(MuzzleVelocityDisplay));
            Solve();
        }

        /// <summary>Refresh bindings and recompute after a drag-model / custom-curve change.</summary>
        public void RefreshAfterDragChange()
        {
            OnPropertyChanged(nameof(SelectedProfile));
            Solve();
        }

        private void Solve()
        {
            var p = SelectedProfile;
            if (p == null) return;

            TrajectorySolution sol;
            try
            {
                sol = _solver.Solve(p.Cartridge, p.Rifle, p.Scope, p.ZeroRangeM, Conditions);
            }
            catch
            {
                return;
            }

            Rows.Clear();

            TrajectorySolution? solLow = null, solHigh = null;
            if (WindBracket)
            {
                try
                {
                    solLow = _solver.Solve(p.Cartridge, p.Rifle, p.Scope, p.ZeroRangeM, WithWind(_windMinMps));
                    solHigh = _solver.Solve(p.Cartridge, p.Rifle, p.Scope, p.ZeroRangeM, WithWind(_windMaxMps));
                }
                catch { solLow = solHigh = null; }
            }

            for (int i = 0; i < sol.Points.Count; i++)
            {
                double? wLo = solLow != null && i < solLow.Points.Count
                    ? (UseMoa ? solLow.Points[i].WindageMoa : solLow.Points[i].WindageMil) : (double?)null;
                double? wHi = solHigh != null && i < solHigh.Points.Count
                    ? (UseMoa ? solHigh.Points[i].WindageMoa : solHigh.Points[i].WindageMil) : (double?)null;
                Rows.Add(Format(sol.Points[i], wLo, wHi));
            }

            LatestSolution = sol;
            LatestUseMoa = UseMoa;

            ZeroAngleText = $"{sol.ZeroAngleMoa:F2} MOA  ({sol.ZeroAngleMoa / Units.MoaPerRadian * Units.MilPerRadian:F2} MIL)";
            DensityAltitudeText = $"{sol.DensityAltitudeFt:F0} ft";
            AirDensityText = $"{sol.AirDensityKgM3:F4} kg/m³";
            MuzzleVelocityText = $"{sol.EffectiveMuzzleVelocityFps:F0} fps";

            string sgState = sol.StabilityFactor >= 1.4 ? "stable"
                : sol.StabilityFactor >= 1.0 ? "marginal" : "UNSTABLE";
            StabilityText = $"SG {sol.StabilityFactor:F2} ({sgState})";

            TransonicText = sol.TransonicRangeM > 0
                ? $"{(UseMetric ? sol.TransonicRangeM : Units.MToYard(sol.TransonicRangeM)):F0} {RangeUnitLabel}"
                : "supersonic to max";

            Solved?.Invoke();
        }

        private ShotConditions WithWind(double windMps) => new()
        {
            Atmosphere = Conditions.Atmosphere,
            WindSpeedMps = windMps,
            WindFromDeg = Conditions.WindFromDeg,
            MaxRangeM = Conditions.MaxRangeM,
            RangeStepM = Conditions.RangeStepM,
            LookAngleDeg = Conditions.LookAngleDeg,
            ApplyCoriolis = Conditions.ApplyCoriolis,
            LatitudeDeg = Conditions.LatitudeDeg,
            AzimuthDeg = Conditions.AzimuthDeg,
            ApplySpinDrift = Conditions.ApplySpinDrift,
            ApplyAeroJump = Conditions.ApplyAeroJump
        };

        private RowDisplay Format(TrajectoryPoint pt, double? windLo = null, double? windHi = null)
        {
            var ci = CultureInfo.InvariantCulture;
            string range = UseMetric
                ? pt.RangeM.ToString("F0", ci)
                : pt.RangeYd.ToString("F0", ci);

            double elev = UseMoa ? pt.ElevationMoa : pt.ElevationMil;
            double wind = UseMoa ? pt.WindageMoa : pt.WindageMil;

            string windText = (windLo.HasValue && windHi.HasValue)
                ? $"{windLo.Value.ToString("F1", ci)}–{windHi.Value.ToString("F1", ci)}"
                : wind.ToString("F2", ci);

            string drop = UseMetric
                ? $"{pt.DropCm:F1} cm"
                : $"{pt.DropInch:F1} in";

            string vel = UseMetric
                ? $"{pt.VelocityMps:F0} m/s"
                : $"{pt.VelocityFps:F0} fps";

            string energy = UseMetric
                ? $"{pt.EnergyJ:F0} J"
                : $"{pt.EnergyFtLb:F0} ft·lb";

            return new RowDisplay
            {
                Range = range,
                Elevation = elev.ToString("F2", ci),
                Windage = windText,
                Drop = drop,
                Velocity = vel,
                Energy = energy,
                Mach = pt.Mach.ToString("F2", ci),
                Tof = pt.TimeOfFlightS.ToString("F3", ci),
                Supersonic = pt.Supersonic
            };
        }

        /// <summary>Build a CSV of the current trajectory in the active units.</summary>
        public string BuildDopeCsv()
        {
            var ci = CultureInfo.InvariantCulture;
            var sol = LatestSolution;
            var sb = new System.Text.StringBuilder();
            string ru = UseMetric ? "m" : "yd";
            string au = AngularUnitLabel;
            string du = UseMetric ? "cm" : "in";
            string vu = UseMetric ? "m/s" : "fps";
            string eu = UseMetric ? "J" : "ftlb";
            sb.AppendLine($"Range({ru}),Elev({au}),Wind({au}),Drop({du}),Velocity({vu}),Energy({eu}),Mach,ToF(s)");
            if (sol != null)
                foreach (var p in sol.Points)
                {
                    double rng = UseMetric ? p.RangeM : p.RangeYd;
                    double elev = UseMoa ? p.ElevationMoa : p.ElevationMil;
                    double wind = UseMoa ? p.WindageMoa : p.WindageMil;
                    double drop = UseMetric ? p.DropCm : p.DropInch;
                    double vel = UseMetric ? p.VelocityMps : p.VelocityFps;
                    double en = UseMetric ? p.EnergyJ : p.EnergyFtLb;
                    sb.Append(rng.ToString("F0", ci)).Append(',')
                      .Append(elev.ToString("F2", ci)).Append(',')
                      .Append(wind.ToString("F2", ci)).Append(',')
                      .Append(drop.ToString("F1", ci)).Append(',')
                      .Append(vel.ToString("F0", ci)).Append(',')
                      .Append(en.ToString("F0", ci)).Append(',')
                      .Append(p.Mach.ToString("F2", ci)).Append(',')
                      .Append(p.TimeOfFlightS.ToString("F3", ci)).AppendLine();
                }
            return sb.ToString();
        }

        private void Save()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Profiles (*.json)|*.json",
                FileName = "profiles.json"
            };
            if (dlg.ShowDialog() == true)
                ProfileStore.Save(Profiles, dlg.FileName);
        }

        private void Load()
        {
            var dlg = new OpenFileDialog { Filter = "Profiles (*.json)|*.json" };
            if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
            {
                var loaded = ProfileStore.Load(dlg.FileName);
                if (loaded.Count > 0)
                {
                    Profiles.Clear();
                    foreach (var p in loaded) Profiles.Add(p);
                    SelectedProfile = Profiles[0];
                }
            }
        }

        private void NewProfile()
        {
            var p = SelectedProfile?.DeepClone() ?? new Profile();
            p.Name = "New profile";
            Profiles.Add(p);
            SelectedProfile = p;
        }

        private void DeleteProfile()
        {
            if (Profiles.Count <= 1) return;
            var idx = Profiles.IndexOf(SelectedProfile);
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles[Math.Max(0, idx - 1)];
        }
    }
}
