using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    public sealed class CompareRow
    {
        public string Range { get; init; } = "";
        public string ElevA { get; init; } = "";
        public string ElevB { get; init; } = "";
        public string ElevDelta { get; init; } = "";
        public string VelA { get; init; } = "";
        public string VelB { get; init; } = "";
    }

    public partial class ComparisonWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly BallisticSolver _solver = new();

        public ComparisonWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            ComboA.ItemsSource = vm.Profiles;
            ComboB.ItemsSource = vm.Profiles;
            ComboA.SelectedItem = vm.SelectedProfile;
            ComboB.SelectedIndex = vm.Profiles.Count > 1 ? 1 : 0;
        }

        private void OnCompare(object sender, RoutedEventArgs e)
        {
            if (ComboA.SelectedItem is not Profile a) return;
            if (ComboB.SelectedItem is not Profile b) return;

            var solA = _solver.Solve(a.Cartridge, a.Rifle, a.Scope, a.ZeroRangeM, _vm.Conditions);
            var solB = _solver.Solve(b.Cartridge, b.Rifle, b.Scope, b.ZeroRangeM, _vm.Conditions);

            var ci = CultureInfo.InvariantCulture;
            bool moa = _vm.UseMoa, metric = _vm.UseMetric;
            var rows = new List<CompareRow>();
            int n = System.Math.Min(solA.Points.Count, solB.Points.Count);
            for (int i = 0; i < n; i++)
            {
                var pa = solA.Points[i];
                var pb = solB.Points[i];
                double ea = moa ? pa.ElevationMoa : pa.ElevationMil;
                double eb = moa ? pb.ElevationMoa : pb.ElevationMil;
                double va = metric ? pa.VelocityMps : pa.VelocityFps;
                double vb = metric ? pb.VelocityMps : pb.VelocityFps;
                double rng = metric ? pa.RangeM : pa.RangeYd;
                rows.Add(new CompareRow
                {
                    Range = rng.ToString("0", ci),
                    ElevA = ea.ToString("0.00", ci),
                    ElevB = eb.ToString("0.00", ci),
                    ElevDelta = (eb - ea).ToString("+0.00;-0.00;0.00", ci),
                    VelA = va.ToString("0", ci),
                    VelB = vb.ToString("0", ci)
                });
            }
            ResultGrid.ItemsSource = rows;
        }
    }
}
