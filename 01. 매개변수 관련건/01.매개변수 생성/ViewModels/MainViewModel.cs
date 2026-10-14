using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using ParameterBatchManager.Models;
using ParameterBatchManager.Services;

namespace ParameterBatchManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly Document _doc;
        private readonly ParameterService _service;

        // Existing parameters
        private List<ProjectParameterModel> _allExistingParameters = new();
        public ObservableCollection<ProjectParameterModel> FilteredExistingParameters { get; } = new();

        private string _parameterSearchText = string.Empty;
        public string ParameterSearchText
        {
            get => _parameterSearchText;
            set
            {
                if (SetProperty(ref _parameterSearchText, value))
                {
                    ApplyParameterFilter();
                }
            }
        }

        private ProjectParameterModel? _selectedParameter;
        public ProjectParameterModel? SelectedParameter
        {
            get => _selectedParameter;
            set
            {
                if (SetProperty(ref _selectedParameter, value))
                {
                    OnSelectedParameterChanged();
                }
            }
        }

        // Available Catalogs
        public List<DataTypeOption> AvailableDataTypes => ForgeCatalog.AvailableDataTypes;
        public List<GroupOption> AvailableGroups => ForgeCatalog.AvailableGroups;

        // Clone Tab Settings
        private string _singleNewName = string.Empty;
        public string SingleNewName
        {
            get => _singleNewName;
            set => SetProperty(ref _singleNewName, value);
        }

        private string _patternPrefix = string.Empty;
        public string PatternPrefix
        {
            get => _patternPrefix;
            set
            {
                if (SetProperty(ref _patternPrefix, value))
                    UpdatePatternPreview();
            }
        }

        private int _patternStart = 1;
        public int PatternStart
        {
            get => _patternStart;
            set
            {
                if (SetProperty(ref _patternStart, value))
                    UpdatePatternPreview();
            }
        }

        private int _patternEnd = 5;
        public int PatternEnd
        {
            get => _patternEnd;
            set
            {
                if (SetProperty(ref _patternEnd, value))
                    UpdatePatternPreview();
            }
        }

        private string _patternPreview = string.Empty;
        public string PatternPreview
        {
            get => _patternPreview;
            set => SetProperty(ref _patternPreview, value);
        }

        private string _multiCloneNames = string.Empty;
        public string MultiCloneNames
        {
            get => _multiCloneNames;
            set => SetProperty(ref _multiCloneNames, value);
        }

        private DataTypeOption _cloneDataType;
        public DataTypeOption CloneDataType
        {
            get => _cloneDataType;
            set => SetProperty(ref _cloneDataType, value);
        }

        private GroupOption _cloneGroup;
        public GroupOption CloneGroup
        {
            get => _cloneGroup;
            set => SetProperty(ref _cloneGroup, value);
        }

        private bool _cloneIsInstance = true;
        public bool CloneIsInstance
        {
            get => _cloneIsInstance;
            set
            {
                if (SetProperty(ref _cloneIsInstance, value))
                {
                    OnPropertyChanged(nameof(CloneIsType));
                }
            }
        }

        public bool CloneIsType
        {
            get => !_cloneIsInstance;
            set
            {
                if (value)
                    CloneIsInstance = false;
            }
        }

        // Batch Tab Settings (Simple Mode)
        private string _batchNamesInput = string.Empty;
        public string BatchNamesInput
        {
            get => _batchNamesInput;
            set => SetProperty(ref _batchNamesInput, value);
        }

        private DataTypeOption _batchDataType;
        public DataTypeOption BatchDataType
        {
            get => _batchDataType;
            set => SetProperty(ref _batchDataType, value);
        }

        private GroupOption _batchGroup;
        public GroupOption BatchGroup
        {
            get => _batchGroup;
            set => SetProperty(ref _batchGroup, value);
        }

        private bool _batchIsInstance = true;
        public bool BatchIsInstance
        {
            get => _batchIsInstance;
            set
            {
                if (SetProperty(ref _batchIsInstance, value))
                {
                    OnPropertyChanged(nameof(BatchIsType));
                }
            }
        }

        public bool BatchIsType
        {
            get => !_batchIsInstance;
            set
            {
                if (value)
                    BatchIsInstance = false;
            }
        }

        // Batch Tab Settings (Table/Grid Mode)
        public ObservableCollection<BatchCreateItem> GridBatchItems { get; } = new();

        private BatchCreateItem? _selectedGridItem;
        public BatchCreateItem? SelectedGridItem
        {
            get => _selectedGridItem;
            set => SetProperty(ref _selectedGridItem, value);
        }

        // Categories
        private List<CategoryItem> _allCategories = new();
        public ObservableCollection<CategoryItem> FilteredCategories { get; } = new();

        private string _categorySearchText = string.Empty;
        public string CategorySearchText
        {
            get => _categorySearchText;
            set
            {
                if (SetProperty(ref _categorySearchText, value))
                {
                    ApplyCategoryFilter();
                }
            }
        }

        // Status and Logs
        private string _statusMessage = "준비 완료";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private StringBuilder _logBuilder = new();
        private string _executionLog = string.Empty;
        public string ExecutionLog
        {
            get => _executionLog;
            set => SetProperty(ref _executionLog, value);
        }

        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand CloneSingleCommand { get; }
        public ICommand ClonePatternCommand { get; }
        public ICommand CloneMultiCommand { get; }
        public ICommand BatchCreateSimpleCommand { get; }
        public ICommand BatchCreateGridCommand { get; }
        public ICommand AddGridRowCommand { get; }
        public ICommand RemoveGridRowCommand { get; }
        public ICommand PasteClipboardToGridCommand { get; }
        public ICommand SelectAllCategoriesCommand { get; }
        public ICommand DeselectAllCategoriesCommand { get; }
        public ICommand SelectModelCategoriesOnlyCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MainViewModel(Document doc)
        {
            _doc = doc;
            _service = new ParameterService();

            _cloneDataType = AvailableDataTypes[0];
            _cloneGroup = AvailableGroups[0];
            _batchDataType = AvailableDataTypes[0];
            _batchGroup = AvailableGroups[0];

            RefreshCommand = new RelayCommand(LoadData);
            CloneSingleCommand = new RelayCommand(ExecuteCloneSingle);
            ClonePatternCommand = new RelayCommand(ExecuteClonePattern);
            CloneMultiCommand = new RelayCommand(ExecuteCloneMulti);
            BatchCreateSimpleCommand = new RelayCommand(ExecuteBatchCreateSimple);
            BatchCreateGridCommand = new RelayCommand(ExecuteBatchCreateGrid);
            AddGridRowCommand = new RelayCommand(ExecuteAddGridRow);
            RemoveGridRowCommand = new RelayCommand(ExecuteRemoveGridRow);
            PasteClipboardToGridCommand = new RelayCommand(ExecutePasteClipboardToGrid);
            SelectAllCategoriesCommand = new RelayCommand(() => SetAllCategories(true));
            DeselectAllCategoriesCommand = new RelayCommand(() => SetAllCategories(false));
            SelectModelCategoriesOnlyCommand = new RelayCommand(SelectModelCategoriesOnly);
            ClearLogCommand = new RelayCommand(ClearLog);

            LoadData();

            // Initial row in Grid
            GridBatchItems.Add(new BatchCreateItem("새_매개변수_1", AvailableDataTypes[0], AvailableGroups[0], true));
        }

        private void LoadData()
        {
            AppendLog("프로젝트 매개변수 및 카테고리 목록을 로드하는 중...");

            // Load Existing Parameters
            _allExistingParameters = _service.GetProjectParameters(_doc);
            ApplyParameterFilter();

            // Load Categories
            _allCategories = _service.GetAllBindableCategories(_doc);
            ApplyCategoryFilter();

            StatusMessage = $"로드 완료: 매개변수 {_allExistingParameters.Count}개, 카테고리 {_allCategories.Count}개";
            AppendLog($"기존 프로젝트 매개변수 {_allExistingParameters.Count}개, 바인딩 가능 카테고리 {_allCategories.Count}개 로드 완료.");
        }

        private void ApplyParameterFilter()
        {
            FilteredExistingParameters.Clear();
            var query = _allExistingParameters.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_parameterSearchText))
            {
                query = query.Where(p => p.Name.Contains(_parameterSearchText, StringComparison.OrdinalIgnoreCase) ||
                                         p.DataTypeName.Contains(_parameterSearchText, StringComparison.OrdinalIgnoreCase) ||
                                         p.GroupName.Contains(_parameterSearchText, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var p in query)
            {
                FilteredExistingParameters.Add(p);
            }
        }

        private void ApplyCategoryFilter()
        {
            FilteredCategories.Clear();
            var query = _allCategories.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_categorySearchText))
            {
                query = query.Where(c => c.Name.Contains(_categorySearchText, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var c in query)
            {
                FilteredCategories.Add(c);
            }
        }

        private void OnSelectedParameterChanged()
        {
            if (_selectedParameter == null) return;

            // Update Clone Settings based on the selected parameter
            CloneDataType = ForgeCatalog.FindDataType(_selectedParameter.DataTypeId);
            CloneGroup = ForgeCatalog.FindGroup(_selectedParameter.GroupTypeId);
            CloneIsInstance = _selectedParameter.IsInstance;

            // Suggest a default clone name
            SingleNewName = _selectedParameter.Name + "_COPY";
            PatternPrefix = _selectedParameter.Name + "_";
            UpdatePatternPreview();

            // Synchronize Category Checkboxes with the selected parameter's categories
            var targetCatIds = new HashSet<ElementId>(_selectedParameter.Categories.Select(c => c.Id));
            foreach (var cat in _allCategories)
            {
                cat.IsSelected = targetCatIds.Contains(cat.Id);
            }

            AppendLog($"선택한 매개변수: '{_selectedParameter.Name}' (유형: {_selectedParameter.DataTypeName}, 그룹: {_selectedParameter.GroupName}, 카테고리: {_selectedParameter.Categories.Count}개)");
        }

        private void UpdatePatternPreview()
        {
            if (string.IsNullOrWhiteSpace(_patternPrefix) || _patternStart > _patternEnd)
            {
                PatternPreview = "(접두사를 입력하고 유효한 번호 범위를 지정하세요)";
                return;
            }

            int count = _patternEnd - _patternStart + 1;
            if (count > 500)
            {
                PatternPreview = "생성 개수가 너무 많습니다 (최대 500개)";
                return;
            }

            var samples = new List<string>();
            for (int i = _patternStart; i <= Math.Min(_patternStart + 2, _patternEnd); i++)
            {
                samples.Add($"{_patternPrefix}{i}");
            }
            if (_patternEnd > _patternStart + 2)
            {
                samples.Add("...");
                samples.Add($"{_patternPrefix}{_patternEnd}");
            }

            PatternPreview = $"생성 대상 (총 {count}개): {string.Join(", ", samples)}";
        }

        private List<Category> GetSelectedCategories()
        {
            return _allCategories.Where(c => c.IsSelected).Select(c => c.Category).ToList();
        }

        private void SetAllCategories(bool isSelected)
        {
            foreach (var cat in _allCategories)
            {
                cat.IsSelected = isSelected;
            }
        }

        private void SelectModelCategoriesOnly()
        {
            foreach (var cat in _allCategories)
            {
                cat.IsSelected = (cat.CategoryType == CategoryType.Model);
            }
        }

        private void ExecuteCloneSingle()
        {
            if (string.IsNullOrWhiteSpace(_singleNewName))
            {
                MessageBox.Show("새로운 매개변수 이름을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCats = GetSelectedCategories();
            if (selectedCats.Count == 0)
            {
                MessageBox.Show("최소 하나 이상의 카테고리를 선택해야 합니다.", "카테고리 선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var requests = new List<CreateParameterRequest>
            {
                new CreateParameterRequest
                {
                    Name = _singleNewName.Trim(),
                    DataTypeId = CloneDataType.SpecTypeId,
                    GroupTypeId = CloneGroup.GroupTypeId,
                    IsInstance = CloneIsInstance,
                    Categories = selectedCats
                }
            };

            ExecuteCreation(requests, "단일 매개변수 복제");
        }

        private void ExecuteClonePattern()
        {
            if (string.IsNullOrWhiteSpace(_patternPrefix))
            {
                MessageBox.Show("접두사를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_patternStart > _patternEnd)
            {
                MessageBox.Show("시작 번호가 종료 번호보다 클 수 없습니다.", "범위 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int count = _patternEnd - _patternStart + 1;
            if (count > 200)
            {
                if (MessageBox.Show($"생성할 매개변수가 {count}개입니다. 계속 진행하시겠습니까?", "대량 생성 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            var selectedCats = GetSelectedCategories();
            if (selectedCats.Count == 0)
            {
                MessageBox.Show("최소 하나 이상의 카테고리를 선택해야 합니다.", "카테고리 선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var requests = new List<CreateParameterRequest>();
            for (int i = _patternStart; i <= _patternEnd; i++)
            {
                requests.Add(new CreateParameterRequest
                {
                    Name = $"{_patternPrefix}{i}",
                    DataTypeId = CloneDataType.SpecTypeId,
                    GroupTypeId = CloneGroup.GroupTypeId,
                    IsInstance = CloneIsInstance,
                    Categories = selectedCats
                });
            }

            ExecuteCreation(requests, $"연번 패턴 복제 ({count}개)");
        }

        private void ExecuteCloneMulti()
        {
            var lines = _multiCloneNames.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .Distinct()
                                        .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show("복제할 매개변수 이름을 한 줄에 하나씩 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCats = GetSelectedCategories();
            if (selectedCats.Count == 0)
            {
                MessageBox.Show("최소 하나 이상의 카테고리를 선택해야 합니다.", "카테고리 선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var requests = lines.Select(name => new CreateParameterRequest
            {
                Name = name,
                DataTypeId = CloneDataType.SpecTypeId,
                GroupTypeId = CloneGroup.GroupTypeId,
                IsInstance = CloneIsInstance,
                Categories = selectedCats
            }).ToList();

            ExecuteCreation(requests, $"목록 다중 복제 ({requests.Count}개)");
        }

        private void ExecuteBatchCreateSimple()
        {
            var lines = _batchNamesInput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .Where(s => !string.IsNullOrWhiteSpace(s))
                                        .Distinct()
                                        .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show("생성할 매개변수 이름을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCats = GetSelectedCategories();
            if (selectedCats.Count == 0)
            {
                MessageBox.Show("바인딩할 카테고리를 최소 1개 이상 선택해주세요.", "카테고리 선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var requests = lines.Select(name => new CreateParameterRequest
            {
                Name = name,
                DataTypeId = BatchDataType.SpecTypeId,
                GroupTypeId = BatchGroup.GroupTypeId,
                IsInstance = BatchIsInstance,
                Categories = selectedCats
            }).ToList();

            ExecuteCreation(requests, $"간편 일괄 생성 ({requests.Count}개)");
        }

        private void ExecuteBatchCreateGrid()
        {
            var validItems = GridBatchItems.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
            if (validItems.Count == 0)
            {
                MessageBox.Show("테이블에 매개변수 이름이 입력된 행이 없습니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCats = GetSelectedCategories();
            if (selectedCats.Count == 0)
            {
                MessageBox.Show("바인딩할 카테고리를 선택해주세요.", "카테고리 선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var requests = validItems.Select(item => new CreateParameterRequest
            {
                Name = item.Name.Trim(),
                DataTypeId = item.DataType.SpecTypeId,
                GroupTypeId = item.Group.GroupTypeId,
                IsInstance = item.IsInstance,
                Categories = selectedCats
            }).ToList();

            ExecuteCreation(requests, $"테이블 일괄 생성 ({requests.Count}개)");
        }

        private void ExecuteAddGridRow()
        {
            int nextIndex = GridBatchItems.Count + 1;
            GridBatchItems.Add(new BatchCreateItem($"매개변수_{nextIndex}", AvailableDataTypes[0], AvailableGroups[0], true));
        }

        private void ExecuteRemoveGridRow()
        {
            if (_selectedGridItem != null)
            {
                GridBatchItems.Remove(_selectedGridItem);
            }
            else if (GridBatchItems.Count > 0)
            {
                GridBatchItems.RemoveAt(GridBatchItems.Count - 1);
            }
        }

        private void ExecutePasteClipboardToGrid()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show("클립보드에 텍스트 데이터가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string text = Clipboard.GetText();
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                int added = 0;

                foreach (var line in lines)
                {
                    var tokens = line.Split('\t');
                    string name = tokens[0].Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var dataType = AvailableDataTypes[0];
                    var group = AvailableGroups[0];
                    bool isInst = true;

                    if (tokens.Length > 1)
                    {
                        var matchType = AvailableDataTypes.FirstOrDefault(d => d.DisplayName.Contains(tokens[1].Trim(), StringComparison.OrdinalIgnoreCase));
                        if (matchType != null) dataType = matchType;
                    }
                    if (tokens.Length > 2)
                    {
                        var matchGroup = AvailableGroups.FirstOrDefault(g => g.DisplayName.Contains(tokens[2].Trim(), StringComparison.OrdinalIgnoreCase));
                        if (matchGroup != null) group = matchGroup;
                    }
                    if (tokens.Length > 3)
                    {
                        string instStr = tokens[3].Trim();
                        if (instStr.Contains("유형") || instStr.Equals("Type", StringComparison.OrdinalIgnoreCase))
                            isInst = false;
                    }

                    GridBatchItems.Add(new BatchCreateItem(name, dataType, group, isInst));
                    added++;
                }

                AppendLog($"클립보드에서 {added}개 매개변수 행 추가 완료.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"클립보드 붙여넣기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteCreation(List<CreateParameterRequest> requests, string taskDescription)
        {
            AppendLog($"\n>>> [{DateTime.Now:HH:mm:ss}] {taskDescription} 시작 (총 {requests.Count}건)...");

            var result = _service.CreateParameters(_doc, requests, AppendLog);

            string summary = $"완료: 성공 {result.SuccessCount}건, 건너뜀 {result.SkipCount}건, 실패 {result.ErrorCount}건";
            StatusMessage = summary;
            AppendLog($"<<< {summary}\n");

            // Update existing parameters list
            _allExistingParameters = _service.GetProjectParameters(_doc);
            ApplyParameterFilter();

            MessageBox.Show($"{taskDescription} 작업이 완료되었습니다.\n\n" +
                            $"- 성공: {result.SuccessCount}건\n" +
                            $"- 건너뜀(중복): {result.SkipCount}건\n" +
                            $"- 실패: {result.ErrorCount}건",
                            "작업 완료", MessageBoxButton.OK,
                            result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void AppendLog(string message)
        {
            _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            ExecutionLog = _logBuilder.ToString();
        }

        private void ClearLog()
        {
            _logBuilder.Clear();
            ExecutionLog = string.Empty;
        }
    }
}
