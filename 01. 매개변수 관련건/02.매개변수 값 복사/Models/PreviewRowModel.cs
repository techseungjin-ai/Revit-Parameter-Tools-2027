using Autodesk.Revit.DB;

namespace ParameterValueCopier.Models
{
    public class PreviewRowModel
    {
        public long ElementIdValue { get; set; }
        public string ElementName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SourceCurrentValue { get; set; } = string.Empty;
        public string TargetCurrentValue { get; set; } = string.Empty;
        public string ConvertedNewValue { get; set; } = string.Empty;
        public string Status { get; set; } = "대기";
    }
}
