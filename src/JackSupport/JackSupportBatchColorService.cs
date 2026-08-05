// =========================================================
// 파일명: JackSupportBatchColorService.cs
// 공개용 설명:
// 1) 현재 모델의 기존 잭서포트 전체 수집
// 2) 층 분류와 높이 규칙을 다시 판정하여 색상 일괄 적용
// 3) 특수 기둥 주변 복수 잭서포트를 한 세트로 동일하게 분류
// 4) 모든 기존 잭서포트를 설정한 공통 색상으로 일괄 변경
// 5) 현재 활성 뷰의 요소별 그래픽 재지정으로 색상 적용
// 6) 높이 규칙별 실제 처리 개수와 오류 결과 집계
// 7) 선택한 색상 분류 기준과 보조 기준 외곽선 적용
// 8) 다른 생성 규칙의 대상 부재는 특수 기둥 세트 판정에서 제외
// =========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    public enum JackSupportBatchColorMode
    {
        Judgment = 0,
        Uniform = 1
    }

    public class JackSupportBatchColorResult
    {
        public int CollectedSupportCount { get; set; }
        public int ProcessedSupportCount { get; set; }
        public int LowestFloorCount { get; set; }
        public int OtherFloorCount { get; set; }
        public int PcSetSupportCount { get; set; }
        public int ColorAppliedCount { get; set; }
        public int ColorFailureCount { get; set; }
        public int UnmatchedHeightCount { get; set; }
        public int HeightRuleColorAppliedCount { get; set; }
        public int BtsColumnOutlineAppliedCount { get; set; }
        public string ColorClassificationModeText { get; set; }
        public List<string> HeightRuleItemResults { get; private set; }
        public List<string> Errors { get; private set; }

        public JackSupportBatchColorResult()
        {
            ColorClassificationModeText = string.Empty;
            HeightRuleItemResults = new List<string>();
            Errors = new List<string>();
        }

        public string BuildMessage(
            JackSupportBatchColorMode mode)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine(
                mode == JackSupportBatchColorMode.Judgment
                    ? "기존 잭서포트 판정색상 일괄 적용 완료"
                    : "기존 잭서포트 공통색상 일괄 적용 완료");

            builder.AppendLine();
            builder.AppendLine(
                "수집 잭서포트: " +
                CollectedSupportCount);
            builder.AppendLine(
                "처리 잭서포트: " +
                ProcessedSupportCount);

            if (mode == JackSupportBatchColorMode.Judgment)
            {
                builder.AppendLine(
                    "색상 분류 기준: " +
                    ColorClassificationModeText);
                builder.AppendLine(
                    "최하층: " + LowestFloorCount);
                builder.AppendLine(
                    "그외층: " + OtherFloorCount);
                builder.AppendLine(
                    "특수 기둥 세트 판정 잭서포트: " +
                    PcSetSupportCount);
                builder.AppendLine(
                    "높이 구간 일치 없음: " +
                    UnmatchedHeightCount);

                if (HeightRuleItemResults.Count > 0)
                {
                    builder.AppendLine(
                        "높이 구간별 입력 결과:");

                    foreach (string itemResult in
                        HeightRuleItemResults)
                    {
                        builder.AppendLine(
                            "- " + itemResult);
                    }
                }
            }

            builder.AppendLine(
                "색상 적용 성공: " +
                ColorAppliedCount);
            builder.AppendLine(
                "높이 규칙 색상 적용: " +
                HeightRuleColorAppliedCount);
            builder.AppendLine(
                "보조 기준 외곽선 적용: " +
                BtsColumnOutlineAppliedCount);
            builder.AppendLine(
                "색상 적용 실패: " +
                ColorFailureCount);

            if (Errors.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("오류 일부:");

                foreach (string error in Errors.Take(10))
                    builder.AppendLine("- " + error);
            }

            return builder.ToString();
        }
    }

    public static class JackSupportBatchColorService
    {
        public static JackSupportBatchColorResult Execute(
            Document doc,
            JackSupportSettings settings,
            JackSupportBatchColorMode mode)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            if (settings == null)
                throw new ArgumentNullException("settings");

            View activeView = doc.ActiveView;

            if (activeView == null || activeView.IsTemplate)
            {
                throw new InvalidOperationException(
                    "현재 활성 뷰가 없거나 뷰 템플릿입니다.");
            }

            IList<FamilyInstance> supports =
                JackSupportFamilyService
                    .CollectConfiguredJackSupports(
                        doc,
                        settings);

            JackSupportBatchColorResult result =
                new JackSupportBatchColorResult();

            result.CollectedSupportCount =
                supports.Count;

            result.ColorClassificationModeText =
                settings.ColorClassificationMode ==
                    JackSupportColorClassificationMode.HeightParameterRule
                    ? "높이별 데이터"
                    : "최하층·그외층";

            if (supports.Count == 0)
                return result;

            JackSupportClassificationService classificationService =
                new JackSupportClassificationService(
                    doc,
                    settings);

            Dictionary<int, bool> floorClassificationByElementId =
                BuildFloorClassifications(
                    doc,
                    supports,
                    settings,
                    classificationService,
                    result);

            using (Transaction transaction =
                new Transaction(
                    doc,
                    mode == JackSupportBatchColorMode.Judgment
                        ? "기존 잭서포트 판정색상 일괄 적용"
                        : "기존 잭서포트 공통색상 일괄 적용"))
            {
                transaction.Start();

                JackSupportFamilyService familyService =
                    new JackSupportFamilyService(
                        doc,
                        settings);

                foreach (FamilyInstance support in supports)
                {
                    try
                    {
                        if (mode == JackSupportBatchColorMode.Uniform)
                        {
                            familyService
                                .ApplyUniformColorToExistingSupport(
                                    support);
                        }
                        else
                        {
                            bool isLowestFloor;

                            if (!floorClassificationByElementId
                                .TryGetValue(
                                    support.Id.IntegerValue,
                                    out isLowestFloor))
                            {
                                isLowestFloor =
                                    classificationService
                                        .IsLowestFloorForExistingSupport(
                                            support);
                            }

                            familyService
                                .ReapplyExistingSupportJudgment(
                                    support,
                                    isLowestFloor);

                            if (settings.EnableLowestFloorClassification)
                            {
                                if (isLowestFloor)
                                    result.LowestFloorCount++;
                                else
                                    result.OtherFloorCount++;
                            }
                        }

                        result.ProcessedSupportCount++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(
                            "ElementId " +
                            support.Id.IntegerValue +
                            " / " + ex.Message);
                    }
                }

                result.ColorAppliedCount =
                    familyService.ViewColorStatistics.AppliedCount;
                result.ColorFailureCount =
                    familyService.ViewColorStatistics.FailureCount;
                result.HeightRuleColorAppliedCount =
                    familyService.ViewColorStatistics
                        .HeightRuleAppliedCount;
                result.BtsColumnOutlineAppliedCount =
                    familyService.ViewColorStatistics
                        .BtsColumnOutlineAppliedCount;
                result.UnmatchedHeightCount =
                    familyService.HeightParameterStatistics
                        .NoMatchingRuleCount;

                result.HeightRuleItemResults.Clear();
                result.HeightRuleItemResults.AddRange(
                    familyService.HeightParameterStatistics
                        .BuildRuleResultLines());

                foreach (string error in
                    familyService.ViewColorStatistics.Errors)
                {
                    result.Errors.Add(error);
                }

                foreach (string error in
                    familyService.HeightParameterStatistics.Errors)
                {
                    result.Errors.Add(error);
                }

                transaction.Commit();
            }

            return result;
        }

        private static Dictionary<int, bool>
            BuildFloorClassifications(
                Document doc,
                IList<FamilyInstance> supports,
                JackSupportSettings settings,
                JackSupportClassificationService classificationService,
                JackSupportBatchColorResult result)
        {
            Dictionary<int, bool> classifications =
                new Dictionary<int, bool>();

            foreach (FamilyInstance support in supports)
            {
                classifications[support.Id.IntegerValue] =
                    classificationService
                        .IsLowestFloorForExistingSupport(
                            support);
            }

            if (!settings.EnableLowestFloorClassification ||
                !settings.EnableCondition2)
            {
                return classifications;
            }

            IList<string> condition2FamilyNameKeywords =
                settings.GetCondition2FamilyNameKeywords();

            double pointTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.DuplicatePointToleranceMm);

            double verticalTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.DuplicateVerticalToleranceMm);

            double offset =
                JackSupportGeometryHelper.MmToInternal(
                    settings.Condition2OffsetMm);

            IList<FamilyInstance> sourceColumns =
                new FilteredElementCollector(doc)
                    .OfCategory(
                        BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsNotElementType()
                    .OfType<FamilyInstance>()
                    .Where(column =>
                        !JackSupportFamilyService
                            .IsConfiguredJackSupportElement(
                                doc,
                                column,
                                settings))
                    .ToList();

            foreach (FamilyInstance sourceColumn in sourceColumns)
            {
                string familyName =
                    JackSupportGeometryHelper.GetElementFamilyName(
                        doc,
                        sourceColumn);

                if (!ContainsAnyIgnoreCase(
                    condition2FamilyNameKeywords,
                    familyName))
                {
                    continue;
                }

                string standardName =
                    JackSupportGeometryHelper.GetStandardMemberName(
                        doc,
                        sourceColumn);

                if (IsReservedForAnotherPlacementRule(
                    settings,
                    standardName))
                {
                    continue;
                }

                double bottomZ;
                double topZ;

                if (!JackSupportGeometryHelper.TryGetElementVerticalRange(
                    sourceColumn,
                    out bottomZ,
                    out topZ))
                {
                    continue;
                }

                IList<XYZ> points =
                    JackSupportGeometryHelper.GetCondition2Points(
                        sourceColumn,
                        offset);

                bool isLowestFloorSet =
                    classificationService.IsLowestFloorForPcSet(
                        sourceColumn,
                        points,
                        bottomZ);

                HashSet<int> matchedSupportIds =
                    new HashSet<int>();

                foreach (XYZ point in points)
                {
                    foreach (FamilyInstance support in supports)
                    {
                        if (!IsSameSupportPositionAndSpan(
                            support,
                            point,
                            bottomZ,
                            topZ,
                            pointTolerance,
                            verticalTolerance))
                        {
                            continue;
                        }

                        classifications[support.Id.IntegerValue] =
                            isLowestFloorSet;

                        matchedSupportIds.Add(
                            support.Id.IntegerValue);
                    }
                }

                result.PcSetSupportCount +=
                    matchedSupportIds.Count;
            }

            return classifications;
        }

        private static bool IsSameSupportPositionAndSpan(
            FamilyInstance support,
            XYZ targetPoint,
            double targetBottomZ,
            double targetTopZ,
            double pointTolerance,
            double verticalTolerance)
        {
            if (support == null || targetPoint == null)
                return false;

            XYZ supportPoint =
                JackSupportClassificationService
                    .GetElementPlanPoint(support);

            if (supportPoint == null)
                return false;

            double dx = supportPoint.X - targetPoint.X;
            double dy = supportPoint.Y - targetPoint.Y;

            if (Math.Sqrt(dx * dx + dy * dy) >
                pointTolerance)
            {
                return false;
            }

            double supportBottomZ;
            double supportTopZ;

            if (!JackSupportClassificationService
                .TryGetSupportVerticalRange(
                    support,
                    out supportBottomZ,
                    out supportTopZ))
            {
                return false;
            }

            return
                Math.Abs(supportBottomZ - targetBottomZ) <=
                    verticalTolerance &&
                Math.Abs(supportTopZ - targetTopZ) <=
                    verticalTolerance;
        }

        private static bool IsReservedForAnotherPlacementRule(
            JackSupportSettings settings,
            string standardName)
        {
            if (settings == null ||
                string.IsNullOrWhiteSpace(standardName))
            {
                return false;
            }

            List<string> reservedNames =
                new List<string>();

            reservedNames.AddRange(
                settings.GetCondition1Names());

            reservedNames.AddRange(
                settings.GetCondition1BNames());

            reservedNames.AddRange(
                settings.GetCondition1ColumnFallbackNames());

            reservedNames.AddRange(
                settings.GetCondition1SpecialBeamNames());

            reservedNames.AddRange(
                settings.GetCondition1SideMemberNames());

            return reservedNames
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name))
                .Any(name =>
                    string.Equals(
                        name.Trim(),
                        standardName.Trim(),
                        StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAnyIgnoreCase(
            IEnumerable<string> keywords,
            string value)
        {
            if (keywords == null ||
                string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    value.IndexOf(
                        keyword.Trim(),
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 분류 기준 재판정 및 색상 일괄 적용 서비스
// 파일명: JackSupportBatchColorService.cs
// =========================================================
