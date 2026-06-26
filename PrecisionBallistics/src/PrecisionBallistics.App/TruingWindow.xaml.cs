using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    public partial class TruingWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly bool _metric;
        private double? _resultFps;

        public TruingWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            _metric = vm.UseMetric;

            ZeroLabel.Text = _metric ? "გასწორების მანძილი / zero (m)" : "გასწორების მანძილი / zero (yd)";
            ObsLabel.Text = _metric
                ? "დაკვირვებები — „მანძილი(m), ვარდნა(cm)“ თითო ხაზზე"
                : "დაკვირვებები — „მანძილი(yd), ვარდნა(in)“ თითო ხაზზე";
            ObsHint.Text = _metric
                ? "მაგ:  400, 150   ან რამდენიმე ხაზი:  300, 80 / 500, 230 / 700, 470"
                : "მაგ:  440, 59    ან რამდენიმე ხაზი:  330, 31 / 550, 90 / 770, 185";

            ZeroBox.Text = _vm.ZeroRangeDisplay.ToString("0.##", CultureInfo.InvariantCulture);
            ObsBox.Text = _metric ? "400, 150" : "440, 59";

            var c = _vm.SelectedProfile.Cartridge;
            var a = _vm.Conditions.Atmosphere;
            InfoText.Text =
                $"Load: {c.Name} · BC {c.BallisticCoefficient:0.###} {c.DragModel} · " +
                $"{a.TemperatureC:0.#}°C / {a.PressureInHg:0.##} inHg / {a.HumidityPct:0}% RH";
        }

        private void OnCalc(object sender, RoutedEventArgs e)
        {
            if (!TryParse(ZeroBox.Text, out double zero))
            {
                Fail("შეამოწმე zero მანძილი."); return;
            }
            double zeroM = _metric ? zero : zero * Units.Yard;

            var obs = new List<(double rangeM, double dropM)>();
            foreach (var rawLine in ObsBox.Text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                var parts = line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!TryParse(parts[0], out double dist) || !TryParse(parts[1], out double drop)) continue;
                double rangeM = _metric ? dist : dist * Units.Yard;
                double dropM = _metric ? drop / 100.0 : drop * Units.Inch;
                if (rangeM <= zeroM || dropM <= 0) continue;
                obs.Add((rangeM, dropM));
            }

            if (obs.Count == 0)
            {
                Fail("ვერ წავიკითხე ვალიდური დაკვირვება (მანძილი > zero, ვარდნა > 0).");
                return;
            }

            try
            {
                double fps = obs.Count == 1
                    ? Truing.TrueMuzzleVelocityFromDrop(
                        _vm.SelectedProfile.Cartridge, _vm.SelectedProfile.Rifle,
                        zeroM, _vm.Conditions, obs[0].rangeM, obs[0].dropM)
                    : Truing.TrueMuzzleVelocityMultiPoint(
                        _vm.SelectedProfile.Cartridge, _vm.SelectedProfile.Rifle,
                        zeroM, _vm.Conditions, obs);

                _resultFps = fps;
                ResultText.Text = $"{fps:0} fps  /  {Units.FpsToMps(fps):0} m/s";
                ResultNote.Text = obs.Count == 1
                    ? "„Apply to load“ ჩასვამს ამ მნიშვნელობას მიმდინარე ვაზნაში."
                    : $"best-fit {obs.Count} დაკვირვებაზე. „Apply to load“ ჩასვამს ვაზნაში.";
            }
            catch (Exception ex)
            {
                Fail("დათვლა ვერ მოხერხდა: " + ex.Message);
            }
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            if (_resultFps is double fps)
            {
                _vm.ApplyTruedMv(fps);
                Close();
            }
            else ResultNote.Text = "ჯერ დააჭირე „Calculate“.";
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();

        private void Fail(string msg) { ResultText.Text = "—"; ResultNote.Text = msg; }

        private static bool TryParse(string s, out double value) =>
            double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
