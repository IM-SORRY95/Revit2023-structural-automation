// =========================================================
// 파일명: CreateJackSupportCommand.cs
// 공개용 설명:
// 1) 설정에 따라 구조 보·기둥을 분석하여 잭서포트 생성 대상을 판정
// 2) 보 형상, 측면 접촉 부재, 하부 바닥·기초 및 기존 지지 부재를 검토
// 3) 남은 유효 구간을 기준으로 잭서포트 위치와 수량을 계산
// 4) 생성 높이와 하부 지지 조건에 따라 분류값과 수량 데이터를 입력
// 5) 활성 뷰 표시 색상, 중복 위치 방지, 실행 결과 저장 및 재확인 지원
// 6) 실제 프로젝트의 부재 코드와 설정값은 별도 설정 파일에서 관리
// 7) 생성형 AI를 구현 보조 도구로 활용하고 실제 Revit 모델에서 검증
// =========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace REVIT_TAP
{
    [Transaction(TransactionMode.Manual)]
    public class CreateJackSupportCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (commandData == null)
                return Result.Cancelled;

            return ExecuteFromExternalEvent(
                commandData.Application,
                ref message);
        }

        public Result ExecuteFromExternalEvent(
            UIApplication uiApplication,
            ref string message)
        {
            UIDocument uiDoc =
                uiApplication == null
                    ? null
                    : uiApplication.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                TaskDialog.Show(
                    "잭서포트",
                    "열려 있는 Revit 모델이 없습니다.");

                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            JackSupportSettings settings = JackSupportSettingsStore.Load();
            JackSupportExecutionResult executionResult =
                new JackSupportExecutionResult();

            try
            {
                IList<Element> targetBeams = CollectTargetElements(
                    doc,
                    BuiltInCategory.OST_StructuralFraming,
                    settings.UseActiveViewOnly);

                IList<Element> targetColumns = CollectTargetElements(
                    doc,
                    BuiltInCategory.OST_StructuralColumns,
                    settings.UseActiveViewOnly);

                IList<Element> allStructuralFramingForBtsSideDetection =
                    settings.EnableCondition1
                        ? CollectAllElements(
                            doc,
                            BuiltInCategory.OST_StructuralFraming)
                            .Where(element =>
                                !IsGeneratedType(
                                    doc,
                                    element,
                                    settings))
                            .ToList()
                        : new List<Element>();

                executionResult
                    .Condition1SideDetectionFramingCount =
                    allStructuralFramingForBtsSideDetection.Count;

                AnalyzeTargets(
                    doc,
                    targetBeams,
                    targetColumns,
                    settings,
                    executionResult);

                IList<Element> allFloors =
                    settings.IncludeFloorsAsLowerSupports
                        ? CollectAllElements(doc, BuiltInCategory.OST_Floors)
                        : new List<Element>();

                bool foundationCollectionRequired =
                    settings.IncludeStructuralFoundationsAsLowerSupports ||
                    settings.EnableLowestFloorClassification;

                IList<Element> allFoundations =
                    foundationCollectionRequired
                        ? CollectAllElements(doc, BuiltInCategory.OST_StructuralFoundation)
                        : new List<Element>();

                IList<Element> matchingFoundations =
                    FilterFoundationsForLowerSupport(
                        doc,
                        allFoundations,
                        settings);

                JackSupportClassificationService classificationService =
                    new JackSupportClassificationService(
                        doc,
                        settings);

                IList<Element> otherFloorMarkerElements =
                    classificationService.OtherFloorMarkerElements;

                List<Element> lowerSupports = new List<Element>();
                lowerSupports.AddRange(allFloors);
                lowerSupports.AddRange(matchingFoundations);

                // 그외층 판정 부재가 일반 하부 지지체 목록에 없더라도
                // 실제 접촉 위치까지 잭서포트 하단을 계산할 수 있도록 함께 포함한다.
                lowerSupports.AddRange(otherFloorMarkerElements);
                lowerSupports = lowerSupports
                    .Where(element => element != null)
                    .GroupBy(element => element.Id.IntegerValue)
                    .Select(group => group.First())
                    .ToList();

                executionResult.CollectedFloorCount = allFloors.Count;
                executionResult.CollectedFoundationCount = allFoundations.Count;
                executionResult.MatchingFoundationCount = matchingFoundations.Count;
                executionResult.FoundationConfiguredNames = string.Join(
                    ", ",
                    settings.GetStructuralFoundationNames());

                executionResult.LowestFloorFoundationConfiguredNames =
                    string.Join(
                        ", ",
                        settings.GetOtherFloorMarkerNames());

                executionResult.LowestFloorFoundationConfiguredSuffixes =
                    string.Join(
                        ", ",
                        settings.GetOtherFloorMarkerSuffixes());

                executionResult.LowestFloorFoundationCount =
                    classificationService.OtherFloorMarkerElementCount;

                if (executionResult.TotalTargetCount <= 0)
                {
                    ShowResult(executionResult, settings);
                    return Result.Succeeded;
                }

                bool wallCollectionRequired =
                    settings.EnableCondition3 &&
                    settings.UseWallsAsSupports;

                IList<Element> allWalls =
                    wallCollectionRequired
                        ? CollectAllElements(
                            doc,
                            BuiltInCategory.OST_Walls)
                        : new List<Element>();

                bool structuralColumnCollectionRequired =
                    settings.EnableCondition1 ||
                    (settings.EnableCondition3 &&
                     settings.UseExistingColumnsAsSupports);

                IList<Element> allStructuralColumns =
                    structuralColumnCollectionRequired
                        ? CollectAllElements(
                            doc,
                            BuiltInCategory.OST_StructuralColumns)
                            .Where(element => !IsGeneratedType(
                                doc,
                                element,
                                settings))
                            .ToList()
                        : new List<Element>();

                using (TransactionGroup group =
                    new TransactionGroup(doc, "잭서포트 자동 생성"))
                {
                    group.Start();

                    using (Transaction transaction =
                        new Transaction(doc, "잭서포트 생성"))
                    {
                        transaction.Start();

                        FailureHandlingOptions failureOptions =
                            transaction.GetFailureHandlingOptions();

                        failureOptions.SetFailuresPreprocessor(
                            new JackSupportZeroHeightWarningPreprocessor());

                        failureOptions.SetClearAfterRollback(true);
                        transaction.SetFailureHandlingOptions(
                            failureOptions);

                        JackSupportFamilyService familyService =
                            new JackSupportFamilyService(doc, settings);

                        FamilySymbol symbol = familyService.GetOrCreateSymbol();

                        if (settings.EnableCondition1)
                        {
                            ProcessCondition1(
                                doc,
                                targetBeams,
                                allStructuralFramingForBtsSideDetection,
                                lowerSupports,
                                allStructuralColumns,
                                settings,
                                symbol,
                                familyService,
                                classificationService,
                                executionResult);
                        }

                        if (settings.EnableCondition2)
                        {
                            ProcessCondition2(
                                doc,
                                targetColumns,
                                settings,
                                symbol,
                                familyService,
                                classificationService,
                                executionResult);
                        }

                        if (settings.EnableCondition3)
                        {
                            ProcessCondition3(
                                doc,
                                targetBeams,
                                lowerSupports,
                                allWalls,
                                allStructuralColumns,
                                settings,
                                symbol,
                                familyService,
                                classificationService,
                                executionResult);
                        }

                        executionResult.CopyHeightParameterStatistics(
                            familyService.HeightParameterStatistics);

                        executionResult.CopyViewColorStatistics(
                            familyService.ViewColorStatistics);

                        executionResult.CopyFloorClassificationStatistics(
                            familyService.FloorClassificationStatistics);

                        transaction.Commit();
                    }

                    group.Assimilate();
                }

                ShowResult(executionResult, settings);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;

                TaskDialog.Show(
                    "잭서포트 생성 오류",
                    ex.ToString());

                return Result.Failed;
            }
        }

        private enum BtsSpecialPlacementCase
        {
            Normal = 0,
            NoSideMember = 1,
            BothSides = 2,
            SingleSide = 3
        }

        private class JackSupportBtsPlacement
        {
            public double Ratio { get; set; }
            public XYZ LateralOffset { get; set; }

            public JackSupportBtsPlacement()
            {
                LateralOffset = XYZ.Zero;
            }
        }

        private static void ProcessCondition1(
            Document doc,
            IList<Element> beams,
            IList<Element> allStructuralFramingForBtsSideDetection,
            IList<Element> lowerSupports,
            IList<Element> allStructuralColumns,
            JackSupportSettings settings,
            FamilySymbol symbol,
            JackSupportFamilyService familyService,
            JackSupportClassificationService classificationService,
            JackSupportExecutionResult result)
        {
            IList<string> namesA = settings.GetCondition1Names();
            IList<double> ratiosA = settings.GetCondition1RatioValues();
            IList<string> namesB = settings.GetCondition1BNames();
            IList<double> ratiosB = settings.GetCondition1BRatioValues();
            IList<string> fallbackColumnNames =
                settings.GetCondition1ColumnFallbackNames();

            double searchDepth = JackSupportGeometryHelper.MmToInternal(
                settings.LowerSupportSearchDepthMm);

            double boundaryMaximumDistance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.BoundarySearchMaximumDistanceMm);

            double boundarySearchStep =
                JackSupportGeometryHelper.MmToInternal(
                    settings.BoundarySearchStepMm);

            double boundaryTopTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.BoundarySupportTopDifferenceToleranceMm);

            double columnTouchTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.Condition1ColumnTouchToleranceMm);

            double sideDetectionTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.Condition1SideDetectionToleranceMm);

            foreach (Element beam in beams)
            {
                string standardName =
                    JackSupportGeometryHelper.GetStandardMemberName(
                        doc,
                        beam);

                IList<double> defaultRatios;
                bool isGroupA;

                if (ContainsExact(namesA, standardName))
                {
                    defaultRatios = ratiosA;
                    isGroupA = true;
                }
                else if (ContainsExact(namesB, standardName))
                {
                    defaultRatios = ratiosB;
                    isGroupA = false;
                }
                else
                {
                    continue;
                }

                try
                {
                    Curve curve =
                        JackSupportGeometryHelper.GetLocationCurve(
                            beam);

                    if (curve == null)
                    {
                        result.AddError(
                            beam,
                            "특수 보 잭서포트: LocationCurve가 없습니다.");
                        continue;
                    }

                    BtsSpecialPlacementCase specialCase;
                    JackSupportBtsSideContactInfo sideContactInfo;

                    IList<JackSupportBtsPlacement> placements =
                        BuildCondition1Placements(
                            doc,
                            beam,
                            curve,
                            allStructuralFramingForBtsSideDetection,
                            standardName,
                            defaultRatios,
                            settings,
                            sideDetectionTolerance,
                            out specialCase,
                            out sideContactInfo);

                    if (sideContactInfo != null)
                    {
                        result.Condition1BtsrsScannedFramingCount +=
                            sideContactInfo.ScannedFramingCount;

                        result.Condition1BtsrsStandardNameMatchedCount +=
                            sideContactInfo.StandardNameMatchedCount;

                        result.Condition1BtsrsContactMatchedCount +=
                            sideContactInfo.ContactMatchedCount;

                        foreach (string sample in
                            sideContactInfo.StandardNameSamples)
                        {
                            result.AddBtsrsStandardNameSample(sample);
                        }

                        foreach (string sample in
                            sideContactInfo.ContactSamples)
                        {
                            result.AddBtsrsContactSample(sample);
                        }
                    }

                    if (specialCase !=
                        BtsSpecialPlacementCase.Normal)
                    {
                        result.Condition1BtstbRequestedPlacementCount +=
                            placements.Count;
                    }

                    if (specialCase == BtsSpecialPlacementCase.NoSideMember)
                        result.Condition1BtstbNoSideBeamCount++;
                    else if (specialCase == BtsSpecialPlacementCase.BothSides)
                        result.Condition1BtstbBothSidesBeamCount++;
                    else if (specialCase == BtsSpecialPlacementCase.SingleSide)
                        result.Condition1BtstbSingleSideBeamCount++;

                    JackSupportBtsColumnFallback columnFallback;
                    bool hasColumnFallback =
                        JackSupportGeometryHelper.TryGetBtsColumnFallback(
                            doc,
                            beam,
                            curve,
                            allStructuralColumns,
                            fallbackColumnNames,
                            columnTouchTolerance,
                            out columnFallback);

                    bool columnFallbackUsedForBeam = false;

                    foreach (JackSupportBtsPlacement placement in placements)
                    {
                        double ratio = placement.Ratio;

                        XYZ centerPoint =
                            JackSupportGeometryHelper
                                .GetPointAtNormalizedParameter(
                                    curve,
                                    ratio);

                        if (centerPoint == null)
                            continue;

                        XYZ planPoint =
                            centerPoint +
                            (placement.LateralOffset ?? XYZ.Zero);

                        double beamBottomZ;

                        if (!JackSupportGeometryHelper
                            .TryGetElementBottomAtPoint(
                                beam,
                                centerPoint,
                                out beamBottomZ))
                        {
                            result.AddError(
                                beam,
                                "특수 보 잭서포트: 보 하단 높이를 계산하지 못했습니다.");
                            continue;
                        }

                        Element lowerSupport;
                        double lowerSupportTopZ;
                        XYZ resolvedPlanPoint;
                        double correctionDistance;
                        bool usedColumnFallback = false;

                        XYZ curveTangent =
                            JackSupportGeometryHelper
                                .GetCurveTangentAtNormalizedParameter(
                                    curve,
                                    ratio);

                        bool foundLowerSupport =
                            JackSupportGeometryHelper
                                .TryFindNearestLowerSupportTopWithBoundarySearch(
                                    planPoint,
                                    curveTangent,
                                    beamBottomZ,
                                    searchDepth,
                                    lowerSupports,
                                    settings.EnableBoundaryLowerSupportSearch,
                                    boundaryMaximumDistance,
                                    boundarySearchStep,
                                    boundaryTopTolerance,
                                    settings.MoveSupportToBoundaryFoundPoint,
                                    out resolvedPlanPoint,
                                    out lowerSupport,
                                    out lowerSupportTopZ,
                                    out correctionDistance);

                        if (!foundLowerSupport &&
                            hasColumnFallback &&
                            columnFallback != null)
                        {
                            lowerSupportTopZ =
                                columnFallback.GetBottomZ(ratio);

                            lowerSupport =
                                columnFallback.GetReferenceElement(ratio);

                            resolvedPlanPoint = planPoint;
                            correctionDistance = 0.0;
                            usedColumnFallback =
                                lowerSupportTopZ < beamBottomZ;
                            foundLowerSupport = usedColumnFallback;
                        }

                        if (!foundLowerSupport)
                        {
                            result.Condition1NoLowerSupportCount++;
                            result.AddError(
                                beam,
                                "특수 보 잭서포트: 바닥·기초가 없고 양단 단부 기둥도 확인하지 못했습니다. " +
                                "생성 비율=" + ratio.ToString("0.###"));
                            continue;
                        }

                        if (correctionDistance > 0.0)
                            result.Condition1BoundaryAdjustedCount++;

                        FamilySymbol sourceTaggedSymbol =
                            familyService.GetOrCreateSourceTaggedSymbol(
                                symbol,
                                usedColumnFallback
                                    ? "특수보_단부기둥기준"
                                    : "특수보",
                                standardName);

                        bool wasCreated;

                        FamilyInstance support =
                            familyService.CreateOrGetVerticalColumn(
                                resolvedPlanPoint,
                                lowerSupportTopZ,
                                beamBottomZ,
                                sourceTaggedSymbol,
                                lowerSupport,
                                beam,
                                out wasCreated);

                        if (support == null)
                            continue;

                        bool isLowestFloor =
                            classificationService.IsLowestFloor(
                                lowerSupport,
                                resolvedPlanPoint,
                                lowerSupportTopZ);

                        familyService.ApplyFloorClassification(
                            support,
                            isLowestFloor);

                        if (wasCreated)
                        {
                            result.Condition1CreatedCount++;

                            if (isGroupA)
                                result.Condition1ACreatedCount++;
                            else
                                result.Condition1BCreatedCount++;

                            if (specialCase !=
                                BtsSpecialPlacementCase.Normal)
                            {
                                result.Condition1BtstbCreatedPlacementCount++;
                            }

                            if (usedColumnFallback)
                            {
                                result.Condition1ColumnFallbackCreatedCount++;
                                columnFallbackUsedForBeam = true;
                            }
                        }
                        else
                        {
                            result.DuplicateSkippedCount++;
                        }
                    }

                    if (columnFallbackUsedForBeam)
                        result.Condition1ColumnFallbackBeamCount++;
                }
                catch (Exception ex)
                {
                    result.AddError(
                        beam,
                        "특수 보 잭서포트: " + ex.Message);
                }
            }
        }

        private static IList<JackSupportBtsPlacement>
            BuildCondition1Placements(
                Document doc,
                Element beam,
                Curve curve,
                IList<Element> allFraming,
                string standardName,
                IList<double> defaultRatios,
                JackSupportSettings settings,
                double sideDetectionTolerance,
                out BtsSpecialPlacementCase specialCase,
                out JackSupportBtsSideContactInfo sideContactInfo)
        {
            List<JackSupportBtsPlacement> result =
                new List<JackSupportBtsPlacement>();

            specialCase = BtsSpecialPlacementCase.Normal;
            sideContactInfo = null;

            if (!ContainsExact(
                settings.GetCondition1SpecialBeamNames(),
                standardName))
            {
                foreach (double ratio in defaultRatios)
                {
                    result.Add(
                        new JackSupportBtsPlacement
                        {
                            Ratio = ratio,
                            LateralOffset = XYZ.Zero
                        });
                }

                return result;
            }

            sideContactInfo =
                JackSupportGeometryHelper.GetBtsSideContactInfo(
                    doc,
                    beam,
                    curve,
                    allFraming,
                    settings.GetCondition1SideMemberNames(),
                    sideDetectionTolerance);

            bool negative =
                sideContactInfo.HasNegativeSide;

            bool positive =
                sideContactInfo.HasPositiveSide;

            XYZ perpendicular =
                sideContactInfo.PerpendicularDirection ??
                XYZ.BasisY;

            double offset =
                sideContactInfo.LateralOffsetDistance;

            if (negative && positive)
            {
                specialCase = BtsSpecialPlacementCase.BothSides;
                IList<double> ratios =
                    JackSupportGeometryHelper.GetEvenNormalizedRatios(
                        settings.Condition1SpecialBothSidesCountPerSide);

                foreach (double ratio in ratios)
                {
                    result.Add(
                        new JackSupportBtsPlacement
                        {
                            Ratio = ratio,
                            LateralOffset = perpendicular * -offset
                        });

                    result.Add(
                        new JackSupportBtsPlacement
                        {
                            Ratio = ratio,
                            LateralOffset = perpendicular * offset
                        });
                }
            }
            else if (negative || positive)
            {
                specialCase = BtsSpecialPlacementCase.SingleSide;
                double sign = positive ? 1.0 : -1.0;
                IList<double> ratios =
                    JackSupportGeometryHelper.GetEvenNormalizedRatios(
                        settings.Condition1SpecialSingleSideCount);

                foreach (double ratio in ratios)
                {
                    result.Add(
                        new JackSupportBtsPlacement
                        {
                            Ratio = ratio,
                            LateralOffset =
                                perpendicular * offset * sign
                        });
                }
            }
            else
            {
                specialCase = BtsSpecialPlacementCase.NoSideMember;
                IList<double> ratios =
                    JackSupportGeometryHelper.GetEvenNormalizedRatios(
                        settings.Condition1SpecialNoSideCount);

                foreach (double ratio in ratios)
                {
                    result.Add(
                        new JackSupportBtsPlacement
                        {
                            Ratio = ratio,
                            LateralOffset = XYZ.Zero
                        });
                }
            }

            return result;
        }

        private static void ProcessCondition2(
            Document doc,
            IList<Element> columns,
            JackSupportSettings settings,
            FamilySymbol symbol,
            JackSupportFamilyService familyService,
            JackSupportClassificationService classificationService,
            JackSupportExecutionResult result)
        {
            IList<string> familyNameKeywords =
                settings.GetCondition2FamilyNameKeywords();
            double offset = JackSupportGeometryHelper.MmToInternal(
                settings.Condition2OffsetMm);

            foreach (Element element in columns)
            {
                if (IsGeneratedType(
                    doc,
                    element,
                    settings))
                {
                    continue;
                }

                FamilyInstance sourceColumn = element as FamilyInstance;

                if (sourceColumn == null)
                    continue;

                string familyName =
                    JackSupportGeometryHelper.GetElementFamilyName(
                        doc,
                        sourceColumn);

                if (!ContainsAnyIgnoreCase(
                    familyNameKeywords,
                    familyName))
                {
                    continue;
                }

                string standardName =
                    JackSupportGeometryHelper.GetStandardMemberName(
                        doc,
                        sourceColumn);

                if (ContainsAnyIgnoreCase(
                    GetCondition2ExcludedStandardNames(settings),
                    standardName))
                {
                    // 대상 분석 단계에서 제외 개수를 이미 집계했으므로
                    // 실제 생성 단계에서는 중복 집계하지 않고 건너뛴다.
                    continue;
                }

                try
                {
                    string columnSourceKind =
                        "DropCaps기둥";

                    FamilySymbol sourceTaggedSymbol =
                        familyService.GetOrCreateSourceTaggedSymbol(
                            symbol,
                            columnSourceKind,
                            standardName);

                    double bottomZ;
                    double topZ;

                    if (!JackSupportGeometryHelper.TryGetElementVerticalRange(
                        sourceColumn,
                        out bottomZ,
                        out topZ))
                    {
                        result.AddError(
                            sourceColumn,
                            "Drop Caps 기둥 잭서포트: 대상 기둥 높이를 계산하지 못했습니다.");
                        continue;
                    }

                    IList<XYZ> points =
                        JackSupportGeometryHelper.GetCondition2Points(
                            sourceColumn,
                            offset);

                    // Drop Caps 포함 기둥 주변 4개는 한 세트로 같은 층이다.
                    // 실제 최하층 레벨이면 4개 모두 최하층으로 우선 판정하고,
                    // 그 외에는 4개 중 한 점이라도 지정 부재에 닿으면 4개 모두 그외층이다.
                    bool isLowestFloorSet =
                        classificationService.IsLowestFloorForPcSet(
                            sourceColumn,
                            points,
                            bottomZ);

                    List<FamilyInstance> createdOrExistingSet =
                        new List<FamilyInstance>();

                    foreach (XYZ point in points)
                    {
                        bool wasCreated;

                        FamilyInstance support =
                            familyService.CreateOrGetVerticalColumn(
                                point,
                                bottomZ,
                                topZ,
                                sourceTaggedSymbol,
                                sourceColumn,
                                sourceColumn,
                                out wasCreated);

                        if (support == null)
                            continue;

                        createdOrExistingSet.Add(support);

                        if (wasCreated)
                            result.Condition2CreatedCount++;
                        else
                            result.DuplicateSkippedCount++;
                    }

                    foreach (FamilyInstance support in
                        createdOrExistingSet
                            .GroupBy(instance => instance.Id.IntegerValue)
                            .Select(group => group.First()))
                    {
                        familyService.ApplyFloorClassification(
                            support,
                            isLowestFloorSet);
                    }
                }
                catch (Exception ex)
                {
                    result.AddError(
                        sourceColumn,
                        "Drop Caps 기둥 잭서포트: " + ex.Message);
                }
            }
        }

        private static void ProcessCondition3(
            Document doc,
            IList<Element> beams,
            IList<Element> lowerSupports,
            IList<Element> walls,
            IList<Element> allStructuralColumns,
            JackSupportSettings settings,
            FamilySymbol symbol,
            JackSupportFamilyService familyService,
            JackSupportClassificationService classificationService,
            JackSupportExecutionResult result)
        {
            IList<string> names =
                settings.GetCondition3Names();

            double interval =
                JackSupportGeometryHelper.MmToInternal(
                    settings.Condition3IntervalMm);

            double wallTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.WallTouchToleranceMm);

            double wallHorizontalExtra =
                JackSupportGeometryHelper.MmToInternal(
                    settings.WallHorizontalExtraMm);

            double columnTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.ExistingColumnTouchToleranceMm);

            double searchDepth =
                JackSupportGeometryHelper.MmToInternal(
                    settings.LowerSupportSearchDepthMm);

            double boundaryMaximumDistance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.BoundarySearchMaximumDistanceMm);

            double boundarySearchStep =
                JackSupportGeometryHelper.MmToInternal(
                    settings.BoundarySearchStepMm);

            double boundaryTopTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.BoundarySupportTopDifferenceToleranceMm);

            foreach (Element beam in beams)
            {
                string standardName =
                    JackSupportGeometryHelper.GetStandardMemberName(
                        doc,
                        beam);

                if (!ContainsExact(
                    names,
                    standardName))
                {
                    continue;
                }

                try
                {
                    FamilySymbol sourceTaggedSymbol =
                        familyService.GetOrCreateSourceTaggedSymbol(
                            symbol,
                            "RC보하부",
                            standardName);

                    Curve curve =
                        JackSupportGeometryHelper.GetLocationCurve(
                            beam);

                    if (curve == null)
                    {
                        result.AddError(
                            beam,
                            "RC보하부 잭서포트: LocationCurve가 없습니다.");

                        continue;
                    }

                    int detectedColumnSupportCount;
                    int detectedWallSupportCount;

                    IList<JackSupportSpan> spans =
                        JackSupportGeometryHelper.GetClearSpans(
                            beam,
                            curve,
                            allStructuralColumns,
                            walls,
                            settings.UseExistingColumnsAsSupports,
                            settings.UseWallsAsSupports,
                            columnTolerance,
                            wallTolerance,
                            wallHorizontalExtra,
                            out detectedColumnSupportCount,
                            out detectedWallSupportCount);

                    result.Condition3DetectedColumnSupportCount +=
                        detectedColumnSupportCount;

                    result.Condition3DetectedWallSupportCount +=
                        detectedWallSupportCount;

                    result.Condition3ClearSpanCount +=
                        spans.Count;

                    foreach (JackSupportSpan span in spans)
                    {
                        int supportCount =
                            JackSupportGeometryHelper.CalculateSupportCount(
                                span.Length,
                                interval);

                        if (supportCount <= 0)
                        {
                            result.Condition3ShortSpanSkippedCount++;
                            continue;
                        }

                        IList<double> distances =
                            JackSupportGeometryHelper.GetEvenlyDistributedDistances(
                                span,
                                supportCount);

                        foreach (double distance in distances)
                        {
                            XYZ planPoint =
                                JackSupportGeometryHelper.GetPointAtDistance(
                                    curve,
                                    distance);

                            if (planPoint == null)
                                continue;

                            double beamBottomZ;

                            if (!JackSupportGeometryHelper.TryGetElementBottomAtPoint(
                                beam,
                                planPoint,
                                out beamBottomZ))
                            {
                                result.AddError(
                                    beam,
                                    "RC보하부 잭서포트: 보 하단 높이를 계산하지 못했습니다.");

                                continue;
                            }

                            Element lowerSupport;
                            double lowerSupportTopZ;
                            XYZ resolvedPlanPoint;
                            double correctionDistance;
                            XYZ curveTangent =
                                JackSupportGeometryHelper
                                    .GetCurveTangentAtDistance(
                                        curve,
                                        distance);

                            if (!JackSupportGeometryHelper
                                .TryFindNearestLowerSupportTopWithBoundarySearch(
                                    planPoint,
                                    curveTangent,
                                    beamBottomZ,
                                    searchDepth,
                                    lowerSupports,
                                    settings.EnableBoundaryLowerSupportSearch,
                                    boundaryMaximumDistance,
                                    boundarySearchStep,
                                    boundaryTopTolerance,
                                    settings.MoveSupportToBoundaryFoundPoint,
                                    out resolvedPlanPoint,
                                    out lowerSupport,
                                    out lowerSupportTopZ,
                                    out correctionDistance))
                            {
                                result.Condition3NoLowerSupportCount++;
                                result.AddError(
                                    beam,
                                    "RC보하부 잭서포트: 하부 지지체를 찾지 못했습니다. " +
                                    "보 시작점 기준 거리=" +
                                    JackSupportGeometryHelper
                                        .InternalToMm(distance)
                                        .ToString("0") + "mm");
                                continue;
                            }

                            if (correctionDistance > 0.0)
                                result.Condition3BoundaryAdjustedCount++;

                            bool wasCreated;

                            FamilyInstance support =
                                familyService.CreateOrGetVerticalColumn(
                                    resolvedPlanPoint,
                                    lowerSupportTopZ,
                                    beamBottomZ,
                                    sourceTaggedSymbol,
                                    lowerSupport,
                                    beam,
                                    out wasCreated);

                            if (support != null)
                            {
                                bool isLowestFloor =
                                    classificationService.IsLowestFloor(
                                        lowerSupport,
                                        resolvedPlanPoint,
                                        lowerSupportTopZ);

                                familyService.ApplyFloorClassification(
                                    support,
                                    isLowestFloor);

                                if (wasCreated)
                                {
                                    result.Condition3CreatedCount++;
                                }
                                else
                                {
                                    result.DuplicateSkippedCount++;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.AddError(
                        beam,
                        "RC보하부 잭서포트: " +
                        ex.Message);
                }
            }
        }

        private static void AnalyzeTargets(
            Document doc,
            IList<Element> beams,
            IList<Element> columns,
            JackSupportSettings settings,
            JackSupportExecutionResult result)
        {
            result.SearchScope =
                settings.UseActiveViewOnly
                    ? "현재 활성 뷰"
                    : "문서 전체";

            result.ActiveViewName =
                doc.ActiveView == null
                    ? string.Empty
                    : doc.ActiveView.Name;

            result.StandardMemberRule =
                "특수 보/RC보: 유형명의 마지막 '_' 뒤 문자열, Drop Caps 기둥: 패밀리명 전체 포함문구";

            result.Condition1ConfiguredNames = string.Join(
                ", ",
                settings.GetCondition1Names());

            result.Condition1BConfiguredNames = string.Join(
                ", ",
                settings.GetCondition1BNames());

            result.Condition1ConfiguredRatios = string.Join(
                ", ",
                settings.GetCondition1RatioValues()
                    .Select(value => value.ToString("0.###")));

            result.Condition1BConfiguredRatios = string.Join(
                ", ",
                settings.GetCondition1BRatioValues()
                    .Select(value => value.ToString("0.###")));

            result.Condition1SpecialBeamConfiguredNames =
                string.Join(
                    ", ",
                    settings.GetCondition1SpecialBeamNames());

            result.Condition1SideMemberConfiguredNames =
                string.Join(
                    ", ",
                    settings.GetCondition1SideMemberNames());

            result.Condition2ConfiguredFamilyNameKeywords = string.Join(
                ", ",
                settings.GetCondition2FamilyNameKeywords());

            result.Condition3ConfiguredNames = string.Join(
                ", ",
                settings.GetCondition3Names());

            result.CollectedBeamCount =
                beams == null ? 0 : beams.Count;

            result.CollectedColumnCount =
                columns == null ? 0 : columns.Count;

            IList<string> condition1Names = settings.GetCondition1Names();
            IList<string> condition1BNames = settings.GetCondition1BNames();
            IList<string> condition2FamilyNameKeywords =
                settings.GetCondition2FamilyNameKeywords();
            IList<string> condition3Names = settings.GetCondition3Names();

            foreach (Element beam in beams ?? new List<Element>())
            {
                string typeName;
                string standardName;

                bool standardNameFound =
                    JackSupportGeometryHelper.TryGetStandardMemberName(
                        doc,
                        beam,
                        out typeName,
                        out standardName);

                if (!string.IsNullOrWhiteSpace(typeName))
                    result.BeamTypeNameFoundCount++;

                if (standardNameFound)
                {
                    result.BeamStandardMemberNameFoundCount++;
                    result.AddBeamValueSample(
                        typeName + " → " + standardName);
                }
                else if (!string.IsNullOrWhiteSpace(typeName))
                {
                    result.AddBeamValueSample(
                        typeName + " → 추출값 없음");
                }

                if (settings.EnableCondition1)
                {
                    if (ContainsExact(condition1Names, standardName))
                    {
                        result.Condition1TargetCount++;
                        result.Condition1ATargetCount++;
                    }
                    else if (ContainsExact(condition1BNames, standardName))
                    {
                        result.Condition1TargetCount++;
                        result.Condition1BTargetCount++;
                    }
                }

                if (settings.EnableCondition3 &&
                    ContainsExact(condition3Names, standardName))
                {
                    result.Condition3TargetCount++;
                }
            }

            foreach (Element column in columns ?? new List<Element>())
            {
                if (IsGeneratedType(
                    doc,
                    column,
                    settings))
                {
                    continue;
                }

                string typeName;
                string standardName;

                bool standardNameFound =
                    JackSupportGeometryHelper.TryGetStandardMemberName(
                        doc,
                        column,
                        out typeName,
                        out standardName);

                if (!string.IsNullOrWhiteSpace(typeName))
                    result.ColumnTypeNameFoundCount++;

                if (standardNameFound)
                {
                    result.ColumnStandardMemberNameFoundCount++;
                    result.AddColumnValueSample(
                        typeName + " → " + standardName);
                }
                else if (!string.IsNullOrWhiteSpace(typeName))
                {
                    result.AddColumnValueSample(
                        typeName + " → 추출값 없음");
                }

                string familyName =
                    JackSupportGeometryHelper.GetElementFamilyName(
                        doc,
                        column);

                if (settings.EnableCondition2 &&
                    ContainsAnyIgnoreCase(
                        condition2FamilyNameKeywords,
                        familyName))
                {
                    if (ContainsAnyIgnoreCase(
                        GetCondition2ExcludedStandardNames(settings),
                        standardName))
                    {
                        result.Condition2BtsExcludedCount++;
                    }
                    else
                    {
                        result.Condition2TargetCount++;
                    }
                }
            }
        }

        private static IList<Element> FilterFoundationsForLowerSupport(
            Document doc,
            IList<Element> foundations,
            JackSupportSettings settings)
        {
            List<Element> result = new List<Element>();
            IList<string> configuredNames =
                settings.GetStructuralFoundationNames();

            foreach (Element foundation in foundations ?? new List<Element>())
            {
                string standardName =
                    JackSupportGeometryHelper.GetStandardMemberName(
                        doc,
                        foundation);

                bool regularLowerSupport =
                    settings.IncludeStructuralFoundationsAsLowerSupports &&
                    ContainsExact(configuredNames, standardName);

                if (regularLowerSupport)
                    result.Add(foundation);
            }

            return result;
        }

        private static IList<Element> CollectTargetElements(
            Document doc,
            BuiltInCategory category,
            bool activeViewOnly)
        {
            FilteredElementCollector collector;

            if (activeViewOnly &&
                doc.ActiveView != null &&
                !doc.ActiveView.IsTemplate)
            {
                collector =
                    new FilteredElementCollector(doc, doc.ActiveView.Id);
            }
            else
            {
                collector = new FilteredElementCollector(doc);
            }

            return collector
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        private static IList<Element> CollectAllElements(
            Document doc,
            BuiltInCategory category)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElements()
                .ToList();
        }

        private static bool ContainsExact(
            IEnumerable<string> values,
            string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;

            return (values ?? Enumerable.Empty<string>())
                .Any(value => string.Equals(
                    value,
                    target,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string>
            GetCondition2ExcludedStandardNames(
                JackSupportSettings settings)
        {
            if (settings == null)
            {
                return Enumerable.Empty<string>();
            }

            return settings
                .GetCondition1Names()
                .Concat(settings.GetCondition1BNames())
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ContainsAnyIgnoreCase(
            IEnumerable<string> keywords,
            string target)
        {
            if (string.IsNullOrWhiteSpace(target))
                return false;

            return (keywords ?? Enumerable.Empty<string>())
                .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                .Any(keyword => target.IndexOf(
                    keyword.Trim(),
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsGeneratedType(
            Document doc,
            Element element,
            JackSupportSettings settings)
        {
            return JackSupportFamilyService
                .IsConfiguredJackSupportElement(
                    doc,
                    element,
                    settings);
        }

        private static void ShowResult(
            JackSupportExecutionResult result,
            JackSupportSettings settings)
        {
            string resultText = BuildResultText(result, settings);

            JackSupportResultStore.Save(resultText);

            TaskDialog.Show(
                "잭서포트 생성 결과",
                resultText);
        }

        private static string BuildResultText(
            JackSupportExecutionResult result,
            JackSupportSettings settings)
        {
            StringBuilder builder = new StringBuilder();

            if (result.TotalTargetCount <= 0)
            {
                builder.AppendLine(
                    "생성 조건과 일치하는 대상이 없어 " +
                    "잭서포트를 생성하지 않았습니다.");
            }
            else
            {
                builder.AppendLine(
                    "잭서포트 자동 생성이 완료되었습니다.");
            }

            builder.AppendLine();
            builder.AppendLine("[검사 범위 및 유형명 판정]");
            builder.AppendLine("검사 범위: " + result.SearchScope);

            if (!string.IsNullOrWhiteSpace(result.ActiveViewName))
                builder.AppendLine("활성 뷰: " + result.ActiveViewName);

            builder.AppendLine(
                "표준부재명 기준: " + result.StandardMemberRule);

            if (settings.UsesSpecifiedFamilyType())
            {
                builder.AppendLine(
                    "생성 패밀리/유형: " +
                    settings.SourceRoundColumnFamilyName +
                    " / " +
                    settings.SourceRoundColumnTypeName +
                    " (지정 유형 직접 사용)");
            }
            else
            {
                builder.AppendLine(
                    "생성 유형: " +
                    settings.GeneratedTypeName +
                    " (자동 생성 모드)");
            }

            builder.AppendLine(
                "수집 구조프레임: " + result.CollectedBeamCount);

            builder.AppendLine(
                "수집 구조기둥: " + result.CollectedColumnCount);

            if (result.BeamValueSamples.Count > 0)
            {
                builder.AppendLine(
                    "구조프레임 유형명 예시: " +
                    string.Join(", ", result.BeamValueSamples));
            }

            if (result.ColumnValueSamples.Count > 0)
            {
                builder.AppendLine(
                    "구조기둥 유형명 예시: " +
                    string.Join(", ", result.ColumnValueSamples));
            }

            builder.AppendLine();
            builder.AppendLine("[하부 지지체]");
            builder.AppendLine(
                "바닥 사용: " +
                (settings.IncludeFloorsAsLowerSupports ? "사용" : "미사용") +
                " / 수집 " + result.CollectedFloorCount);

            builder.AppendLine(
                "구조기초 사용: " +
                (settings.IncludeStructuralFoundationsAsLowerSupports ? "사용" : "미사용") +
                " / 전체 " + result.CollectedFoundationCount +
                " / 하부 지지체 일치 " + result.MatchingFoundationCount);

            builder.AppendLine(
                "구조기초 접미어: " + result.FoundationConfiguredNames);

            builder.AppendLine(
                "슬래브 경계부 주변 검색: " +
                (settings.EnableBoundaryLowerSupportSearch ? "사용" : "미사용") +
                " / 최대 " + settings.BoundarySearchMaximumDistanceMm + "mm" +
                " / 간격 " + settings.BoundarySearchStepMm + "mm" +
                " / 높이차 " +
                settings.BoundarySupportTopDifferenceToleranceMm + "mm");

            builder.AppendLine();
            builder.AppendLine(
                "[특수 보 잭서포트 - " +
                (settings.EnableCondition1 ? "사용" : "미사용") + "]");

            builder.AppendLine(
                "A 대상/비율: " +
                result.Condition1ConfiguredNames +
                " / " +
                result.Condition1ConfiguredRatios);

            builder.AppendLine(
                "B 대상/비율: " +
                result.Condition1BConfiguredNames +
                " / " +
                result.Condition1BConfiguredRatios);

            builder.AppendLine(
                "대상 보: " + result.Condition1TargetCount +
                " / A " + result.Condition1ATargetCount +
                " / B " + result.Condition1BTargetCount);

            builder.AppendLine(
                "생성: " + result.Condition1CreatedCount +
                " / A " + result.Condition1ACreatedCount +
                " / B " + result.Condition1BCreatedCount);
            builder.AppendLine(
                "벽체 판정: 사용 안 함 · 특수 보는 벽체 유무와 관계없이 생성");

            builder.AppendLine(
                "슬래브 경계부 위치 보정: " +
                result.Condition1BoundaryAdjustedCount);

            builder.AppendLine(
                "단부 기둥 기준 생성: 보 " +
                result.Condition1ColumnFallbackBeamCount +
                "개 / 잭서포트 " +
                result.Condition1ColumnFallbackCreatedCount +
                "개");

            builder.AppendLine(
                "특수 보/측면 접촉 부재 판정: 양쪽 " +
                result.Condition1BtstbBothSidesBeamCount +
                "개 / 한쪽 " +
                result.Condition1BtstbSingleSideBeamCount +
                "개 / 없음 " +
                result.Condition1BtstbNoSideBeamCount +
                "개");

            builder.AppendLine(
                "측면 접촉 부재 인식 기준: 구조프레임 유형명의 마지막 '_' 뒤 " +
                "표준부재명이 설정값과 정확히 일치");

            builder.AppendLine(
                "특수 보 표준부재명: " +
                result.Condition1SpecialBeamConfiguredNames +
                " / 측면 접촉 부재 표준부재명: " +
                result.Condition1SideMemberConfiguredNames);

            builder.AppendLine(
                "측면 접촉 부재 검사 구조프레임: 문서 전체 " +
                result.Condition1SideDetectionFramingCount +
                "개 / 검사 누적 " +
                result.Condition1BtsrsScannedFramingCount +
                "개");

            builder.AppendLine(
                "측면 접촉 부재 표준부재명 일치 누적: " +
                result.Condition1BtsrsStandardNameMatchedCount +
                "개 / 실제 긴 측면 접촉 누적: " +
                result.Condition1BtsrsContactMatchedCount +
                "개");

            builder.AppendLine(
                "특수 보 배치: 요청 " +
                result.Condition1BtstbRequestedPlacementCount +
                "개 / 실제 신규 생성 " +
                result.Condition1BtstbCreatedPlacementCount +
                "개");

            if (result.BtsrsStandardNameSamples.Count > 0)
            {
                builder.AppendLine(
                    "측면 접촉 부재 표준부재명 인식 예시: " +
                    string.Join(
                        ", ",
                        result.BtsrsStandardNameSamples));
            }

            if (result.BtsrsContactSamples.Count > 0)
            {
                builder.AppendLine(
                    "측면 접촉 부재 접촉 예시: " +
                    string.Join(
                        ", ",
                        result.BtsrsContactSamples));
            }

            builder.AppendLine(
                "하부 기준 없음: " +
                result.Condition1NoLowerSupportCount);

            builder.AppendLine();
            builder.AppendLine(
                "[Drop Caps 기둥 잭서포트 - " +
                (settings.EnableCondition2 ? "사용" : "미사용") +
                " / 패밀리명 포함문구 " + result.Condition2ConfiguredFamilyNameKeywords + "]");

            builder.AppendLine("대상 기둥: " + result.Condition2TargetCount);
            builder.AppendLine(
                "설정된 제외 키워드 포함 대상 제외: " +
                result.Condition2BtsExcludedCount);
            builder.AppendLine("생성: " + result.Condition2CreatedCount);

            builder.AppendLine();
            builder.AppendLine(
                "[RC보하부 잭서포트 - " +
                (settings.EnableCondition3 ? "사용" : "미사용") +
                " / " + result.Condition3ConfiguredNames + "]");

            builder.AppendLine(
                "대상 보: " +
                result.Condition3TargetCount);

            builder.AppendLine(
                "생성: " +
                result.Condition3CreatedCount);

            builder.AppendLine(
                "기존 구조기둥 구간 제외: " +
                (settings.UseExistingColumnsAsSupports
                    ? "사용"
                    : "미사용") +
                " / 감지 " +
                result.Condition3DetectedColumnSupportCount);

            builder.AppendLine(
                "벽체 구간 제외: " +
                (settings.UseWallsAsSupports
                    ? "사용"
                    : "미사용") +
                " / 감지 " +
                result.Condition3DetectedWallSupportCount);

            builder.AppendLine(
                "재계산된 남은 구간: " +
                result.Condition3ClearSpanCount);

            builder.AppendLine(
                "기준 간격 이하 남은 구간: " +
                result.Condition3ShortSpanSkippedCount);

            builder.AppendLine(
                "슬래브 경계부 위치 보정: " +
                result.Condition3BoundaryAdjustedCount);

            builder.AppendLine(
                "바닥/구조기초 없음: " +
                result.Condition3NoLowerSupportCount);

            builder.AppendLine();
            builder.AppendLine(
                "[최하층 구분 - " +
                (settings.EnableLowestFloorClassification
                    ? "사용"
                    : "미사용") +
                "]");

            builder.AppendLine(
                "실제 최하층 우선 레벨: " +
                string.Join(", ", settings.GetActualLowestLevelNames()));

            builder.AppendLine(
                "그외층 판정 부재 정확 일치: " +
                result.LowestFloorFoundationConfiguredNames);

            builder.AppendLine(
                "그외층 판정 부재 접미어: " +
                result.LowestFloorFoundationConfiguredSuffixes);

            builder.AppendLine(
                "감지 그외층 판정 부재: " +
                result.LowestFloorFoundationCount);

            builder.AppendLine(
                "처리 잭서포트: " +
                result.FloorClassificationProcessedCount +
                " / 최하층 " +
                result.LowestFloorSupportCount +
                " / 그외층 " +
                result.OtherFloorSupportCount);

            builder.AppendLine(
                "분류값 입력 성공: " +
                result.FloorClassificationTextWrittenCount +
                " / 실패 " +
                result.FloorClassificationTextFailureCount);

            builder.AppendLine(
                "수량 매개변수 입력 성공: " +
                result.FloorClassificationCountWrittenCount +
                " / 실패 " +
                result.FloorClassificationCountFailureCount +
                " / 기능 " +
                (settings.EnableFloorClassificationCountParameters
                    ? "사용"
                    : "미사용"));

            if (settings.EnableFloorClassificationCountParameters)
            {
                builder.AppendLine(
                    "최하층 수량 매개변수: " +
                    settings.LowestFloorCountParameterName +
                    " / 그외층 수량 매개변수: " +
                    settings.OtherFloorCountParameterName);
            }

            if (result.FloorClassificationErrors.Count > 0)
            {
                builder.AppendLine("최하층 구분 오류 일부:");

                foreach (string error in
                    result.FloorClassificationErrors.Take(10))
                {
                    builder.AppendLine("- " + error);
                }
            }

            builder.AppendLine();
            builder.AppendLine(
                "[높이별 데이터 - " +
                (settings.EnableHeightParameterRules
                    ? "사용"
                    : "미사용") +
                "]");

            builder.AppendLine(
                "등록 규칙: " +
                settings.GetValidHeightParameterRules().Count);

            builder.AppendLine(
                "처리 잭서포트: " +
                result.HeightRuleProcessedSupportCount);

            builder.AppendLine(
                "높이 구간 일치: " +
                result.HeightRuleMatchedSupportCount);

            builder.AppendLine(
                "매개변수 입력 성공: " +
                result.HeightRuleValueWrittenCount);

            if (result.HeightRuleItemResults.Count > 0)
            {
                builder.AppendLine(
                    "높이 구간별 입력 결과:");

                foreach (string itemResult in
                    result.HeightRuleItemResults)
                {
                    builder.AppendLine(
                        "- " + itemResult);
                }
            }

            builder.AppendLine(
                "일치 구간 없음: " +
                result.HeightRuleNoMatchingRuleCount);

            if (result.HeightRuleNoMatchSamples.Count > 0)
            {
                builder.AppendLine(
                    "일치 구간 없음 객체 일부(ElementId / 높이):");

                foreach (string sample in
                    result.HeightRuleNoMatchSamples.Take(20))
                {
                    builder.AppendLine("- " + sample);
                }
            }

            builder.AppendLine(
                "입력 실패: " +
                result.HeightRuleWriteFailureCount +
                " / 매개변수 없음 " +
                result.HeightRuleMissingParameterCount +
                " / 읽기 전용 " +
                result.HeightRuleReadOnlyParameterCount +
                " / 지원하지 않는 형식 " +
                result.HeightRuleUnsupportedStorageTypeCount);

            if (result.HeightRuleErrors.Count > 0)
            {
                builder.AppendLine("높이별 데이터 오류 일부:");

                foreach (string error in
                    result.HeightRuleErrors.Take(10))
                {
                    builder.AppendLine("- " + error);
                }
            }

            builder.AppendLine();
            builder.AppendLine(
                "[표시 색상 - " +
                (settings.EnableViewColorOverride
                    ? "사용"
                    : "미사용") +
                "]");

            string colorModeText =
                settings.ColorClassificationMode ==
                    JackSupportColorClassificationMode.HeightParameterRule
                    ? "높이별 데이터 기준"
                    : "최하층·그외층 기준";

            builder.AppendLine(
                "판정색상 분류 기준: " +
                colorModeText);

            if (settings.ColorClassificationMode ==
                JackSupportColorClassificationMode.HeightParameterRule)
            {
                builder.AppendLine(
                    "높이 규칙 색상 적용: " +
                    result.ViewColorHeightRuleAppliedCount);

                if (settings.EnableUnmatchedHeightColor &&
                    settings.EnableHeightParameterRules)
                {
                    builder.AppendLine(
                        "높이 구간 불일치 색상: RGB " +
                        settings.UnmatchedHeightColorRed + ", " +
                        settings.UnmatchedHeightColorGreen + ", " +
                        settings.UnmatchedHeightColorBlue);

                    builder.AppendLine(
                        "높이 구간 불일치 색상 적용: " +
                        result.ViewColorUnmatchedHeightAppliedCount);
                }
            }
            else if (settings.UseSeparateFloorColors &&
                settings.EnableLowestFloorClassification)
            {
                builder.AppendLine(
                    "최하층 색상: RGB " +
                    settings.LowestFloorColorRed + ", " +
                    settings.LowestFloorColorGreen + ", " +
                    settings.LowestFloorColorBlue);

                builder.AppendLine(
                    "그외층 색상: RGB " +
                    settings.OtherFloorColorRed + ", " +
                    settings.OtherFloorColorGreen + ", " +
                    settings.OtherFloorColorBlue);

                builder.AppendLine(
                    "최하층 색상 적용: " +
                    result.ViewColorLowestFloorAppliedCount);

                builder.AppendLine(
                    "그외층 색상 적용: " +
                    result.ViewColorOtherFloorAppliedCount);
            }
            else
            {
                builder.AppendLine(
                    "공통 색상: RGB " +
                    settings.ViewColorRed + ", " +
                    settings.ViewColorGreen + ", " +
                    settings.ViewColorBlue);

                builder.AppendLine(
                    "공통 색상 적용: " +
                    result.ViewColorCommonAppliedCount);
            }

            if (settings.EnableBtsColumnBasedOutline)
            {
                builder.AppendLine(
                    "단부 기둥 기준 외곽선: RGB " +
                    settings.BtsColumnBasedOutlineRed + ", " +
                    settings.BtsColumnBasedOutlineGreen + ", " +
                    settings.BtsColumnBasedOutlineBlue +
                    " / 두께 " +
                    settings.BtsColumnBasedOutlineLineWeight);

                builder.AppendLine(
                    "단부 기둥 기준 외곽선 적용: " +
                    result.ViewColorBtsColumnOutlineAppliedCount);
            }

            builder.AppendLine(
                "활성 뷰 적용 성공 합계: " +
                result.ViewColorAppliedCount);

            builder.AppendLine(
                "색상 적용 실패: " +
                result.ViewColorFailureCount);

            if (result.ViewColorErrors.Count > 0)
            {
                builder.AppendLine("색상 적용 오류 일부:");

                foreach (string error in
                    result.ViewColorErrors.Take(10))
                {
                    builder.AppendLine("- " + error);
                }
            }

            builder.AppendLine();
            builder.AppendLine(
                "중복 위치 제외: " + result.DuplicateSkippedCount);

            builder.AppendLine("오류: " + result.Errors.Count);

            if (result.Errors.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("[오류 일부]");

                foreach (string error in result.Errors.Take(10))
                    builder.AppendLine("- " + error);
            }

            return builder.ToString();
        }
    }

    public static class JackSupportResultDialog
    {
        public static void Show(
            Forms.IWin32Window owner,
            string title,
            string resultText)
        {
            using (Forms.Form dialog =
                new Forms.Form())
            {
                dialog.Text =
                    string.IsNullOrWhiteSpace(title)
                        ? "잭서포트 결과"
                        : title;

                dialog.StartPosition =
                    owner == null
                        ? Forms.FormStartPosition.CenterScreen
                        : Forms.FormStartPosition.CenterParent;

                dialog.Size =
                    new Drawing.Size(920, 720);

                dialog.MinimumSize =
                    new Drawing.Size(700, 500);

                dialog.FormBorderStyle =
                    Forms.FormBorderStyle.Sizable;

                dialog.MaximizeBox = true;
                dialog.MinimizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.KeyPreview = true;

                Forms.TextBox resultBox =
                    new Forms.TextBox();

                resultBox.Dock =
                    Forms.DockStyle.Fill;

                resultBox.Multiline = true;
                resultBox.ReadOnly = true;
                resultBox.WordWrap = false;

                resultBox.ScrollBars =
                    Forms.ScrollBars.Both;

                resultBox.AcceptsReturn = true;
                resultBox.AcceptsTab = true;
                resultBox.Font =
                    new Drawing.Font(
                        "맑은 고딕",
                        9.0F,
                        Drawing.FontStyle.Regular);

                resultBox.Text =
                    resultText ?? string.Empty;

                Forms.Panel bottomPanel =
                    new Forms.Panel();

                bottomPanel.Dock =
                    Forms.DockStyle.Bottom;

                bottomPanel.Height = 52;
                bottomPanel.Padding =
                    new Forms.Padding(8);

                Forms.Button closeButton =
                    new Forms.Button();

                closeButton.Text = "닫기";
                closeButton.Width = 96;
                closeButton.Height = 32;
                closeButton.Dock =
                    Forms.DockStyle.Right;

                closeButton.DialogResult =
                    Forms.DialogResult.OK;

                bottomPanel.Controls.Add(closeButton);
                dialog.Controls.Add(resultBox);
                dialog.Controls.Add(bottomPanel);

                dialog.AcceptButton = closeButton;
                dialog.CancelButton = closeButton;

                dialog.Shown += delegate
                {
                    resultBox.SelectionStart = 0;
                    resultBox.SelectionLength = 0;
                    resultBox.ScrollToCaret();
                    resultBox.Focus();
                };

                dialog.KeyDown += delegate(
                    object sender,
                    Forms.KeyEventArgs e)
                {
                    if (e.KeyCode == Forms.Keys.Escape)
                    {
                        dialog.Close();
                    }
                };

                if (owner == null)
                {
                    dialog.ShowDialog();
                }
                else
                {
                    dialog.ShowDialog(owner);
                }
            }
        }
    }

    public static class JackSupportResultStore
    {
        public static readonly string ResultFilePath =
            Path.Combine(
                JackSupportSettingsStore.DefaultDataFolder,
                "JackSupportLastResult.txt");

        public static void Save(string resultText)
        {
            try
            {
                Directory.CreateDirectory(
                    JackSupportSettingsStore.DefaultDataFolder);

                File.WriteAllText(
                    ResultFilePath,
                    resultText ?? string.Empty,
                    Encoding.UTF8);
            }
            catch
            {
                // 결과 저장 실패가 자동 생성 자체를 중단시키지 않도록 무시
            }
        }

        public static bool TryLoad(out string resultText)
        {
            resultText = string.Empty;

            try
            {
                if (!File.Exists(ResultFilePath))
                    return false;

                resultText = File.ReadAllText(
                    ResultFilePath,
                    Encoding.UTF8);

                return !string.IsNullOrWhiteSpace(resultText);
            }
            catch
            {
                return false;
            }
        }

        public static void ShowLatestResult()
        {
            string resultText;

            if (!TryLoad(out resultText))
            {
                TaskDialog.Show(
                    "잭서포트 최신 결과",
                    "저장된 최신 실행 결과가 없습니다.\n\n" +
                    "먼저 잭서포트 자동 생성을 한 번 실행해 주십시오.");

                return;
            }

            JackSupportResultDialog.Show(
                null,
                "잭서포트 최신 결과",
                resultText);
        }
    }

    public class JackSupportExecutionResult
    {
        public string SearchScope { get; set; }
        public string ActiveViewName { get; set; }
        public string StandardMemberRule { get; set; }

        public string Condition1ConfiguredNames { get; set; }
        public string Condition1BConfiguredNames { get; set; }
        public string Condition1ConfiguredRatios { get; set; }
        public string Condition1BConfiguredRatios { get; set; }
        public string Condition1SpecialBeamConfiguredNames { get; set; }
        public string Condition1SideMemberConfiguredNames { get; set; }
        public string Condition2ConfiguredFamilyNameKeywords { get; set; }
        public string Condition3ConfiguredNames { get; set; }
        public string FoundationConfiguredNames { get; set; }
        public string LowestFloorFoundationConfiguredNames { get; set; }
        public string LowestFloorFoundationConfiguredSuffixes { get; set; }

        public int CollectedBeamCount { get; set; }
        public int CollectedColumnCount { get; set; }
        public int CollectedFloorCount { get; set; }
        public int CollectedFoundationCount { get; set; }
        public int MatchingFoundationCount { get; set; }
        public int LowestFloorFoundationCount { get; set; }

        public int BeamTypeNameFoundCount { get; set; }
        public int BeamStandardMemberNameFoundCount { get; set; }
        public int ColumnTypeNameFoundCount { get; set; }
        public int ColumnStandardMemberNameFoundCount { get; set; }

        public int Condition1TargetCount { get; set; }
        public int Condition1ATargetCount { get; set; }
        public int Condition1BTargetCount { get; set; }
        public int Condition1CreatedCount { get; set; }
        public int Condition1ACreatedCount { get; set; }
        public int Condition1BCreatedCount { get; set; }
        public int Condition1NoLowerSupportCount { get; set; }
        public int Condition1BoundaryAdjustedCount { get; set; }
        public int Condition1WallExcludedBeamCount { get; set; }
        public int Condition1ColumnFallbackBeamCount { get; set; }
        public int Condition1ColumnFallbackCreatedCount { get; set; }
        public int Condition1BtstbNoSideBeamCount { get; set; }
        public int Condition1BtstbBothSidesBeamCount { get; set; }
        public int Condition1BtstbSingleSideBeamCount { get; set; }
        public int Condition1SideDetectionFramingCount { get; set; }
        public int Condition1BtsrsScannedFramingCount { get; set; }
        public int Condition1BtsrsStandardNameMatchedCount { get; set; }
        public int Condition1BtsrsContactMatchedCount { get; set; }
        public int Condition1BtstbRequestedPlacementCount { get; set; }
        public int Condition1BtstbCreatedPlacementCount { get; set; }

        public int Condition2TargetCount { get; set; }
        public int Condition2CreatedCount { get; set; }
        public int Condition2BtsExcludedCount { get; set; }

        public int Condition3TargetCount { get; set; }
        public int Condition3CreatedCount { get; set; }
        public int Condition3ShortSpanSkippedCount { get; set; }
        public int Condition3DetectedColumnSupportCount { get; set; }
        public int Condition3DetectedWallSupportCount { get; set; }
        public int Condition3ClearSpanCount { get; set; }
        public int Condition3NoLowerSupportCount { get; set; }
        public int Condition3BoundaryAdjustedCount { get; set; }

        public int DuplicateSkippedCount { get; set; }

        public int HeightRuleProcessedSupportCount { get; set; }
        public int HeightRuleMatchedSupportCount { get; set; }
        public int HeightRuleNoMatchingRuleCount { get; set; }
        public int HeightRuleValueWrittenCount { get; set; }
        public int HeightRuleWriteFailureCount { get; set; }
        public int HeightRuleMissingParameterCount { get; set; }
        public int HeightRuleReadOnlyParameterCount { get; set; }
        public int HeightRuleUnsupportedStorageTypeCount { get; set; }

        public int ViewColorAppliedCount { get; set; }
        public int ViewColorCommonAppliedCount { get; set; }
        public int ViewColorLowestFloorAppliedCount { get; set; }
        public int ViewColorOtherFloorAppliedCount { get; set; }
        public int ViewColorHeightRuleAppliedCount { get; set; }
        public int ViewColorUnmatchedHeightAppliedCount { get; set; }
        public int ViewColorBtsColumnOutlineAppliedCount { get; set; }
        public int ViewColorFailureCount { get; set; }

        public int FloorClassificationProcessedCount { get; set; }
        public int LowestFloorSupportCount { get; set; }
        public int OtherFloorSupportCount { get; set; }
        public int FloorClassificationWrittenCount { get; set; }
        public int FloorClassificationFailureCount { get; set; }
        public int FloorClassificationTextWrittenCount { get; set; }
        public int FloorClassificationTextFailureCount { get; set; }
        public int FloorClassificationCountWrittenCount { get; set; }
        public int FloorClassificationCountFailureCount { get; set; }

        public List<string> BeamValueSamples { get; private set; }
        public List<string> ColumnValueSamples { get; private set; }
        public List<string> BtsrsStandardNameSamples { get; private set; }
        public List<string> BtsrsContactSamples { get; private set; }
        public List<string> HeightRuleErrors { get; private set; }
        public List<string> HeightRuleNoMatchSamples { get; private set; }
        public List<string> HeightRuleItemResults { get; private set; }
        public List<string> ViewColorErrors { get; private set; }
        public List<string> FloorClassificationErrors { get; private set; }
        public List<string> Errors { get; private set; }

        public int TotalTargetCount
        {
            get
            {
                return Condition1TargetCount +
                       Condition2TargetCount +
                       Condition3TargetCount;
            }
        }

        public JackSupportExecutionResult()
        {
            SearchScope = string.Empty;
            ActiveViewName = string.Empty;
            StandardMemberRule = string.Empty;
            Condition1ConfiguredNames = string.Empty;
            Condition1BConfiguredNames = string.Empty;
            Condition1ConfiguredRatios = string.Empty;
            Condition1BConfiguredRatios = string.Empty;
            Condition1SpecialBeamConfiguredNames = string.Empty;
            Condition1SideMemberConfiguredNames = string.Empty;
            Condition2ConfiguredFamilyNameKeywords = string.Empty;
            Condition3ConfiguredNames = string.Empty;
            FoundationConfiguredNames = string.Empty;
            LowestFloorFoundationConfiguredNames = string.Empty;
            LowestFloorFoundationConfiguredSuffixes = string.Empty;

            BeamValueSamples = new List<string>();
            ColumnValueSamples = new List<string>();
            BtsrsStandardNameSamples = new List<string>();
            BtsrsContactSamples = new List<string>();
            HeightRuleErrors = new List<string>();
            HeightRuleNoMatchSamples = new List<string>();
            HeightRuleItemResults = new List<string>();
            ViewColorErrors = new List<string>();
            FloorClassificationErrors = new List<string>();
            Errors = new List<string>();
        }

        public void CopyHeightParameterStatistics(
            JackSupportHeightParameterStatistics statistics)
        {
            if (statistics == null)
                return;

            HeightRuleProcessedSupportCount =
                statistics.ProcessedSupportCount;

            HeightRuleMatchedSupportCount =
                statistics.MatchedSupportCount;

            HeightRuleNoMatchingRuleCount =
                statistics.NoMatchingRuleCount;

            HeightRuleValueWrittenCount =
                statistics.ValueWrittenCount;

            HeightRuleWriteFailureCount =
                statistics.WriteFailureCount;

            HeightRuleMissingParameterCount =
                statistics.MissingParameterCount;

            HeightRuleReadOnlyParameterCount =
                statistics.ReadOnlyParameterCount;

            HeightRuleUnsupportedStorageTypeCount =
                statistics.UnsupportedStorageTypeCount;

            HeightRuleErrors.Clear();
            HeightRuleErrors.AddRange(statistics.Errors);

            HeightRuleNoMatchSamples.Clear();
            HeightRuleNoMatchSamples.AddRange(
                statistics.NoMatchSamples);

            HeightRuleItemResults.Clear();
            HeightRuleItemResults.AddRange(
                statistics.BuildRuleResultLines());
        }

        public void CopyViewColorStatistics(
            JackSupportViewColorStatistics statistics)
        {
            if (statistics == null)
                return;

            ViewColorAppliedCount =
                statistics.AppliedCount;

            ViewColorCommonAppliedCount =
                statistics.CommonAppliedCount;

            ViewColorLowestFloorAppliedCount =
                statistics.LowestFloorAppliedCount;

            ViewColorOtherFloorAppliedCount =
                statistics.OtherFloorAppliedCount;

            ViewColorHeightRuleAppliedCount =
                statistics.HeightRuleAppliedCount;

            ViewColorUnmatchedHeightAppliedCount =
                statistics.UnmatchedHeightAppliedCount;

            ViewColorBtsColumnOutlineAppliedCount =
                statistics.BtsColumnOutlineAppliedCount;

            ViewColorFailureCount =
                statistics.FailureCount;

            ViewColorErrors.Clear();
            ViewColorErrors.AddRange(
                statistics.Errors);
        }

        public void CopyFloorClassificationStatistics(
            JackSupportFloorClassificationStatistics statistics)
        {
            if (statistics == null)
                return;

            FloorClassificationProcessedCount =
                statistics.ProcessedCount;

            LowestFloorSupportCount =
                statistics.LowestFloorCount;

            OtherFloorSupportCount =
                statistics.OtherFloorCount;

            FloorClassificationWrittenCount =
                statistics.WrittenCount;

            FloorClassificationFailureCount =
                statistics.FailureCount;

            FloorClassificationTextWrittenCount =
                statistics.ClassificationTextWrittenCount;

            FloorClassificationTextFailureCount =
                statistics.ClassificationTextFailureCount;

            FloorClassificationCountWrittenCount =
                statistics.CountParameterWrittenCount;

            FloorClassificationCountFailureCount =
                statistics.CountParameterFailureCount;

            FloorClassificationErrors.Clear();
            FloorClassificationErrors.AddRange(
                statistics.Errors);
        }

        public void AddBeamValueSample(string value)
        {
            AddSample(BeamValueSamples, value);
        }

        public void AddColumnValueSample(string value)
        {
            AddSample(ColumnValueSamples, value);
        }

        public void AddBtsrsStandardNameSample(
            string value)
        {
            AddSample(
                BtsrsStandardNameSamples,
                value);
        }

        public void AddBtsrsContactSample(
            string value)
        {
            AddSample(
                BtsrsContactSamples,
                value);
        }

        public void AddError(Element element, string text)
        {
            string prefix =
                element == null
                    ? "ElementId 없음"
                    : "ElementId " + element.Id.IntegerValue;

            Errors.Add(prefix + " - " + text);
        }

        private static void AddSample(
            IList<string> samples,
            string value)
        {
            if (samples == null ||
                string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            bool exists = samples.Any(
                item => string.Equals(
                    item,
                    value,
                    StringComparison.OrdinalIgnoreCase));

            if (!exists && samples.Count < 15)
                samples.Add(value);
        }
    }

    /// <summary>
    /// 기둥의 최종 높이는 정상이나 Revit 내부 생성 순서에서 남을 수 있는
    /// 높이 0 관련 경고만 선택적으로 삭제한다.
    /// 오류(Error)는 삭제하지 않으며 다른 경고도 그대로 유지한다.
    /// </summary>
    public class JackSupportZeroHeightWarningPreprocessor :
        IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(
            FailuresAccessor failuresAccessor)
        {
            if (failuresAccessor == null)
                return FailureProcessingResult.Continue;

            IList<FailureMessageAccessor> failureMessages =
                failuresAccessor.GetFailureMessages();

            if (failureMessages == null ||
                failureMessages.Count == 0)
            {
                return FailureProcessingResult.Continue;
            }

            foreach (FailureMessageAccessor failure in failureMessages)
            {
                if (failure == null ||
                    failure.GetSeverity() != FailureSeverity.Warning)
                {
                    continue;
                }

                string description =
                    failure.GetDescriptionText() ?? string.Empty;

                if (IsZeroHeightColumnWarning(description))
                {
                    failuresAccessor.DeleteWarning(failure);
                }
            }

            return FailureProcessingResult.Continue;
        }

        private static bool IsZeroHeightColumnWarning(
            string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return false;

            string normalized =
                description
                    .Replace(" ", string.Empty)
                    .ToLowerInvariant();

            bool mentionsColumn =
                normalized.Contains("열높이") ||
                normalized.Contains("기둥높이") ||
                normalized.Contains("columnheight");

            bool mentionsZero =
                normalized.Contains("0") ||
                normalized.Contains("zero");

            bool mentionsOffset =
                normalized.Contains("간격띄우기") ||
                normalized.Contains("offset");

            return
                mentionsColumn &&
                mentionsZero &&
                mentionsOffset;
        }
    }
}

// =========================================================
// 코드 제목: 구조 부재 조건 판정·하부 지지체 탐색·결과 저장을 포함한 잭서포트 자동 생성 명령
// 파일명: CreateJackSupportCommand.cs
// =========================================================
