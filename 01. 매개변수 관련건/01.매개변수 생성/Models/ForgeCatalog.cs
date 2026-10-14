using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace ParameterBatchManager.Models
{
    public class DataTypeOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public ForgeTypeId SpecTypeId { get; set; } = null!;
        public string Discipline { get; set; } = "공통";

        public override string ToString() => DisplayName;
    }

    public class GroupOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public ForgeTypeId GroupTypeId { get; set; } = null!;

        public override string ToString() => DisplayName;
    }

    public static class ForgeCatalog
    {
        public static List<DataTypeOption> AvailableDataTypes { get; } = new()
        {
            new DataTypeOption { DisplayName = "문자 (Text)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.String.Text, Discipline = "공통" },
            new DataTypeOption { DisplayName = "길이 (Length)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Length, Discipline = "공통" },
            new DataTypeOption { DisplayName = "정수 (Integer)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Int.Integer, Discipline = "공통" },
            new DataTypeOption { DisplayName = "번호 / 실수 (Number)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Number, Discipline = "공통" },
            new DataTypeOption { DisplayName = "예/아니오 (Yes/No)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Boolean.YesNo, Discipline = "공통" },
            new DataTypeOption { DisplayName = "면적 (Area)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Area, Discipline = "공통" },
            new DataTypeOption { DisplayName = "체적 (Volume)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Volume, Discipline = "공통" },
            new DataTypeOption { DisplayName = "각도 (Angle)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Angle, Discipline = "공통" },
            new DataTypeOption { DisplayName = "통화 (Currency)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Currency, Discipline = "공통" },
            new DataTypeOption { DisplayName = "URL", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.String.Url, Discipline = "공통" },
            new DataTypeOption { DisplayName = "재료 (Material)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Reference.Material, Discipline = "공통" },
            new DataTypeOption { DisplayName = "이미지 (Image)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.Reference.Image, Discipline = "공통" },
            new DataTypeOption { DisplayName = "다중 행 문자 (Multiline Text)", SpecTypeId = Autodesk.Revit.DB.SpecTypeId.String.MultilineText, Discipline = "공통" },
        };

        public static List<GroupOption> AvailableGroups { get; } = new()
        {
            new GroupOption { DisplayName = "데이터 (Data)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Data },
            new GroupOption { DisplayName = "치수 (Dimensions)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Geometry },
            new GroupOption { DisplayName = "문자 (Text)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Text },
            new GroupOption { DisplayName = "일반 (General)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.General },
            new GroupOption { DisplayName = "식별 데이터 (Identity Data)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.IdentityData },
            new GroupOption { DisplayName = "그래픽 (Graphics)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Graphics },
            new GroupOption { DisplayName = "공정 (Phasing)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Phasing },
            new GroupOption { DisplayName = "구속조건 (Constraints)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Constraints },
            new GroupOption { DisplayName = "재료 및 마감 (Materials)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Materials },
            new GroupOption { DisplayName = "구조 (Structural)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Structural },
            new GroupOption { DisplayName = "분석 결과 (Analysis Results)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.AnalysisResults },
            new GroupOption { DisplayName = "전기 (Electrical)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Electrical },
            new GroupOption { DisplayName = "배관 (Plumbing)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Plumbing },
            new GroupOption { DisplayName = "기계 (Mechanical)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Mechanical },
            new GroupOption { DisplayName = "제목 (Title)", GroupTypeId = Autodesk.Revit.DB.GroupTypeId.Title }
        };

        public static DataTypeOption FindDataType(ForgeTypeId specTypeId)
        {
            if (specTypeId == null || string.IsNullOrEmpty(specTypeId.TypeId))
                return AvailableDataTypes[0];

            return AvailableDataTypes.FirstOrDefault(d => d.SpecTypeId.TypeId.Equals(specTypeId.TypeId, StringComparison.OrdinalIgnoreCase))
                ?? new DataTypeOption { DisplayName = specTypeId.TypeId, SpecTypeId = specTypeId };
        }

        public static GroupOption FindGroup(ForgeTypeId groupTypeId)
        {
            if (groupTypeId == null || string.IsNullOrEmpty(groupTypeId.TypeId))
                return AvailableGroups[0];

            return AvailableGroups.FirstOrDefault(g => g.GroupTypeId.TypeId.Equals(groupTypeId.TypeId, StringComparison.OrdinalIgnoreCase))
                ?? new GroupOption { DisplayName = groupTypeId.TypeId, GroupTypeId = groupTypeId };
        }
    }
}
