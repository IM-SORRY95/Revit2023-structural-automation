// =========================================================
// 생성 날짜 및 시간: 2026-07-13 (KST)
// 공개용 정리 날짜: 2026-08-05 (KST)
// 파일명: JackSupportSettingsCommand.cs
// 설명:
// 1) 잭서포트 통합 설정 버튼 실행
// 2) 모델리스 옵션창을 한 개만 표시
// 3) 옵션창이 열린 상태에서도 Revit 모델 탐색과 객체 선택 허용
// 4) Revit API 작업은 ExternalEvent를 통해 안전하게 실행
// =========================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    [Transaction(TransactionMode.Manual)]
    public class JackSupportSettingsCommand :
        IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApplication =
                commandData == null
                    ? null
                    : commandData.Application;

            UIDocument uiDocument =
                uiApplication == null
                    ? null
                    : uiApplication.ActiveUIDocument;

            if (uiDocument == null ||
                uiDocument.Document == null)
            {
                TaskDialog.Show(
                    "잭서포트",
                    "열려 있는 Revit 모델이 없습니다."
                );

                return Result.Cancelled;
            }

            JackSupportModelessController.Show(
                uiApplication
            );

            return Result.Succeeded;
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 모델리스 옵션창 실행 명령
// 파일명: JackSupportSettingsCommand.cs
// =========================================================
