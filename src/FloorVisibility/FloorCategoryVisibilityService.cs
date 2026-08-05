using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Autodesk.Revit.DB;

namespace REVIT_TAP
{
    public class FloorVisibilitySelectionSettings
    {
        private static readonly string DataFolderPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                ),
                "RevitStructuralAutomation",
                "Data"
            );

        private const string SettingsFileName =
            "FloorVisibilitySettings.txt";

        public List<string> SelectedLevelNames
        {
            get;
            set;
        }

        public List<string> SelectedCategoryNames
        {
            get;
            set;
        }

        public static string SettingsFilePath
        {
            get
            {
                return Path.Combine(
                    DataFolderPath,
                    SettingsFileName
                );
            }
        }

        public FloorVisibilitySelectionSettings()
        {
            SelectedLevelNames =
                new List<string>();

            SelectedCategoryNames =
                new List<string>();
        }

        public static FloorVisibilitySelectionSettings
            Load()
        {
            FloorVisibilitySelectionSettings settings =
                new FloorVisibilitySelectionSettings();

            string path =
                SettingsFilePath;

            if (!File.Exists(path))
            {
                return settings;
            }

            string section =
                string.Empty;

            foreach (string rawLine in
                File.ReadAllLines(path, Encoding.UTF8))
            {
                string line =
                    (rawLine ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("[") &&
                    line.EndsWith("]"))
                {
                    section = line;
                    continue;
                }

                if (string.Equals(
                    section,
                    "[Levels]",
                    StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(
                        settings.SelectedLevelNames,
                        line
                    );
                }
                else if (string.Equals(
                    section,
                    "[Categories]",
                    StringComparison.OrdinalIgnoreCase))
                {
                    AddUnique(
                        settings.SelectedCategoryNames,
                        line
                    );
                }
            }

            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(
                DataFolderPath
            );

            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine("[Levels]");

            foreach (string name in
                CleanDistinct(SelectedLevelNames))
            {
                builder.AppendLine(name);
            }

            builder.AppendLine();
            builder.AppendLine("[Categories]");

            foreach (string name in
                CleanDistinct(SelectedCategoryNames))
            {
                builder.AppendLine(name);
            }

            File.WriteAllText(
                SettingsFilePath,
                builder.ToString(),
                Encoding.UTF8
            );
        }

        private static IList<string> CleanDistinct(
            IEnumerable<string> values)
        {
            return (values ??
                    Enumerable.Empty<string>())
                .Select(CleanLine)
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string CleanLine(
            string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static void AddUnique(
            IList<string> target,
            string value)
        {
            string clean =
                CleanLine(value);

            if (string.IsNullOrWhiteSpace(clean))
            {
                return;
            }

            if (target.Any(existing =>
                string.Equals(
                    existing,
                    clean,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            target.Add(clean);
        }
    }

    public class FloorVisibilityLevelOption
    {
        public ElementId LevelId { get; set; }

        public string LevelName { get; set; }

        public double Elevation { get; set; }

        public override string ToString()
        {
            double elevationMillimeters =
                UnitUtils.ConvertFromInternalUnits(
                    Elevation,
                    UnitTypeId.Millimeters
                );

            return string.Format(
                "{0}  ·  기준높이 {1:0.##}mm",
                LevelName,
                elevationMillimeters
            );
        }
    }

    public class FloorVisibilityCategoryOption
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; }

        public int ElementCount { get; set; }

        public override string ToString()
        {
            return string.Format(
                "{0}  ({1:N0}개)",
                CategoryName,
                ElementCount
            );
        }
    }

    public class FloorVisibilityApplyResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public int VisibleElementCount { get; set; }
    }

    public static class FloorCategoryVisibilityService
    {
        private static readonly BuiltInParameter[]
            LevelParameterCandidates =
        {
            BuiltInParameter.FAMILY_LEVEL_PARAM,
            BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
            BuiltInParameter.SCHEDULE_LEVEL_PARAM,
            BuiltInParameter.LEVEL_PARAM,
            BuiltInParameter.WALL_BASE_CONSTRAINT
        };

        public static bool IsSupportedView(
            View view,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (view == null)
            {
                errorMessage =
                    "현재 활성 뷰를 찾지 못했습니다.";

                return false;
            }

            if (view.IsTemplate)
            {
                errorMessage =
                    "뷰 템플릿에서는 층별 부재 보기를 " +
                    "실행할 수 없습니다.";

                return false;
            }

            if (view.ViewType == ViewType.Schedule ||
                view.ViewType == ViewType.DrawingSheet)
            {
                errorMessage =
                    "현재 뷰 형식에서는 층별 부재 보기를 " +
                    "실행할 수 없습니다.\n" +
                    "평면도, 단면도, 입면도 또는 3D 뷰에서 " +
                    "실행해 주십시오.";

                return false;
            }

            return true;
        }

        public static IList<FloorVisibilityLevelOption>
            GetLevelOptions(
                Document document)
        {
            if (document == null)
            {
                return new List
                    <FloorVisibilityLevelOption>();
            }

            return new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderByDescending(
                    level => level.Elevation)
                .Select(
                    level =>
                        new FloorVisibilityLevelOption
                        {
                            LevelId = level.Id,
                            LevelName = level.Name,
                            Elevation = level.Elevation
                        })
                .ToList();
        }

        public static IList<FloorVisibilityCategoryOption>
            GetCategoryOptions(
                Document document,
                View view)
        {
            Dictionary<int,
                FloorVisibilityCategoryOption>
                    optionByCategoryId =
                        new Dictionary<int,
                            FloorVisibilityCategoryOption>();

            if (document == null ||
                view == null)
            {
                return optionByCategoryId.Values
                    .ToList();
            }

            IEnumerable<Element> elements =
                new FilteredElementCollector(document)
                    .WhereElementIsNotElementType()
                    .ToElements();

            foreach (Element element in elements)
            {
                if (!IsUsableModelElement(
                    element,
                    view))
                {
                    continue;
                }

                Category category =
                    element.Category;

                int categoryId =
                    category.Id.IntegerValue;

                FloorVisibilityCategoryOption option;

                if (!optionByCategoryId.TryGetValue(
                    categoryId,
                    out option))
                {
                    option =
                        new FloorVisibilityCategoryOption
                        {
                            CategoryId =
                                categoryId,

                            CategoryName =
                                category.Name,

                            ElementCount =
                                0
                        };

                    optionByCategoryId.Add(
                        categoryId,
                        option
                    );
                }

                option.ElementCount++;
            }

            return optionByCategoryId.Values
                .OrderBy(
                    option =>
                        option.CategoryName)
                .ToList();
        }

        public static FloorVisibilityApplyResult Apply(
            Document document,
            View view,
            ISet<int> selectedLevelIds,
            ISet<int> selectedCategoryIds)
        {
            FloorVisibilityApplyResult result =
                new FloorVisibilityApplyResult
                {
                    Succeeded = false,
                    Message = string.Empty,
                    VisibleElementCount = 0
                };

            if (document == null ||
                view == null)
            {
                result.Message =
                    "현재 문서 또는 활성 뷰를 " +
                    "찾지 못했습니다.";

                return result;
            }

            if (selectedLevelIds == null ||
                selectedLevelIds.Count == 0)
            {
                result.Message =
                    "선택된 층이 없습니다.";

                return result;
            }

            if (selectedCategoryIds == null ||
                selectedCategoryIds.Count == 0)
            {
                result.Message =
                    "선택된 카테고리가 없습니다.";

                return result;
            }

            HashSet<int> validLevelIds =
                new HashSet<int>();

            List<string> selectedLevelNames =
                new List<string>();

            foreach (int levelIdValue in
                selectedLevelIds)
            {
                Level level =
                    document.GetElement(
                        new ElementId(
                            levelIdValue
                        )
                    ) as Level;

                if (level == null)
                {
                    continue;
                }

                validLevelIds.Add(
                    level.Id.IntegerValue
                );

                selectedLevelNames.Add(
                    level.Name
                );
            }

            if (validLevelIds.Count == 0)
            {
                result.Message =
                    "선택한 레벨을 현재 문서에서 " +
                    "찾지 못했습니다.";

                return result;
            }

            IList<Level> allLevels =
                new FilteredElementCollector(document)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(
                        level =>
                            level.Elevation)
                    .ToList();

            List<ElementId> visibleIds =
                new List<ElementId>();

            IEnumerable<Element> elements =
                new FilteredElementCollector(document)
                    .WhereElementIsNotElementType()
                    .ToElements();

            foreach (Element element in elements)
            {
                if (!IsUsableModelElement(
                    element,
                    view))
                {
                    continue;
                }

                Category category =
                    element.Category;

                int categoryId =
                    category.Id.IntegerValue;

                if (!selectedCategoryIds.Contains(
                    categoryId))
                {
                    continue;
                }

                if (!IsElementOnAnyLevel(
                    document,
                    view,
                    element,
                    validLevelIds,
                    allLevels))
                {
                    continue;
                }

                visibleIds.Add(
                    element.Id
                );
            }

            if (visibleIds.Count == 0)
            {
                result.Message =
                    "선택한 층과 카테고리에 해당하는 " +
                    "부재를 현재 문서에서 찾지 못했습니다.\n\n" +
                    "선택 층: " +
                    string.Join(
                        ", ",
                        selectedLevelNames.ToArray()
                    );

                return result;
            }

            using (Transaction transaction =
                new Transaction(
                    document,
                    "복수 층·카테고리 부재만 보기"))
            {
                transaction.Start();

                if (view.IsTemporaryHideIsolateActive())
                {
                    view.DisableTemporaryViewMode(
                        TemporaryViewMode
                            .TemporaryHideIsolate
                    );

                    document.Regenerate();
                }

                view.IsolateElementsTemporary(
                    visibleIds
                );

                transaction.Commit();
            }

            result.Succeeded = true;

            result.VisibleElementCount =
                visibleIds.Count;

            return result;
        }

        public static void RestoreAll(
            Document document,
            View view)
        {
            if (document == null ||
                view == null)
            {
                return;
            }

            using (Transaction transaction =
                new Transaction(
                    document,
                    "층별 부재 보기 전체 원복"))
            {
                transaction.Start();

                if (view.IsTemporaryHideIsolateActive())
                {
                    view.DisableTemporaryViewMode(
                        TemporaryViewMode
                            .TemporaryHideIsolate
                    );

                    document.Regenerate();
                }

                transaction.Commit();
            }
        }

        private static bool IsUsableModelElement(
            Element element,
            View view)
        {
            if (element == null ||
                element.Category == null ||
                view == null)
            {
                return false;
            }

            if (element.Category.CategoryType !=
                CategoryType.Model)
            {
                return false;
            }

            try
            {
                if (element.ViewSpecific &&
                    element.OwnerViewId !=
                        view.Id)
                {
                    return false;
                }
            }
            catch
            {
                // ViewSpecific 정보를 제공하지 않는 요소는
                // 계속 검사합니다.
            }

            try
            {
                if (!element.CanBeHidden(view))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static bool IsElementOnAnyLevel(
            Document document,
            View view,
            Element element,
            ISet<int> selectedLevelIds,
            IList<Level> allLevels)
        {
            ElementId directLevelId =
                GetDirectLevelId(
                    document,
                    element
                );

            if (IsValidLevelId(
                document,
                directLevelId))
            {
                return selectedLevelIds.Contains(
                    directLevelId.IntegerValue
                );
            }

            double baseElevation;

            if (!TryGetElementBaseElevation(
                element,
                view,
                out baseElevation))
            {
                return false;
            }

            Level closestLevel =
                FindClosestLevelBelowOrNearest(
                    allLevels,
                    baseElevation
                );

            return closestLevel != null &&
                selectedLevelIds.Contains(
                    closestLevel.Id.IntegerValue
                );
        }

        private static ElementId GetDirectLevelId(
            Document document,
            Element element)
        {
            try
            {
                ElementId elementLevelId =
                    element.LevelId;

                if (IsValidLevelId(
                    document,
                    elementLevelId))
                {
                    return elementLevelId;
                }
            }
            catch
            {
                // LevelId가 없는 요소는
                // 매개변수로 계속 확인합니다.
            }

            foreach (BuiltInParameter builtInParameter in
                LevelParameterCandidates)
            {
                try
                {
                    Parameter parameter =
                        element.get_Parameter(
                            builtInParameter
                        );

                    if (parameter == null ||
                        parameter.StorageType !=
                            StorageType.ElementId)
                    {
                        continue;
                    }

                    ElementId levelId =
                        parameter.AsElementId();

                    if (IsValidLevelId(
                        document,
                        levelId))
                    {
                        return levelId;
                    }
                }
                catch
                {
                    // 지원하지 않는 매개변수이면
                    // 다음 후보를 확인합니다.
                }
            }

            return ElementId.InvalidElementId;
        }

        private static bool IsValidLevelId(
            Document document,
            ElementId levelId)
        {
            if (document == null ||
                levelId == null ||
                levelId.IntegerValue ==
                    ElementId.InvalidElementId
                        .IntegerValue)
            {
                return false;
            }

            return document.GetElement(
                levelId
            ) is Level;
        }

        private static bool TryGetElementBaseElevation(
            Element element,
            View view,
            out double elevation)
        {
            elevation = 0.0;

            if (element == null)
            {
                return false;
            }

            LocationPoint locationPoint =
                element.Location as LocationPoint;

            if (locationPoint != null &&
                locationPoint.Point != null)
            {
                elevation =
                    locationPoint.Point.Z;

                return true;
            }

            LocationCurve locationCurve =
                element.Location as LocationCurve;

            if (locationCurve != null &&
                locationCurve.Curve != null)
            {
                XYZ startPoint =
                    locationCurve.Curve.GetEndPoint(
                        0
                    );

                XYZ endPoint =
                    locationCurve.Curve.GetEndPoint(
                        1
                    );

                elevation =
                    Math.Min(
                        startPoint.Z,
                        endPoint.Z
                    );

                return true;
            }

            BoundingBoxXYZ boundingBox =
                null;

            try
            {
                boundingBox =
                    element.get_BoundingBox(
                        null
                    );
            }
            catch
            {
                boundingBox =
                    null;
            }

            if (boundingBox == null)
            {
                try
                {
                    boundingBox =
                        element.get_BoundingBox(
                            view
                        );
                }
                catch
                {
                    boundingBox =
                        null;
                }
            }

            if (boundingBox != null)
            {
                elevation =
                    boundingBox.Min.Z;

                return true;
            }

            return false;
        }

        private static Level
            FindClosestLevelBelowOrNearest(
                IList<Level> allLevels,
                double elevation)
        {
            if (allLevels == null ||
                allLevels.Count == 0)
            {
                return null;
            }

            const double tolerance =
                0.000001;

            Level belowOrEqual =
                allLevels
                    .Where(
                        level =>
                            level.Elevation <=
                            elevation +
                            tolerance
                    )
                    .OrderByDescending(
                        level =>
                            level.Elevation
                    )
                    .FirstOrDefault();

            if (belowOrEqual != null)
            {
                return belowOrEqual;
            }

            return allLevels
                .OrderBy(
                    level =>
                        Math.Abs(
                            level.Elevation -
                            elevation
                        )
                )
                .FirstOrDefault();
        }
    }
}

// ========================================================= 
// 코드 제목: 층별 선택 저장·전체 모델 카테고리 임시 표시 및 원복 서비스
// 파일명: FloorCategoryVisibilityService.cs
// =========================================================
