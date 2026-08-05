using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

namespace REVIT_TAP
{
    public class GroupVisibilityOption
    {
        public ElementId GroupTypeId { get; set; }

        public string GroupTypeName { get; set; }

        public int InstanceCount { get; set; }

        public string LevelSummary { get; set; }

        public override string ToString()
        {
            string levelText =
                string.IsNullOrWhiteSpace(
                    LevelSummary
                )
                    ? string.Empty
                    : " · " + LevelSummary;

            return string.Format(
                "{0}  ({1:N0}개){2}",
                GroupTypeName,
                InstanceCount,
                levelText
            );
        }
    }

    public class GroupVisibilityApplyResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public int GroupInstanceCount { get; set; }

        public int VisibleElementCount { get; set; }
    }

    public static class GroupVisibilityService
    {
        public static bool IsSupportedView(
            View view,
            out string errorMessage)
        {
            errorMessage =
                string.Empty;

            if (view == null)
            {
                errorMessage =
                    "현재 활성 뷰를 찾지 못했습니다.";

                return false;
            }

            if (view.IsTemplate)
            {
                errorMessage =
                    "뷰 템플릿에서는 그룹별 보기를 " +
                    "실행할 수 없습니다.";

                return false;
            }

            if (view.ViewType ==
                    ViewType.Schedule ||
                view.ViewType ==
                    ViewType.DrawingSheet)
            {
                errorMessage =
                    "현재 뷰 형식에서는 그룹별 보기를 " +
                    "실행할 수 없습니다.\n" +
                    "평면도, 단면도, 입면도 또는 3D 뷰에서 " +
                    "실행해 주십시오.";

                return false;
            }

            return true;
        }

        public static IList<GroupVisibilityOption>
            GetGroupOptions(
                Document document)
        {
            if (document == null)
            {
                return new List
                    <GroupVisibilityOption>();
            }

            IList<Group> groups =
                GetAllModelGroups(
                    document
                );

            Dictionary<int, List<Group>>
                groupsByTypeId =
                    new Dictionary
                        <int, List<Group>>();

            foreach (Group group in groups)
            {
                GroupType groupType =
                    group.GroupType;

                if (groupType == null)
                {
                    continue;
                }

                int groupTypeId =
                    groupType.Id.IntegerValue;

                List<Group> instances;

                if (!groupsByTypeId.TryGetValue(
                    groupTypeId,
                    out instances))
                {
                    instances =
                        new List<Group>();

                    groupsByTypeId.Add(
                        groupTypeId,
                        instances
                    );
                }

                instances.Add(
                    group
                );
            }

            List<GroupVisibilityOption> result =
                new List
                    <GroupVisibilityOption>();

            foreach (
                KeyValuePair<int, List<Group>>
                    pair in groupsByTypeId)
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }

                Group firstGroup =
                    pair.Value[0];

                GroupType groupType =
                    firstGroup.GroupType;

                if (groupType == null)
                {
                    continue;
                }

                result.Add(
                    new GroupVisibilityOption
                    {
                        GroupTypeId =
                            groupType.Id,

                        GroupTypeName =
                            groupType.Name,

                        InstanceCount =
                            pair.Value.Count,

                        LevelSummary =
                            BuildLevelSummary(
                                document,
                                pair.Value
                            )
                    }
                );
            }

            return result
                .OrderBy(
                    option =>
                        option.GroupTypeName
                )
                .ToList();
        }

        public static GroupVisibilityApplyResult
            Apply(
                Document document,
                View view,
                ISet<int> selectedGroupTypeIds)
        {
            GroupVisibilityApplyResult result =
                new GroupVisibilityApplyResult
                {
                    Succeeded =
                        false,

                    Message =
                        string.Empty,

                    GroupInstanceCount =
                        0,

                    VisibleElementCount =
                        0
                };

            if (document == null ||
                view == null)
            {
                result.Message =
                    "현재 문서 또는 활성 뷰를 " +
                    "찾지 못했습니다.";

                return result;
            }

            if (selectedGroupTypeIds == null ||
                selectedGroupTypeIds.Count == 0)
            {
                result.Message =
                    "선택된 모델 그룹이 없습니다.";

                return result;
            }

            IList<Group> selectedGroups =
                GetAllModelGroups(
                    document
                )
                    .Where(
                        group =>
                            group.GroupType != null &&
                            selectedGroupTypeIds
                                .Contains(
                                    group.GroupType
                                        .Id
                                        .IntegerValue
                                )
                    )
                    .ToList();

            if (selectedGroups.Count == 0)
            {
                result.Message =
                    "선택한 모델 그룹 인스턴스를 " +
                    "현재 문서에서 찾지 못했습니다.";

                return result;
            }

            HashSet<ElementId> isolateIds =
                new HashSet<ElementId>(
                    new ElementIdComparer()
                );

            HashSet<int> visitedGroupIds =
                new HashSet<int>();

            foreach (
                Group group in selectedGroups)
            {
                AddGroupAndMembers(
                    document,
                    view,
                    group,
                    isolateIds,
                    visitedGroupIds
                );
            }

            if (isolateIds.Count == 0)
            {
                result.Message =
                    "선택한 그룹에서 현재 뷰에 표시할 수 있는 " +
                    "요소를 찾지 못했습니다.";

                return result;
            }

            using (Transaction transaction =
                new Transaction(
                    document,
                    "선택 모델 그룹만 보기"))
            {
                transaction.Start();

                if (view
                    .IsTemporaryHideIsolateActive())
                {
                    view.DisableTemporaryViewMode(
                        TemporaryViewMode
                            .TemporaryHideIsolate
                    );

                    document.Regenerate();
                }

                view.IsolateElementsTemporary(
                    isolateIds.ToList()
                );

                transaction.Commit();
            }

            result.Succeeded =
                true;

            result.GroupInstanceCount =
                selectedGroups.Count;

            result.VisibleElementCount =
                isolateIds.Count;

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
                    "그룹별 보기 전체 원복"))
            {
                transaction.Start();

                if (view
                    .IsTemporaryHideIsolateActive())
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

        private static IList<Group>
            GetAllModelGroups(
                Document document)
        {
            if (document == null)
            {
                return new List<Group>();
            }

            return new FilteredElementCollector(
                    document
                )
                .OfClass(
                    typeof(Group)
                )
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .Where(
                    group =>
                        group != null &&
                        !group.ViewSpecific &&
                        group.GroupType != null
                )
                .ToList();
        }

        private static void AddGroupAndMembers(
            Document document,
            View view,
            Group group,
            ISet<ElementId> resultIds,
            ISet<int> visitedGroupIds)
        {
            if (document == null ||
                view == null ||
                group == null ||
                resultIds == null ||
                visitedGroupIds == null)
            {
                return;
            }

            int groupIdValue =
                group.Id.IntegerValue;

            if (!visitedGroupIds.Add(
                groupIdValue
            ))
            {
                return;
            }

            TryAddElementId(
                view,
                group,
                resultIds
            );

            ICollection<ElementId> memberIds =
                group.GetMemberIds();

            if (memberIds == null)
            {
                return;
            }

            foreach (
                ElementId memberId in memberIds)
            {
                Element member =
                    document.GetElement(
                        memberId
                    );

                if (member == null)
                {
                    continue;
                }

                Group nestedGroup =
                    member as Group;

                if (nestedGroup != null &&
                    !nestedGroup.ViewSpecific)
                {
                    AddGroupAndMembers(
                        document,
                        view,
                        nestedGroup,
                        resultIds,
                        visitedGroupIds
                    );

                    continue;
                }

                TryAddElementId(
                    view,
                    member,
                    resultIds
                );
            }
        }

        private static void TryAddElementId(
            View view,
            Element element,
            ISet<ElementId> resultIds)
        {
            if (view == null ||
                element == null ||
                resultIds == null)
            {
                return;
            }

            try
            {
                if (element.CanBeHidden(
                    view
                ))
                {
                    resultIds.Add(
                        element.Id
                    );
                }
            }
            catch
            {
                // 현재 뷰에서 숨김 제어가
                // 불가능한 요소는 제외합니다.
            }
        }

        private static string BuildLevelSummary(
            Document document,
            IList<Group> groups)
        {
            if (document == null ||
                groups == null ||
                groups.Count == 0)
            {
                return string.Empty;
            }

            SortedSet<string> levelNames =
                new SortedSet<string>(
                    StringComparer
                        .CurrentCultureIgnoreCase
                );

            foreach (Group group in groups)
            {
                string levelName =
                    GetGroupLevelName(
                        document,
                        group
                    );

                if (!string.IsNullOrWhiteSpace(
                    levelName
                ))
                {
                    levelNames.Add(
                        levelName
                    );
                }
            }

            if (levelNames.Count == 0)
            {
                return "레벨 미확인";
            }

            if (levelNames.Count <= 3)
            {
                return string.Join(
                    ", ",
                    levelNames.ToArray()
                );
            }

            string[] firstNames =
                levelNames
                    .Take(3)
                    .ToArray();

            return string.Join(
                ", ",
                firstNames
            ) +
            " 외 " +
            (
                levelNames.Count -
                3
            ).ToString() +
            "개 층";
        }

        private static string GetGroupLevelName(
            Document document,
            Group group)
        {
            if (document == null ||
                group == null)
            {
                return string.Empty;
            }

            try
            {
                ElementId levelId =
                    group.LevelId;

                if (levelId != null &&
                    levelId !=
                        ElementId.InvalidElementId)
                {
                    Level level =
                        document.GetElement(
                            levelId
                        ) as Level;

                    if (level != null)
                    {
                        return level.Name;
                    }
                }
            }
            catch
            {
                // LevelId가 없는 그룹은
                // 위치 Z 기준으로 계속 검사합니다.
            }

            double z;

            if (!TryGetGroupElevation(
                group,
                out z
            ))
            {
                return string.Empty;
            }

            Level closestLevel =
                new FilteredElementCollector(
                    document
                )
                    .OfClass(
                        typeof(Level)
                    )
                    .Cast<Level>()
                    .OrderBy(
                        level =>
                            Math.Abs(
                                level.Elevation -
                                z
                            )
                    )
                    .FirstOrDefault();

            return closestLevel == null
                ? string.Empty
                : closestLevel.Name;
        }

        private static bool TryGetGroupElevation(
            Group group,
            out double elevation)
        {
            elevation =
                0.0;

            if (group == null)
            {
                return false;
            }

            LocationPoint locationPoint =
                group.Location as LocationPoint;

            if (locationPoint != null &&
                locationPoint.Point != null)
            {
                elevation =
                    locationPoint.Point.Z;

                return true;
            }

            BoundingBoxXYZ boundingBox =
                null;

            try
            {
                boundingBox =
                    group.get_BoundingBox(
                        null
                    );
            }
            catch
            {
                boundingBox =
                    null;
            }

            if (boundingBox != null)
            {
                elevation =
                    boundingBox.Min.Z;

                return true;
            }

            return false;
        }

        private sealed class ElementIdComparer :
            IEqualityComparer<ElementId>
        {
            public bool Equals(
                ElementId first,
                ElementId second)
            {
                if (ReferenceEquals(
                    first,
                    second
                ))
                {
                    return true;
                }

                if (first == null ||
                    second == null)
                {
                    return false;
                }

                return first.IntegerValue ==
                    second.IntegerValue;
            }

            public int GetHashCode(
                ElementId elementId)
            {
                return elementId == null
                    ? 0
                    : elementId.IntegerValue;
            }
        }
    }
}

// =========================================================
// 코드 제목: 현재 모델 그룹 전체 수집·임시 표시·원복 서비스
// 파일명: GroupVisibilityService.cs
// =========================================================
