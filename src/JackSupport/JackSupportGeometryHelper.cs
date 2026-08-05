// =========================================================
// 생성 날짜 및 시간: 2026-07-13 (KST)
// 수정 날짜 및 시간: 2026-07-13 (KST)
// 파일명: JackSupportGeometryHelper.cs
// 설명:
// 1) 요소 Solid 수집
// 2) 슬래브 경계부 주변 검색과 곡선 접선 기준 위치 보정
// 2) 구조프레임 하단 Z 및 바닥 상단 Z 계산
// 3) 보와 접촉·겹치는 기존 구조기둥 및 벽체의 점유 구간 계산
// 4) 바닥 및 구조기초 중 가장 가까운 하부 지지체 상단 검색
// 5) 사각기둥 외곽면 기준 4개 생성점 및 RC보하부 잭서포트 지지구간 계산
// 6) 점유 구간을 합친 뒤 남은 각 구간을 독립적으로 재계산
// 7) 특수 보 양단의 단부 구조기둥 하단 높이 보간
// 8) 특수 보 긴 측면의 측면 부재 접촉 방향 판정
// 9) 측면 부재는 구조프레임 유형명 마지막 '_' 뒤 식별값과 정확히 일치하도록 판정
// 10) 특수 보·측면 부재 판정 대상과 접촉 결과 진단 정보 제공
// =========================================================

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace REVIT_TAP
{
    public class JackSupportSpan
    {
        public double StartDistance { get; set; }
        public double EndDistance { get; set; }

        public double Length
        {
            get { return Math.Max(0.0, EndDistance - StartDistance); }
        }
    }

    public class JackSupportElementExtents
    {
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }

        public bool IsValid
        {
            get
            {
                return MaxX >= MinX &&
                       MaxY >= MinY &&
                       MaxZ >= MinZ;
            }
        }
    }

    public class JackSupportBtsColumnFallback
    {
        public FamilyInstance StartColumn { get; set; }
        public FamilyInstance EndColumn { get; set; }
        public double StartBottomZ { get; set; }
        public double EndBottomZ { get; set; }

        public double GetBottomZ(double ratio)
        {
            double clamped = Math.Max(0.0, Math.Min(1.0, ratio));
            return StartBottomZ +
                (EndBottomZ - StartBottomZ) * clamped;
        }

        public Element GetReferenceElement(double ratio)
        {
            return ratio <= 0.5
                ? (Element)StartColumn
                : EndColumn;
        }
    }

    public class JackSupportBtsSideContactInfo
    {
        public bool HasNegativeSide { get; set; }
        public bool HasPositiveSide { get; set; }
        public XYZ PerpendicularDirection { get; set; }
        public double LateralOffsetDistance { get; set; }

        public int ScannedFramingCount { get; set; }
        public int StandardNameMatchedCount { get; set; }
        public int ContactMatchedCount { get; set; }

        public List<string> StandardNameSamples { get; private set; }
        public List<string> ContactSamples { get; private set; }

        public JackSupportBtsSideContactInfo()
        {
            PerpendicularDirection = XYZ.BasisY;
            StandardNameSamples = new List<string>();
            ContactSamples = new List<string>();
        }

        public void AddStandardNameSample(string value)
        {
            AddSample(StandardNameSamples, value);
        }

        public void AddContactSample(string value)
        {
            AddSample(ContactSamples, value);
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
            {
                samples.Add(value);
            }
        }
    }

    public static class JackSupportGeometryHelper
    {
        private const double Tiny = 1.0e-9;

        public static double MmToInternal(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }

        public static double InternalToMm(double internalValue)
        {
            return UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters);
        }

        public static string GetElementTypeName(
            Document doc,
            Element element)
        {
            if (doc == null || element == null)
                return string.Empty;

            try
            {
                ElementType elementType =
                    doc.GetElement(element.GetTypeId())
                    as ElementType;

                if (elementType == null)
                    return string.Empty;

                return string.IsNullOrWhiteSpace(elementType.Name)
                    ? string.Empty
                    : elementType.Name.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetElementFamilyName(
            Document doc,
            Element element)
        {
            if (doc == null ||
                element == null)
            {
                return string.Empty;
            }

            try
            {
                FamilyInstance familyInstance =
                    element as FamilyInstance;

                if (familyInstance != null &&
                    familyInstance.Symbol != null &&
                    familyInstance.Symbol.Family != null)
                {
                    string familyName =
                        familyInstance.Symbol.Family.Name;

                    return string.IsNullOrWhiteSpace(
                        familyName)
                            ? string.Empty
                            : familyName.Trim();
                }
            }
            catch
            {
                // FamilyInstance 방식으로 확인하지 못하면
                // ElementType.FamilyName으로 계속 확인한다.
            }

            try
            {
                ElementType elementType =
                    doc.GetElement(
                        element.GetTypeId())
                    as ElementType;

                if (elementType == null)
                {
                    return string.Empty;
                }

                string familyName =
                    elementType.FamilyName;

                return string.IsNullOrWhiteSpace(
                    familyName)
                        ? string.Empty
                        : familyName.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetStandardMemberName(
            Document doc,
            Element element)
        {
            string typeName;
            string standardMemberName;

            TryGetStandardMemberName(
                doc,
                element,
                out typeName,
                out standardMemberName);

            return standardMemberName;
        }

        public static bool TryGetStandardMemberName(
            Document doc,
            Element element,
            out string typeName,
            out string standardMemberName)
        {
            typeName = GetElementTypeName(doc, element);
            standardMemberName = string.Empty;

            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            int lastUnderscoreIndex =
                typeName.LastIndexOf('_');

            if (lastUnderscoreIndex < 0 ||
                lastUnderscoreIndex >= typeName.Length - 1)
            {
                return false;
            }

            standardMemberName =
                typeName
                    .Substring(lastUnderscoreIndex + 1)
                    .Trim();

            return !string.IsNullOrWhiteSpace(
                standardMemberName);
        }

        public static Curve GetLocationCurve(Element element)
        {
            LocationCurve locationCurve = element == null ? null : element.Location as LocationCurve;
            return locationCurve == null ? null : locationCurve.Curve;
        }

        public static XYZ GetPointAtNormalizedParameter(Curve curve, double ratio)
        {
            if (curve == null)
                return null;

            double clamped = Math.Max(0.0, Math.Min(1.0, ratio));
            return curve.Evaluate(clamped, true);
        }

        public static XYZ GetPointAtDistance(Curve curve, double distance)
        {
            if (curve == null)
                return null;

            double length = curve.Length;
            if (length < Tiny)
                return curve.GetEndPoint(0);

            double ratio = Math.Max(0.0, Math.Min(1.0, distance / length));
            return curve.Evaluate(ratio, true);
        }

        public static XYZ GetCurveTangentAtNormalizedParameter(
            Curve curve,
            double normalizedParameter)
        {
            if (curve == null)
                return XYZ.BasisX;

            try
            {
                double parameter =
                    Math.Max(0.0, Math.Min(1.0, normalizedParameter));

                Transform derivatives =
                    curve.ComputeDerivatives(parameter, true);

                XYZ tangent =
                    derivatives == null
                        ? null
                        : derivatives.BasisX;

                return NormalizeHorizontal(tangent);
            }
            catch
            {
                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                return NormalizeHorizontal(end - start);
            }
        }

        public static XYZ GetCurveTangentAtDistance(
            Curve curve,
            double distance)
        {
            if (curve == null || curve.Length <= Tiny)
                return XYZ.BasisX;

            return GetCurveTangentAtNormalizedParameter(
                curve,
                distance / curve.Length);
        }

        public static IList<double> GetEvenNormalizedRatios(
            int count)
        {
            List<double> ratios = new List<double>();

            if (count <= 0)
                return ratios;

            for (int index = 0;
                index < count;
                index++)
            {
                ratios.Add(
                    (index + 1.0) /
                    (count + 1.0));
            }

            return ratios;
        }

        public static bool TryGetBtsColumnFallback(
            Document doc,
            Element beam,
            Curve beamCurve,
            IList<Element> structuralColumns,
            IEnumerable<string> configuredStandardNames,
            double touchTolerance,
            out JackSupportBtsColumnFallback fallback)
        {
            fallback = null;

            if (doc == null ||
                beam == null ||
                beamCurve == null ||
                structuralColumns == null)
            {
                return false;
            }

            XYZ startPoint = beamCurve.GetEndPoint(0);
            XYZ endPoint = beamCurve.GetEndPoint(1);

            double startBeamBottomZ;
            double endBeamBottomZ;

            if (!TryGetElementBottomAtPoint(
                beam,
                startPoint,
                out startBeamBottomZ) ||
                !TryGetElementBottomAtPoint(
                    beam,
                    endPoint,
                    out endBeamBottomZ))
            {
                return false;
            }

            FamilyInstance startColumn;
            FamilyInstance endColumn;
            double startBottomZ;
            double endBottomZ;

            bool foundStart = TryFindAttachedBtsColumn(
                doc,
                startPoint,
                startBeamBottomZ,
                structuralColumns,
                configuredStandardNames,
                touchTolerance,
                out startColumn,
                out startBottomZ);

            bool foundEnd = TryFindAttachedBtsColumn(
                doc,
                endPoint,
                endBeamBottomZ,
                structuralColumns,
                configuredStandardNames,
                touchTolerance,
                out endColumn,
                out endBottomZ);

            if (!foundStart ||
                !foundEnd ||
                startColumn == null ||
                endColumn == null ||
                startColumn.Id.IntegerValue ==
                    endColumn.Id.IntegerValue)
            {
                return false;
            }

            fallback = new JackSupportBtsColumnFallback
            {
                StartColumn = startColumn,
                EndColumn = endColumn,
                StartBottomZ = startBottomZ,
                EndBottomZ = endBottomZ
            };

            return true;
        }

        public static JackSupportBtsSideContactInfo
            GetBtsSideContactInfo(
                Document doc,
                Element targetBeam,
                Curve targetCurve,
                IList<Element> framingElements,
                IEnumerable<string> sideMemberStandardNames,
                double contactTolerance)
        {
            JackSupportBtsSideContactInfo result =
                new JackSupportBtsSideContactInfo();

            if (doc == null ||
                targetBeam == null ||
                targetCurve == null ||
                framingElements == null)
            {
                return result;
            }

            XYZ start = targetCurve.GetEndPoint(0);
            XYZ tangent =
                GetCurveTangentAtNormalizedParameter(
                    targetCurve,
                    0.5);

            XYZ perpendicular =
                NormalizeHorizontal(
                    new XYZ(
                        -tangent.Y,
                        tangent.X,
                        0.0));

            result.PerpendicularDirection = perpendicular;

            JackSupportElementExtents beamExtents =
                GetElementExtents(
                    targetBeam,
                    tangent,
                    perpendicular,
                    start);

            if (beamExtents == null ||
                !beamExtents.IsValid)
            {
                return result;
            }

            double beamCenterY =
                (beamExtents.MinY + beamExtents.MaxY) * 0.5;

            double halfWidth =
                Math.Max(
                    0.0,
                    (beamExtents.MaxY - beamExtents.MinY) * 0.5);

            result.LateralOffsetDistance =
                Math.Max(
                    0.0,
                    halfWidth * 0.65);

            foreach (Element candidate in framingElements)
            {
                if (candidate == null ||
                    candidate.Id.IntegerValue ==
                        targetBeam.Id.IntegerValue)
                {
                    continue;
                }

                result.ScannedFramingCount++;

                string candidateTypeName;
                string standardName;

                bool standardNameFound =
                    TryGetStandardMemberName(
                        doc,
                        candidate,
                        out candidateTypeName,
                        out standardName);

                if (!standardNameFound ||
                    !MatchesAnyExactText(
                        standardName,
                        sideMemberStandardNames))
                {
                    continue;
                }

                result.StandardNameMatchedCount++;
                result.AddStandardNameSample(
                    "ElementId " +
                    candidate.Id.IntegerValue +
                    " / " +
                    candidateTypeName +
                    " → " +
                    standardName);

                JackSupportElementExtents candidateExtents =
                    GetElementExtents(
                        candidate,
                        tangent,
                        perpendicular,
                        start);

                if (candidateExtents == null ||
                    !candidateExtents.IsValid)
                {
                    continue;
                }

                bool overlapsAlongLength =
                    IntervalsOverlap(
                        beamExtents.MinX,
                        beamExtents.MaxX,
                        candidateExtents.MinX,
                        candidateExtents.MaxX,
                        contactTolerance);

                bool overlapsVertically =
                    IntervalsOverlap(
                        beamExtents.MinZ,
                        beamExtents.MaxZ,
                        candidateExtents.MinZ,
                        candidateExtents.MaxZ,
                        contactTolerance);

                if (!overlapsAlongLength ||
                    !overlapsVertically)
                {
                    continue;
                }

                double candidateCenterY =
                    (candidateExtents.MinY +
                     candidateExtents.MaxY) * 0.5;

                bool contactMatched = false;
                string contactSide = string.Empty;

                if (candidateCenterY >= beamCenterY)
                {
                    double gap =
                        candidateExtents.MinY -
                        beamExtents.MaxY;

                    if (gap <= contactTolerance &&
                        candidateExtents.MaxY >=
                            beamExtents.MaxY -
                            contactTolerance)
                    {
                        result.HasPositiveSide = true;
                        contactMatched = true;
                        contactSide = "양의 긴 측면";
                    }
                }
                else
                {
                    double gap =
                        beamExtents.MinY -
                        candidateExtents.MaxY;

                    if (gap <= contactTolerance &&
                        candidateExtents.MinY <=
                            beamExtents.MinY +
                            contactTolerance)
                    {
                        result.HasNegativeSide = true;
                        contactMatched = true;
                        contactSide = "음의 긴 측면";
                    }
                }

                if (contactMatched)
                {
                    result.ContactMatchedCount++;
                    result.AddContactSample(
                        "ElementId " +
                        candidate.Id.IntegerValue +
                        " / " +
                        standardName +
                        " / " +
                        contactSide);
                }
            }

            return result;
        }

        private static bool TryFindAttachedBtsColumn(
            Document doc,
            XYZ beamEndPoint,
            double beamBottomZ,
            IList<Element> structuralColumns,
            IEnumerable<string> configuredStandardNames,
            double touchTolerance,
            out FamilyInstance selectedColumn,
            out double selectedBottomZ)
        {
            selectedColumn = null;
            selectedBottomZ = 0.0;
            double bestScore = double.MaxValue;

            foreach (Element element in structuralColumns)
            {
                FamilyInstance column =
                    element as FamilyInstance;

                if (column == null)
                    continue;

                string standardName =
                    GetStandardMemberName(
                        doc,
                        column);

                if (!MatchesAnyText(
                    standardName,
                    configuredStandardNames))
                {
                    continue;
                }

                JackSupportElementExtents extents =
                    GetElementExtents(
                        column,
                        XYZ.BasisX,
                        XYZ.BasisY,
                        XYZ.Zero);

                if (extents == null ||
                    !extents.IsValid)
                {
                    continue;
                }

                bool containsEndPoint =
                    beamEndPoint.X >=
                        extents.MinX - touchTolerance &&
                    beamEndPoint.X <=
                        extents.MaxX + touchTolerance &&
                    beamEndPoint.Y >=
                        extents.MinY - touchTolerance &&
                    beamEndPoint.Y <=
                        extents.MaxY + touchTolerance;

                bool touchesBeamBottom =
                    extents.MinZ <
                        beamBottomZ - Tiny &&
                    extents.MaxZ >=
                        beamBottomZ - touchTolerance;

                if (!containsEndPoint ||
                    !touchesBeamBottom)
                {
                    continue;
                }

                XYZ center = GetElementCenter(column);
                double dx = center.X - beamEndPoint.X;
                double dy = center.Y - beamEndPoint.Y;
                double horizontalDistance =
                    Math.Sqrt(dx * dx + dy * dy);

                double score =
                    horizontalDistance +
                    Math.Abs(
                        extents.MaxZ - beamBottomZ) * 0.05;

                if (score >= bestScore)
                    continue;

                bestScore = score;
                selectedColumn = column;
                selectedBottomZ = extents.MinZ;
            }

            return selectedColumn != null &&
                selectedBottomZ < beamBottomZ - Tiny;
        }

        private static bool MatchesAnyExactText(
            string value,
            IEnumerable<string> configuredValues)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                configuredValues == null)
            {
                return false;
            }

            foreach (string configuredValue in configuredValues)
            {
                if (string.IsNullOrWhiteSpace(configuredValue))
                    continue;

                if (string.Equals(
                    value.Trim(),
                    configuredValue.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesAnyText(
            string value,
            IEnumerable<string> configuredValues)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                configuredValues == null)
            {
                return false;
            }

            foreach (string configuredValue in configuredValues)
            {
                if (string.IsNullOrWhiteSpace(configuredValue))
                    continue;

                string token = configuredValue.Trim();

                if (string.Equals(
                    value,
                    token,
                    StringComparison.OrdinalIgnoreCase) ||
                    value.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static IList<Solid> GetSolids(Element element)
        {
            List<Solid> solids = new List<Solid>();
            if (element == null)
                return solids;

            Options options = new Options();
            options.ComputeReferences = false;
            options.IncludeNonVisibleObjects = false;
            options.DetailLevel = ViewDetailLevel.Fine;

            GeometryElement geometry = element.get_Geometry(options);
            CollectSolids(geometry, solids);
            return solids;
        }

        private static void CollectSolids(GeometryElement geometry, IList<Solid> solids)
        {
            if (geometry == null)
                return;

            foreach (GeometryObject geometryObject in geometry)
            {
                Solid solid = geometryObject as Solid;
                if (solid != null)
                {
                    if (solid.Volume > Tiny && solid.Faces.Size > 0)
                        solids.Add(solid);
                    continue;
                }

                GeometryInstance instance = geometryObject as GeometryInstance;
                if (instance != null)
                {
                    GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                    CollectSolids(instanceGeometry, solids);
                }
            }
        }

        public static IList<XYZ> GetGeometryVertices(Element element)
        {
            List<XYZ> points = new List<XYZ>();

            foreach (Solid solid in GetSolids(element))
            {
                foreach (Edge edge in solid.Edges)
                {
                    IList<XYZ> tessellated = edge.Tessellate();
                    if (tessellated == null)
                        continue;

                    foreach (XYZ point in tessellated)
                    {
                        if (point != null)
                            points.Add(point);
                    }
                }
            }

            if (points.Count == 0)
            {
                BoundingBoxXYZ box = element == null ? null : element.get_BoundingBox(null);
                if (box != null)
                    points.AddRange(GetBoundingBoxCorners(box));
            }

            return points;
        }

        public static JackSupportElementExtents GetElementExtents(
            Element element,
            XYZ axisX,
            XYZ axisY,
            XYZ origin)
        {
            List<XYZ> points = GetGeometryVertices(element).ToList();
            if (points.Count == 0)
                return null;

            XYZ x = NormalizeHorizontal(axisX);
            XYZ y = NormalizeHorizontal(axisY);
            XYZ o = origin ?? XYZ.Zero;

            JackSupportElementExtents result = new JackSupportElementExtents();
            result.MinX = double.MaxValue;
            result.MaxX = double.MinValue;
            result.MinY = double.MaxValue;
            result.MaxY = double.MinValue;
            result.MinZ = double.MaxValue;
            result.MaxZ = double.MinValue;

            foreach (XYZ point in points)
            {
                XYZ relative = point - o;
                double px = relative.DotProduct(x);
                double py = relative.DotProduct(y);

                result.MinX = Math.Min(result.MinX, px);
                result.MaxX = Math.Max(result.MaxX, px);
                result.MinY = Math.Min(result.MinY, py);
                result.MaxY = Math.Max(result.MaxY, py);
                result.MinZ = Math.Min(result.MinZ, point.Z);
                result.MaxZ = Math.Max(result.MaxZ, point.Z);
            }

            return result;
        }

        public static IList<XYZ> GetCondition2Points(
            FamilyInstance column,
            double offsetInternal)
        {
            List<XYZ> result = new List<XYZ>();
            if (column == null)
                return result;

            XYZ axisX = GetColumnAxisX(column);
            XYZ axisY = GetColumnAxisY(column, axisX);
            XYZ origin = GetElementCenter(column);

            JackSupportElementExtents extents =
                GetElementExtents(column, axisX, axisY, origin);

            if (extents == null || !extents.IsValid)
                return result;

            double centerX = (extents.MinX + extents.MaxX) * 0.5;
            double centerY = (extents.MinY + extents.MaxY) * 0.5;
            double centerZ = extents.MinZ;

            result.Add(origin + axisX * (extents.MaxX + offsetInternal) + axisY * centerY);
            result.Add(origin + axisX * (extents.MinX - offsetInternal) + axisY * centerY);
            result.Add(origin + axisX * centerX + axisY * (extents.MaxY + offsetInternal));
            result.Add(origin + axisX * centerX + axisY * (extents.MinY - offsetInternal));

            for (int i = 0; i < result.Count; i++)
                result[i] = new XYZ(result[i].X, result[i].Y, centerZ);

            return result;
        }

        public static bool TryGetElementVerticalRange(
            Element element,
            out double minZ,
            out double maxZ)
        {
            minZ = double.MaxValue;
            maxZ = double.MinValue;

            IList<XYZ> points = GetGeometryVertices(element);
            foreach (XYZ point in points)
            {
                minZ = Math.Min(minZ, point.Z);
                maxZ = Math.Max(maxZ, point.Z);
            }

            if (minZ == double.MaxValue || maxZ == double.MinValue)
            {
                BoundingBoxXYZ box = element == null ? null : element.get_BoundingBox(null);
                if (box == null)
                    return false;

                minZ = box.Min.Z;
                maxZ = box.Max.Z;
            }

            return maxZ >= minZ;
        }

        public static bool TryGetElementBottomAtPoint(
            Element element,
            XYZ xyPoint,
            out double bottomZ)
        {
            bottomZ = 0.0;
            if (element == null || xyPoint == null)
                return false;

            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null)
                return false;

            double margin = MmToInternal(200.0);
            double lineBottom = box.Min.Z - margin;
            double lineTop = box.Max.Z + margin;

            if (TryIntersectVerticalLine(
                GetSolids(element),
                xyPoint,
                lineBottom,
                lineTop,
                out double minIntersectionZ,
                out double maxIntersectionZ))
            {
                bottomZ = minIntersectionZ;
                return true;
            }

            bottomZ = box.Min.Z;
            return true;
        }

        public static bool TryFindNearestFloorTop(
            XYZ xyPoint,
            double upperZ,
            double searchDepth,
            IList<Element> floors,
            out Element floor,
            out double floorTopZ)
        {
            return TryFindNearestLowerSupportTop(
                xyPoint,
                upperZ,
                searchDepth,
                floors,
                out floor,
                out floorTopZ);
        }

        public static bool TryFindNearestLowerSupportTop(
            XYZ xyPoint,
            double upperZ,
            double searchDepth,
            IList<Element> lowerSupports,
            out Element lowerSupport,
            out double supportTopZ)
        {
            lowerSupport = null;
            supportTopZ = double.MinValue;

            if (xyPoint == null || lowerSupports == null)
                return false;

            double lowerZ = upperZ - searchDepth;
            double xyTolerance = MmToInternal(2.0);

            foreach (Element candidate in lowerSupports)
            {
                BoundingBoxXYZ box =
                    candidate == null
                        ? null
                        : candidate.get_BoundingBox(null);

                if (box == null)
                    continue;

                if (xyPoint.X < box.Min.X - xyTolerance ||
                    xyPoint.X > box.Max.X + xyTolerance ||
                    xyPoint.Y < box.Min.Y - xyTolerance ||
                    xyPoint.Y > box.Max.Y + xyTolerance)
                {
                    continue;
                }

                if (box.Min.Z > upperZ || box.Max.Z < lowerZ)
                    continue;

                IList<Solid> candidateSolids = GetSolids(candidate);

                double minIntersectionZ;
                double maxIntersectionZ;

                bool intersects = TryIntersectVerticalLine(
                    candidateSolids,
                    xyPoint,
                    lowerZ,
                    upperZ,
                    out minIntersectionZ,
                    out maxIntersectionZ);

                if (!intersects && candidateSolids.Count > 0)
                    continue;

                double candidateTop =
                    intersects
                        ? maxIntersectionZ
                        : box.Max.Z;

                if (candidateTop <= upperZ + Tiny &&
                    candidateTop > supportTopZ)
                {
                    lowerSupport = candidate;
                    supportTopZ = candidateTop;
                }
            }

            return lowerSupport != null;
        }

        public static bool TryFindNearestLowerSupportTopWithBoundarySearch(
            XYZ originalPoint,
            XYZ curveTangent,
            double upperZ,
            double searchDepth,
            IList<Element> lowerSupports,
            bool enableBoundarySearch,
            double maximumSearchDistance,
            double searchStep,
            double supportTopDifferenceTolerance,
            bool moveToFoundPoint,
            out XYZ resolvedPoint,
            out Element lowerSupport,
            out double supportTopZ,
            out double horizontalCorrectionDistance)
        {
            resolvedPoint = originalPoint;
            lowerSupport = null;
            supportTopZ = double.MinValue;
            horizontalCorrectionDistance = 0.0;

            if (TryFindNearestLowerSupportTop(
                originalPoint,
                upperZ,
                searchDepth,
                lowerSupports,
                out lowerSupport,
                out supportTopZ))
            {
                return true;
            }

            if (!enableBoundarySearch ||
                originalPoint == null ||
                maximumSearchDistance <= Tiny ||
                searchStep <= Tiny)
            {
                return false;
            }

            XYZ tangent = NormalizeHorizontal(curveTangent);
            XYZ perpendicular =
                NormalizeHorizontal(
                    new XYZ(-tangent.Y, tangent.X, 0.0));

            List<XYZ> directions = new List<XYZ>();
            AddUniqueDirection(directions, perpendicular);
            AddUniqueDirection(directions, perpendicular * -1.0);
            AddUniqueDirection(directions, tangent);
            AddUniqueDirection(directions, tangent * -1.0);
            AddUniqueDirection(directions, tangent + perpendicular);
            AddUniqueDirection(directions, tangent - perpendicular);
            AddUniqueDirection(directions, tangent * -1.0 + perpendicular);
            AddUniqueDirection(directions, tangent * -1.0 + perpendicular * -1.0);
            AddUniqueDirection(directions, XYZ.BasisX);
            AddUniqueDirection(directions, XYZ.BasisX * -1.0);
            AddUniqueDirection(directions, XYZ.BasisY);
            AddUniqueDirection(directions, XYZ.BasisY * -1.0);

            List<BoundaryLowerSupportHit> hits =
                new List<BoundaryLowerSupportHit>();

            int ringCount =
                Math.Max(1,
                    Convert.ToInt32(
                        Math.Ceiling(
                            maximumSearchDistance / searchStep)));

            for (int ring = 1; ring <= ringCount; ring++)
            {
                double distance =
                    Math.Min(
                        maximumSearchDistance,
                        ring * searchStep);

                for (int directionIndex = 0;
                    directionIndex < directions.Count;
                    directionIndex++)
                {
                    XYZ candidatePoint =
                        originalPoint +
                        directions[directionIndex] * distance;

                    Element candidateSupport;
                    double candidateTopZ;

                    if (!TryFindNearestLowerSupportTop(
                        candidatePoint,
                        upperZ,
                        searchDepth,
                        lowerSupports,
                        out candidateSupport,
                        out candidateTopZ))
                    {
                        continue;
                    }

                    hits.Add(
                        new BoundaryLowerSupportHit
                        {
                            Point = candidatePoint,
                            Support = candidateSupport,
                            SupportTopZ = candidateTopZ,
                            Distance = distance,
                            DirectionPriority = directionIndex
                        });
                }

                if (hits.Count > 0)
                    break;
            }

            if (hits.Count == 0)
                return false;

            double highestTopZ = hits.Max(hit => hit.SupportTopZ);
            double topTolerance =
                Math.Max(0.0, supportTopDifferenceTolerance);

            BoundaryLowerSupportHit selected = hits
                .Where(hit =>
                    hit.SupportTopZ >= highestTopZ - topTolerance)
                .OrderBy(hit => hit.Distance)
                .ThenBy(hit => hit.DirectionPriority)
                .ThenByDescending(hit => hit.SupportTopZ)
                .FirstOrDefault();

            if (selected == null)
                return false;

            lowerSupport = selected.Support;
            supportTopZ = selected.SupportTopZ;
            horizontalCorrectionDistance = selected.Distance;
            resolvedPoint = moveToFoundPoint
                ? selected.Point
                : originalPoint;

            return true;
        }

        public static bool HasWallTouchingOrOverlappingBeam(
            Element beam,
            Curve beamCurve,
            double tolerance,
            double horizontalExtra,
            IList<Element> walls)
        {
            if (beam == null ||
                beamCurve == null ||
                walls == null)
            {
                return false;
            }

            XYZ start = beamCurve.GetEndPoint(0);
            XYZ direction = GetCurveHorizontalDirection(beamCurve);
            XYZ perpendicular =
                new XYZ(-direction.Y, direction.X, 0.0);

            JackSupportElementExtents beamExtents =
                GetElementExtents(
                    beam,
                    direction,
                    perpendicular,
                    start);

            if (beamExtents == null ||
                !beamExtents.IsValid)
            {
                return false;
            }

            foreach (Element wall in walls)
            {
                JackSupportElementExtents wallExtents =
                    GetElementExtents(
                        wall,
                        direction,
                        perpendicular,
                        start);

                if (wallExtents == null ||
                    !wallExtents.IsValid)
                {
                    continue;
                }

                bool overlapsAlongBeam =
                    IntervalsOverlap(
                        beamExtents.MinX,
                        beamExtents.MaxX,
                        wallExtents.MinX,
                        wallExtents.MaxX,
                        tolerance);

                bool overlapsAcrossBeam =
                    IntervalsOverlap(
                        beamExtents.MinY - horizontalExtra,
                        beamExtents.MaxY + horizontalExtra,
                        wallExtents.MinY,
                        wallExtents.MaxY,
                        tolerance);

                bool touchesBeamFromBelow =
                    TouchesBeamFromBelow(
                        beamExtents,
                        wallExtents,
                        tolerance);

                if (overlapsAlongBeam &&
                    overlapsAcrossBeam &&
                    touchesBeamFromBelow)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasWallInSupportZone(
            Element beam,
            Curve beamCurve,
            XYZ planPoint,
            double lowerZ,
            double upperZ,
            double verticalTolerance,
            double horizontalExtra,
            double probeLength,
            IList<Element> walls)
        {
            if (beam == null ||
                beamCurve == null ||
                planPoint == null ||
                walls == null)
            {
                return false;
            }

            XYZ direction =
                GetCurveHorizontalDirection(beamCurve);

            XYZ perpendicular =
                new XYZ(-direction.Y, direction.X, 0.0);

            JackSupportElementExtents beamExtents =
                GetElementExtents(
                    beam,
                    direction,
                    perpendicular,
                    planPoint);

            if (beamExtents == null ||
                !beamExtents.IsValid)
            {
                return HasWallBelowPoint(
                    planPoint,
                    lowerZ,
                    upperZ,
                    verticalTolerance,
                    walls);
            }

            double halfProbeLength =
                Math.Max(
                    probeLength * 0.5,
                    verticalTolerance);

            double zoneMinX = -halfProbeLength;
            double zoneMaxX = halfProbeLength;
            double zoneMinY =
                beamExtents.MinY - horizontalExtra;
            double zoneMaxY =
                beamExtents.MaxY + horizontalExtra;

            foreach (Element wall in walls)
            {
                JackSupportElementExtents wallExtents =
                    GetElementExtents(
                        wall,
                        direction,
                        perpendicular,
                        planPoint);

                if (wallExtents == null ||
                    !wallExtents.IsValid)
                {
                    continue;
                }

                bool overlapsAlongBeam =
                    IntervalsOverlap(
                        zoneMinX,
                        zoneMaxX,
                        wallExtents.MinX,
                        wallExtents.MaxX,
                        verticalTolerance);

                bool overlapsAcrossBeam =
                    IntervalsOverlap(
                        zoneMinY,
                        zoneMaxY,
                        wallExtents.MinY,
                        wallExtents.MaxY,
                        verticalTolerance);

                bool overlapsVertically =
                    IntervalsOverlap(
                        lowerZ,
                        upperZ,
                        wallExtents.MinZ,
                        wallExtents.MaxZ,
                        verticalTolerance);

                if (overlapsAlongBeam &&
                    overlapsAcrossBeam &&
                    overlapsVertically)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasWallBelowPoint(
            XYZ xyPoint,
            double lowerZ,
            double upperZ,
            double tolerance,
            IList<Element> walls)
        {
            if (xyPoint == null || walls == null)
                return false;

            foreach (Element wall in walls)
            {
                BoundingBoxXYZ box =
                    wall == null
                        ? null
                        : wall.get_BoundingBox(null);

                if (box == null)
                    continue;

                if (xyPoint.X < box.Min.X - tolerance ||
                    xyPoint.X > box.Max.X + tolerance ||
                    xyPoint.Y < box.Min.Y - tolerance ||
                    xyPoint.Y > box.Max.Y + tolerance)
                {
                    continue;
                }

                bool overlapsVertically =
                    box.Max.Z >= lowerZ - tolerance &&
                    box.Min.Z <= upperZ + tolerance;

                if (!overlapsVertically)
                    continue;

                IList<Solid> wallSolids = GetSolids(wall);

                double minIntersectionZ;
                double maxIntersectionZ;

                if (TryIntersectVerticalLine(
                    wallSolids,
                    xyPoint,
                    lowerZ - tolerance,
                    upperZ + tolerance,
                    out minIntersectionZ,
                    out maxIntersectionZ))
                {
                    return true;
                }

                if (wallSolids.Count == 0)
                    return true;
            }

            return false;
        }

        public static IList<JackSupportSpan> GetClearSpans(
            Element beam,
            Curve beamCurve,
            IList<Element> existingColumns,
            IList<Element> walls,
            bool useExistingColumns,
            bool useWalls,
            double columnTouchTolerance,
            double wallTouchTolerance,
            double wallHorizontalExtra,
            out int detectedColumnSupportCount,
            out int detectedWallSupportCount)
        {
            detectedColumnSupportCount = 0;
            detectedWallSupportCount = 0;

            List<JackSupportSpan> clearSpans =
                new List<JackSupportSpan>();

            if (beam == null || beamCurve == null)
                return clearSpans;

            Line line = beamCurve as Line;

            if (line == null)
            {
                clearSpans.Add(new JackSupportSpan
                {
                    StartDistance = 0.0,
                    EndDistance = beamCurve.Length
                });

                return clearSpans;
            }

            XYZ start = line.GetEndPoint(0);
            XYZ end = line.GetEndPoint(1);
            XYZ direction = NormalizeHorizontal(end - start);
            XYZ perpendicular =
                new XYZ(-direction.Y, direction.X, 0.0);

            double beamLength = beamCurve.Length;

            JackSupportElementExtents beamExtents =
                GetElementExtents(
                    beam,
                    direction,
                    perpendicular,
                    start);

            if (beamExtents == null ||
                !beamExtents.IsValid)
            {
                clearSpans.Add(new JackSupportSpan
                {
                    StartDistance = 0.0,
                    EndDistance = beamLength
                });

                return clearSpans;
            }

            List<JackSupportSpan> supportIntervals =
                new List<JackSupportSpan>();

            if (useExistingColumns)
            {
                foreach (Element column in
                    existingColumns ?? new List<Element>())
                {
                    JackSupportElementExtents columnExtents =
                        GetElementExtents(
                            column,
                            direction,
                            perpendicular,
                            start);

                    if (columnExtents == null ||
                        !columnExtents.IsValid)
                    {
                        continue;
                    }

                    bool overlapsAlongBeam =
                        columnExtents.MaxX >=
                            -columnTouchTolerance &&
                        columnExtents.MinX <=
                            beamLength + columnTouchTolerance;

                    bool overlapsAcrossBeam =
                        IntervalsOverlap(
                            beamExtents.MinY,
                            beamExtents.MaxY,
                            columnExtents.MinY,
                            columnExtents.MaxY,
                            columnTouchTolerance);

                    bool touchesFromBelow =
                        TouchesBeamFromBelow(
                            beamExtents,
                            columnExtents,
                            columnTouchTolerance);

                    if (!overlapsAlongBeam ||
                        !overlapsAcrossBeam ||
                        !touchesFromBelow)
                    {
                        continue;
                    }

                    double intervalStart = Math.Max(
                        0.0,
                        columnExtents.MinX -
                            columnTouchTolerance);

                    double intervalEnd = Math.Min(
                        beamLength,
                        columnExtents.MaxX +
                            columnTouchTolerance);

                    if (intervalEnd > intervalStart + Tiny)
                    {
                        supportIntervals.Add(
                            new JackSupportSpan
                            {
                                StartDistance = intervalStart,
                                EndDistance = intervalEnd
                            });

                        detectedColumnSupportCount++;
                    }
                }
            }

            if (useWalls)
            {
                foreach (Element wall in
                    walls ?? new List<Element>())
                {
                    JackSupportElementExtents wallExtents =
                        GetElementExtents(
                            wall,
                            direction,
                            perpendicular,
                            start);

                    if (wallExtents == null ||
                        !wallExtents.IsValid)
                    {
                        continue;
                    }

                    bool overlapsAlongBeam =
                        wallExtents.MaxX >=
                            -wallTouchTolerance &&
                        wallExtents.MinX <=
                            beamLength + wallTouchTolerance;

                    bool overlapsAcrossBeam =
                        IntervalsOverlap(
                            beamExtents.MinY -
                                wallHorizontalExtra,
                            beamExtents.MaxY +
                                wallHorizontalExtra,
                            wallExtents.MinY,
                            wallExtents.MaxY,
                            wallTouchTolerance);

                    bool touchesFromBelow =
                        TouchesBeamFromBelow(
                            beamExtents,
                            wallExtents,
                            wallTouchTolerance);

                    if (!overlapsAlongBeam ||
                        !overlapsAcrossBeam ||
                        !touchesFromBelow)
                    {
                        continue;
                    }

                    double intervalStart = Math.Max(
                        0.0,
                        wallExtents.MinX -
                            wallTouchTolerance);

                    double intervalEnd = Math.Min(
                        beamLength,
                        wallExtents.MaxX +
                            wallTouchTolerance);

                    if (intervalEnd > intervalStart + Tiny)
                    {
                        supportIntervals.Add(
                            new JackSupportSpan
                            {
                                StartDistance = intervalStart,
                                EndDistance = intervalEnd
                            });

                        detectedWallSupportCount++;
                    }
                }
            }

            double mergeTolerance = Math.Max(
                useExistingColumns
                    ? columnTouchTolerance
                    : 0.0,
                useWalls
                    ? wallTouchTolerance
                    : 0.0);

            IList<JackSupportSpan> mergedSupports =
                MergeIntervals(
                    supportIntervals,
                    mergeTolerance);

            double cursor = 0.0;

            foreach (JackSupportSpan support in mergedSupports)
            {
                if (support.StartDistance > cursor + Tiny)
                {
                    clearSpans.Add(
                        new JackSupportSpan
                        {
                            StartDistance = cursor,
                            EndDistance = support.StartDistance
                        });
                }

                cursor = Math.Max(
                    cursor,
                    support.EndDistance);
            }

            if (cursor < beamLength - Tiny)
            {
                clearSpans.Add(
                    new JackSupportSpan
                    {
                        StartDistance = cursor,
                        EndDistance = beamLength
                    });
            }

            if (mergedSupports.Count == 0)
            {
                clearSpans.Clear();

                clearSpans.Add(
                    new JackSupportSpan
                    {
                        StartDistance = 0.0,
                        EndDistance = beamLength
                    });
            }

            return clearSpans;
        }

        public static int CalculateSupportCount(double spanLength, double interval)
        {
            if (interval <= Tiny || spanLength <= interval + Tiny)
                return 0;

            int count = (int)Math.Ceiling(spanLength / interval) - 1;
            return Math.Max(0, count);
        }

        public static IList<double> GetEvenlyDistributedDistances(
            JackSupportSpan span,
            int count)
        {
            List<double> distances = new List<double>();
            if (span == null || count <= 0 || span.Length <= Tiny)
                return distances;

            for (int i = 0; i < count; i++)
            {
                double ratio = (i + 1.0) / (count + 1.0);
                distances.Add(span.StartDistance + span.Length * ratio);
            }

            return distances;
        }

        public static XYZ GetElementCenter(Element element)
        {
            LocationPoint locationPoint = element == null ? null : element.Location as LocationPoint;
            if (locationPoint != null)
                return locationPoint.Point;

            BoundingBoxXYZ box = element == null ? null : element.get_BoundingBox(null);
            if (box != null)
                return (box.Min + box.Max) * 0.5;

            return XYZ.Zero;
        }

        private static XYZ GetColumnAxisX(FamilyInstance column)
        {
            XYZ axis = column.HandOrientation;
            axis = NormalizeHorizontal(axis);

            if (axis.GetLength() > Tiny)
                return axis;

            LocationPoint locationPoint = column.Location as LocationPoint;
            double rotation = locationPoint == null ? 0.0 : locationPoint.Rotation;
            return new XYZ(Math.Cos(rotation), Math.Sin(rotation), 0.0);
        }

        private static XYZ GetColumnAxisY(FamilyInstance column, XYZ axisX)
        {
            XYZ axis = column.FacingOrientation;
            axis = NormalizeHorizontal(axis);

            if (axis.GetLength() > Tiny && Math.Abs(axis.DotProduct(axisX)) < 0.99)
                return axis;

            return new XYZ(-axisX.Y, axisX.X, 0.0);
        }

        private static XYZ NormalizeHorizontal(XYZ vector)
        {
            if (vector == null)
                return XYZ.BasisX;

            XYZ horizontal = new XYZ(vector.X, vector.Y, 0.0);
            if (horizontal.GetLength() < Tiny)
                return XYZ.BasisX;

            return horizontal.Normalize();
        }

        private static XYZ GetCurveHorizontalDirection(
            Curve curve)
        {
            if (curve == null)
                return XYZ.BasisX;

            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);

            return NormalizeHorizontal(end - start);
        }

        private static bool TouchesBeamFromBelow(
            JackSupportElementExtents beamExtents,
            JackSupportElementExtents supportExtents,
            double tolerance)
        {
            if (beamExtents == null ||
                supportExtents == null ||
                !beamExtents.IsValid ||
                !supportExtents.IsValid)
            {
                return false;
            }

            double beamBottomZ = beamExtents.MinZ;

            // 지지체의 상단이 보 하단까지 도달하고,
            // 지지체의 하단이 보 하단보다 위에서 시작하지 않아야 한다.
            // 따라서 보 상부에만 닿는 벽체/기둥은 점유 구간으로 보지 않는다.
            bool reachesBeamBottom =
                supportExtents.MaxZ >= beamBottomZ - tolerance;

            bool startsAtOrBelowBeamBottom =
                supportExtents.MinZ <= beamBottomZ + tolerance;

            return reachesBeamBottom &&
                   startsAtOrBelowBeamBottom;
        }

        private static bool IntervalsOverlap(
            double firstMin,
            double firstMax,
            double secondMin,
            double secondMax,
            double tolerance)
        {
            return firstMax >= secondMin - tolerance &&
                   firstMin <= secondMax + tolerance;
        }

        private static IList<JackSupportSpan> MergeIntervals(
            IList<JackSupportSpan> intervals,
            double tolerance)
        {
            List<JackSupportSpan> ordered = (intervals ?? new List<JackSupportSpan>())
                .OrderBy(x => x.StartDistance)
                .ToList();

            List<JackSupportSpan> merged = new List<JackSupportSpan>();
            foreach (JackSupportSpan current in ordered)
            {
                if (merged.Count == 0)
                {
                    merged.Add(new JackSupportSpan
                    {
                        StartDistance = current.StartDistance,
                        EndDistance = current.EndDistance
                    });
                    continue;
                }

                JackSupportSpan last = merged[merged.Count - 1];
                if (current.StartDistance <= last.EndDistance + tolerance)
                {
                    last.EndDistance = Math.Max(last.EndDistance, current.EndDistance);
                }
                else
                {
                    merged.Add(new JackSupportSpan
                    {
                        StartDistance = current.StartDistance,
                        EndDistance = current.EndDistance
                    });
                }
            }

            return merged;
        }

        private static bool TryIntersectVerticalLine(
            IList<Solid> solids,
            XYZ xyPoint,
            double lowerZ,
            double upperZ,
            out double minZ,
            out double maxZ)
        {
            minZ = double.MaxValue;
            maxZ = double.MinValue;

            if (solids == null || xyPoint == null || upperZ <= lowerZ)
                return false;

            Line verticalLine = Line.CreateBound(
                new XYZ(xyPoint.X, xyPoint.Y, lowerZ),
                new XYZ(xyPoint.X, xyPoint.Y, upperZ));

            SolidCurveIntersectionOptions options = new SolidCurveIntersectionOptions();

            foreach (Solid solid in solids)
            {
                if (solid == null || solid.Volume <= Tiny)
                    continue;

                try
                {
                    SolidCurveIntersection intersection =
                        solid.IntersectWithCurve(verticalLine, options);

                    if (intersection == null)
                        continue;

                    for (int i = 0; i < intersection.SegmentCount; i++)
                    {
                        Curve segment = intersection.GetCurveSegment(i);
                        if (segment == null)
                            continue;

                        XYZ p0 = segment.GetEndPoint(0);
                        XYZ p1 = segment.GetEndPoint(1);

                        minZ = Math.Min(minZ, Math.Min(p0.Z, p1.Z));
                        maxZ = Math.Max(maxZ, Math.Max(p0.Z, p1.Z));
                    }
                }
                catch
                {
                    // 일부 비정상 Solid는 건너뜀
                }
            }

            return minZ != double.MaxValue && maxZ != double.MinValue;
        }

        private static void AddUniqueDirection(
            IList<XYZ> directions,
            XYZ direction)
        {
            if (directions == null)
                return;

            XYZ normalized = NormalizeHorizontal(direction);

            bool alreadyExists = directions.Any(existing =>
                existing.DotProduct(normalized) > 0.999999);

            if (!alreadyExists)
                directions.Add(normalized);
        }

        private class BoundaryLowerSupportHit
        {
            public XYZ Point { get; set; }
            public Element Support { get; set; }
            public double SupportTopZ { get; set; }
            public double Distance { get; set; }
            public int DirectionPriority { get; set; }
        }

        private static IEnumerable<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ box)
        {
            if (box == null)
                yield break;

            Transform transform = box.Transform ?? Transform.Identity;

            double[] xs = { box.Min.X, box.Max.X };
            double[] ys = { box.Min.Y, box.Max.Y };
            double[] zs = { box.Min.Z, box.Max.Z };

            foreach (double x in xs)
            {
                foreach (double y in ys)
                {
                    foreach (double z in zs)
                    {
                        yield return transform.OfPoint(new XYZ(x, y, z));
                    }
                }
            }
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 단부 기둥 높이 보간·특수 보 측면 접촉 판정 형상 도우미
// 파일명: JackSupportGeometryHelper.cs
// =========================================================
