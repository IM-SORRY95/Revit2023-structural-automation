// =========================================================
// 생성 날짜 및 시간: 2026-07-15 (KST)
// 파일명: JackSupportClassificationService.cs
// 설명:
// 1) 실제 최하층으로 선택한 Revit 레벨을 최우선으로 판정
// 2) 실제 최하층이 아닌 경우 지정 표준부재명 하부 객체 접촉 시 그외층 판정
// 3) 지정 표준부재명 객체에 닿지 않으면 최하층 판정
// 4) 특수 기둥 주변의 복수 잭서포트를 한 세트로 동일하게 분류
// 5) 기존 잭서포트 재판정 및 색상 일괄 적용에서 공통 사용
// =========================================================

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace REVIT_TAP
{
    public class JackSupportClassificationService
    {
        private readonly Document _doc;
        private readonly JackSupportSettings _settings;
        private readonly HashSet<string> _actualLowestLevelNames;
        private readonly IList<Level> _levels;
        private readonly IList<Element> _otherFloorMarkerElements;
        private readonly double _touchTolerance;
        private readonly double _levelElevationTolerance;

        public JackSupportClassificationService(
            Document doc,
            JackSupportSettings settings)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            if (settings == null)
                throw new ArgumentNullException("settings");

            _doc = doc;
            _settings = settings;

            _actualLowestLevelNames =
                new HashSet<string>(
                    settings.GetActualLowestLevelNames(),
                    StringComparer.OrdinalIgnoreCase);

            _levels =
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(level => level.Elevation)
                    .ToList();

            _touchTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    Math.Max(
                        0.0,
                        settings.LowestFloorTouchToleranceMm));

            _levelElevationTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    Math.Max(
                        0.0,
                        settings.ActualLowestLevelElevationToleranceMm));

            _otherFloorMarkerElements =
                CollectOtherFloorMarkerElements();
        }

        public IList<Element> OtherFloorMarkerElements
        {
            get { return _otherFloorMarkerElements; }
        }

        public int OtherFloorMarkerElementCount
        {
            get
            {
                return _otherFloorMarkerElements == null
                    ? 0
                    : _otherFloorMarkerElements.Count;
            }
        }

        public bool IsLowestFloor(
            Element lowerSupport,
            XYZ planPoint,
            double supportBottomZ)
        {
            if (!_settings.EnableLowestFloorClassification)
                return false;

            // 실제 최하층 레벨은 표준부재명 접촉보다 항상 우선한다.
            if (IsActualLowestLevel(
                lowerSupport,
                supportBottomZ))
            {
                return true;
            }

            // 실제 최하층이 아닌 위치에서 지정 표준부재명 객체에 닿으면 그외층.
            if (IsOtherFloorMarkerElement(lowerSupport) ||
                IsPointTouchingOtherFloorMarker(
                    planPoint,
                    supportBottomZ))
            {
                return false;
            }

            // 지정 부재와 닿지 않으면 최하층.
            return true;
        }

        public bool IsLowestFloorForExistingSupport(
            FamilyInstance support)
        {
            if (support == null ||
                !_settings.EnableLowestFloorClassification)
            {
                return false;
            }

            double bottomZ;
            double topZ;

            if (!TryGetSupportVerticalRange(
                support,
                out bottomZ,
                out topZ))
            {
                return false;
            }

            if (IsActualLowestLevel(
                support,
                bottomZ))
            {
                return true;
            }

            XYZ planPoint =
                GetElementPlanPoint(support);

            if (IsPointTouchingOtherFloorMarker(
                planPoint,
                bottomZ))
            {
                return false;
            }

            return true;
        }

        public bool IsLowestFloorForPcSet(
            FamilyInstance sourceColumn,
            IList<XYZ> supportPoints,
            double supportBottomZ)
        {
            if (!_settings.EnableLowestFloorClassification)
                return false;

            // 원본 특수 기둥이 실제 최하층에 속하면 주변 지지체 전체를 최하층으로 분류한다.
            if (IsActualLowestLevel(
                sourceColumn,
                supportBottomZ))
            {
                return true;
            }

            // 실제 최하층이 아니면서 4개 중 한 곳이라도 지정 부재에 닿으면
            // 특수 기둥 주변 지지체 전체를 그외층으로 통일한다.
            if (supportPoints != null)
            {
                foreach (XYZ point in supportPoints)
                {
                    if (IsPointTouchingOtherFloorMarker(
                        point,
                        supportBottomZ))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool IsActualLowestLevel(
            Element element,
            double bottomZ)
        {
            if (_actualLowestLevelNames.Count == 0)
                return false;

            Level directLevel =
                TryGetElementLevel(element);

            if (directLevel != null &&
                _actualLowestLevelNames.Contains(
                    directLevel.Name))
            {
                return true;
            }

            // 직접 레벨을 읽지 못한 경우 또는 레벨 간격띄우기가 큰 경우를 대비해
            // 선택한 최하층 레벨의 기준높이와 하단 높이를 허용오차로 비교한다.
            foreach (Level level in _levels)
            {
                if (!_actualLowestLevelNames.Contains(level.Name))
                    continue;

                if (Math.Abs(
                    level.Elevation - bottomZ) <=
                    _levelElevationTolerance)
                {
                    return true;
                }
            }

            Level mappedLevel =
                FindLevelAtOrBelow(bottomZ);

            return mappedLevel != null &&
                _actualLowestLevelNames.Contains(
                    mappedLevel.Name);
        }

        public bool IsOtherFloorMarkerElement(
            Element element)
        {
            if (element == null)
                return false;

            string standardName =
                JackSupportGeometryHelper.GetStandardMemberName(
                    _doc,
                    element);

            return IsOtherFloorMarkerStandardName(
                standardName);
        }

        public bool IsOtherFloorMarkerStandardName(
            string standardName)
        {
            if (string.IsNullOrWhiteSpace(standardName))
                return false;

            foreach (string configuredName in
                _settings.GetOtherFloorMarkerNames())
            {
                if (string.Equals(
                    configuredName,
                    standardName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (string suffix in
                _settings.GetOtherFloorMarkerSuffixes())
            {
                if (!string.IsNullOrWhiteSpace(suffix) &&
                    standardName.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsPointTouchingOtherFloorMarker(
            XYZ planPoint,
            double supportBottomZ)
        {
            if (planPoint == null ||
                _otherFloorMarkerElements == null ||
                _otherFloorMarkerElements.Count == 0)
            {
                return false;
            }

            double searchDepth =
                Math.Max(
                    _touchTolerance * 2.0,
                    JackSupportGeometryHelper.MmToInternal(10.0));

            Element markerElement;
            double markerTopZ;

            bool found =
                JackSupportGeometryHelper.TryFindNearestLowerSupportTop(
                    planPoint,
                    supportBottomZ + _touchTolerance,
                    searchDepth,
                    _otherFloorMarkerElements,
                    out markerElement,
                    out markerTopZ);

            return found &&
                Math.Abs(
                    markerTopZ - supportBottomZ) <=
                _touchTolerance;
        }

        public static bool TryGetSupportVerticalRange(
            FamilyInstance support,
            out double bottomZ,
            out double topZ)
        {
            bottomZ = 0.0;
            topZ = 0.0;

            if (support == null)
                return false;

            Parameter baseLevelParameter =
                support.get_Parameter(
                    BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);

            Parameter baseOffsetParameter =
                support.get_Parameter(
                    BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);

            if (baseLevelParameter != null &&
                baseLevelParameter.StorageType == StorageType.ElementId)
            {
                Level baseLevel =
                    support.Document.GetElement(
                        baseLevelParameter.AsElementId()) as Level;

                if (baseLevel != null)
                {
                    double baseOffset =
                        baseOffsetParameter != null &&
                        baseOffsetParameter.StorageType == StorageType.Double
                            ? baseOffsetParameter.AsDouble()
                            : 0.0;

                    bottomZ =
                        baseLevel.Elevation + baseOffset;

                    Parameter topLevelParameter =
                        support.get_Parameter(
                            BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);

                    if (topLevelParameter != null &&
                        topLevelParameter.StorageType == StorageType.ElementId)
                    {
                        Level topLevel =
                            support.Document.GetElement(
                                topLevelParameter.AsElementId()) as Level;

                        if (topLevel != null)
                        {
                            Parameter topOffsetParameter =
                                support.get_Parameter(
                                    BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);

                            double topOffset =
                                topOffsetParameter != null &&
                                topOffsetParameter.StorageType == StorageType.Double
                                    ? topOffsetParameter.AsDouble()
                                    : 0.0;

                            topZ =
                                topLevel.Elevation + topOffset;

                            if (topZ > bottomZ)
                                return true;
                        }
                    }

                    Parameter heightParameter =
                        support.get_Parameter(
                            BuiltInParameter.FAMILY_HEIGHT_PARAM);

                    if (heightParameter != null &&
                        heightParameter.StorageType == StorageType.Double)
                    {
                        double height =
                            heightParameter.AsDouble();

                        if (height > 0.0)
                        {
                            topZ = bottomZ + height;
                            return true;
                        }
                    }
                }
            }

            return JackSupportGeometryHelper.TryGetElementVerticalRange(
                support,
                out bottomZ,
                out topZ);
        }

        public static XYZ GetElementPlanPoint(
            Element element)
        {
            if (element == null)
                return null;

            LocationPoint locationPoint =
                element.Location as LocationPoint;

            if (locationPoint != null &&
                locationPoint.Point != null)
            {
                return locationPoint.Point;
            }

            BoundingBoxXYZ box =
                element.get_BoundingBox(null);

            if (box == null)
                return null;

            return new XYZ(
                (box.Min.X + box.Max.X) * 0.5,
                (box.Min.Y + box.Max.Y) * 0.5,
                box.Min.Z);
        }

        private IList<Element>
            CollectOtherFloorMarkerElements()
        {
            if (!_settings.EnableLowestFloorClassification)
                return new List<Element>();

            if (_settings.GetOtherFloorMarkerNames().Count == 0 &&
                _settings.GetOtherFloorMarkerSuffixes().Count == 0)
            {
                return new List<Element>();
            }

            List<Element> result =
                new List<Element>();

            foreach (Element element in
                new FilteredElementCollector(_doc)
                    .WhereElementIsNotElementType()
                    .ToElements())
            {
                if (element == null ||
                    element.Category == null ||
                    element.Category.CategoryType != CategoryType.Model)
                {
                    continue;
                }

                if (JackSupportFamilyService.IsConfiguredJackSupportElement(
                    _doc,
                    element,
                    _settings))
                {
                    continue;
                }

                if (IsOtherFloorMarkerElement(element))
                    result.Add(element);
            }

            return result;
        }

        private Level TryGetElementLevel(
            Element element)
        {
            if (element == null)
                return null;

            try
            {
                ElementId levelId = element.LevelId;

                Level level =
                    _doc.GetElement(levelId) as Level;

                if (level != null)
                    return level;
            }
            catch
            {
                // 다음 매개변수 후보를 검사한다.
            }

            BuiltInParameter[] candidates =
            {
                BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                BuiltInParameter.LEVEL_PARAM,
                BuiltInParameter.WALL_BASE_CONSTRAINT
            };

            foreach (BuiltInParameter candidate in candidates)
            {
                try
                {
                    Parameter parameter =
                        element.get_Parameter(candidate);

                    if (parameter == null ||
                        parameter.StorageType != StorageType.ElementId)
                    {
                        continue;
                    }

                    Level level =
                        _doc.GetElement(
                            parameter.AsElementId()) as Level;

                    if (level != null)
                        return level;
                }
                catch
                {
                    // 지원하지 않는 매개변수는 다음 후보로 진행한다.
                }
            }

            return null;
        }

        private Level FindLevelAtOrBelow(
            double elevation)
        {
            if (_levels == null ||
                _levels.Count == 0)
            {
                return null;
            }

            Level below =
                _levels
                    .Where(level =>
                        level.Elevation <=
                        elevation + _levelElevationTolerance)
                    .OrderByDescending(level => level.Elevation)
                    .FirstOrDefault();

            if (below != null)
                return below;

            return _levels
                .OrderBy(level =>
                    Math.Abs(
                        level.Elevation - elevation))
                .FirstOrDefault();
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 최하층 우선 및 하부 표식 요소 접촉 판정 서비스
// 파일명: JackSupportClassificationService.cs
// =========================================================
