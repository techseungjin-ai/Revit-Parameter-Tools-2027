using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace ParameterBatchManager
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "매개변수 도구";
            string panelName = "매개변수 관리";

            RibbonPanel? panel = GetOrCreatePanel(application, tabName, panelName);
            if (panel == null)
                return Result.Failed;

            // Check if button already exists in panel
            string buttonName = "cmdParameterBatchManager";
            if (panel.GetItems().Any(it => it.Name == buttonName))
            {
                return Result.Succeeded;
            }

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            PushButtonData buttonData = new PushButtonData(
                buttonName,
                "매개변수\n복제 & 일괄생성",
                assemblyPath,
                typeof(Command).FullName
            )
            {
                ToolTip = "기존 매개변수의 설정을 복사/복제하거나, 여러 매개변수를 한 번에 일괄 생성합니다."
            };

            // Set Ribbon Icons
            buttonData.LargeImage = LoadIcon("icon32.png", "ParameterBatchManager.Resources.icon32.png");
            buttonData.Image = LoadIcon("icon16.png", "ParameterBatchManager.Resources.icon16.png");

            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static RibbonPanel? GetOrCreatePanel(UIControlledApplication app, string tabName, string panelName)
        {
            // 1. Create tab if it doesn't exist
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch
            {
                // Tab already exists or error
            }

            // 2. Check if panel exists in tab
            try
            {
                var tabPanels = app.GetRibbonPanels(tabName);
                var existing = tabPanels?.FirstOrDefault(p => p.Name == panelName);
                if (existing != null) return existing;
            }
            catch { }

            // 3. Create panel in tab
            try
            {
                return app.CreateRibbonPanel(tabName, panelName);
            }
            catch
            {
                // Fallback to Add-Ins tab
                try
                {
                    var panels = app.GetRibbonPanels();
                    return panels?.FirstOrDefault(p => p.Name == panelName) ?? app.CreateRibbonPanel(panelName);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static BitmapImage? LoadIcon(string fileName, string manifestResourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // 1. Try embedded resource stream
            try
            {
                using (var stream = assembly.GetManifestResourceStream(manifestResourceName))
                {
                    if (stream != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch { }

            // 2. Fallback: file on disk next to DLL
            try
            {
                string dir = Path.GetDirectoryName(assembly.Location) ?? string.Empty;
                string localPath = Path.Combine(dir, "Resources", fileName);
                if (File.Exists(localPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(localPath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { }

            return null;
        }
    }
}
