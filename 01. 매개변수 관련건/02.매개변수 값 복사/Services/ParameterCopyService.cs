using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ParameterValueCopier.Models;

namespace ParameterValueCopier.Services
{
    public class CopyConversionOptions
    {
        public int DecimalPlaces { get; set; } = 0; // 기본값 0자리 (정수 반올림: 635.6165 -> 636). -1이면 원본 정밀값 유지
        public bool AppendUnitSymbol { get; set; } = false; // mm 등 단위 기호 부착 여부
        public bool ConvertYesNoToKorean { get; set; } = true;
        public bool OverwriteExisting { get; set; } = true;
        public bool SkipEmptySource { get; set; } = true;
    }

    public class CopyExecutionResult
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int SkipCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Logs { get; set; } = new();
    }

    public class ParameterCopyService
    {
        public List<Element> GetTargetElements(Document doc, UIDocument? uidoc, ElementScope scope, ElementId? categoryId)
        {
            var results = new List<Element>();
            if (doc == null) return results;

            switch (scope)
            {
                case ElementScope.ActiveViewOrSchedule:
                    if (doc.ActiveView is ViewSchedule schedule)
                    {
                        var collector = new FilteredElementCollector(doc, schedule.Id)
                            .WhereElementIsNotElementType();
                        results = collector.ToElements().ToList();
                    }
                    else if (doc.ActiveView != null)
                    {
                        var collector = new FilteredElementCollector(doc, doc.ActiveView.Id)
                            .WhereElementIsNotElementType();
                        results = collector.ToElements().ToList();
                    }
                    break;

                case ElementScope.SelectedElements:
                    if (uidoc != null)
                    {
                        var selIds = uidoc.Selection.GetElementIds();
                        foreach (var id in selIds)
                        {
                            var elem = doc.GetElement(id);
                            if (elem != null && !(elem is ElementType))
                            {
                                results.Add(elem);
                            }
                        }
                    }
                    break;

                case ElementScope.CategoryElements:
                    if (categoryId != null && categoryId != ElementId.InvalidElementId)
                    {
                        var collector = new FilteredElementCollector(doc)
                            .OfCategoryId(categoryId)
                            .WhereElementIsNotElementType();
                        results = collector.ToElements().ToList();
                    }
                    break;
            }

            return results;
        }

        public List<ParameterItemModel> GetScheduleVirtualParameters(ViewSchedule schedule, Document doc)
        {
            var list = new List<ParameterItemModel>();
            if (schedule?.Definition == null) return list;

            ScheduleDefinition def = schedule.Definition;
            var fieldOrder = def.GetFieldOrder();

            for (int i = 0; i < fieldOrder.Count; i++)
            {
                var fieldId = fieldOrder[i];
                ScheduleField field = def.GetField(fieldId);
                if (field == null) continue;

                string name = field.GetName();
                bool isCalculated = field.IsCalculatedField || field.FieldType == ScheduleFieldType.Formula;
                bool isCombined = field.IsCombinedParameterField || field.FieldType == ScheduleFieldType.CombinedParameter;

                if (isCalculated)
                {
                    ForgeTypeId? spec = field.GetSpecTypeId();
                    string specName = spec?.TypeId ?? "수식/실수";
                    list.Add(new ParameterItemModel
                    {
                        Name = name,
                        StorageType = StorageType.Double,
                        IsReadOnly = true,
                        GroupName = $"일람표 계산식: {schedule.Name}",
                        DataTypeName = specName,
                        SpecTypeId = spec,
                        IsScheduleField = true,
                        IsCalculatedField = true,
                        ScheduleFieldId = fieldId,
                        ScheduleColumnIndex = i
                    });
                }
                else if (isCombined)
                {
                    var combined = field.GetCombinedParameters();
                    list.Add(new ParameterItemModel
                    {
                        Name = name,
                        StorageType = StorageType.String,
                        IsReadOnly = true,
                        GroupName = $"일람표 결합: {schedule.Name}",
                        DataTypeName = "문자 (결합식)",
                        IsScheduleField = true,
                        IsCombinedParameterField = true,
                        ScheduleFieldId = fieldId,
                        ScheduleColumnIndex = i,
                        CombinedParts = combined
                    });
                }
            }

            return list;
        }

        public List<ParameterItemModel> GetParametersFromElements(List<Element> elements)
        {
            var dict = new Dictionary<string, ParameterItemModel>(StringComparer.OrdinalIgnoreCase);
            if (elements == null || elements.Count == 0) return dict.Values.ToList();

            int sampleCount = Math.Min(elements.Count, 30);
            for (int i = 0; i < sampleCount; i++)
            {
                var elem = elements[i];
                if (elem == null) continue;

                foreach (Parameter p in elem.Parameters)
                {
                    if (p?.Definition == null || string.IsNullOrWhiteSpace(p.Definition.Name))
                        continue;

                    string name = p.Definition.Name;
                    if (!dict.ContainsKey(name))
                    {
                        string groupName = p.Definition.GetGroupTypeId()?.TypeId ?? "일반";
                        string dataTypeName = p.Definition.GetDataType()?.TypeId ?? p.StorageType.ToString();

                        dict[name] = new ParameterItemModel
                        {
                            Name = name,
                            StorageType = p.StorageType,
                            IsReadOnly = p.IsReadOnly,
                            GroupName = groupName,
                            DataTypeName = dataTypeName,
                            SpecTypeId = p.Definition.GetDataType()
                        };
                    }
                }
            }

            return dict.Values.OrderBy(p => p.Name).ToList();
        }

        public static Parameter? GetParameterById(Element elem, ElementId paramId)
        {
            if (elem == null || paramId == null || paramId == ElementId.InvalidElementId) return null;

            if (paramId.Value < 0 && Enum.IsDefined(typeof(BuiltInParameter), (int)paramId.Value))
            {
                Parameter p = elem.get_Parameter((BuiltInParameter)(int)paramId.Value);
                if (p != null) return p;
            }

            try
            {
                Element defElem = elem.Document.GetElement(paramId);
                if (defElem is ParameterElement pe)
                {
                    Parameter p = elem.get_Parameter(pe.GetDefinition());
                    if (p != null) return p;
                }

                if (defElem != null && !string.IsNullOrEmpty(defElem.Name))
                {
                    Parameter p = elem.LookupParameter(defElem.Name);
                    if (p != null) return p;
                }
            }
            catch { }

            return null;
        }

        public string EvaluateCombinedParameters(Element elem, IList<TableCellCombinedParameterData>? combinedParams, Document doc, int decimalPlaces = 0)
        {
            if (elem == null || combinedParams == null || combinedParams.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < combinedParams.Count; i++)
            {
                var part = combinedParams[i];
                Parameter? p = GetParameterById(elem, part.ParamId);

                string valStr = string.Empty;
                if (p != null && p.HasValue)
                {
                    if (p.StorageType == StorageType.Double)
                    {
                        double exact = GetExactValueInDisplayUnits(p, doc);
                        valStr = FormatNumber(exact, decimalPlaces);
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        valStr = p.AsInteger().ToString();
                    }
                    else if (p.StorageType == StorageType.String)
                    {
                        valStr = p.AsString() ?? string.Empty;
                    }
                    else if (p.StorageType == StorageType.ElementId)
                    {
                        var refElem = doc.GetElement(p.AsElementId());
                        valStr = refElem != null ? refElem.Name : p.AsElementId().ToString();
                    }
                }

                if (!string.IsNullOrEmpty(valStr))
                {
                    if (!string.IsNullOrEmpty(part.Prefix))
                        sb.Append(part.Prefix);

                    sb.Append(valStr);

                    if (!string.IsNullOrEmpty(part.Suffix))
                        sb.Append(part.Suffix);
                }

                if (i < combinedParams.Count - 1 && !string.IsNullOrEmpty(part.Separator))
                {
                    sb.Append(part.Separator);
                }
            }

            return sb.ToString().Trim();
        }

        public static Parameter? GetElementParameterForField(Element elem, ScheduleField field)
        {
            if (elem == null || field == null) return null;

            if (field.ParameterId != null && field.ParameterId != ElementId.InvalidElementId)
            {
                if (field.ParameterId.Value < 0)
                {
                    try
                    {
                        var bp = elem.get_Parameter((BuiltInParameter)field.ParameterId.Value);
                        if (bp != null) return bp;
                    }
                    catch { }
                }

                foreach (Parameter p in elem.Parameters)
                {
                    if (p.Id == field.ParameterId) return p;
                }
            }

            string fieldName = field.GetName();
            if (!string.IsNullOrEmpty(fieldName))
            {
                var p = elem.LookupParameter(fieldName);
                if (p != null) return p;
            }

            string heading = field.ColumnHeading;
            if (!string.IsNullOrEmpty(heading) && !heading.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                var p = elem.LookupParameter(heading);
                if (p != null) return p;
            }

            return null;
        }

        private static string GetCellTextSafe(ViewSchedule schedule, TableSectionData body, int row, int col)
        {
            try
            {
                string t = schedule.GetCellText(SectionType.Body, row, col);
                if (!string.IsNullOrEmpty(t)) return t;
            }
            catch { }

            try
            {
                return body.GetCellText(row, col);
            }
            catch
            {
                return string.Empty;
            }
        }

        public Dictionary<ElementId, Dictionary<string, string>> BuildScheduleCellCache(ViewSchedule schedule, List<Element> elements, Document doc)
        {
            var cache = new Dictionary<ElementId, Dictionary<string, string>>();
            if (schedule == null || elements == null || elements.Count == 0) return cache;

            TableData tableData = schedule.GetTableData();
            if (tableData == null) return cache;
            TableSectionData body = tableData.GetSectionData(SectionType.Body);
            if (body == null || body.NumberOfRows == 0 || body.NumberOfColumns == 0) return cache;

            ScheduleDefinition def = schedule.Definition;
            var fieldOrder = def.GetFieldOrder().ToList();

            var visibleFields = new List<ScheduleField>();
            for (int i = 0; i < fieldOrder.Count; i++)
            {
                var f = def.GetField(fieldOrder[i]);
                if (f != null && !f.IsHidden)
                {
                    visibleFields.Add(f);
                }
            }

            var elementByMark = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);

            void RegisterElementKey(string? key, Element elem)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    string trimmed = key.Trim();
                    if (!elementByMark.ContainsKey(trimmed))
                    {
                        elementByMark[trimmed] = elem;
                    }
                }
            }

            foreach (var elem in elements)
            {
                if (elem == null) continue;

                var pMark = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                if (pMark != null && pMark.HasValue) RegisterElementKey(pMark.AsString(), elem);

                RegisterElementKey(elem.LookupParameter("마크")?.AsString(), elem);
                RegisterElementKey(elem.LookupParameter("Mark")?.AsString(), elem);
                RegisterElementKey(elem.LookupParameter("BH_NAME")?.AsString(), elem);
                RegisterElementKey(elem.LookupParameter("BH_STD_NAME")?.AsString(), elem);
                RegisterElementKey(elem.Name, elem);

                foreach (var field in visibleFields)
                {
                    if (!field.IsCalculatedField && !field.IsCombinedParameterField)
                    {
                        try
                        {
                            var p = GetElementParameterForField(elem, field);
                            if (p != null && p.HasValue)
                            {
                                string valStr = p.StorageType == StorageType.String ? (p.AsString() ?? string.Empty) : p.AsValueString();
                                RegisterElementKey(valStr, elem);
                            }
                        }
                        catch { }
                    }
                }
            }

            int firstRow = body.FirstRowNumber;
            int lastRow = body.LastRowNumber;
            int firstCol = body.FirstColumnNumber;
            int lastCol = body.LastColumnNumber;

            int firstDataRow = firstRow;
            while (firstDataRow <= lastRow)
            {
                bool hasElementMatch = false;
                bool hasValidNumber = false;
                int headerMatches = 0;

                for (int c = firstCol; c <= lastCol; c++)
                {
                    string text = GetCellTextSafe(schedule, body, firstDataRow, c).Trim();
                    if (string.IsNullOrEmpty(text)) continue;

                    if (elementByMark.ContainsKey(text))
                    {
                        hasElementMatch = true;
                        break;
                    }

                    foreach (var f in visibleFields)
                    {
                        if (text.Equals(f.ColumnHeading, StringComparison.OrdinalIgnoreCase) ||
                            text.Equals(f.GetName(), StringComparison.OrdinalIgnoreCase) ||
                            text.Equals("마크", StringComparison.OrdinalIgnoreCase) ||
                            text.Equals("Mark", StringComparison.OrdinalIgnoreCase))
                        {
                            headerMatches++;
                            break;
                        }
                    }

                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ||
                        double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out _))
                    {
                        hasValidNumber = true;
                    }
                }

                if (hasElementMatch || hasValidNumber)
                {
                    break;
                }

                if (headerMatches > 0 || !hasElementMatch)
                {
                    firstDataRow++;
                }
                else
                {
                    break;
                }
            }

            var colToFieldNames = new Dictionary<int, List<string>>();
            for (int c = firstCol; c <= lastCol; c++)
            {
                var names = new List<string> { c.ToString() };

                int fieldIdx = c - firstCol;
                if (fieldIdx >= 0 && fieldIdx < visibleFields.Count)
                {
                    var f = visibleFields[fieldIdx];
                    names.Add(f.GetName());
                    names.Add(f.ColumnHeading);
                }

                for (int hr = firstRow; hr < firstDataRow; hr++)
                {
                    string hText = GetCellTextSafe(schedule, body, hr, c).Trim();
                    if (!string.IsNullOrEmpty(hText) && !names.Contains(hText, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(hText);
                    }
                }

                colToFieldNames[c] = names;
            }

            int dataRowIndex = 0;
            for (int r = firstDataRow; r <= lastRow; r++)
            {
                Element? matchedElem = null;

                for (int c = firstCol; c <= lastCol; c++)
                {
                    string cellText = GetCellTextSafe(schedule, body, r, c).Trim();
                    if (!string.IsNullOrEmpty(cellText) && elementByMark.TryGetValue(cellText, out var e))
                    {
                        matchedElem = e;
                        break;
                    }
                }

                if (matchedElem == null && dataRowIndex < elements.Count)
                {
                    matchedElem = elements[dataRowIndex];
                }

                if (matchedElem != null)
                {
                    if (!cache.ContainsKey(matchedElem.Id))
                    {
                        cache[matchedElem.Id] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    for (int c = firstCol; c <= lastCol; c++)
                    {
                        string cellText = GetCellTextSafe(schedule, body, r, c);
                        if (colToFieldNames.TryGetValue(c, out var aliases))
                        {
                            foreach (var alias in aliases)
                            {
                                cache[matchedElem.Id][alias] = cellText;
                            }
                        }
                        cache[matchedElem.Id][c.ToString()] = cellText;
                    }
                }

                dataRowIndex++;
            }

            return cache;
        }

        public string GetSourceValueString(
            ParameterItemModel sourceModel,
            Element elem,
            Document doc,
            Dictionary<ElementId, Dictionary<string, string>>? cellCache = null,
            int decimalPlaces = 0)
        {
            if (elem == null || sourceModel == null) return string.Empty;

            if (sourceModel.IsScheduleField)
            {
                if (cellCache != null && cellCache.TryGetValue(elem.Id, out var cols))
                {
                    if (cols.TryGetValue(sourceModel.Name, out string? text) && !string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }

                    if (cols.TryGetValue(sourceModel.ScheduleColumnIndex.ToString(), out text) && !string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }

                    foreach (var kvp in cols)
                    {
                        if (kvp.Key.Contains(sourceModel.Name, StringComparison.OrdinalIgnoreCase) ||
                            sourceModel.Name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrWhiteSpace(kvp.Value))
                                return kvp.Value.Trim();
                        }
                    }
                }

                if (sourceModel.IsCombinedParameterField)
                {
                    return EvaluateCombinedParameters(elem, sourceModel.CombinedParts, doc, decimalPlaces);
                }

                if (sourceModel.IsCalculatedField)
                {
                    return string.Empty;
                }
            }

            var p = elem.LookupParameter(sourceModel.Name);
            return GetParameterDisplayValue(p, doc, decimalPlaces);
        }

        public static double GetExactValueInDisplayUnits(Parameter param, Document doc)
        {
            if (param.StorageType != StorageType.Double) return 0;

            double internalVal = param.AsDouble();
            ForgeTypeId? spec = param.Definition?.GetDataType();
            if (spec != null && UnitUtils.IsMeasurableSpec(spec))
            {
                try
                {
                    Units units = doc.GetUnits();
                    FormatOptions fo = units.GetFormatOptions(spec);
                    ForgeTypeId unitTypeId = fo.GetUnitTypeId();
                    return UnitUtils.ConvertFromInternalUnits(internalVal, unitTypeId);
                }
                catch { }
            }

            // Fallback for length (internal feet -> mm)
            if (spec != null && spec.TypeId.Contains("length", StringComparison.OrdinalIgnoreCase))
            {
                return internalVal * 304.8;
            }

            return internalVal;
        }

        public static double RoundValue(double val, int decimalPlaces)
        {
            if (decimalPlaces < 0) return val;
            return Math.Round(val, decimalPlaces, MidpointRounding.AwayFromZero);
        }

        public static string FormatNumber(double val, int decimalPlaces)
        {
            if (decimalPlaces >= 0)
            {
                double rounded = Math.Round(val, decimalPlaces, MidpointRounding.AwayFromZero);
                if (decimalPlaces == 0)
                {
                    return ((long)rounded).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    string format = "0." + new string('0', decimalPlaces);
                    return rounded.ToString(format, CultureInfo.InvariantCulture);
                }
            }

            // decimalPlaces < 0: unrounded exact number
            if (Math.Abs(val - Math.Round(val)) < 0.0001)
            {
                return ((long)Math.Round(val)).ToString(CultureInfo.InvariantCulture);
            }
            return val.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public static string FormatExactNumber(double val) => FormatNumber(val, -1);

        public string GetParameterDisplayValue(Parameter? param, Document doc, int decimalPlaces = 0)
        {
            if (param == null || !param.HasValue)
                return "(비어있음)";

            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString() ?? string.Empty;

                case StorageType.Integer:
                    var dataType = param.Definition?.GetDataType();
                    if (dataType != null && dataType.TypeId.Contains("boolean", StringComparison.OrdinalIgnoreCase))
                    {
                        return param.AsInteger() == 1 ? "예" : "아니오";
                    }
                    return param.AsInteger().ToString();

                case StorageType.Double:
                    double exact = GetExactValueInDisplayUnits(param, doc);
                    return FormatNumber(exact, decimalPlaces);

                case StorageType.ElementId:
                    var id = param.AsElementId();
                    if (id == ElementId.InvalidElementId)
                        return "(없음)";
                    var elem = doc.GetElement(id);
                    return elem != null ? elem.Name : id.ToString();

                default:
                    return param.AsValueString() ?? string.Empty;
            }
        }

        public bool TryConvertRawStringToTarget(
            string sourceStr,
            Parameter targetParam,
            Document doc,
            CopyConversionOptions options,
            out Action? applyAction,
            out string previewValue,
            out string errorMessage)
        {
            applyAction = null;
            previewValue = string.Empty;
            errorMessage = string.Empty;

            if (targetParam.IsReadOnly)
            {
                errorMessage = "대상 매개변수가 읽기 전용입니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceStr) || sourceStr == "(비어있음)" || sourceStr == "(매개변수 없음)")
            {
                if (options.SkipEmptySource)
                {
                    errorMessage = "원본 값이 비어있어 건너뜀";
                    return false;
                }
                previewValue = "(비우기)";
                applyAction = () =>
                {
                    if (targetParam.StorageType == StorageType.String)
                        targetParam.Set(string.Empty);
                };
                return true;
            }

            if (!options.OverwriteExisting && targetParam.HasValue)
            {
                errorMessage = "대상에 이미 값이 있어 건너뜀 (덮어쓰기 비활성)";
                return false;
            }

            try
            {
                // 1. Target is String
                if (targetParam.StorageType == StorageType.String)
                {
                    string cleanNumStr = new string(sourceStr.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray()).Trim();
                    if (options.DecimalPlaces >= 0 &&
                        (double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double num) ||
                         double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.CurrentCulture, out num)))
                    {
                        string formatted = FormatNumber(num, options.DecimalPlaces);
                        previewValue = formatted;
                        applyAction = () => targetParam.Set(formatted);
                        return true;
                    }

                    previewValue = sourceStr;
                    applyAction = () => targetParam.Set(sourceStr);
                    return true;
                }

                // 2. Target is Double
                if (targetParam.StorageType == StorageType.Double)
                {
                    ForgeTypeId? targetSpec = targetParam.Definition?.GetDataType();
                    bool isTargetMeasurable = targetSpec != null && UnitUtils.IsMeasurableSpec(targetSpec);

                    string cleanNumStr = new string(sourceStr.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray()).Trim();

                    if (double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal) ||
                        double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedVal))
                    {
                        double rounded = RoundValue(parsedVal, options.DecimalPlaces);
                        previewValue = FormatNumber(rounded, options.DecimalPlaces);
                        if (isTargetMeasurable)
                        {
                            var fo = doc.GetUnits().GetFormatOptions(targetSpec!);
                            double internalFeet = UnitUtils.ConvertToInternalUnits(rounded, fo.GetUnitTypeId());
                            applyAction = () => targetParam.Set(internalFeet);
                        }
                        else
                        {
                            applyAction = () => targetParam.Set(rounded);
                        }
                        return true;
                    }
                    else
                    {
                        errorMessage = $"수치 변환 불가: '{sourceStr}'";
                        return false;
                    }
                }

                // 3. Target is Integer
                if (targetParam.StorageType == StorageType.Integer)
                {
                    bool isTargetYesNo = targetParam.Definition?.GetDataType()?.TypeId.Contains("boolean", StringComparison.OrdinalIgnoreCase) == true;
                    int intVal = 0;
                    if (isTargetYesNo)
                    {
                        string lower = sourceStr.Trim().ToLowerInvariant();
                        intVal = (lower == "예" || lower == "true" || lower == "1" || lower == "참" || lower == "yes") ? 1 : 0;
                    }
                    else
                    {
                        string cleanNumStr = new string(sourceStr.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray()).Trim();
                        if (!int.TryParse(cleanNumStr, out intVal))
                        {
                            if (double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ||
                                double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                            {
                                intVal = (int)Math.Round(d);
                            }
                        }
                    }

                    previewValue = isTargetYesNo ? (intVal == 1 ? "예" : "아니오") : intVal.ToString();
                    applyAction = () => targetParam.Set(intVal);
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"변환 오류: {ex.Message}";
                return false;
            }

            errorMessage = $"지원되지 않는 대상 유형: {targetParam.StorageType}";
            return false;
        }

        public bool TryConvertParameterValue(
            Parameter sourceParam,
            Parameter targetParam,
            Document doc,
            CopyConversionOptions options,
            out Action? applyAction,
            out string previewValue,
            out string errorMessage)
        {
            applyAction = null;
            previewValue = string.Empty;
            errorMessage = string.Empty;

            if (targetParam.IsReadOnly)
            {
                errorMessage = "대상 매개변수가 읽기 전용입니다.";
                return false;
            }

            if (!sourceParam.HasValue)
            {
                if (options.SkipEmptySource)
                {
                    errorMessage = "원본 값이 비어있어 건너뜀";
                    return false;
                }
                previewValue = "(비우기)";
                applyAction = () =>
                {
                    if (targetParam.StorageType == StorageType.String)
                        targetParam.Set(string.Empty);
                };
                return true;
            }

            if (!options.OverwriteExisting && targetParam.HasValue)
            {
                errorMessage = "대상에 이미 값이 있어 건너뜀 (덮어쓰기 비활성)";
                return false;
            }

            try
            {
                // CASE 1: TARGET IS STRING
                if (targetParam.StorageType == StorageType.String)
                {
                    string finalString = string.Empty;

                    if (sourceParam.StorageType == StorageType.String)
                    {
                        finalString = sourceParam.AsString() ?? string.Empty;
                    }
                    else if (sourceParam.StorageType == StorageType.Double)
                    {
                        double exact = GetExactValueInDisplayUnits(sourceParam, doc);
                        finalString = FormatNumber(exact, options.DecimalPlaces);

                        if (options.AppendUnitSymbol)
                        {
                            try
                            {
                                string valStr = sourceParam.AsValueString();
                                if (!string.IsNullOrWhiteSpace(valStr))
                                {
                                    string nonDigits = new string(valStr.Where(c => !char.IsDigit(c) && c != '.' && c != '-').ToArray()).Trim();
                                    if (!string.IsNullOrEmpty(nonDigits))
                                        finalString += " " + nonDigits;
                                }
                            }
                            catch { }
                        }
                    }
                    else if (sourceParam.StorageType == StorageType.Integer)
                    {
                        var srcSpec = sourceParam.Definition?.GetDataType();
                        if (options.ConvertYesNoToKorean && srcSpec != null && srcSpec.TypeId.Contains("boolean", StringComparison.OrdinalIgnoreCase))
                        {
                            finalString = sourceParam.AsInteger() == 1 ? "예" : "아니오";
                        }
                        else
                        {
                            finalString = sourceParam.AsInteger().ToString();
                        }
                    }
                    else if (sourceParam.StorageType == StorageType.ElementId)
                    {
                        var elem = doc.GetElement(sourceParam.AsElementId());
                        finalString = elem != null ? elem.Name : sourceParam.AsElementId().ToString();
                    }

                    previewValue = finalString;
                    applyAction = () => targetParam.Set(finalString);
                    return true;
                }

                // CASE 2: TARGET IS DOUBLE
                if (targetParam.StorageType == StorageType.Double)
                {
                    ForgeTypeId? targetSpec = targetParam.Definition?.GetDataType();
                    bool isTargetMeasurable = targetSpec != null && UnitUtils.IsMeasurableSpec(targetSpec);

                    if (sourceParam.StorageType == StorageType.Double)
                    {
                        double displayVal = GetExactValueInDisplayUnits(sourceParam, doc);
                        double roundedVal = RoundValue(displayVal, options.DecimalPlaces);
                        previewValue = FormatNumber(roundedVal, options.DecimalPlaces);

                        if (isTargetMeasurable)
                        {
                            var fo = doc.GetUnits().GetFormatOptions(targetSpec!);
                            double internalFeet = UnitUtils.ConvertToInternalUnits(roundedVal, fo.GetUnitTypeId());
                            applyAction = () => targetParam.Set(internalFeet);
                        }
                        else
                        {
                            applyAction = () => targetParam.Set(roundedVal);
                        }
                        return true;
                    }
                    else if (sourceParam.StorageType == StorageType.Integer)
                    {
                        double intVal = (double)sourceParam.AsInteger();
                        previewValue = FormatNumber(intVal, options.DecimalPlaces);

                        if (isTargetMeasurable)
                        {
                            var fo = doc.GetUnits().GetFormatOptions(targetSpec!);
                            double internalFeet = UnitUtils.ConvertToInternalUnits(intVal, fo.GetUnitTypeId());
                            applyAction = () => targetParam.Set(internalFeet);
                        }
                        else
                        {
                            applyAction = () => targetParam.Set(intVal);
                        }
                        return true;
                    }
                    else if (sourceParam.StorageType == StorageType.String)
                    {
                        string str = sourceParam.AsString()?.Trim() ?? string.Empty;
                        string cleanNumStr = new string(str.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray()).Trim();

                        if (double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedVal) ||
                            double.TryParse(cleanNumStr, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedVal))
                        {
                            double roundedVal = RoundValue(parsedVal, options.DecimalPlaces);
                            previewValue = FormatNumber(roundedVal, options.DecimalPlaces);

                            applyAction = () =>
                            {
                                if (isTargetMeasurable)
                                {
                                    var fo = doc.GetUnits().GetFormatOptions(targetSpec!);
                                    double internalFeet = UnitUtils.ConvertToInternalUnits(roundedVal, fo.GetUnitTypeId());
                                    targetParam.Set(internalFeet);
                                }
                                else
                                {
                                    targetParam.Set(roundedVal);
                                }
                            };
                            return true;
                        }
                        else
                        {
                            previewValue = str;
                            applyAction = () => targetParam.SetValueString(str);
                            return true;
                        }
                    }
                }

                // CASE 3: TARGET IS INTEGER
                if (targetParam.StorageType == StorageType.Integer)
                {
                    bool isTargetYesNo = targetParam.Definition?.GetDataType()?.TypeId.Contains("boolean", StringComparison.OrdinalIgnoreCase) == true;

                    if (sourceParam.StorageType == StorageType.Integer)
                    {
                        int val = sourceParam.AsInteger();
                        previewValue = isTargetYesNo ? (val == 1 ? "예" : "아니오") : val.ToString();
                        applyAction = () => targetParam.Set(val);
                        return true;
                    }
                    else if (sourceParam.StorageType == StorageType.Double)
                    {
                        double exact = GetExactValueInDisplayUnits(sourceParam, doc);
                        int intVal = isTargetYesNo ? (exact > 0 ? 1 : 0) : (int)Math.Round(exact);
                        previewValue = isTargetYesNo ? (intVal == 1 ? "예" : "아니오") : intVal.ToString();
                        applyAction = () => targetParam.Set(intVal);
                        return true;
                    }
                    else if (sourceParam.StorageType == StorageType.String)
                    {
                        string str = sourceParam.AsString()?.Trim() ?? string.Empty;
                        int intVal = 0;
                        if (isTargetYesNo)
                        {
                            string lower = str.ToLowerInvariant();
                            intVal = (lower == "예" || lower == "true" || lower == "1" || lower == "참" || lower == "yes") ? 1 : 0;
                        }
                        else
                        {
                            if (!int.TryParse(str, out intVal))
                            {
                                if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ||
                                    double.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                                {
                                    intVal = (int)Math.Round(d);
                                }
                            }
                        }
                        previewValue = isTargetYesNo ? (intVal == 1 ? "예" : "아니오") : intVal.ToString();
                        applyAction = () => targetParam.Set(intVal);
                        return true;
                    }
                }

                // CASE 4: TARGET IS ELEMENT ID
                if (targetParam.StorageType == StorageType.ElementId)
                {
                    if (sourceParam.StorageType == StorageType.ElementId)
                    {
                        var id = sourceParam.AsElementId();
                        previewValue = id.ToString();
                        applyAction = () => targetParam.Set(id);
                        return true;
                    }
                    else if (sourceParam.StorageType == StorageType.Integer)
                    {
                        var id = new ElementId((long)sourceParam.AsInteger());
                        previewValue = id.ToString();
                        applyAction = () => targetParam.Set(id);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"변환 계산 오류: {ex.Message}";
                return false;
            }

            errorMessage = $"지원되지 않는 변환 유형: {sourceParam.StorageType} -> {targetParam.StorageType}";
            return false;
        }

        public CopyExecutionResult ExecuteCopy(
            Document doc,
            List<Element> elements,
            ParameterItemModel sourceParamModel,
            string targetParamName,
            CopyConversionOptions options,
            ViewSchedule? activeSchedule = null,
            Action<string>? logger = null)
        {
            var result = new CopyExecutionResult { TotalCount = elements.Count };
            if (doc == null || elements.Count == 0 || sourceParamModel == null)
                return result;

            Dictionary<ElementId, Dictionary<string, string>>? cellCache = null;
            if (sourceParamModel.IsScheduleField && activeSchedule != null)
            {
                cellCache = BuildScheduleCellCache(activeSchedule, elements, doc);
            }

            using (Transaction trans = new Transaction(doc, "매개변수 데이터 복사"))
            {
                trans.Start();

                foreach (var elem in elements)
                {
                    if (elem == null) continue;

                    Parameter targetParam = elem.LookupParameter(targetParamName);

                    if (targetParam == null)
                    {
                        result.ErrorCount++;
                        string log = $"[오류] 요소 ID {elem.Id}: 대상 매개변수 '{targetParamName}'을(를) 찾을 수 없습니다.";
                        logger?.Invoke(log);
                        result.Logs.Add(log);
                        continue;
                    }

                    if (targetParam.IsReadOnly)
                    {
                        result.ErrorCount++;
                        string log = $"[읽기 전용] 요소 ID {elem.Id}: 대상 매개변수 '{targetParamName}'이(가) 읽기 전용입니다.";
                        logger?.Invoke(log);
                        result.Logs.Add(log);
                        continue;
                    }

                    bool convertSuccess = false;
                    Action? applyAction = null;
                    string previewVal = string.Empty;
                    string errorMsg = string.Empty;

                    if (sourceParamModel.IsScheduleField)
                    {
                        string sourceStr = GetSourceValueString(sourceParamModel, elem, doc, cellCache, options.DecimalPlaces);
                        convertSuccess = TryConvertRawStringToTarget(sourceStr, targetParam, doc, options, out applyAction, out previewVal, out errorMsg);
                    }
                    else
                    {
                        Parameter sourceParam = elem.LookupParameter(sourceParamModel.Name);
                        if (sourceParam == null)
                        {
                            result.SkipCount++;
                            continue;
                        }
                        convertSuccess = TryConvertParameterValue(sourceParam, targetParam, doc, options, out applyAction, out previewVal, out errorMsg);
                    }

                    if (convertSuccess)
                    {
                        try
                        {
                            applyAction?.Invoke();
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            result.ErrorCount++;
                            string log = $"[실패] 요소 ID {elem.Id}: {ex.Message}";
                            logger?.Invoke(log);
                            result.Logs.Add(log);
                        }
                    }
                    else
                    {
                        result.SkipCount++;
                        if (!string.IsNullOrEmpty(errorMsg) && !errorMsg.Contains("건너뜀"))
                        {
                            string log = $"[건너뜀] 요소 ID {elem.Id}: {errorMsg}";
                            logger?.Invoke(log);
                            result.Logs.Add(log);
                        }
                    }
                }

                trans.Commit();
            }

            return result;
        }
    }
}
