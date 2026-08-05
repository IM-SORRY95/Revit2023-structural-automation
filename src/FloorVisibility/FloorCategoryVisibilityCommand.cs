using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    [Transaction(TransactionMode.Manual)]
    public class FloorCategoryVisibilityCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApplication =
                    commandData.Application;

                UIDocument uiDocument =
                    uiApplication.ActiveUIDocument;

                if (uiDocument == null ||
                    uiDocument.Document == null)
                {
                    TaskDialog.Show(
                        "층별 부재 보기",
                        "열려 있는 Revit 문서가 없습니다."
                    );

                    return Result.Cancelled;
                }

                Document document =
                    uiDocument.Document;

                View activeView =
                    document.ActiveView;

                string viewError;

                if (!FloorCategoryVisibilityService
                    .IsSupportedView(
                        activeView,
                        out viewError))
                {
                    TaskDialog.Show(
                        "층별 부재 보기",
                        viewError
                    );

                    return Result.Cancelled;
                }

                IList<FloorVisibilityLevelOption>
                    levelOptions =
                        FloorCategoryVisibilityService
                            .GetLevelOptions(document);

                if (levelOptions.Count == 0)
                {
                    TaskDialog.Show(
                        "층별 부재 보기",
                        "현재 문서에서 레벨을 찾지 못했습니다."
                    );

                    return Result.Cancelled;
                }

                IList<FloorVisibilityCategoryOption>
                    categoryOptions =
                        FloorCategoryVisibilityService
                            .GetCategoryOptions(
                                document,
                                activeView
                            );

                if (categoryOptions.Count == 0)
                {
                    TaskDialog.Show(
                        "층별 부재 보기",
                        "현재 문서에서 층별로 구분할 수 있는 " +
                        "모델 카테고리를 찾지 못했습니다."
                    );

                    return Result.Cancelled;
                }

                FloorVisibilitySelectionSettings
                    savedSettings =
                        FloorVisibilitySelectionSettings
                            .Load();

                using (FloorCategoryVisibilityForm form =
                    new FloorCategoryVisibilityForm(
                        levelOptions,
                        categoryOptions,
                        savedSettings))
                {
                    form.ShowDialog();

                    if (form.SelectedAction ==
                        FloorVisibilityAction.Cancel)
                    {
                        return Result.Cancelled;
                    }

                    if (form.SelectedAction ==
                        FloorVisibilityAction.Restore)
                    {
                        FloorCategoryVisibilityService
                            .RestoreAll(
                                document,
                                activeView
                            );

                        return Result.Succeeded;
                    }

                    ISet<int> selectedLevelIds =
                        form.GetSelectedLevelIds();

                    ISet<int> selectedCategoryIds =
                        form.GetSelectedCategoryIds();

                    if (selectedLevelIds == null ||
                        selectedLevelIds.Count == 0)
                    {
                        TaskDialog.Show(
                            "층별 부재 보기",
                            "한 개 이상의 층을 선택해 주십시오."
                        );

                        return Result.Cancelled;
                    }

                    if (selectedCategoryIds == null ||
                        selectedCategoryIds.Count == 0)
                    {
                        TaskDialog.Show(
                            "층별 부재 보기",
                            "한 개 이상의 카테고리를 " +
                            "선택해 주십시오."
                        );

                        return Result.Cancelled;
                    }

                    FloorVisibilityApplyResult applyResult =
                        FloorCategoryVisibilityService.Apply(
                            document,
                            activeView,
                            selectedLevelIds,
                            selectedCategoryIds
                        );

                    if (!applyResult.Succeeded)
                    {
                        TaskDialog.Show(
                            "층별 부재 보기",
                            applyResult.Message
                        );

                        return Result.Cancelled;
                    }

                    savedSettings.SelectedLevelNames =
                        form.GetSelectedLevelNames()
                            .ToList();

                    savedSettings.SelectedCategoryNames =
                        form.GetSelectedCategoryNames()
                            .ToList();

                    savedSettings.Save();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;

                TaskDialog.Show(
                    "층별 부재 보기 오류",
                    "층별 부재 보기 실행 중 오류가 " +
                    "발생했습니다.\n\n" +
                    ex.ToString()
                );

                return Result.Failed;
            }
        }
    }
}

// ========================================================= 
// 코드 제목: 층별·카테고리 선택 기억 보기 명령
// 파일명: FloorCategoryVisibilityCommand.cs
// =========================================================
