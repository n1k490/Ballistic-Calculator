using System.Windows;

namespace PrecisionBallistics.App
{
    public partial class ChartWindow : Window
    {
        public ChartWindow(MainViewModel vm)
        {
            InitializeComponent();
            Chart.SetData(vm.LatestSolution, vm.LatestUseMoa, vm.UseMetric);
        }
    }
}
