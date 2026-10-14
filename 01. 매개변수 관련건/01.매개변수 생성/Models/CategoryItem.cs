using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace ParameterBatchManager.Models
{
    public class CategoryItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public Category Category { get; }
        public ElementId Id => Category.Id;
        public string Name => Category.Name;
        public CategoryType CategoryType => Category.CategoryType;

        public string CategoryTypeDisplayName => CategoryType switch
        {
            CategoryType.Model => "모델",
            CategoryType.Annotation => "주석",
            CategoryType.AnalyticalModel => "해석 모델",
            _ => "기타"
        };

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public CategoryItem(Category category, bool isSelected = false)
        {
            Category = category;
            _isSelected = isSelected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
