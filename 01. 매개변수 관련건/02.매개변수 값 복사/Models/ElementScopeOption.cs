namespace ParameterValueCopier.Models
{
    public enum ElementScope
    {
        ActiveViewOrSchedule, // 현재 활성 뷰 또는 일람표의 모든 요소
        SelectedElements,     // 현재 사용자가 선택한 요소들만
        CategoryElements      // 지정한 카테고리의 프로젝트 전체 요소
    }

    public class CategoryOption
    {
        public string Name { get; set; } = string.Empty;
        public Autodesk.Revit.DB.ElementId Id { get; set; } = Autodesk.Revit.DB.ElementId.InvalidElementId;
        public Autodesk.Revit.DB.Category Category { get; set; } = null!;

        public override string ToString() => Name;
    }
}
