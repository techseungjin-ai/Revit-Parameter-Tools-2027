using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ParameterBatchManager.Models
{
    public class ProjectParameterModel
    {
        public string Name { get; set; } = string.Empty;
        public ForgeTypeId DataTypeId { get; set; } = null!;
        public string DataTypeName { get; set; } = string.Empty;
        public ForgeTypeId GroupTypeId { get; set; } = null!;
        public string GroupName { get; set; } = string.Empty;
        public bool IsInstance { get; set; } = true;
        public string BindingTypeDescription => IsInstance ? "인스턴스" : "유형";
        public Definition Definition { get; set; } = null!;
        public List<Category> Categories { get; set; } = new();

        public string CategorySummary
        {
            get
            {
                if (Categories == null || Categories.Count == 0)
                    return "(카테고리 없음)";
                if (Categories.Count <= 3)
                    return string.Join(", ", Categories.Select(c => c.Name));
                return $"{Categories[0].Name}, {Categories[1].Name} 외 {Categories.Count - 2}개";
            }
        }
    }
}
