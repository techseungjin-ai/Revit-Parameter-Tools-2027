using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ParameterBatchManager.Views;

namespace ParameterBatchManager
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument? uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show("알림", "활성화된 Revit 프로젝트 문서가 없습니다. 프로젝트를 먼저 열어주세요.");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("알림", "이 도구는 프로젝트 문서(.rvt)의 프로젝트 매개변수 관리를 지원합니다.");
                return Result.Cancelled;
            }

            try
            {
                var window = new MainWindow(doc);
                var helper = new WindowInteropHelper(window);
                helper.Owner = uiApp.MainWindowHandle;

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("오류", $"애드인 실행 중 오류가 발생했습니다: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
