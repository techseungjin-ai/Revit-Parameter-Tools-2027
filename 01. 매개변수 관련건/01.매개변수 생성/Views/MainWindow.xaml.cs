using System.Windows;
using Autodesk.Revit.DB;
using ParameterBatchManager.ViewModels;

namespace ParameterBatchManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(Document doc)
        {
            InitializeComponent();
            DataContext = new MainViewModel(doc);
        }
    }
}
