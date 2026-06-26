using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    public sealed class CardRow
    {
        public string Range { get; init; } = "";
        public string Elev { get; init; } = "";
        public string Wind { get; init; } = "";
        public string Drop { get; init; } = "";
        public string Vel { get; init; } = "";
    }

    public partial class RangeCardWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly BallisticSolver _solver = new();
        private List<CardRow> _rows = new();

        public RangeCardWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DistLabel.Text = _vm.UseMetric ? "დისტანციები (m)" : "დისტანციები (yd)";
            DistBox.Text = _vm.UseMetric ? "100, 200, 300, 400, 500, 600, 700, 800"
                                         : "100, 200, 300, 400, 500, 600, 700, 800";
        }

        private void OnGenerate(object sender, RoutedEventArgs e)
        {
            var ci = CultureInfo.InvariantCulture;
            var profile = _vm.SelectedProfile;
            bool metric = _vm.UseMetric, moa = _vm.UseMoa;
            var rows = new List<CardRow>();

            foreach (var token in DistBox.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(token, NumberStyles.Float, ci, out double d) || d <= 0) continue;
                double rangeM = metric ? d : d * Units.Yard;

                var cond = new ShotConditions
                {
                    Atmosphere = _vm.Conditions.Atmosphere,
                    WindSpeedMps = _vm.Conditions.WindSpeedMps,
                    WindFromDeg = _vm.Conditions.WindFromDeg,
                    MaxRangeM = rangeM,
                    RangeStepM = rangeM,
                    LookAngleDeg = _vm.Conditions.LookAngleDeg,
                    ApplyCoriolis = _vm.Conditions.ApplyCoriolis,
                    LatitudeDeg = _vm.Conditions.LatitudeDeg,
                    AzimuthDeg = _vm.Conditions.AzimuthDeg,
                    ApplySpinDrift = _vm.Conditions.ApplySpinDrift,
                    ApplyAeroJump = _vm.Conditions.ApplyAeroJump
                };

                TrajectorySolution sol;
                try { sol = _solver.Solve(profile.Cartridge, profile.Rifle, profile.Scope, profile.ZeroRangeM, cond); }
                catch { continue; }
                if (sol.Points.Count == 0) continue;
                var p = sol.Points[^1];

                rows.Add(new CardRow
                {
                    Range = d.ToString("0", ci),
                    Elev = (moa ? p.ElevationMoa : p.ElevationMil).ToString("0.00", ci),
                    Wind = (moa ? p.WindageMoa : p.WindageMil).ToString("0.00", ci),
                    Drop = metric ? $"{p.DropCm:0.0} cm" : $"{p.DropInch:0.0} in",
                    Vel = metric ? $"{p.VelocityMps:0} m/s" : $"{p.VelocityFps:0} fps"
                });
            }

            _rows = rows;
            ResultGrid.ItemsSource = rows;
        }

        private void OnExportCsv(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0) { OnGenerate(sender, e); }
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "range_card.csv" };
            if (dlg.ShowDialog() != true) return;

            string au = _vm.UseMoa ? "MOA" : "MIL";
            string ru = _vm.UseMetric ? "m" : "yd";
            var sb = new StringBuilder();
            sb.AppendLine($"Range({ru}),Elev({au}),Wind({au}),Drop,Velocity");
            foreach (var r in _rows)
                sb.AppendLine($"{r.Range},{r.Elev},{r.Wind},{r.Drop},{r.Vel}");
            File.WriteAllText(dlg.FileName, sb.ToString());
        }

        private void OnPrint(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0) OnGenerate(sender, e);
            var pd = new PrintDialog();
            if (pd.ShowDialog() != true) return;

            var doc = BuildPrintVisual(pd.PrintableAreaWidth);
            pd.PrintVisual(doc, "Range card — " + _vm.SelectedProfile.Name);
        }

        private FrameworkElement BuildPrintVisual(double width)
        {
            var panel = new StackPanel { Margin = new Thickness(24), Width = width - 48, Background = System.Windows.Media.Brushes.White };
            var title = new TextBlock
            {
                Text = "RANGE CARD — " + _vm.SelectedProfile.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = System.Windows.Media.Brushes.Black,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var c = _vm.SelectedProfile.Cartridge;
            var a = _vm.Conditions.Atmosphere;
            var sub = new TextBlock
            {
                Text = $"{c.Name} · BC {c.BallisticCoefficient:0.###} {c.DragModel} · MV {c.MuzzleVelocityFps:0} fps · " +
                       $"{a.TemperatureC:0}°C / {a.PressureInHg:0.00} inHg",
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(title);
            panel.Children.Add(sub);

            string au = _vm.UseMoa ? "MOA" : "MIL";
            string ru = _vm.UseMetric ? "m" : "yd";
            var grid = new Grid { Background = System.Windows.Media.Brushes.White };
            for (int i = 0; i < 5; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            AddPrintHeaderRow(grid, 0, $"Range({ru})", $"Elev({au})", $"Wind({au})", "Drop", "Vel");
            int row = 1;
            foreach (var r in _rows)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                AddPrintRow(grid, row++, r.Range, r.Elev, r.Wind, r.Drop, r.Vel);
            }
            panel.Children.Add(grid);
            panel.Measure(new Size(width, double.PositiveInfinity));
            panel.Arrange(new Rect(new Size(width, panel.DesiredSize.Height)));
            panel.UpdateLayout();
            return panel;
        }

        private void AddPrintHeaderRow(Grid grid, int row, params string[] cells)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            for (int i = 0; i < cells.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text = cells[i],
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.Black,
                    Margin = new Thickness(4, 2, 4, 2)
                };
                Grid.SetRow(tb, row); Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }
        }

        private void AddPrintRow(Grid grid, int row, params string[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text = cells[i],
                    Foreground = System.Windows.Media.Brushes.Black,
                    Margin = new Thickness(4, 1, 4, 1),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas")
                };
                Grid.SetRow(tb, row); Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }
        }
    }
}
