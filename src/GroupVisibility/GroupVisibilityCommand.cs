using System;
using System.Collections.Generic;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    [Transaction(TransactionMode.Manual)]
    public class GroupVisibilityCommand : IExternalCommand
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
                        "그룹별 보기",
                        "열려 있는 Revit 문서가 없습니다."
                    );

                    return Result.Cancelled;
                }

                Document document =
                    uiDocument.Document;

                View activeView =
                    document.ActiveView;

                string viewError;

                if (!GroupVisibilityService.IsSupportedView(
                    activeView,
                    out viewError))
                {
                    TaskDialog.Show(
                        "그룹별 보기",
                        viewError
                    );

                    return Result.Cancelled;
                }

                IList<GroupVisibilityOption>
                    groupOptions =
                        GroupVisibilityService
                            .GetGroupOptions(document);

                if (groupOptions.Count == 0)
                {
                    TaskDialog.Show(
                        "그룹별 보기",
                        "현재 문서에서 모델 그룹을 " +
                        "찾지 못했습니다."
                    );

                    return Result.Cancelled;
                }

                using (GroupVisibilityForm form =
                    new GroupVisibilityForm(
                        groupOptions
                    ))
                {
                    form.ShowDialog();

                    if (form.SelectedAction ==
                        GroupVisibilityAction.Cancel)
                    {
                        return Result.Cancelled;
                    }

                    if (form.SelectedAction ==
                        GroupVisibilityAction.Restore)
                    {
                        GroupVisibilityService.RestoreAll(
                            document,
                            activeView
                        );

                        return Result.Succeeded;
                    }

                    ISet<int> selectedGroupTypeIds =
                        form.GetSelectedGroupTypeIds();

                    if (selectedGroupTypeIds == null ||
                        selectedGroupTypeIds.Count == 0)
                    {
                        TaskDialog.Show(
                            "그룹별 보기",
                            "한 개 이상의 그룹을 " +
                            "선택해 주십시오."
                        );

                        return Result.Cancelled;
                    }

                    GroupVisibilityApplyResult result =
                        GroupVisibilityService.Apply(
                            document,
                            activeView,
                            selectedGroupTypeIds
                        );

                    if (!result.Succeeded)
                    {
                        TaskDialog.Show(
                            "그룹별 보기",
                            result.Message
                        );

                        return Result.Cancelled;
                    }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message =
                    ex.Message;

                TaskDialog.Show(
                    "그룹별 보기 오류",
                    "그룹별 보기 실행 중 오류가 " +
                    "발생했습니다.\n\n" +
                    ex.ToString()
                );

                return Result.Failed;
            }
        }
    }
}

// =========================================================
// 코드 제목: 모델 그룹 복수 선택 보기 명령
// 파일명: GroupVisibilityCommand.cs
// =========================================================
