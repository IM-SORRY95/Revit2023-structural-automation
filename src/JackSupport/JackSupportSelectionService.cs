// =========================================================
// 생성 날짜 및 시간: 2026-07-16 (KST)
// 공개용 정리 날짜: 2026-08-05 (KST)
// 파일명: JackSupportSelectionService.cs
// 설명:
// 1) 생성 원인별 잭서포트 수집
// 2) 특수 보, 일반 보 하부, 특수 기둥, 일반 기둥, 기타 유형 구분
// 3) Revit 모델 선택, 현재 뷰 임시 분리 및 임시 분리 원복
// 4) 포트폴리오 공개를 위해 실제 부재 코드는 일반적인 식별 문자열로 대체
// =========================================================

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    public class JackSupportSelectionOptions
    {
        public bool IncludeBtsBeamSupports { get; set; }

        public bool IncludeRcBeamSupports { get; set; }

        public bool IncludePcColumnSupports { get; set; }

        public bool IncludeRcColumnSupports { get; set; }

        public bool IncludeOtherSupports { get; set; }

        public JackSupportSelectionOptions()
        {
            IncludeBtsBeamSupports =
                true;

            IncludeRcBeamSupports =
                true;

            IncludePcColumnSupports =
                true;

            IncludeRcColumnSupports =
                true;

            IncludeOtherSupports =
                false;
        }

        public bool HasAnySelection()
        {
            return
                IncludeBtsBeamSupports ||
                IncludeRcBeamSupports ||
                IncludePcColumnSupports ||
                IncludeRcColumnSupports ||
                IncludeOtherSupports;
        }
    }

    public class JackSupportSelectionResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public int SelectedCount { get; set; }

        public JackSupportSelectionResult()
        {
            Succeeded =
                false;

            Message =
                string.Empty;

            SelectedCount =
                0;
        }
    }

    public static class JackSupportSelectionService
    {
        private const string SpecialBeamMarker =
            "_특수보_";

        private const string RcBeamMarker =
            "_RC보하부_";

        private const string DropCapsColumnMarker =
            "_DropCaps기둥_";

        private const string SpecialColumnMarker =
            "_특수기둥_";

        private const string RcColumnMarker =
            "_RC기둥_";

        private const string GeneralColumnMarker =
            "_일반기둥_";

        public static JackSupportSelectionResult
            SelectInModel(
                UIDocument uiDocument,
                JackSupportSettings settings,
                JackSupportSelectionOptions options)
        {
            IList<ElementId> ids;

            JackSupportSelectionResult result =
                CreateResultForRequest(
                    uiDocument,
                    settings,
                    options,
                    out ids
                );

            if (!result.Succeeded)
            {
                return result;
            }

            uiDocument.Selection.SetElementIds(
                ids
            );

            try
            {
                uiDocument.ShowElements(
                    ids
                );
            }
            catch
            {
                // 선택은 유지하고 화면 맞춤 실패만 무시합니다.
            }

            return result;
        }

        public static JackSupportSelectionResult
            IsolateInActiveView(
                UIDocument uiDocument,
                JackSupportSettings settings,
                JackSupportSelectionOptions options)
        {
            IList<ElementId> ids;

            JackSupportSelectionResult result =
                CreateResultForRequest(
                    uiDocument,
                    settings,
                    options,
                    out ids
                );

            if (!result.Succeeded)
            {
                return result;
            }

            Document document =
                uiDocument.Document;

            View activeView =
                document.ActiveView;

            if (activeView == null ||
                activeView.IsTemplate)
            {
                result.Succeeded =
                    false;

                result.Message =
                    "현재 활성 뷰에서는 임시 분리를 " +
                    "적용할 수 없습니다.";

                return result;
            }

            using (Transaction transaction =
                new Transaction(
                    document,
                    "생성 원인별 잭서포트 임시 분리"))
            {
                transaction.Start();

                if (activeView
                    .IsTemporaryHideIsolateActive())
                {
                    activeView
                        .DisableTemporaryViewMode(
                            TemporaryViewMode
                                .TemporaryHideIsolate
                        );

                    document.Regenerate();
                }

                activeView.IsolateElementsTemporary(
                    ids
                );

                transaction.Commit();
            }

            uiDocument.Selection.SetElementIds(
                ids
            );

            return result;
        }

        public static void RestoreTemporaryIsolation(
            Document document,
            View activeView)
        {
            if (document == null ||
                activeView == null ||
                activeView.IsTemplate)
            {
                return;
            }

            using (Transaction transaction =
                new Transaction(
                    document,
                    "잭서포트 임시 분리 원복"))
            {
                transaction.Start();

                if (activeView
                    .IsTemporaryHideIsolateActive())
                {
                    activeView
                        .DisableTemporaryViewMode(
                            TemporaryViewMode
                                .TemporaryHideIsolate
                        );

                    document.Regenerate();
                }

                transaction.Commit();
            }
        }

        private static JackSupportSelectionResult
            CreateResultForRequest(
                UIDocument uiDocument,
                JackSupportSettings settings,
                JackSupportSelectionOptions options,
                out IList<ElementId> ids)
        {
            ids =
                new List<ElementId>();

            JackSupportSelectionResult result =
                new JackSupportSelectionResult();

            if (uiDocument == null ||
                uiDocument.Document == null)
            {
                result.Message =
                    "열려 있는 Revit 모델이 없습니다.";

                return result;
            }

            if (settings == null)
            {
                result.Message =
                    "잭서포트 설정을 불러오지 못했습니다.";

                return result;
            }

            if (options == null ||
                !options.HasAnySelection())
            {
                result.Message =
                    "선택할 잭서포트 종류를 " +
                    "하나 이상 체크해 주십시오.";

                return result;
            }

            Document document =
                uiDocument.Document;

            ids =
                new FilteredElementCollector(
                    document
                )
                    .OfCategory(
                        BuiltInCategory
                            .OST_StructuralColumns
                    )
                    .WhereElementIsNotElementType()
                    .ToElements()
                    .Where(
                        element =>
                            JackSupportFamilyService
                                .IsConfiguredJackSupportElement(
                                    document,
                                    element,
                                    settings
                                )
                    )
                    .Where(
                        element =>
                            IsIncludedByTypeName(
                                document,
                                element,
                                options
                            )
                    )
                    .Select(
                        element =>
                            element.Id
                    )
                    .Distinct(
                        new ElementIdComparer()
                    )
                    .ToList();

            if (ids.Count == 0)
            {
                result.Message =
                    "선택한 생성 원인에 해당하는 " +
                    "잭서포트를 찾지 못했습니다.\n\n" +
                    "생성 원인 정보가 없는 기존 잭서포트는 " +
                    "[기타]를 체크해 주십시오.";

                return result;
            }

            result.Succeeded =
                true;

            result.SelectedCount =
                ids.Count;

            result.Message =
                "선택된 잭서포트: " +
                ids.Count.ToString("N0") +
                "개";

            return result;
        }

        private static bool IsIncludedByTypeName(
            Document document,
            Element element,
            JackSupportSelectionOptions options)
        {
            if (document == null ||
                element == null ||
                options == null)
            {
                return false;
            }

            ElementType type =
                document.GetElement(
                    element.GetTypeId()
                ) as ElementType;

            string typeName =
                type == null
                    ? string.Empty
                    : type.Name ?? string.Empty;

            if (ContainsMarker(
                typeName,
                SpecialBeamMarker
            ))
            {
                return options
                    .IncludeBtsBeamSupports;
            }

            if (ContainsMarker(
                typeName,
                RcBeamMarker
            ))
            {
                return options
                    .IncludeRcBeamSupports;
            }

            if (ContainsMarker(
                    typeName,
                    DropCapsColumnMarker
                ) ||
                ContainsMarker(
                    typeName,
                    SpecialColumnMarker
                ))
            {
                return options
                    .IncludePcColumnSupports;
            }

            if (ContainsMarker(
                    typeName,
                    RcColumnMarker
                ) ||
                ContainsMarker(
                    typeName,
                    GeneralColumnMarker
                ))
            {
                return options
                    .IncludeRcColumnSupports;
            }

            return options
                .IncludeOtherSupports;
        }

        private static bool ContainsMarker(
            string typeName,
            string marker)
        {
            return
                !string.IsNullOrWhiteSpace(
                    typeName
                ) &&
                !string.IsNullOrWhiteSpace(
                    marker
                ) &&
                typeName.IndexOf(
                    marker,
                    StringComparison
                        .OrdinalIgnoreCase
                ) >= 0;
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
// 코드 제목: 생성 원인별 잭서포트 선택·임시 분리 서비스
// 파일명: JackSupportSelectionService.cs
// =========================================================
