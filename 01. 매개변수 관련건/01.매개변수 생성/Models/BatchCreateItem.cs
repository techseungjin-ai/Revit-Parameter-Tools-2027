using System.ComponentModel;
using System.Runtime.CompilerServices;
using ParameterBatchManager.Models;

namespace ParameterBatchManager.Models
{
    public class BatchCreateItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private DataTypeOption _dataType;
        private GroupOption _group;
        private bool _isInstance = true;
        private string _status = "대기";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public DataTypeOption DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); }
        }

        public GroupOption Group
        {
            get => _group;
            set { _group = value; OnPropertyChanged(); }
        }

        public bool IsInstance
        {
            get => _isInstance;
            set { _isInstance = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public BatchCreateItem()
        {
            _dataType = ForgeCatalog.AvailableDataTypes[0]; // 문자
            _group = ForgeCatalog.AvailableGroups[0];       // 데이터
        }

        public BatchCreateItem(string name, DataTypeOption dataType, GroupOption group, bool isInstance)
        {
            _name = name;
            _dataType = dataType;
            _group = group;
            _isInstance = isInstance;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
