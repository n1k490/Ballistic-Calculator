using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Win32;
using PrecisionBallistics.Core;

namespace PrecisionBallistics.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            // Parse numbers with '.' as the decimal separator regardless of the
            // machine's regional settings, so inputs like "0.315" are consistent.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-US")));

            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;

            _vm.Solved += UpdateReticle;
            Loaded += (_, _) => UpdateReticle();
        }

        private void UpdateReticle()
        {
            Reticle.SetData(_vm.LatestSolution, _vm.LatestUseMoa, _vm.UseMetric);
        }

        private void OnTrueMv(object sender, RoutedEventArgs e) =>
            new TruingWindow(_vm) { Owner = this }.ShowDialog();

        private void OnRangeCard(object sender, RoutedEventArgs e) =>
            new RangeCardWindow(_vm) { Owner = this }.ShowDialog();

        private void OnCompare(object sender, RoutedEventArgs e) =>
            new ComparisonWindow(_vm) { Owner = this }.ShowDialog();

        private void OnGraph(object sender, RoutedEventArgs e) =>
            new ChartWindow(_vm) { Owner = this }.ShowDialog();

        private void OnDragCurve(object sender, RoutedEventArgs e) =>
            new DragCurveWindow(_vm) { Owner = this }.ShowDialog();

        private void OnExportCsv(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "dope.csv" };
            if (dlg.ShowDialog() == true)
                File.WriteAllText(dlg.FileName, _vm.BuildDopeCsv());
        }

        private void OnImportProfile(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Profile (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;
            var p = ProfileStore.LoadOne(dlg.FileName);
            if (p == null)
            {
                MessageBox.Show(this, "ფაილი ვერ წაიკითხა.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _vm.Profiles.Add(p);
            _vm.SelectedProfile = p;
        }

        private void OnExportProfile(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedProfile == null) return;
            var safe = string.Join("_", _vm.SelectedProfile.Name.Split(Path.GetInvalidFileNameChars()));
            var dlg = new SaveFileDialog { Filter = "Profile (*.json)|*.json", FileName = safe + ".json" };
            if (dlg.ShowDialog() == true)
                ProfileStore.SaveOne(_vm.SelectedProfile, dlg.FileName);
        }
    }
}
