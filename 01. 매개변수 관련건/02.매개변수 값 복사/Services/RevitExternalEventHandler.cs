using System;
using Autodesk.Revit.UI;

namespace ParameterValueCopier.Services
{
    public class RevitExternalEventHandler : IExternalEventHandler
    {
        private Action<UIApplication>? _pendingAction;

        public void Run(Action<UIApplication> action, ExternalEvent exEvent)
        {
            _pendingAction = action;
            exEvent.Raise();
        }

        public void Execute(UIApplication app)
        {
            try
            {
                _pendingAction?.Invoke(app);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("실행 오류", $"작업 중 오류가 발생했습니다: {ex.Message}");
            }
            finally
            {
                _pendingAction = null;
            }
        }

        public string GetName() => "ParameterValueCopier_ExternalEvent";
    }
}
