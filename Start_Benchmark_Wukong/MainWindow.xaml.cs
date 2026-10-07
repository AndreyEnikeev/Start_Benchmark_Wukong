using Start_Benchmark_Wukong.Models;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Start_Benchmark_Wukong
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Опционально: собрать при старте автоматически
            // CollectAndShow();
        }

        private void BtnCollect_Click(object sender, RoutedEventArgs e)
        {
            CollectAndShow();
        }

        private void CollectAndShow()
        {
            try
            {
                var info = ComputerSpecifications.Collect();
                OutputBox.Text = info.ToDisplayString();
            }
            catch (Exception ex)
            {
                OutputBox.Text = $"Ошибка: {ex.Message}";
            }
        }
    }
}