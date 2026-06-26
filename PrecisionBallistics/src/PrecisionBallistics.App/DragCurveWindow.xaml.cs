using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    public partial class DragCurveWindow : Window
    {
        private readonly MainViewModel _vm;

        public DragCurveWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;

            var c = _vm.SelectedProfile.Cartridge;
            if (c.HasCustomCurve)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < c.CustomMach!.Length; i++)
                    sb.AppendLine($"{c.CustomMach[i].ToString(CultureInfo.InvariantCulture)}, {c.CustomCd![i].ToString(CultureInfo.InvariantCulture)}");
                CsvBox.Text = sb.ToString();
                StatusText.Text = $"მიმდინარე custom curve: {c.CustomMach.Length} წერტილი.";
            }
            else
            {
                CsvBox.Text = "0.00, 0.1198\n0.90, 0.1464\n1.00, 0.3803\n1.20, 0.3884\n2.00, 0.2980\n3.00, 0.2424";
                StatusText.Text = "ამჟამად სტანდარტული მოდელია (" + c.DragModel + ").";
            }
        }

        private void OnImport(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Drag curve (*.csv;*.txt)|*.csv;*.txt|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
                CsvBox.Text = File.ReadAllText(dlg.FileName);
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            if (!TryParseCurve(CsvBox.Text, out double[] mach, out double[] cd, out string error))
            {
                StatusText.Text = "შეცდომა: " + error;
                return;
            }

            var c = _vm.SelectedProfile.Cartridge;
            c.CustomMach = mach;
            c.CustomCd = cd;
            c.DragModel = DragModel.Custom;
            _vm.RefreshAfterDragChange();
            StatusText.Text = $"გამოყენებულია {mach.Length} წერტილი. Drag model = Custom.";
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            var c = _vm.SelectedProfile.Cartridge;
            c.CustomMach = null;
            c.CustomCd = null;
            if (c.DragModel == DragModel.Custom) c.DragModel = DragModel.G7;
            _vm.RefreshAfterDragChange();
            StatusText.Text = "Custom curve წაიშალა. Drag model = G7.";
        }

        private static bool TryParseCurve(string text, out double[] mach, out double[] cd, out string error)
        {
            var pts = new List<(double m, double c)>();
            error = "";
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                var parts = line.Split(new[] { ',', ';', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double m)) continue;
                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cc)) continue;
                if (m < 0 || cc <= 0) continue;
                pts.Add((m, cc));
            }

            pts.Sort((x, y) => x.m.CompareTo(y.m));
            // de-duplicate identical Mach values
            var clean = new List<(double m, double c)>();
            foreach (var p in pts)
                if (clean.Count == 0 || p.m > clean[^1].m + 1e-9) clean.Add(p);

            if (clean.Count < 2)
            {
                mach = Array.Empty<double>(); cd = Array.Empty<double>();
                error = "საჭიროა ≥2 ვალიდური (Mach, Cd) წერტილი.";
                return false;
            }

            mach = new double[clean.Count];
            cd = new double[clean.Count];
            for (int i = 0; i < clean.Count; i++) { mach[i] = clean[i].m; cd[i] = clean[i].c; }
            return true;
        }
    }
}
