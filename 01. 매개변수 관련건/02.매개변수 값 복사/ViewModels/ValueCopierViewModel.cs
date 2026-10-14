using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ParameterValueCopier.Models;
using ParameterValueCopier.Services;

namespace ParameterValueCopier.ViewModels
{
    public class ValueCopierViewModel : ViewModelBase
    {
        private readonly UIApplication _uiApp;
        private readonly RevitExternalEventHandler _handler;
        private readonly ExternalEvent _exEvent;
        private readonly ParameterCopyService _service;

        // Current Elements
        private List<Element> _currentElements = new();
        public int ElementCount => _currentElements.Count;

        private string _activeViewDescription = string.Empty;
        public string ActiveViewDescription
        {
            get => _activeViewDescription;
            set => SetProperty(ref _activeViewDescription, value);
        }

        // Scope options
        private bool _isActiveViewScope = true;
        public bool IsActiveViewScope
        {
            get => _isActiveViewScope;
            set
            {
                if (SetProperty(ref _isActiveViewScope, value) && value)
                {
                    OnScopeChanged();
                }
            }
        }

        private bool _isSelectedScope;
        public bool IsSelectedScope
        {
            get => _isSelectedScope;
            set
            {
                if (SetProperty(ref _isSelectedScope, value) && value)
                {
                    OnScopeChanged();
                }
            }
        }

        private bool _isCategoryScope;
        public bool IsCategoryScope
        {
            get => _isCategoryScope;
            set
            {
                if (SetProperty(ref _isCategoryScope, value) && value)
                {
                    OnScopeChanged();
                }
            }
        }

        // Categories
        public ObservableCollection<CategoryOption> AvailableCategories { get; } = new();

        private CategoryOption? _selectedCategory;
        public CategoryOption? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    if (IsCategoryScope)
                        OnScopeChanged();
                }
            }
        }

        // Parameters
        private List<ParameterItemModel> _allParameters = new();
        public ObservableCollection<ParameterItemModel> FilteredSourceParameters { get; } = new();
        public ObservableCollection<ParameterItemModel> FilteredTargetParameters { get; } = new();

        private string _sourceSearchText = string.Empty;
        public string SourceSearchText
        {
            get => _sourceSearchText;
            set
            {
                if (SetProperty(ref _sourceSearchText, value))
                    ApplySourceFilter();
            }
        }

        private string _targetSearchText = string.Empty;
        public string TargetSearchText
        {
            get => _targetSearchText;
            set
            {
                if (SetProperty(ref _targetSearchText, value))
                    ApplyTargetFilter();
            }
        }

        private ParameterItemModel? _selectedSourceParameter;
        public ParameterItemModel? SelectedSourceParameter
        {
            get => _selectedSourceParameter;
            set
            {
                if (SetProperty(ref _selectedSourceParameter, value))
                {
                    UpdateTypeCompatibilityAnalysis();
                    UpdatePreview();
                }
            }
        }

        private ParameterItemModel? _selectedTargetParameter;
        public ParameterItemModel? SelectedTargetParameter
        {
            get => _selectedTargetParameter;
            set
            {
                if (SetProperty(ref _selectedTargetParameter, value))
                {
                    UpdateTypeCompatibilityAnalysis();
                    UpdatePreview();
                }
            }
        }

        // Compatibility Notice
        private string _typeCompatibilityNotice = "원본 및 대상 매개변수를 선택해주세요.";
        public string TypeCompatibilityNotice
        {
            get => _typeCompatibilityNotice;
            set => SetProperty(ref _typeCompatibilityNotice, value);
        }

        private bool _isCompatible = true;
        public bool IsCompatible
        {
            get => _isCompatible;
            set => SetProperty(ref _isCompatible, value);
        }

        // Decimal places options
        public ObservableCollection<DecimalPlaceOption> AvailableDecimalPlaces { get; } = new()
        {
            new DecimalPlaceOption { Places = 0, DisplayName = "0자리 (정수: 예 636) [기본값]" },
            new DecimalPlaceOption { Places = 1, DisplayName = "1자리 (0.1: 예 635.6)" },
            new DecimalPlaceOption { Places = 2, DisplayName = "2자리 (0.01: 예 635.62)" },
            new DecimalPlaceOption { Places = 3, DisplayName = "3자리 (0.001: 예 635.617)" },
            new DecimalPlaceOption { Places = 4, DisplayName = "4자리 (0.0001: 예 635.6165)" },
            new DecimalPlaceOption { Places = -1, DisplayName = "제한 없음 (원본 정밀값 그대로)" }
        };

        private DecimalPlaceOption _selectedDecimalPlaceOption;
        public DecimalPlaceOption SelectedDecimalPlaceOption
        {
            get => _selectedDecimalPlaceOption;
            set
            {
                if (SetProperty(ref _selectedDecimalPlaceOption, value))
                {
                    UpdatePreview();
                }
            }
        }

        private bool _appendUnitSymbol = false;
        public bool AppendUnitSymbol
        {
            get => _appendUnitSymbol;
            set
            {
                if (SetProperty(ref _appendUnitSymbol, value))
                    UpdatePreview();
            }
        }

        private bool _convertYesNoToKorean = true;
        public bool ConvertYesNoToKorean
        {
            get => _convertYesNoToKorean;
            set
            {
                if (SetProperty(ref _convertYesNoToKorean, value))
                    UpdatePreview();
            }
        }

        private bool _overwriteExisting = true;
        public bool OverwriteExisting
        {
            get => _overwriteExisting;
            set
            {
                if (SetProperty(ref _overwriteExisting, value))
                    UpdatePreview();
            }
        }

        private bool _skipEmptySource = true;
        public bool SkipEmptySource
        {
            get => _skipEmptySource;
            set
            {
                if (SetProperty(ref _skipEmptySource, value))
                    UpdatePreview();
            }
        }

        // Previews
        public ObservableCollection<PreviewRowModel> PreviewRows { get; } = new();

        // Status & Logs
        private string _statusMessage = "준비 완료 (모델리스 모드: 레빗 작업 가능)";
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
        public ICommand RefreshScopeCommand { get; }
        public ICommand FetchCurrentSelectionCommand { get; }
        public ICommand RefreshPreviewCommand { get; }
        public ICommand ExecuteCopyCommand { get; }
        public ICommand ClearLogCommand { get; }

        public ValueCopierViewModel(UIApplication uiApp, RevitExternalEventHandler handler, ExternalEvent exEvent)
        {
            _uiApp = uiApp;
            _handler = handler;
            _exEvent = exEvent;
            _service = new ParameterCopyService();
            _selectedDecimalPlaceOption = AvailableDecimalPlaces[0];

            RefreshScopeCommand = new RelayCommand(OnScopeChanged);
            FetchCurrentSelectionCommand = new RelayCommand(FetchCurrentSelection);
            RefreshPreviewCommand = new RelayCommand(UpdatePreview);
            ExecuteCopyCommand = new RelayCommand(ExecuteCopy);
            ClearLogCommand = new RelayCommand(ClearLog);

            InitializeViewInfo();
            LoadCategories();
            OnScopeChanged();
        }

        private Document? GetCurrentDoc() => _uiApp.ActiveUIDocument?.Document;

        private void InitializeViewInfo()
        {
            var doc = GetCurrentDoc();
            if (doc == null) return;

            if (doc.ActiveView is ViewSchedule schedule)
            {
                ActiveViewDescription = $"현재 일람표: '{schedule.Name}'";
            }
            else if (doc.ActiveView != null)
            {
                ActiveViewDescription = $"현재 활성 뷰: '{doc.ActiveView.Name}' ({doc.ActiveView.ViewType})";
            }
            else
            {
                ActiveViewDescription = "활성 뷰 없음";
            }
        }

        private void LoadCategories()
        {
            var doc = GetCurrentDoc();
            if (doc == null) return;

            AvailableCategories.Clear();
            foreach (Category cat in doc.Settings.Categories)
            {
                if (cat.CategoryType == CategoryType.Model && cat.AllowsBoundParameters)
                {
                    AvailableCategories.Add(new CategoryOption
                    {
                        Name = cat.Name,
                        Id = cat.Id,
                        Category = cat
                    });
                }
            }

            var sorted = AvailableCategories.OrderBy(c => c.Name).ToList();
            AvailableCategories.Clear();
            foreach (var c in sorted) AvailableCategories.Add(c);

            if (AvailableCategories.Count > 0)
                SelectedCategory = AvailableCategories[0];
        }

        private void OnScopeChanged()
        {
            var doc = GetCurrentDoc();
            var uidoc = _uiApp.ActiveUIDocument;
            if (doc == null) return;

            InitializeViewInfo();

            ElementScope scope = ElementScope.ActiveViewOrSchedule;
            if (IsSelectedScope) scope = ElementScope.SelectedElements;
            else if (IsCategoryScope) scope = ElementScope.CategoryElements;

            _currentElements = _service.GetTargetElements(doc, uidoc, scope, SelectedCategory?.Id);
            OnPropertyChanged(nameof(ElementCount));

            AppendLog($"범위 업데이트: {GetScopeDescription(scope)} -> 요소 {_currentElements.Count}개 감지됨.");

            // Update parameters available on these elements
            _allParameters = _service.GetParametersFromElements(_currentElements);

            if (doc.ActiveView is ViewSchedule schedule)
            {
                var schedParams = _service.GetScheduleVirtualParameters(schedule, doc);
                if (schedParams.Count > 0)
                {
                    _allParameters.InsertRange(0, schedParams);
                    AppendLog($"일람표 '{schedule.Name}' 가상 필드 {schedParams.Count}개(계산된 값/결합식) 원본 목록 추가 완료.");
                }
            }

            ApplySourceFilter();
            ApplyTargetFilter();

            UpdatePreview();
        }

        public void FetchCurrentSelection()
        {
            var uidoc = _uiApp.ActiveUIDocument;
            var doc = GetCurrentDoc();
            if (uidoc == null || doc == null) return;

            var selIds = uidoc.Selection.GetElementIds();
            if (selIds.Count == 0)
            {
                MessageBox.Show("현재 Revit 뷰에서 선택된 객체가 없습니다.\nRevit에서 요소를 마우스로 선택한 후 다시 눌러주세요.",
                                "선택 객체 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isSelectedScope = true;
            _isActiveViewScope = false;
            _isCategoryScope = false;
            OnPropertyChanged(nameof(IsSelectedScope));
            OnPropertyChanged(nameof(IsActiveViewScope));
            OnPropertyChanged(nameof(IsCategoryScope));

            _currentElements = selIds.Select(id => doc.GetElement(id))
                                     .Where(e => e != null && !(e is ElementType))
                                     .ToList();
            OnPropertyChanged(nameof(ElementCount));

            AppendLog($"Revit 현재 선택 요소 {_currentElements.Count}개 즉시 반영 완료.");

            _allParameters = _service.GetParametersFromElements(_currentElements);

            if (doc.ActiveView is ViewSchedule sched)
            {
                var schedParams = _service.GetScheduleVirtualParameters(sched, doc);
                if (schedParams.Count > 0)
                {
                    _allParameters.InsertRange(0, schedParams);
                }
            }

            ApplySourceFilter();
            ApplyTargetFilter();

            UpdatePreview();
        }

        private string GetScopeDescription(ElementScope scope) => scope switch
        {
            ElementScope.ActiveViewOrSchedule => ActiveViewDescription,
            ElementScope.SelectedElements => "선택된 요소",
            ElementScope.CategoryElements => $"카테고리 '{SelectedCategory?.Name}'",
            _ => ""
        };

        private void ApplySourceFilter()
        {
            FilteredSourceParameters.Clear();
            var query = _allParameters.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_sourceSearchText))
            {
                query = query.Where(p => p.Name.Contains(_sourceSearchText, StringComparison.OrdinalIgnoreCase) ||
                                         p.StorageTypeName.Contains(_sourceSearchText, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var p in query) FilteredSourceParameters.Add(p);

            if (SelectedSourceParameter != null && !FilteredSourceParameters.Contains(SelectedSourceParameter))
            {
                SelectedSourceParameter = FilteredSourceParameters.FirstOrDefault(p => p.Name == SelectedSourceParameter.Name);
            }
        }

        private void ApplyTargetFilter()
        {
            FilteredTargetParameters.Clear();
            var query = _allParameters.Where(p => !p.IsReadOnly);
            if (!string.IsNullOrWhiteSpace(_targetSearchText))
            {
                query = query.Where(p => p.Name.Contains(_targetSearchText, StringComparison.OrdinalIgnoreCase) ||
                                         p.StorageTypeName.Contains(_targetSearchText, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var p in query) FilteredTargetParameters.Add(p);

            if (SelectedTargetParameter != null && !FilteredTargetParameters.Contains(SelectedTargetParameter))
            {
                SelectedTargetParameter = FilteredTargetParameters.FirstOrDefault(p => p.Name == SelectedTargetParameter.Name);
            }
        }

        private void UpdateTypeCompatibilityAnalysis()
        {
            if (SelectedSourceParameter == null || SelectedTargetParameter == null)
            {
                TypeCompatibilityNotice = "원본(A)과 대상(B) 매개변수를 모두 선택해주세요.";
                IsCompatible = true;
                return;
            }

            var src = SelectedSourceParameter;
            var tgt = SelectedTargetParameter;

            if (tgt.IsReadOnly)
            {
                TypeCompatibilityNotice = $"❌ 대상 매개변수 '{tgt.Name}'은(는) 읽기 전용이므로 값을 쓸 수 없습니다.";
                IsCompatible = false;
                return;
            }

            if (src.IsScheduleField)
            {
                if (src.IsCalculatedField)
                {
                    TypeCompatibilityNotice = $"⚡ 일람표 수식 [계산된 값]: '{src.Name}' ➔ '{tgt.Name}'\n" +
                                              $"일람표에서 실시간 계산된 수치를 대상 매개변수({tgt.StorageTypeName})에 정밀 주입합니다.";
                    IsCompatible = true;
                    return;
                }
                if (src.IsCombinedParameterField)
                {
                    TypeCompatibilityNotice = $"🔗 일람표 [매개변수 결합]: '{src.Name}' ➔ '{tgt.Name}'\n" +
                                              $"일람표 결합식 조합 텍스트(예: 249 x 1628)를 대상 매개변수({tgt.StorageTypeName})에 주입합니다.";
                    IsCompatible = true;
                    return;
                }
            }

            if (src.StorageType == tgt.StorageType)
            {
                TypeCompatibilityNotice = $"✅ 동일한 데이터 저장 유형 ({src.StorageTypeName})입니다. 반올림 왜곡 없이 원본 수치 그대로 1:1 복사됩니다.";
                IsCompatible = true;
                return;
            }

            if (tgt.StorageType == StorageType.String)
            {
                TypeCompatibilityNotice = $"ℹ️ 유형 변환: {src.StorageTypeName} ➔ 문자 (String)\n" +
                                          $"단위 반올림 왜곡 없이 원본의 정밀 수치(예: '249')를 그대로 문자열로 변환하여 주입합니다.";
                IsCompatible = true;
            }
            else if (tgt.StorageType == StorageType.Double && src.StorageType == StorageType.String)
            {
                TypeCompatibilityNotice = $"ℹ️ 유형 변환: 문자 (String) ➔ 숫자/실수 (Double)\n" +
                                          $"텍스트의 수치를 Revit 프로젝트 단위(mm 등)에 맞춰 내부 단위로 정밀 변환하여 주입합니다.";
                IsCompatible = true;
            }
            else if (tgt.StorageType == StorageType.Integer && src.StorageType == StorageType.Double)
            {
                TypeCompatibilityNotice = $"ℹ️ 유형 변환: 실수 (Double) ➔ 정수 (Integer)\n" +
                                          $"Revit 프로젝트 표시 수치(예: 249 mm -> 249)를 정수로 변환하여 주입합니다.";
                IsCompatible = true;
            }
            else if (tgt.StorageType == StorageType.Integer && src.StorageType == StorageType.String)
            {
                TypeCompatibilityNotice = $"ℹ️ 유형 변환: 문자 (String) ➔ 정수/예·아니오 (Integer)\n" +
                                          $"정수 수치 또는 예/아니오(1/0)로 자동 변환 주입됩니다.";
                IsCompatible = true;
            }
            else
            {
                TypeCompatibilityNotice = $"⚠️ 유형 변환: {src.StorageTypeName} ➔ {tgt.StorageTypeName}\n" +
                                          $"스마트 자동 변환을 시도합니다.";
                IsCompatible = true;
            }
        }

        private void UpdatePreview()
        {
            PreviewRows.Clear();
            var doc = GetCurrentDoc();
            if (doc == null || SelectedSourceParameter == null || SelectedTargetParameter == null || _currentElements.Count == 0)
                return;

            var options = new CopyConversionOptions
            {
                DecimalPlaces = SelectedDecimalPlaceOption?.Places ?? 0,
                AppendUnitSymbol = AppendUnitSymbol,
                ConvertYesNoToKorean = ConvertYesNoToKorean,
                OverwriteExisting = OverwriteExisting,
                SkipEmptySource = SkipEmptySource
            };

            int sampleCount = Math.Min(_currentElements.Count, 15);
            var sampleElements = _currentElements.Take(sampleCount).ToList();

            Dictionary<ElementId, Dictionary<string, string>>? previewCache = null;
            if (SelectedSourceParameter.IsScheduleField && doc.ActiveView is ViewSchedule sched)
            {
                previewCache = _service.BuildScheduleCellCache(sched, _currentElements, doc);
            }

            for (int i = 0; i < sampleElements.Count; i++)
            {
                var elem = sampleElements[i];
                if (elem == null) continue;

                var tgtParam = elem.LookupParameter(SelectedTargetParameter.Name);
                string tgtVal = tgtParam != null ? _service.GetParameterDisplayValue(tgtParam, doc, options.DecimalPlaces) : "(매개변수 없음)";
                string srcVal = string.Empty;
                string convertedVal = "-";
                string status = "대기";

                if (SelectedSourceParameter.IsScheduleField)
                {
                    srcVal = _service.GetSourceValueString(SelectedSourceParameter, elem, doc, previewCache, options.DecimalPlaces);
                    if (string.IsNullOrEmpty(srcVal)) srcVal = "(값 없음)";

                    if (tgtParam != null)
                    {
                        if (_service.TryConvertRawStringToTarget(srcVal, tgtParam, doc, options, out _, out string preview, out string err))
                        {
                            convertedVal = preview;
                            status = "변환 성공";
                        }
                        else
                        {
                            convertedVal = "-";
                            status = err;
                        }
                    }
                    else
                    {
                        status = "대상 없음";
                    }
                }
                else
                {
                    var srcParam = elem.LookupParameter(SelectedSourceParameter.Name);
                    srcVal = srcParam != null ? _service.GetParameterDisplayValue(srcParam, doc, options.DecimalPlaces) : "(매개변수 없음)";

                    if (srcParam != null && tgtParam != null)
                    {
                        if (_service.TryConvertParameterValue(srcParam, tgtParam, doc, options, out _, out string preview, out string err))
                        {
                            convertedVal = preview;
                            status = "변환 성공";
                        }
                        else
                        {
                            convertedVal = "-";
                            status = err;
                        }
                    }
                    else if (tgtParam == null)
                    {
                        status = "대상 없음";
                    }
                }

                PreviewRows.Add(new PreviewRowModel
                {
                    ElementIdValue = elem.Id.Value,
                    ElementName = elem.Name,
                    CategoryName = elem.Category?.Name ?? "-",
                    SourceCurrentValue = srcVal,
                    TargetCurrentValue = tgtVal,
                    ConvertedNewValue = convertedVal,
                    Status = status
                });
            }
        }

        private void ExecuteCopy()
        {
            if (SelectedSourceParameter == null)
            {
                MessageBox.Show("원본 매개변수(A)를 선택해주세요.", "선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedTargetParameter == null)
            {
                MessageBox.Show("대상 매개변수(B)를 선택해주세요.", "선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedTargetParameter.IsReadOnly)
            {
                MessageBox.Show("대상 매개변수가 읽기 전용입니다. 값을 수정할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_currentElements.Count == 0)
            {
                MessageBox.Show("선택된 범위에 적용할 요소가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string confirmMsg = $"총 {_currentElements.Count}개 요소에 대해\n" +
                               $"'{SelectedSourceParameter.Name}' ➔ '{SelectedTargetParameter.Name}'\n\n" +
                               $"[소수점 자릿수: {SelectedDecimalPlaceOption?.DisplayName}]\n\n" +
                               $"매개변수 값을 복사하시겠습니까?";

            if (MessageBox.Show(confirmMsg, "매개변수 값 복사 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            StatusMessage = "Revit 트랜잭션 실행 중...";

            var options = new CopyConversionOptions
            {
                DecimalPlaces = SelectedDecimalPlaceOption?.Places ?? 0,
                AppendUnitSymbol = AppendUnitSymbol,
                ConvertYesNoToKorean = ConvertYesNoToKorean,
                OverwriteExisting = OverwriteExisting,
                SkipEmptySource = SkipEmptySource
            };

            var srcModel = SelectedSourceParameter;
            string tgtName = SelectedTargetParameter.Name;
            var targetList = new List<Element>(_currentElements);

            AppendLog($"\n>>> [{DateTime.Now:HH:mm:ss}] 매개변수 값 복사 요청: '{srcModel.Name}' ➔ '{tgtName}' ({targetList.Count}개 요소)...");

            // Execute via ExternalEvent in Revit thread!
            _handler.Run(app =>
            {
                Document doc = app.ActiveUIDocument.Document;
                ViewSchedule? activeSchedule = doc.ActiveView as ViewSchedule;
                var result = _service.ExecuteCopy(doc, targetList, srcModel, tgtName, options, activeSchedule, AppendLog);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    string summary = $"완료: 성공 {result.SuccessCount}건, 건너뜀 {result.SkipCount}건, 오류 {result.ErrorCount}건";
                    StatusMessage = summary;
                    AppendLog($"<<< {summary}\n");

                    UpdatePreview();

                    MessageBox.Show($"매개변수 값 복사가 완료되었습니다!\n\n" +
                                    $"- 대상 요소 수: {result.TotalCount}개\n" +
                                    $"- 성공: {result.SuccessCount}개\n" +
                                    $"- 건너뜀: {result.SkipCount}개\n" +
                                    $"- 오류: {result.ErrorCount}개\n\n" +
                                    $"창을 닫지 않고 Revit에서 객체를 클릭하여 특성 창(Properties)을 바로 확인하실 수 있습니다.",
                                    "작업 완료", MessageBoxButton.OK,
                                    result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                });
            }, _exEvent);
        }

        private void AppendLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                ExecutionLog = _logBuilder.ToString();
            });
        }

        private void ClearLog()
        {
            _logBuilder.Clear();
            ExecutionLog = string.Empty;
        }
    }
}
