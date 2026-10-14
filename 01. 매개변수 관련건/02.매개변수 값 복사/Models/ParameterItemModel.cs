using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ParameterValueCopier.Models
{
    public class ParameterItemModel
    {
        public string Name { get; set; } = string.Empty;
        public StorageType StorageType { get; set; }
        public string StorageTypeName => StorageType switch
        {
            StorageType.Double => "숫자/실수/길이 (Double)",
            StorageType.Integer => "정수/예·아니오 (Integer)",
            StorageType.String => "문자 (String)",
            StorageType.ElementId => "요소 ID (ElementId)",
            _ => "기타"
        };

        public string DataTypeName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public bool IsReadOnly { get; set; }
        public bool IsInstance { get; set; } = true;
        public ForgeTypeId? SpecTypeId { get; set; }

        // Schedule virtual field extensions
        public bool IsScheduleField { get; set; }
        public bool IsCalculatedField { get; set; }
        public bool IsCombinedParameterField { get; set; }
        public ScheduleFieldId? ScheduleFieldId { get; set; }
        public int ScheduleColumnIndex { get; set; } = -1;
        public IList<TableCellCombinedParameterData>? CombinedParts { get; set; }

        // UI Badges
        public string BadgeText
        {
            get
            {
                if (IsCalculatedField) return "fx 계산된 값";
                if (IsCombinedParameterField) return "🔗 매개변수 결합";
                return StorageTypeName;
            }
        }

        public string BadgeBgColor
        {
            get
            {
                if (IsCalculatedField) return "#FEF3C7";      // Amber light
                if (IsCombinedParameterField) return "#F3E8FF"; // Purple light
                return "#E0F2FE";                             // Blue light
            }
        }

        public string BadgeFgColor
        {
            get
            {
                if (IsCalculatedField) return "#B45309";      // Amber dark
                if (IsCombinedParameterField) return "#7E22CE"; // Purple dark
                return "#0369A1";                             // Blue dark
            }
        }

        public string DisplaySummary => $"{Name} [{BadgeText}] - {GroupName} {(IsReadOnly ? "(읽기 전용)" : "")}";

        public override string ToString() => Name;
    }
}
