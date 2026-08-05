// =========================================================
// 생성 날짜 및 시간: 2026-07-16 (KST)
// 파일명: JackSupportExternalEventHandler.cs
// 설명:
// 1) 모델리스 잭서포트 옵션창의 Revit API 요청을 ExternalEvent로 실행
// 2) 자동 생성, 색상 일괄 적용, 생성 원인별 선택/임시 분리/원복 처리
// 3) 옵션창이 열린 상태에서도 Revit 모델 탐색과 객체 선택을 허용
// 4) 동일 모델의 Document 래퍼가 달라도 경로/프로젝트 식별값으로 동일 문서 판정
// 5) 다른 모델에서 다시 실행하면 기존 창 정리 후 현재 모델용 창 생성
// =========================================================

using System;
using System.Diagnostics;
using System.Windows.Forms;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    public enum JackSupportExternalRequestType
    {
        None = 0,
        Generate = 1,
        ApplyJudgmentColors = 2,
        ApplyUniformColor = 3,
        SelectSupports = 4,
        IsolateSupports = 5,
        RestoreTemporaryIsolation = 6
    }

    public class JackSupportExternalRequest
    {
        public JackSupportExternalRequestType RequestType { get; set; }
        public JackSupportSelectionOptions SelectionOptions { get; set; }

        public JackSupportExternalRequest()
        {
            RequestType = JackSupportExternalRequestType.None;
            SelectionOptions = new JackSupportSelectionOptions();
        }
    }

    public class JackSupportExternalEventHandler : IExternalEventHandler
    {
        private readonly object _syncRoot;
        private readonly Document _sourceDocument;
        private readonly string _sourceDocumentIdentity;
        private JackSupportExternalRequest _pendingRequest;

        public JackSupportExternalEventHandler(Document sourceDocument)
        {
            _sourceDocument = sourceDocument;
            _sourceDocumentIdentity =
                GetDocumentIdentity(sourceDocument);

            _syncRoot = new object();
            _pendingRequest = new JackSupportExternalRequest();
        }

        public void SetRequest(JackSupportExternalRequest request)
        {
            lock (_syncRoot)
            {
                _pendingRequest = request ?? new JackSupportExternalRequest();
            }
        }

        public void Execute(UIApplication application)
        {
            JackSupportExternalRequest request;

            lock (_syncRoot)
            {
                request = _pendingRequest;
                _pendingRequest = new JackSupportExternalRequest();
            }

            if (request == null ||
                request.RequestType == JackSupportExternalRequestType.None)
            {
                return;
            }

            UIDocument uiDocument =
                application == null
                    ? null
                    : application.ActiveUIDocument;

            if (uiDocument == null ||
                uiDocument.Document == null)
            {
                TaskDialog.Show(
                    "잭서포트",
                    "열려 있는 Revit 모델이 없습니다.");

                return;
            }

            if (!IsForDocument(uiDocument.Document))
            {
                TaskDialog.Show(
                    "잭서포트",
                    "옵션창을 열었던 모델과 현재 활성 모델이 다릅니다.\n\n" +
                    "기존 옵션창을 닫고 현재 모델에서 다시 열어 주십시오.");

                return;
            }

            try
            {
                ExecuteRequest(
                    application,
                    uiDocument,
                    request);
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "잭서포트 실행 오류",
                    ex.ToString());
            }
        }

        public string GetName()
        {
            return "REVIT_TAP 잭서포트 모델리스 요청";
        }

        public bool IsForDocument(Document document)
        {
            if (_sourceDocument == null ||
                document == null)
            {
                return false;
            }

            try
            {
                if (!_sourceDocument.IsValidObject ||
                    !document.IsValidObject)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            if (ReferenceEquals(
                _sourceDocument,
                document))
            {
                return true;
            }

            try
            {
                if (_sourceDocument.Equals(document))
                {
                    return true;
                }
            }
            catch
            {
                // Revit 래퍼 비교가 실패하면 식별값으로 계속 비교
            }

            try
            {
                if (_sourceDocument.GetHashCode() ==
                    document.GetHashCode())
                {
                    return true;
                }
            }
            catch
            {
                // 해시 비교가 불가능하면 식별값으로 계속 비교
            }

            string activeIdentity =
                GetDocumentIdentity(document);

            return
                !string.IsNullOrWhiteSpace(
                    _sourceDocumentIdentity) &&
                !string.IsNullOrWhiteSpace(
                    activeIdentity) &&
                string.Equals(
                    _sourceDocumentIdentity,
                    activeIdentity,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDocumentIdentity(
            Document document)
        {
            if (document == null)
            {
                return string.Empty;
            }

            try
            {
                string pathName =
                    document.PathName;

                if (!string.IsNullOrWhiteSpace(pathName))
                {
                    return
                        "PATH:" +
                        pathName.Trim();
                }
            }
            catch
            {
                // 저장되지 않은 문서는 프로젝트 정보로 계속 확인
            }

            try
            {
                ProjectInfo projectInfo =
                    document.ProjectInformation;

                if (projectInfo != null &&
                    !string.IsNullOrWhiteSpace(
                        projectInfo.UniqueId))
                {
                    return
                        "PROJECT:" +
                        projectInfo.UniqueId.Trim();
                }
            }
            catch
            {
                // 프로젝트 정보가 없으면 제목과 해시로 계속 확인
            }

            try
            {
                return
                    "TITLE:" +
                    (document.Title ?? string.Empty).Trim() +
                    "|HASH:" +
                    document.GetHashCode().ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void ExecuteRequest(
            UIApplication application,
            UIDocument uiDocument,
            JackSupportExternalRequest request)
        {
            Document document = uiDocument.Document;
            JackSupportSettings settings =
                JackSupportSettingsStore.Load();

            if (request.RequestType ==
                JackSupportExternalRequestType.Generate)
            {
                string message = string.Empty;

                CreateJackSupportCommand command =
                    new CreateJackSupportCommand();

                Result result = command.ExecuteFromExternalEvent(
                    application,
                    ref message);

                if (result == Result.Failed &&
                    !string.IsNullOrWhiteSpace(message))
                {
                    TaskDialog.Show(
                        "잭서포트 생성 오류",
                        message);
                }

                return;
            }

            if (request.RequestType ==
                JackSupportExternalRequestType.ApplyJudgmentColors)
            {
                JackSupportBatchColorResult result =
                    JackSupportBatchColorService.Execute(
                        document,
                        settings,
                        JackSupportBatchColorMode.Judgment);

                TaskDialog.Show(
                    "잭서포트 판정색상 일괄 적용",
                    result.BuildMessage(
                        JackSupportBatchColorMode.Judgment));

                return;
            }

            if (request.RequestType ==
                JackSupportExternalRequestType.ApplyUniformColor)
            {
                JackSupportBatchColorResult result =
                    JackSupportBatchColorService.Execute(
                        document,
                        settings,
                        JackSupportBatchColorMode.Uniform);

                TaskDialog.Show(
                    "잭서포트 공통색상 일괄 적용",
                    result.BuildMessage(
                        JackSupportBatchColorMode.Uniform));

                return;
            }

            if (request.RequestType ==
                JackSupportExternalRequestType.RestoreTemporaryIsolation)
            {
                JackSupportSelectionService.RestoreTemporaryIsolation(
                    document,
                    document.ActiveView);

                return;
            }

            JackSupportSelectionOptions selectionOptions =
                request.SelectionOptions ??
                new JackSupportSelectionOptions();

            if (request.RequestType ==
                JackSupportExternalRequestType.SelectSupports)
            {
                JackSupportSelectionResult result =
                    JackSupportSelectionService.SelectInModel(
                        uiDocument,
                        settings,
                        selectionOptions);

                if (!result.Succeeded)
                {
                    TaskDialog.Show(
                        "잭서포트 선택",
                        result.Message);
                }

                return;
            }

            if (request.RequestType ==
                JackSupportExternalRequestType.IsolateSupports)
            {
                JackSupportSelectionResult result =
                    JackSupportSelectionService.IsolateInActiveView(
                        uiDocument,
                        settings,
                        selectionOptions);

                if (!result.Succeeded)
                {
                    TaskDialog.Show(
                        "잭서포트 임시 분리",
                        result.Message);
                }
            }
        }
    }

    public static class JackSupportModelessController
    {
        private static JackSupportSettingsForm _form;
        private static JackSupportExternalEventHandler _handler;
        private static ExternalEvent _externalEvent;

        public static void Show(UIApplication application)
        {
            UIDocument uiDocument =
                application == null
                    ? null
                    : application.ActiveUIDocument;

            if (uiDocument == null ||
                uiDocument.Document == null)
            {
                TaskDialog.Show(
                    "잭서포트",
                    "열려 있는 Revit 모델이 없습니다.");

                return;
            }

            if (_form != null &&
                !_form.IsDisposed)
            {
                if (_handler != null &&
                    _handler.IsForDocument(
                        uiDocument.Document))
                {
                    if (!_form.Visible)
                    {
                        _form.Show();
                    }

                    _form.WindowState =
                        FormWindowState.Normal;

                    _form.Activate();
                    _form.BringToFront();
                    return;
                }

                JackSupportSettingsForm oldForm =
                    _form;

                try
                {
                    oldForm.Close();
                }
                catch
                {
                    try
                    {
                        oldForm.Dispose();
                    }
                    catch
                    {
                        // 기존 창 정리에 실패해도 새 창 생성은 계속 진행
                    }

                    _form = null;
                    _handler = null;

                    if (_externalEvent != null)
                    {
                        try
                        {
                            _externalEvent.Dispose();
                        }
                        catch
                        {
                            // 무시
                        }
                    }

                    _externalEvent = null;
                }
            }

            JackSupportSettings settings =
                JackSupportSettingsStore.Load();

            _handler =
                new JackSupportExternalEventHandler(
                    uiDocument.Document);

            _externalEvent = ExternalEvent.Create(_handler);

            _form = new JackSupportSettingsForm(
                settings,
                uiDocument.Document,
                _handler,
                _externalEvent);

            _form.FormClosed += Form_FormClosed;

            IntPtr mainWindowHandle =
                Process.GetCurrentProcess().MainWindowHandle;

            if (mainWindowHandle != IntPtr.Zero)
            {
                _form.Show(
                    new RevitWindowHandle(
                        mainWindowHandle));
            }
            else
            {
                _form.Show();
            }
        }

        private static void Form_FormClosed(
            object sender,
            FormClosedEventArgs e)
        {
            if (_form != null)
                _form.FormClosed -= Form_FormClosed;

            if (_externalEvent != null)
                _externalEvent.Dispose();

            _form = null;
            _handler = null;
            _externalEvent = null;
        }

        private sealed class RevitWindowHandle : IWin32Window
        {
            public IntPtr Handle { get; private set; }

            public RevitWindowHandle(IntPtr handle)
            {
                Handle = handle;
            }
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 모델리스 ExternalEvent 실행기와 창 관리자
// 파일명: JackSupportExternalEventHandler.cs
// =========================================================
