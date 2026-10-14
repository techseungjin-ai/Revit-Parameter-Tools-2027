using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ParameterValueCopier.Services;
using ParameterValueCopier.Views;

namespace ParameterValueCopier
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        private static ValueCopierWindow? _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument? uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("알림", "활성화된 Revit 프로젝트 문서가 없습니다. 프로젝트를 먼저 열어주세요.");
                return Result.Cancelled;
            }

            try
            {
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                var handler = new RevitExternalEventHandler();
                var exEvent = ExternalEvent.Create(handler);

                _window = new ValueCopierWindow(uiApp, handler, exEvent);
                var helper = new WindowInteropHelper(_window);
                helper.Owner = uiApp.MainWindowHandle;

                _window.Closed += (s, e) => _window = null;
                _window.Show(); // Modeless window! User can interact with Revit!

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("오류", $"매개변수 값 복사 도구 실행 중 오류가 발생했습니다: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
