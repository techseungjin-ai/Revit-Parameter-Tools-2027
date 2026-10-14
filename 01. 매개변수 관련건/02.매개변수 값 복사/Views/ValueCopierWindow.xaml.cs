using System.Windows;
using Autodesk.Revit.UI;
using ParameterValueCopier.Services;
using ParameterValueCopier.ViewModels;

namespace ParameterValueCopier.Views
{
    public partial class ValueCopierWindow : Window
    {
        public ValueCopierWindow(UIApplication uiApp, RevitExternalEventHandler handler, ExternalEvent exEvent)
        {
            InitializeComponent();
            DataContext = new ValueCopierViewModel(uiApp, handler, exEvent);
        }
    }
}
