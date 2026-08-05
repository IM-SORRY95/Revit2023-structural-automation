// =========================================================
// 생성 날짜 및 시간: 2026-07-13 (KST)
// 수정 날짜 및 시간: 2026-08-05 (KST)
// 파일명: JackSupportSettingsForm.cs
// 공개 범위: 포트폴리오용으로 실제 부재 코드와 로컬 경로를 일반화
// 설명:
// 1) 잭서포트 설정을 탭별로 구분하여 관리
// 2) 프로젝트의 구조기둥 패밀리와 유형을 선택하면 해당 유형을 직접 사용
// 3) 지정 패밀리가 프로젝트에 없으면 설정한 RFA 파일을 자동 로드
// 4) 패밀리/유형을 비워두면 PORTFOLIO_JACK_SUPPORT 유형 자동 생성 방식 사용
// 5) 특수 보·Drop Caps 기둥·RC보하부 잭서포트 조건과 하부 지지체 설정
// 6) 생성 높이 구간별 데이터 매개변수와 입력값 설정
// 7) Windows 색상 선택창과 즉시 미리보기를 이용한 활성 뷰 색상 설정
// 8) 층간 잭서포트 끝점 접촉은 허용하고 동일 높이 구간만 중복으로 판정하도록 설정
// 9) 특수 보 잭서포트 A/B 그룹별 대상과 생성 위치 비율 설정
// 10) 실제 최하층 레벨 우선 및 그외층 판정 부재 접촉으로 층 분류 데이터 입력
// 11) 최하층/그외층별 수량 집계용 인스턴스 매개변수에 1 또는 0 입력
// 12) 최하층과 그외층의 활성 뷰 표시 색상을 각각 선택하고 즉시 미리보기
// 13) 특수 보 잭서포트는 벽체 유무와 관계없이 생성
// 14) RC보하부 잭서포트는 보 하부 지지 벽체/기둥만 구간 제외
// 15) 최신 실행 결과를 옵션창에서 다시 확인
// 16) 높이별 데이터 일치 구간 없음 객체의 전용 경고 색상 선택 및 미리보기
// 17) 실제 최하층 레벨 복수 선택 및 그외층 판정 부재 설정
// 18) Drop Caps 포함 기둥 주변 4개 잭서포트의 동일 층 판정 안내
// 19) 기존 잭서포트 판정색상/공통색상 일괄 적용 버튼
// 20) 모델리스 옵션창과 ExternalEvent 실행
// 21) Enter 키 자동 생성 차단
// 22) 생성 원인별 잭서포트 선택·임시 분리 탭
// 23) 슬래브 경계부 주변 검색 설정
// 24) 특수 보 양단 단부 기둥 하단 보간 및 전용 외곽선 설정
// 25) 특수 대상 보와 측면 접촉 부재의 긴 측면 접촉 방향별 생성 개수 설정
// 26) 최신 결과창을 가로·세로 스크롤과 크기 조절이 가능한 창으로 표시
// 26) 높이 규칙별 색상과 최하층/높이 색상 분류 기준 선택
// 27) 자주 사용하는 탭을 최하층·높이별 데이터·표시 색상 순으로 재배치
// 28) Drop Caps 기둥 중 특수 보 제외 규칙 해당 객체 제외 안내
// 29) %AppData%\RevitStructuralAutomation\JackSupport\JackSupport.rfa를 기본 경로로 사용
// 30) 로드된 패밀리는 첫 번째 구조기둥 유형을 자동 선택
// 31) RFA가 아직 로드되지 않아 유형이 비어 있어도 자동 로드 모드로 실행
// 32) 실행 후 자동으로 찾은 실제 패밀리명/유형명을 옵션창에 반영
// =========================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace REVIT_TAP
{
    public enum JackSupportSettingsAction
    {
        None = 0,
        SaveOnly = 1,
        Generate = 2,
        ApplyJudgmentColors = 3,
        ApplyUniformColor = 4
    }

    public class JackSupportSettingsForm : System.Windows.Forms.Form
    {
        private static readonly string FixedJackSupportRfaPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "RevitStructuralAutomation",
                "JackSupport",
                "JackSupport.rfa");

        private readonly JackSupportSettings _settings;
        private readonly Document _doc;
        private readonly JackSupportExternalEventHandler _externalEventHandler;
        private readonly ExternalEvent _externalEvent;
        private readonly Dictionary<string, List<string>> _projectFamilyTypes;
        private readonly IList<Level> _projectLevels;

        private System.Windows.Forms.Timer _familySelectionRefreshTimer;
        private int _familySelectionRefreshTickCount;

        private TextBox txtGeneratedTypeName;
        private ComboBox cboSourceFamilyName;
        private ComboBox cboSourceTypeName;
        private TextBox txtFamilyRfaPath;
        private TextBox txtDiameterParameterName;
        private NumericUpDown numDiameter;

        private CheckBox chkEnableCondition1;
        private TextBox txtCondition1Names;
        private TextBox txtCondition1Ratios;
        private TextBox txtCondition1BNames;
        private TextBox txtCondition1BRatios;
        private TextBox txtCondition1ColumnFallbackNames;
        private NumericUpDown numCondition1ColumnTouchTolerance;
        private TextBox txtCondition1SpecialBeamNames;
        private TextBox txtCondition1SideMemberNames;
        private NumericUpDown numCondition1SpecialNoSideCount;
        private NumericUpDown numCondition1SpecialBothSidesCountPerSide;
        private NumericUpDown numCondition1SpecialSingleSideCount;
        private NumericUpDown numCondition1SideDetectionTolerance;

        private CheckBox chkEnableCondition2;
        private TextBox txtCondition2TypeNameKeywords;
        private NumericUpDown numCondition2Offset;

        private CheckBox chkEnableCondition3;
        private TextBox txtCondition3Names;
        private NumericUpDown numCondition3Interval;
        private CheckBox chkUseExistingColumns;
        private CheckBox chkUseWalls;
        private NumericUpDown numWallTolerance;
        private NumericUpDown numWallHorizontalExtra;

        private CheckBox chkIncludeFloors;
        private CheckBox chkIncludeFoundations;
        private TextBox txtFoundationNames;
        private NumericUpDown numLowerSupportSearchDepth;
        private CheckBox chkEnableBoundaryLowerSupportSearch;
        private NumericUpDown numBoundarySearchMaximumDistance;
        private NumericUpDown numBoundarySearchStep;
        private NumericUpDown numBoundarySupportTopTolerance;
        private CheckBox chkMoveSupportToBoundaryFoundPoint;

        private CheckBox chkEnableLowestFloorClassification;
        private CheckedListBox lstActualLowestLevels;
        private NumericUpDown numActualLowestLevelTolerance;
        private TextBox txtLowestFloorFoundationNames;
        private TextBox txtLowestFloorFoundationSuffixes;
        private NumericUpDown numLowestFloorTouchTolerance;
        private TextBox txtFloorClassificationParameterName;
        private TextBox txtLowestFloorClassificationValue;
        private TextBox txtOtherFloorClassificationValue;
        private CheckBox chkEnableFloorClassificationCountParameters;
        private CheckBox chkResetFloorClassificationCountParameters;
        private TextBox txtLowestFloorCountParameterName;
        private TextBox txtOtherFloorCountParameterName;

        private NumericUpDown numColumnTolerance;
        private NumericUpDown numDuplicateTolerance;
        private NumericUpDown numDuplicateVerticalTolerance;
        private CheckBox chkUseActiveViewOnly;

        private CheckBox chkEnableHeightParameterRules;
        private CheckBox chkResetHeightRuleParameters;
        private NumericUpDown numHeightRuleRounding;
        private DataGridView gridHeightParameterRules;
        private Button btnAddHeightRule;
        private Button btnDeleteHeightRule;

        private CheckBox chkEnableViewColorOverride;
        private CheckBox chkApplyColorToExistingSupports;
        private CheckBox chkUseSeparateFloorColors;
        private ComboBox cboColorClassificationMode;
        private CheckBox chkEnableBtsColumnBasedOutline;
        private NumericUpDown numBtsColumnBasedOutlineLineWeight;

        private System.Windows.Forms.Panel pnlViewColorPreview;
        private Label lblViewColorRgb;
        private Button btnChooseViewColor;

        private System.Windows.Forms.Panel pnlLowestFloorColorPreview;
        private Label lblLowestFloorColorRgb;
        private Button btnChooseLowestFloorColor;

        private System.Windows.Forms.Panel pnlOtherFloorColorPreview;
        private Label lblOtherFloorColorRgb;
        private Button btnChooseOtherFloorColor;

        private CheckBox chkEnableUnmatchedHeightColor;
        private System.Windows.Forms.Panel pnlUnmatchedHeightColorPreview;
        private Label lblUnmatchedHeightColorRgb;
        private Button btnChooseUnmatchedHeightColor;

        private System.Windows.Forms.Panel pnlBtsColumnOutlineColorPreview;
        private Label lblBtsColumnOutlineColorRgb;
        private Button btnChooseBtsColumnOutlineColor;

        private CheckBox chkSelectBtsBeamSupports;
        private CheckBox chkSelectRcBeamSupports;
        private CheckBox chkSelectPcColumnSupports;
        private CheckBox chkSelectRcColumnSupports;
        private CheckBox chkSelectOtherSupports;

        private int selectedViewColorRed;
        private int selectedViewColorGreen;
        private int selectedViewColorBlue;

        private int selectedLowestFloorColorRed;
        private int selectedLowestFloorColorGreen;
        private int selectedLowestFloorColorBlue;

        private int selectedOtherFloorColorRed;
        private int selectedOtherFloorColorGreen;
        private int selectedOtherFloorColorBlue;

        private int selectedUnmatchedHeightColorRed;
        private int selectedUnmatchedHeightColorGreen;
        private int selectedUnmatchedHeightColorBlue;

        private int selectedBtsColumnOutlineColorRed;
        private int selectedBtsColumnOutlineColorGreen;
        private int selectedBtsColumnOutlineColorBlue;

        public JackSupportSettingsAction RequestedAction
        {
            get;
            private set;
        }

        public bool RunAfterSave
        {
            get
            {
                return RequestedAction ==
                    JackSupportSettingsAction.Generate;
            }
        }

        public JackSupportSettingsForm(
            JackSupportSettings settings,
            Document doc)
            : this(settings, doc, null, null)
        {
        }

        public JackSupportSettingsForm(
            JackSupportSettings settings,
            Document doc,
            JackSupportExternalEventHandler externalEventHandler,
            ExternalEvent externalEvent)
        {
            _settings = settings ?? new JackSupportSettings();
            _doc = doc;
            _externalEventHandler = externalEventHandler;
            _externalEvent = externalEvent;
            _projectFamilyTypes = CollectProjectStructuralColumnFamilyTypes();
            _projectLevels = CollectProjectLevels();
            RequestedAction = JackSupportSettingsAction.None;

            InitializeForm();
            PopulateFamilyItems();
            LoadValues();
            UpdateEnabledStates();
            InitializeFamilySelectionRefreshTimer();
        }

        private void InitializeForm()
        {
            Text = "잭서포트 설정 및 자동 생성";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(780, 650);
            Size = new Size(900, 730);
            Font = new Font("맑은 고딕", 9F);
            AutoScaleMode = AutoScaleMode.Font;
            KeyPreview = true;
            ShowInTaskbar = false;

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 1;
            shell.RowCount = 3;
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.Padding = new Padding(10);
            Controls.Add(shell);

            Label guide = new Label();
            guide.AutoSize = true;
            guide.Text =
                "표준부재명은 별도 매개변수가 아니라 유형명의 마지막 '_' 뒤 문자열입니다. " +
                "예: GIRDER_600_RC_BEAM → RC_BEAM";
            guide.Padding = new Padding(2, 2, 2, 10);
            shell.Controls.Add(guide, 0, 0);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            shell.Controls.Add(tabs, 0, 1);

            tabs.TabPages.Add(CreateLowestFloorClassificationTab());
            tabs.TabPages.Add(CreateHeightParameterTab());
            tabs.TabPages.Add(CreateViewColorTab());
            tabs.TabPages.Add(CreateGeneratedSupportSelectionTab());
            tabs.TabPages.Add(CreateBasicTab());
            tabs.TabPages.Add(CreateCondition1Tab());
            tabs.TabPages.Add(CreateCondition2Tab());
            tabs.TabPages.Add(CreateCondition3Tab());
            tabs.TabPages.Add(CreateLowerSupportTab());
            tabs.TabPages.Add(CreateAdvancedTab());

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, 10, 0, 0);

            Button btnRun = new Button();
            btnRun.Text = "자동 생성";
            btnRun.Width = 120;
            btnRun.Height = 36;
            btnRun.Font = new Font(
                btnRun.Font,
                FontStyle.Bold);
            btnRun.Click += BtnRun_Click;

            Button btnSave = new Button();
            btnSave.Text = "설정 저장";
            btnSave.Width = 120;
            btnSave.Height = 36;
            btnSave.Click += BtnSave_Click;

            Button btnCancel = new Button();
            btnCancel.Text = "닫기";
            btnCancel.Width = 100;
            btnCancel.Height = 36;
            btnCancel.Click += BtnClose_Click;

            Button btnLatestResult = new Button();
            btnLatestResult.Text = "최신 결과창 다시보기";
            btnLatestResult.Width = 160;
            btnLatestResult.Height = 36;
            btnLatestResult.Click += BtnLatestResult_Click;

            buttons.Controls.Add(btnRun);
            buttons.Controls.Add(btnSave);
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnLatestResult);
            shell.Controls.Add(buttons, 0, 2);

            AcceptButton = null;
            CancelButton = null;
        }

        private TabPage CreateBasicTab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("기본 생성", out table);

            AddSection(table, "사용할 잭서포트 패밀리");

            AddInformationRow(
                table,
                "지정 패밀리 직접 사용",
                "프로젝트에 로드된 구조기둥 패밀리와 유형을 선택하면 그 유형으로 잭서포트를 직접 생성합니다. " +
                "지정한 패밀리의 형상과 유형 매개변수는 변경하지 않습니다.");

            cboSourceFamilyName = AddComboRow(
                table,
                "사용할 패밀리명");

            cboSourceFamilyName.SelectedIndexChanged +=
                CboSourceFamilyName_SelectedIndexChanged;

            cboSourceFamilyName.TextChanged +=
                FamilySelectionChanged;

            cboSourceTypeName = AddComboRow(
                table,
                "사용할 유형명");

            cboSourceTypeName.TextChanged +=
                FamilySelectionChanged;

            Button btnClearSelection = new Button();
            btnClearSelection.Text = "패밀리 지정 해제 · 자동 생성 모드 사용";
            btnClearSelection.AutoSize = true;
            btnClearSelection.Height = 32;
            btnClearSelection.Click += BtnClearFamilySelection_Click;

            AddControlRow(
                table,
                "선택 초기화",
                btnClearSelection);

            txtFamilyRfaPath = AddPathRow(
                table,
                "패밀리 RFA 경로",
                BtnBrowseRfa_Click);

            AddInformationRow(
                table,
                "RFA 자동 로드",
                "%AppData%\\RevitStructuralAutomation\\JackSupport\\JackSupport.rfa를 우선 사용합니다. 현재 프로젝트에 패밀리가 없거나 유형이 비어 있어도 " +
                "실행 시 RFA를 자동으로 불러와 실제 구조기둥 패밀리와 유형을 선택하고 설정에 저장합니다.");

            AddSection(table, "패밀리를 지정하지 않았을 때의 자동 생성");

            AddInformationRow(
                table,
                "자동 생성 방식",
                "사용할 패밀리명과 유형명을 모두 비워두면 먼저 %AppData%\\RevitStructuralAutomation\\JackSupport\\JackSupport.rfa를 자동 로드합니다. " +
                "해당 RFA를 사용할 수 없을 때만 프로젝트의 PORTFOLIO_JACK_SUPPORT 유형 또는 원형 구조기둥 복제 방식을 사용합니다.");

            txtGeneratedTypeName = AddTextRow(
                table,
                "자동 생성 유형명");

            numDiameter = AddNumberRow(
                table,
                "자동 생성 기둥 지름(mm)",
                1M,
                5000M,
                1);

            txtDiameterParameterName = AddTextRow(
                table,
                "자동 생성 지름 매개변수");

            return tab;
        }

        private TabPage CreateCondition1Tab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("특수 보 잭서포트", out table);

            chkEnableCondition1 = AddCheckRow(
                table,
                "특수 보 잭서포트 사용");
            chkEnableCondition1.CheckedChanged += ConditionCheckChanged;

            AddInformationRow(
                table,
                "기능",
                "특수 보를 A/B 두 기준으로 나누어 각각 다른 개수와 위치에 잭서포트를 생성합니다. " +
                "같은 표준부재명은 A와 B에 중복 등록할 수 없습니다. 하단은 [하부 지지체] 탭의 바닥 또는 구조기초를 사용합니다. " +
                "특수 보는 벽체 유무와 관계없이 설정된 위치에 생성합니다.");

            AddSection(table, "A 기준");

            AddInformationRow(
                table,
                "기본 예",
                "A 대상은 0.25;0.75를 사용하면 보 길이의 1/4, 3/4 위치에 2개가 생성됩니다.");

            txtCondition1Names = AddMultilineRow(
                table,
                "A 대상 표준부재명\n(; 또는 줄바꿈 구분)",
                90);

            txtCondition1Ratios = AddTextRow(
                table,
                "A 생성 위치 비율\n예: 0.25;0.75");

            AddSection(table, "B 기준");

            AddInformationRow(
                table,
                "기본 예",
                "B 대상은 0.25;0.5;0.75를 사용하면 보 길이의 1/4, 중앙, 3/4 위치에 3개가 생성됩니다.");

            txtCondition1BNames = AddMultilineRow(
                table,
                "B 대상 표준부재명\n(; 또는 줄바꿈 구분)",
                90);

            txtCondition1BRatios = AddTextRow(
                table,
                "B 생성 위치 비율\n예: 0.25;0.5;0.75");

            AddSection(table, "바닥·기초가 없을 때 단부 기둥 기준");

            AddInformationRow(
                table,
                "생성 방식",
                "특수 보 하부에서 바닥과 지정 구조기초를 찾지 못하면 보 양쪽 끝에 붙은 단부 구조기둥을 찾습니다. " +
                "양쪽 기둥의 실제 하단 높이를 보 길이 방향으로 보간하여 잭서포트 하단을 정합니다. 양쪽 기둥이 모두 있어야 적용합니다.");

            txtCondition1ColumnFallbackNames = AddMultilineRow(
                table,
                "대체 기준 기둥 표준부재명",
                70);

            numCondition1ColumnTouchTolerance = AddNumberRow(
                table,
                "보 끝·단부 기둥 접촉 허용오차(mm)",
                1M,
                5000M,
                1);

            AddSection(table, "특수 대상 보 긴 측면의 측면 접촉 부재 접촉 규칙");

            txtCondition1SpecialBeamNames = AddMultilineRow(
                table,
                "특수 대상 보 표준부재명",
                60);

            txtCondition1SideMemberNames = AddMultilineRow(
                table,
                "측면 접촉 부재 표준부재명",
                60);

            numCondition1SpecialNoSideCount = AddNumberRow(
                table,
                "측면 접촉 부재 없음 · 중앙 생성 개수",
                0M,
                20M,
                0);

            numCondition1SpecialBothSidesCountPerSide = AddNumberRow(
                table,
                "측면 접촉 부재 양쪽 · 측면별 생성 개수",
                0M,
                20M,
                0);

            numCondition1SpecialSingleSideCount = AddNumberRow(
                table,
                "측면 접촉 부재 한쪽 · 접촉 측면 생성 개수",
                0M,
                20M,
                0);

            numCondition1SideDetectionTolerance = AddNumberRow(
                table,
                "특수 대상 보·측면 접촉 부재 접촉 허용오차(mm)",
                1M,
                5000M,
                1);

            AddInformationRow(
                table,
                "판정 및 기본 결과",
                "측면 접촉 부재는 문서 전체 구조프레임을 검사하고, 유형명의 마지막 '_' 뒤 표준부재명이 설정값과 정확히 일치하는 부재만 사용합니다. " +
                "긴 측면 양쪽에 측면 접촉 부재가 있으면 측면별 1개씩 총 2개, 한쪽에만 있으면 접촉한 측면에 3개, 양쪽 모두 없으면 중앙에 1개를 생성합니다.");

            return tab;
        }

        private TabPage CreateCondition2Tab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("Drop Caps 기둥", out table);

            chkEnableCondition2 = AddCheckRow(
                table,
                "Drop Caps 포함 기둥 잭서포트 사용");
            chkEnableCondition2.CheckedChanged += ConditionCheckChanged;

            AddInformationRow(
                table,
                "기능",
                "구조기둥 패밀리명 전체에 지정 문구가 하나라도 포함되면, " +
                "대상 기둥 네 외곽면에서 설정 거리만큼 떨어진 위치에 4개를 생성합니다. " +
                "단, 표준부재명이 특수 보 제외 규칙에 해당하는 구조기둥은 Drop Caps 대상에서 제외합니다. " +
                "기본 포함 문구는 Drop Caps이며 대소문자를 구분하지 않습니다.");

            txtCondition2TypeNameKeywords = AddMultilineRow(
                table,
                "패밀리명 포함 문구\n예: Drop Caps",
                80);

            numCondition2Offset = AddNumberRow(
                table,
                "기둥 외곽면 이격거리(mm)",
                0M,
                10000M,
                1);

            return tab;
        }

        private TabPage CreateCondition3Tab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab(
                "RC보하부 잭서포트",
                out table);

            chkEnableCondition3 = AddCheckRow(
                table,
                "RC보하부 잭서포트 사용");
            chkEnableCondition3.CheckedChanged += ConditionCheckChanged;

            AddInformationRow(
                table,
                "기능",
                "RC_BEAM 등의 구조프레임에서 보 하부를 실제로 지지하는 구조기둥과 벽체의 점유 구간만 먼저 제외합니다. " +
                "보 상부에 닿아 있는 벽체나 기둥은 제외 구간으로 보지 않습니다. " +
                "그 후 남은 각 구간을 독립적으로 계산하여 설정 간격을 초과할 때 잭서포트를 균등 배치합니다.");

            txtCondition3Names = AddMultilineRow(
                table,
                "대상 표준부재명",
                90);

            numCondition3Interval = AddNumberRow(
                table,
                "남은 구간 기준 간격(mm)",
                100M,
                30000M,
                1);

            AddSection(table, "보 하부의 기존 지지 객체");

            chkUseExistingColumns = AddCheckRow(
                table,
                "기존 구조기둥 점유 구간을 제외하고 나머지 구간 재계산");

            chkUseWalls = AddCheckRow(
                table,
                "벽체 점유 구간을 제외하고 나머지 구간 재계산");
            chkUseWalls.CheckedChanged += ConditionCheckChanged;

            AddInformationRow(
                table,
                "구간 계산 예",
                "보 길이가 10m여도 중간의 기둥·벽체가 나누는 남은 구간이 모두 3m 이하이면 생성하지 않습니다. " +
                "남은 구간이 4m이면 1개, 7m이면 2개, 10m이면 3개를 생성합니다.");

            AddSection(table, "벽체 점유 구간 판정");

            numWallTolerance = AddNumberRow(
                table,
                "벽 접촉/수직 허용오차(mm)",
                0M,
                1000M,
                1);

            numWallHorizontalExtra = AddNumberRow(
                table,
                "보 폭 바깥 추가 검사거리(mm)",
                0M,
                3000M,
                1);

            AddInformationRow(
                table,
                "벽체 판정",
                "벽체가 보의 길이 방향과 겹치고, 보 폭 범위에 들어오며, 보와 수직으로 닿거나 겹치면 " +
                "해당 벽체의 실제 점유 길이를 보에서 제외합니다.");

            return tab;
        }

        private TabPage CreateLowerSupportTab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("하부 지지체", out table);

            AddInformationRow(
                table,
                "기능",
                "특수 보 잭서포트와 RC보하부 잭서포트에서 생성 기둥의 하단을 결정합니다. " +
                "생성 위치 바로 아래에서 가장 가까운 바닥 또는 지정 구조기초의 상단면을 사용합니다.");

            chkIncludeFloors = AddCheckRow(
                table,
                "바닥 객체를 하부 지지체로 사용");
            chkIncludeFloors.CheckedChanged += LowerSupportCheckChanged;

            chkIncludeFoundations = AddCheckRow(
                table,
                "구조기초 객체를 하부 지지체로 사용");
            chkIncludeFoundations.CheckedChanged += LowerSupportCheckChanged;

            txtFoundationNames = AddMultilineRow(
                table,
                "구조기초 표준부재명\n기본값: FOUNDATION_SUPPORT",
                90);

            numLowerSupportSearchDepth = AddNumberRow(
                table,
                "아래 방향 최대 검색거리(mm)",
                100M,
                200000M,
                1);

            AddSection(table, "슬래브 경계부 보정 검색");

            chkEnableBoundaryLowerSupportSearch = AddCheckRow(
                table,
                "원래 생성점 아래에 바닥이 없으면 주변 지점 검색");

            chkEnableBoundaryLowerSupportSearch.CheckedChanged +=
                LowerSupportCheckChanged;

            numBoundarySearchMaximumDistance = AddNumberRow(
                table,
                "주변 최대 검색거리(mm)",
                0M,
                5000M,
                1);

            numBoundarySearchStep = AddNumberRow(
                table,
                "주변 검색 간격(mm)",
                1M,
                1000M,
                1);

            numBoundarySupportTopTolerance = AddNumberRow(
                table,
                "후보 바닥 상단 높이차 허용(mm)",
                0M,
                5000M,
                1);

            chkMoveSupportToBoundaryFoundPoint = AddCheckRow(
                table,
                "찾은 바닥 유효 지점으로 잭서포트 위치 이동");

            AddInformationRow(
                table,
                "권장값",
                "최대 300mm / 간격 50mm / 높이차 100mm를 권장합니다. " +
                "곡선 보는 해당 위치의 접선·직각 방향을 우선 검색하며, 가까운 링에서 가장 높은 유효 바닥을 선택합니다.");

            AddInformationRow(
                table,
                "구조기초 판정 예",
                "구조기초 유형명이 FOUNDATION_1200_FOUNDATION_SUPPORT이면 마지막 '_' 뒤의 FOUNDATION_SUPPORT를 읽어 대상에 포함합니다.");

            return tab;
        }


        private TabPage CreateLowestFloorClassificationTab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("최하층 구분", out table);

            chkEnableLowestFloorClassification = AddCheckRow(
                table,
                "생성 잭서포트를 최하층/그외층으로 구분하여 매개변수 입력");

            chkEnableLowestFloorClassification.CheckedChanged +=
                ConditionCheckChanged;

            AddInformationRow(
                table,
                "판정 우선순위",
                "1순위: 아래에서 선택한 실제 최하층 레벨에 속하면 하부 표준부재명과 관계없이 최하층입니다. " +
                "2순위: 실제 최하층이 아닌 위치에서 하부가 설정한 표준부재명 객체와 닿으면 그외층입니다. " +
                "3순위: 실제 최하층이 아니어도 해당 표준부재명 객체와 닿지 않으면 최하층으로 판정합니다.");

            AddSection(table, "실제 최하층 레벨 우선 판정");

            lstActualLowestLevels = new CheckedListBox();
            lstActualLowestLevels.CheckOnClick = true;
            lstActualLowestLevels.Height = 150;
            lstActualLowestLevels.Dock = DockStyle.Top;
            lstActualLowestLevels.IntegralHeight = false;
            lstActualLowestLevels.HorizontalScrollbar = true;

            AddControlRow(
                table,
                "실제 최하층 레벨\n복수 선택 가능",
                lstActualLowestLevels);

            numActualLowestLevelTolerance = AddNumberRow(
                table,
                "레벨 기준높이 보조 판정\n허용오차(mm)",
                0M,
                5000M,
                1);

            AddInformationRow(
                table,
                "예시",
                "실제 최하층이 B2F라면 B2F를 체크합니다. B2F 잭서포트 하부가 OTHER_FLOOR_MARKER에 닿아도 B2F가 우선되어 최하층으로 분류됩니다. " +
                "레벨이 여러 개로 분리된 현장은 최하층에 해당하는 레벨을 모두 체크할 수 있습니다.");

            AddSection(table, "그외층 판정 하부 부재");

            txtLowestFloorFoundationNames = AddMultilineRow(
                table,
                "그외층 판정 표준부재명\n정확 일치 예: OTHER_FLOOR_MARKER",
                80);

            txtLowestFloorFoundationSuffixes = AddMultilineRow(
                table,
                "그외층 판정 접미어\n예: MF;SF",
                80);

            numLowestFloorTouchTolerance = AddNumberRow(
                table,
                "잭서포트 하단-판정 부재 상단\n접촉 허용오차(mm)",
                0M,
                1000M,
                1);

            AddInformationRow(
                table,
                "Drop Caps 기둥 4개 세트",
                "Drop Caps 포함 기둥 주변에 생성되는 4개 잭서포트는 같은 층으로 처리합니다. " +
                "실제 최하층 레벨이면 4개 모두 최하층이며, 실제 최하층이 아닌 경우 4개 중 1개라도 그외층 판정 부재에 닿으면 4개 모두 그외층입니다.");

            AddSection(table, "분류 데이터 입력");

            txtFloorClassificationParameterName = AddTextRow(
                table,
                "입력할 인스턴스 매개변수명");

            txtLowestFloorClassificationValue = AddTextRow(
                table,
                "최하층 입력값");

            txtOtherFloorClassificationValue = AddTextRow(
                table,
                "그외층 입력값");

            AddInformationRow(
                table,
                "권장 매개변수",
                "문자 인스턴스 매개변수를 권장합니다. 예: SUPPORT_FLOOR_CLASS / 최하층 값 '최하층' / 그외층 값 '그외층'. " +
                "정수 또는 숫자 매개변수도 사용할 수 있습니다.");

            AddSection(table, "최하층·그외층 수량 집계");

            chkEnableFloorClassificationCountParameters = AddCheckRow(
                table,
                "최하층/그외층별 수량 매개변수에 1 입력");

            chkEnableFloorClassificationCountParameters.CheckedChanged +=
                ConditionCheckChanged;

            chkResetFloorClassificationCountParameters = AddCheckRow(
                table,
                "반대 분류의 수량 매개변수를 0으로 초기화");

            txtLowestFloorCountParameterName = AddTextRow(
                table,
                "최하층 수량 인스턴스 매개변수명");

            txtOtherFloorCountParameterName = AddTextRow(
                table,
                "그외층 수량 인스턴스 매개변수명");

            AddInformationRow(
                table,
                "수량 입력 방식",
                "최하층 잭서포트는 [최하층=1, 그외층=0], 그외층 잭서포트는 [최하층=0, 그외층=1]로 입력할 수 있습니다. " +
                "일람표에서 합계 처리하면 각 분류별 수량을 확인할 수 있습니다.");

            return tab;
        }


        private TabPage CreateHeightParameterTab()
        {
            TabPage tab = new TabPage("높이별 데이터");

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.Padding = new Padding(14);
            shell.ColumnCount = 1;
            shell.RowCount = 5;
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tab.Controls.Add(shell);

            chkEnableHeightParameterRules = new CheckBox();
            chkEnableHeightParameterRules.Text =
                "생성된 잭서포트 높이에 따라 데이터 매개변수 입력";
            chkEnableHeightParameterRules.AutoSize = true;
            chkEnableHeightParameterRules.Padding =
                new Padding(0, 6, 0, 6);
            chkEnableHeightParameterRules.CheckedChanged +=
                HeightParameterRuleControlChanged;
            shell.Controls.Add(chkEnableHeightParameterRules, 0, 0);

            Label guide = new Label();
            guide.AutoSize = true;
            guide.MaximumSize = new Size(820, 0);
            guide.Padding = new Padding(0, 3, 0, 8);
            guide.Text =
                "각 잭서포트의 실제 생성 높이를 mm로 판정하여, 일치한 구간의 인스턴스 매개변수에 설정값을 입력합니다.\n" +
                "구간은 최소높이 이상, 최대높이 미만입니다. 예: 4000~5000은 4000 이상 5000 미만이며, 정확히 5000은 다음 구간에 포함됩니다.\n" +
                "등록 매개변수는 구조기둥 인스턴스 매개변수여야 하며, 정수·실수·문자열 형식을 지원합니다.";
            shell.Controls.Add(guide, 0, 1);

            TableLayoutPanel options = new TableLayoutPanel();
            options.Dock = DockStyle.Top;
            options.AutoSize = true;
            options.ColumnCount = 4;
            options.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 210F));
            options.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 170F));
            options.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 300F));
            options.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100F));

            Label roundingLabel = new Label();
            roundingLabel.Text = "높이 판정 반올림 단위(mm)";
            roundingLabel.AutoSize = true;
            roundingLabel.Anchor = AnchorStyles.Left;

            numHeightRuleRounding = new NumericUpDown();
            numHeightRuleRounding.Minimum = 0.1M;
            numHeightRuleRounding.Maximum = 1000M;
            numHeightRuleRounding.DecimalPlaces = 1;
            numHeightRuleRounding.Increment = 0.1M;
            numHeightRuleRounding.Width = 150;

            chkResetHeightRuleParameters = new CheckBox();
            chkResetHeightRuleParameters.Text =
                "입력 전 등록된 모든 높이구간 매개변수를 0으로 초기화";
            chkResetHeightRuleParameters.AutoSize = true;
            chkResetHeightRuleParameters.Padding =
                new Padding(8, 3, 0, 3);

            options.Controls.Add(roundingLabel, 0, 0);
            options.Controls.Add(numHeightRuleRounding, 1, 0);
            options.Controls.Add(chkResetHeightRuleParameters, 2, 0);
            options.SetColumnSpan(chkResetHeightRuleParameters, 2);
            shell.Controls.Add(options, 0, 2);

            gridHeightParameterRules = new DataGridView();
            gridHeightParameterRules.Dock = DockStyle.Fill;
            gridHeightParameterRules.AllowUserToAddRows = false;
            gridHeightParameterRules.AllowUserToDeleteRows = true;
            gridHeightParameterRules.AutoGenerateColumns = false;
            gridHeightParameterRules.RowHeadersVisible = false;
            gridHeightParameterRules.SelectionMode =
                DataGridViewSelectionMode.FullRowSelect;
            gridHeightParameterRules.MultiSelect = true;
            gridHeightParameterRules.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.Fill;

            DataGridViewTextBoxColumn minimumColumn =
                new DataGridViewTextBoxColumn();
            minimumColumn.Name = "MinimumHeightMm";
            minimumColumn.HeaderText = "최소 높이(mm)\n이상";
            minimumColumn.FillWeight = 22F;

            DataGridViewTextBoxColumn maximumColumn =
                new DataGridViewTextBoxColumn();
            maximumColumn.Name = "MaximumHeightMm";
            maximumColumn.HeaderText = "최대 높이(mm)\n미만";
            maximumColumn.FillWeight = 22F;

            DataGridViewTextBoxColumn parameterColumn =
                new DataGridViewTextBoxColumn();
            parameterColumn.Name = "ParameterName";
            parameterColumn.HeaderText = "입력할 인스턴스 매개변수명";
            parameterColumn.FillWeight = 38F;

            DataGridViewTextBoxColumn valueColumn =
                new DataGridViewTextBoxColumn();
            valueColumn.Name = "Value";
            valueColumn.HeaderText = "입력값";
            valueColumn.FillWeight = 14F;

            DataGridViewButtonColumn colorColumn =
                new DataGridViewButtonColumn();
            colorColumn.Name = "RuleColor";
            colorColumn.HeaderText = "표시 색상";
            colorColumn.Text = "색상 선택";
            colorColumn.UseColumnTextForButtonValue = false;
            colorColumn.FillWeight = 20F;

            DataGridViewTextBoxColumn colorRedColumn =
                new DataGridViewTextBoxColumn();
            colorRedColumn.Name = "ColorRed";
            colorRedColumn.Visible = false;

            DataGridViewTextBoxColumn colorGreenColumn =
                new DataGridViewTextBoxColumn();
            colorGreenColumn.Name = "ColorGreen";
            colorGreenColumn.Visible = false;

            DataGridViewTextBoxColumn colorBlueColumn =
                new DataGridViewTextBoxColumn();
            colorBlueColumn.Name = "ColorBlue";
            colorBlueColumn.Visible = false;

            gridHeightParameterRules.Columns.Add(minimumColumn);
            gridHeightParameterRules.Columns.Add(maximumColumn);
            gridHeightParameterRules.Columns.Add(parameterColumn);
            gridHeightParameterRules.Columns.Add(valueColumn);
            gridHeightParameterRules.Columns.Add(colorColumn);
            gridHeightParameterRules.Columns.Add(colorRedColumn);
            gridHeightParameterRules.Columns.Add(colorGreenColumn);
            gridHeightParameterRules.Columns.Add(colorBlueColumn);

            gridHeightParameterRules.CellContentClick +=
                GridHeightParameterRules_CellContentClick;

            shell.Controls.Add(gridHeightParameterRules, 0, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = true;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Padding = new Padding(0, 8, 0, 0);

            btnAddHeightRule = new Button();
            btnAddHeightRule.Text = "구간 추가";
            btnAddHeightRule.AutoSize = true;
            btnAddHeightRule.Height = 32;
            btnAddHeightRule.Click += BtnAddHeightRule_Click;

            btnDeleteHeightRule = new Button();
            btnDeleteHeightRule.Text = "선택 구간 삭제";
            btnDeleteHeightRule.AutoSize = true;
            btnDeleteHeightRule.Height = 32;
            btnDeleteHeightRule.Click += BtnDeleteHeightRule_Click;

            Button btnAddExample = new Button();
            btnAddExample.Text = "예시 2개 추가";
            btnAddExample.AutoSize = true;
            btnAddExample.Height = 32;
            btnAddExample.Click += BtnAddHeightRuleExamples_Click;

            buttons.Controls.Add(btnAddHeightRule);
            buttons.Controls.Add(btnDeleteHeightRule);
            buttons.Controls.Add(btnAddExample);
            shell.Controls.Add(buttons, 0, 4);

            return tab;
        }

        private TabPage CreateViewColorTab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("표시 색상", out table);

            AddSection(table, "생성 잭서포트 색상 강조");

            chkEnableViewColorOverride = AddCheckRow(
                table,
                "자동 생성 후 현재 활성 뷰에 선택 색상 적용");

            chkEnableViewColorOverride.CheckedChanged +=
                ViewColorControlChanged;

            chkApplyColorToExistingSupports = AddCheckRow(
                table,
                "동일 위치에서 다시 발견된 기존 잭서포트에도 색상 적용");

            chkApplyColorToExistingSupports.CheckedChanged +=
                ViewColorControlChanged;

            AddInformationRow(
                table,
                "적용 방식",
                "요소의 재료나 패밀리 색상을 변경하지 않고 현재 활성 뷰에 요소별 그래픽 재지정을 적용합니다. " +
                "다른 뷰에서는 원래 색상으로 보일 수 있습니다.");

            AddSection(table, "색상 적용 방식");

            cboColorClassificationMode = AddComboRow(
                table,
                "판정색상 분류 기준");

            cboColorClassificationMode.DropDownStyle =
                ComboBoxStyle.DropDownList;
            cboColorClassificationMode.Items.Add(
                "최하층·그외층 기준");
            cboColorClassificationMode.Items.Add(
                "높이별 데이터 기준");
            cboColorClassificationMode.SelectedIndexChanged +=
                ViewColorControlChanged;

            AddInformationRow(
                table,
                "선택 기준",
                "[판정색상 일괄 적용]과 자동 생성 후 색상은 선택한 분류 방법 하나를 사용합니다. " +
                "최하층 기준은 최하층/그외층 색상을, 높이별 데이터 기준은 각 높이 규칙 행의 색상을 적용합니다.");

            chkUseSeparateFloorColors = AddCheckRow(
                table,
                "최하층과 그외층 색상을 각각 적용");

            chkUseSeparateFloorColors.CheckedChanged +=
                ViewColorControlChanged;

            AddInformationRow(
                table,
                "분리 색상 조건",
                "분리 색상을 사용하려면 [최하층 구분] 탭의 기능도 체크해야 합니다. " +
                "선택한 실제 최하층 레벨을 우선 적용한 뒤, 그외층 판정 표준부재명 접촉 결과에 따라 색상이 나뉩니다.");

            AddSection(table, "공통 색상");

            AddInformationRow(
                table,
                "사용 조건",
                "최하층/그외층 색상 분리를 사용하지 않을 때 모든 잭서포트에 적용되는 색상입니다.");

            TableLayoutPanel commonColorSelector =
                CreateColorSelector(
                    out pnlViewColorPreview,
                    out btnChooseViewColor,
                    out lblViewColorRgb,
                    BtnChooseViewColor_Click);

            AddControlRow(
                table,
                "공통 색상",
                commonColorSelector);

            AddSection(table, "최하층 색상");

            TableLayoutPanel lowestColorSelector =
                CreateColorSelector(
                    out pnlLowestFloorColorPreview,
                    out btnChooseLowestFloorColor,
                    out lblLowestFloorColorRgb,
                    BtnChooseLowestFloorColor_Click);

            AddControlRow(
                table,
                "최하층 잭서포트",
                lowestColorSelector);

            AddSection(table, "그외층 색상");

            TableLayoutPanel otherColorSelector =
                CreateColorSelector(
                    out pnlOtherFloorColorPreview,
                    out btnChooseOtherFloorColor,
                    out lblOtherFloorColorRgb,
                    BtnChooseOtherFloorColor_Click);

            AddControlRow(
                table,
                "그외층 잭서포트",
                otherColorSelector);

            AddSection(table, "높이별 데이터 일치 구간 없음 색상");

            chkEnableUnmatchedHeightColor = AddCheckRow(
                table,
                "높이별 데이터에 일치하는 구간이 없는 잭서포트를 별도 색상으로 표시");

            chkEnableUnmatchedHeightColor.CheckedChanged +=
                ViewColorControlChanged;

            AddInformationRow(
                table,
                "우선 적용",
                "높이별 데이터 규칙을 사용 중인데 실제 높이가 어떤 구간에도 포함되지 않으면 " +
                "최하층/그외층 색상보다 이 경고 색상을 우선 적용합니다. 생성 결과창에도 ElementId와 높이를 표시합니다.");

            TableLayoutPanel unmatchedColorSelector =
                CreateColorSelector(
                    out pnlUnmatchedHeightColorPreview,
                    out btnChooseUnmatchedHeightColor,
                    out lblUnmatchedHeightColorRgb,
                    BtnChooseUnmatchedHeightColor_Click);

            AddControlRow(
                table,
                "일치 구간 없음",
                unmatchedColorSelector);

            AddSection(table, "단부 기둥 기준 특수 보 잭서포트 외곽선");

            chkEnableBtsColumnBasedOutline = AddCheckRow(
                table,
                "단부 기둥 길이로 생성된 특수 보 잭서포트에 별도 외곽선 적용");
            chkEnableBtsColumnBasedOutline.CheckedChanged +=
                ViewColorControlChanged;

            TableLayoutPanel btsColumnOutlineSelector =
                CreateColorSelector(
                    out pnlBtsColumnOutlineColorPreview,
                    out btnChooseBtsColumnOutlineColor,
                    out lblBtsColumnOutlineColorRgb,
                    BtnChooseBtsColumnOutlineColor_Click);

            AddControlRow(
                table,
                "단부 기둥 기준 외곽선 색상",
                btsColumnOutlineSelector);

            numBtsColumnBasedOutlineLineWeight = AddNumberRow(
                table,
                "단부 기둥 기준 외곽선 두께(1~16)",
                1M,
                16M,
                0);

            AddInformationRow(
                table,
                "동시 표시",
                "잭서포트 면은 선택한 최하층/높이 색상을 유지하고, 투영선과 절단선만 별도 색상으로 표시합니다. " +
                "예: 최하층 파란색 면 + 단부 기둥 기준 노란색 외곽선.");

            AddInformationRow(
                table,
                "표시 결과",
                "선택한 색상은 투영선·절단선·표면 솔리드 채우기·절단 솔리드 채우기에 함께 적용되어 " +
                "3D 뷰, 평면, 단면에서 눈에 띄게 표시됩니다.");

            AddInformationRow(
                table,
                "색상 변경",
                "[색상 선택...] 버튼을 누르면 Windows 색상 창이 열립니다. " +
                "색상을 고르고 확인을 누르는 즉시 해당 미리보기와 RGB 값이 갱신됩니다.");

            AddSection(table, "기존 잭서포트 일괄 색상 적용");

            FlowLayoutPanel batchButtons = new FlowLayoutPanel();
            batchButtons.AutoSize = true;
            batchButtons.Dock = DockStyle.Top;
            batchButtons.FlowDirection = FlowDirection.LeftToRight;
            batchButtons.WrapContents = true;

            Button btnApplyJudgmentColors = new Button();
            btnApplyJudgmentColors.Text = "판정색상 일괄 적용";
            btnApplyJudgmentColors.Width = 180;
            btnApplyJudgmentColors.Height = 36;
            btnApplyJudgmentColors.Click +=
                BtnApplyJudgmentColors_Click;

            Button btnApplyUniformColor = new Button();
            btnApplyUniformColor.Text = "공통색상 일괄 적용";
            btnApplyUniformColor.Width = 180;
            btnApplyUniformColor.Height = 36;
            btnApplyUniformColor.Click +=
                BtnApplyUniformColor_Click;

            batchButtons.Controls.Add(btnApplyJudgmentColors);
            batchButtons.Controls.Add(btnApplyUniformColor);

            AddControlRow(
                table,
                "기존 잭서포트",
                batchButtons);

            AddInformationRow(
                table,
                "판정색상 일괄 적용",
                "현재 모델의 기존 잭서포트 전체를 다시 검사합니다. 선택한 색상 분류 기준에 따라 최하층·그외층 또는 높이별 데이터 색상을 적용하고, 단부 기둥 기준 잭서포트에는 별도 외곽선을 함께 적용합니다.");

            AddInformationRow(
                table,
                "공통색상 일괄 적용",
                "현재 모델의 기존 잭서포트 전체를 위에서 선택한 공통 색상으로 변경합니다. 최하층·그외층·높이 불일치 판정은 색상 적용에 사용하지 않습니다.");

            return tab;
        }

        private TabPage CreateGeneratedSupportSelectionTab()
        {
            TableLayoutPanel table;
            TabPage tab =
                CreateScrollableTab(
                    "생성 잭서포트 선택",
                    out table);

            AddInformationRow(
                table,
                "기능",
                "생성된 잭서포트 유형명에 포함된 생성 원인을 기준으로 모아서 선택하거나 현재 뷰에서 임시 분리합니다. " +
                "옵션창을 닫지 않아도 Revit 모델에서 선택 결과를 확인할 수 있습니다.");

            chkSelectBtsBeamSupports = AddCheckRow(
                table,
                "특수보 하부 잭서포트");

            chkSelectRcBeamSupports = AddCheckRow(
                table,
                "RC보 하부 잭서포트");

            chkSelectPcColumnSupports = AddCheckRow(
                table,
                "Drop Caps 기둥 주변 잭서포트");

            chkSelectRcColumnSupports = AddCheckRow(
                table,
                "RC기둥 주변 잭서포트");

            chkSelectOtherSupports = AddCheckRow(
                table,
                "기타 또는 생성 원인 정보가 없는 잭서포트");

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true;
            buttons.Dock = DockStyle.Top;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = true;

            Button btnSelect = new Button();
            btnSelect.Text = "모델에서 선택";
            btnSelect.Width = 150;
            btnSelect.Height = 36;
            btnSelect.Click += BtnSelectGeneratedSupports_Click;

            Button btnIsolate = new Button();
            btnIsolate.Text = "현재 뷰 임시 분리";
            btnIsolate.Width = 170;
            btnIsolate.Height = 36;
            btnIsolate.Click += BtnIsolateGeneratedSupports_Click;

            Button btnRestore = new Button();
            btnRestore.Text = "임시 분리 원복";
            btnRestore.Width = 150;
            btnRestore.Height = 36;
            btnRestore.Click += BtnRestoreGeneratedSupports_Click;

            buttons.Controls.Add(btnSelect);
            buttons.Controls.Add(btnIsolate);
            buttons.Controls.Add(btnRestore);

            AddControlRow(
                table,
                "선택 실행",
                buttons);

            AddInformationRow(
                table,
                "유형명 판정",
                "예: PORTFOLIO_JACK_SUPPORT_특수보_BEAM_TYPE_A, PORTFOLIO_JACK_SUPPORT_RC보하부_RC_BEAM, " +
                "PORTFOLIO_JACK_SUPPORT_DropCaps기둥_COLUMN_CAP 형식으로 구분합니다. 기존 생성물처럼 생성 원인 정보가 없으면 [기타]로 분류합니다.");

            return tab;
        }

        private static TableLayoutPanel CreateColorSelector(
            out System.Windows.Forms.Panel previewPanel,
            out Button chooseButton,
            out Label rgbLabel,
            EventHandler chooseHandler)
        {
            TableLayoutPanel colorSelector = new TableLayoutPanel();
            colorSelector.AutoSize = true;
            colorSelector.Dock = DockStyle.Top;
            colorSelector.ColumnCount = 3;

            colorSelector.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 110F));

            colorSelector.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 180F));

            colorSelector.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100F));

            previewPanel = new System.Windows.Forms.Panel();
            previewPanel.Width = 90;
            previewPanel.Height = 44;
            previewPanel.BorderStyle = BorderStyle.FixedSingle;
            previewPanel.Margin = new Padding(3, 5, 12, 5);

            chooseButton = new Button();
            chooseButton.Text = "색상 선택...";
            chooseButton.Width = 150;
            chooseButton.Height = 34;

            if (chooseHandler != null)
                chooseButton.Click += chooseHandler;

            rgbLabel = new Label();
            rgbLabel.AutoSize = true;
            rgbLabel.Anchor = AnchorStyles.Left;
            rgbLabel.Padding = new Padding(8, 8, 0, 0);

            colorSelector.Controls.Add(previewPanel, 0, 0);
            colorSelector.Controls.Add(chooseButton, 1, 0);
            colorSelector.Controls.Add(rgbLabel, 2, 0);

            return colorSelector;
        }

        private TabPage CreateAdvancedTab()
        {
            TableLayoutPanel table;
            TabPage tab = CreateScrollableTab("검사 범위·고급", out table);

            chkUseActiveViewOnly = AddCheckRow(
                table,
                "현재 활성 뷰에 보이는 객체만 검사\n(해제 시 문서 전체 검사)");

            numColumnTolerance = AddNumberRow(
                table,
                "기존 구조기둥 지지 판정 허용오차(mm)",
                0M,
                3000M,
                1);

            AddSection(table, "기존 잭서포트 중복 판정");

            numDuplicateTolerance = AddNumberRow(
                table,
                "XY 위치 동일 허용오차(mm)",
                0M,
                1000M,
                1);

            numDuplicateVerticalTolerance = AddNumberRow(
                table,
                "하단·상단 높이 동일 허용오차(mm)",
                1M,
                3000M,
                1);

            AddInformationRow(
                table,
                "층간 접촉 처리",
                "같은 XY 위치라도 기존 잭서포트와 신규 잭서포트의 하단·상단 높이가 모두 같지 않으면 서로 다른 층의 잭서포트로 보고 생성합니다. " +
                "아래층 상단과 위층 하단이 단순히 맞닿거나 일부 겹쳐도 중복으로 제외하지 않습니다.");

            AddInformationRow(
                table,
                "실제 중복 조건",
                "XY 위치가 허용오차 이내이고, 하단 높이 차이와 상단 높이 차이가 모두 설정값 이내일 때만 기존 잭서포트와 동일한 객체로 판단합니다.");

            AddInformationRow(
                table,
                "권장 설정",
                "문서 전체 검사, 기존 구조기둥 허용오차 50mm, XY 위치 허용오차 50mm, 하단·상단 높이 허용오차 50mm를 권장합니다.");

            return tab;
        }

        private Dictionary<string, List<string>> CollectProjectStructuralColumnFamilyTypes()
        {
            Dictionary<string, List<string>> result =
                new Dictionary<string, List<string>>(
                    StringComparer.OrdinalIgnoreCase);

            if (_doc == null)
                return result;

            try
            {
                IList<FamilySymbol> symbols =
                    new FilteredElementCollector(_doc)
                        .OfCategory(BuiltInCategory.OST_StructuralColumns)
                        .WhereElementIsElementType()
                        .OfType<FamilySymbol>()
                        .ToList();

                foreach (FamilySymbol symbol in symbols)
                {
                    if (symbol == null || symbol.Family == null)
                        continue;

                    string familyName = symbol.Family.Name ?? string.Empty;
                    string typeName = symbol.Name ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(familyName) ||
                        string.IsNullOrWhiteSpace(typeName))
                    {
                        continue;
                    }

                    List<string> types;

                    if (!result.TryGetValue(familyName, out types))
                    {
                        types = new List<string>();
                        result.Add(familyName, types);
                    }

                    if (!types.Any(value => string.Equals(
                        value,
                        typeName,
                        StringComparison.OrdinalIgnoreCase)))
                    {
                        types.Add(typeName);
                    }
                }

                foreach (List<string> types in result.Values)
                {
                    types.Sort(StringComparer.CurrentCultureIgnoreCase);
                }
            }
            catch
            {
                // 패밀리 목록 조회 실패 시에도 직접 입력은 가능하도록 빈 목록을 사용함.
            }

            return result;
        }

        private IList<Level> CollectProjectLevels()
        {
            if (_doc == null)
                return new List<Level>();

            try
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(level => level.Elevation)
                    .ToList();
            }
            catch
            {
                return new List<Level>();
            }
        }

        private void LoadActualLowestLevelItems()
        {
            if (lstActualLowestLevels == null)
                return;

            HashSet<string> configuredNames =
                new HashSet<string>(
                    _settings.GetActualLowestLevelNames(),
                    StringComparer.OrdinalIgnoreCase);

            lstActualLowestLevels.BeginUpdate();

            try
            {
                lstActualLowestLevels.Items.Clear();

                foreach (Level level in _projectLevels)
                {
                    double elevationMm =
                        UnitUtils.ConvertFromInternalUnits(
                            level.Elevation,
                            UnitTypeId.Millimeters);

                    string displayText =
                        level.Name +
                        "  ·  " +
                        elevationMm.ToString("0.##") +
                        "mm";

                    int itemIndex =
                        lstActualLowestLevels.Items.Add(
                            new JackSupportLevelListItem(
                                level.Name,
                                displayText));

                    if (configuredNames.Contains(level.Name))
                    {
                        lstActualLowestLevels.SetItemChecked(
                            itemIndex,
                            true);
                    }
                }

                // 구버전 설정에는 실제 최하층 레벨이 없으므로
                // 가장 낮은 레벨을 기본 체크하되 사용자가 변경할 수 있게 한다.
                if (lstActualLowestLevels.CheckedItems.Count == 0 &&
                    lstActualLowestLevels.Items.Count > 0)
                {
                    lstActualLowestLevels.SetItemChecked(0, true);
                }
            }
            finally
            {
                lstActualLowestLevels.EndUpdate();
            }
        }

        private string GetSelectedActualLowestLevelNames()
        {
            List<string> names = new List<string>();

            if (lstActualLowestLevels == null)
                return string.Empty;

            foreach (object item in
                lstActualLowestLevels.CheckedItems)
            {
                JackSupportLevelListItem levelItem =
                    item as JackSupportLevelListItem;

                if (levelItem != null &&
                    !string.IsNullOrWhiteSpace(levelItem.LevelName))
                {
                    names.Add(levelItem.LevelName);
                }
            }

            return string.Join(
                ";",
                names.Distinct(
                    StringComparer.OrdinalIgnoreCase));
        }

        private void PopulateFamilyItems()
        {
            cboSourceFamilyName.BeginUpdate();

            try
            {
                cboSourceFamilyName.Items.Clear();
                cboSourceFamilyName.Items.Add(string.Empty);

                foreach (string familyName in _projectFamilyTypes.Keys
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
                {
                    cboSourceFamilyName.Items.Add(familyName);
                }
            }
            finally
            {
                cboSourceFamilyName.EndUpdate();
            }
        }

        private void PopulateTypeItems(
            string familyName,
            string selectedTypeName)
        {
            cboSourceTypeName.BeginUpdate();

            try
            {
                cboSourceTypeName.Items.Clear();
                cboSourceTypeName.Items.Add(string.Empty);

                List<string> types;

                if (!string.IsNullOrWhiteSpace(familyName) &&
                    _projectFamilyTypes.TryGetValue(
                        familyName.Trim(),
                        out types))
                {
                    foreach (string typeName in types)
                        cboSourceTypeName.Items.Add(typeName);
                }

                cboSourceTypeName.Text =
                    selectedTypeName ?? string.Empty;
            }
            finally
            {
                cboSourceTypeName.EndUpdate();
            }
        }

        private void SelectFirstLoadedTypeWhenNeeded()
        {
            string familyName =
                cboSourceFamilyName == null
                    ? string.Empty
                    : cboSourceFamilyName.Text.Trim();

            if (string.IsNullOrWhiteSpace(familyName) ||
                !string.IsNullOrWhiteSpace(
                    cboSourceTypeName.Text))
            {
                return;
            }

            List<string> types;

            if (_projectFamilyTypes.TryGetValue(
                familyName,
                out types) &&
                types != null &&
                types.Count > 0)
            {
                cboSourceTypeName.Text = types[0];
            }
        }

        private string ResolvePreferredRfaPath()
        {
            if (File.Exists(FixedJackSupportRfaPath))
            {
                return FixedJackSupportRfaPath;
            }

            string configuredPath =
                txtFamilyRfaPath == null
                    ? string.Empty
                    : txtFamilyRfaPath.Text;

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                configuredPath = _settings.FamilyRfaPath;
            }

            return string.IsNullOrWhiteSpace(configuredPath)
                ? FixedJackSupportRfaPath
                : configuredPath.Trim();
        }

        private bool HasUsableRfaPath()
        {
            string rfaPath = ResolvePreferredRfaPath();

            return
                !string.IsNullOrWhiteSpace(rfaPath) &&
                File.Exists(rfaPath);
        }

        private void NormalizeIncompleteFamilySelectionForRfaLoad()
        {
            bool hasFamily =
                !string.IsNullOrWhiteSpace(
                    _settings.SourceRoundColumnFamilyName);

            bool hasType =
                !string.IsNullOrWhiteSpace(
                    _settings.SourceRoundColumnTypeName);

            if (hasFamily == hasType || !HasUsableRfaPath())
            {
                return;
            }

            string rfaPath = ResolvePreferredRfaPath();

            // 파일명만 입력되고 유형이 아직 없는 경우는 직접 지정으로
            // 보지 않고 RFA 자동 로드 모드로 전환한다. 실제 이름은
            // JackSupportFamilyService가 로드 후 설정에 다시 기록한다.
            _settings.SourceRoundColumnFamilyName = string.Empty;
            _settings.SourceRoundColumnTypeName = string.Empty;
            _settings.FamilyRfaPath = rfaPath;

            cboSourceFamilyName.Text = string.Empty;
            PopulateTypeItems(string.Empty, string.Empty);
            txtFamilyRfaPath.Text = rfaPath;
        }

        private void InitializeFamilySelectionRefreshTimer()
        {
            _familySelectionRefreshTimer =
                new System.Windows.Forms.Timer();

            _familySelectionRefreshTimer.Interval = 500;
            _familySelectionRefreshTimer.Tick +=
                FamilySelectionRefreshTimer_Tick;

            FormClosed += delegate
            {
                if (_familySelectionRefreshTimer == null)
                    return;

                _familySelectionRefreshTimer.Stop();
                _familySelectionRefreshTimer.Dispose();
                _familySelectionRefreshTimer = null;
            };
        }

        private void StartFamilySelectionRefreshTimer()
        {
            if (_familySelectionRefreshTimer == null)
                return;

            _familySelectionRefreshTickCount = 0;
            _familySelectionRefreshTimer.Start();
        }

        private void FamilySelectionRefreshTimer_Tick(
            object sender,
            EventArgs e)
        {
            _familySelectionRefreshTickCount++;

            string familyName =
                _settings.SourceRoundColumnFamilyName == null
                    ? string.Empty
                    : _settings.SourceRoundColumnFamilyName.Trim();

            string typeName =
                _settings.SourceRoundColumnTypeName == null
                    ? string.Empty
                    : _settings.SourceRoundColumnTypeName.Trim();

            if (!string.IsNullOrWhiteSpace(familyName) &&
                !string.IsNullOrWhiteSpace(typeName))
            {
                cboSourceFamilyName.Text = familyName;
                PopulateTypeItems(familyName, typeName);
                txtFamilyRfaPath.Text = ResolvePreferredRfaPath();
                UpdateEnabledStates();
                _familySelectionRefreshTimer.Stop();
                return;
            }

            if (_familySelectionRefreshTickCount >= 120)
            {
                _familySelectionRefreshTimer.Stop();
            }
        }

        private static TabPage CreateScrollableTab(
            string title,
            out TableLayoutPanel table)
        {
            TabPage tab = new TabPage(title);

            System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            tab.Controls.Add(panel);

            table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.Padding = new Padding(14);
            table.ColumnCount = 2;
            table.ColumnStyles.Add(
                new ColumnStyle(SizeType.Absolute, 285F));
            table.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100F));
            panel.Controls.Add(table);

            return tab;
        }

        private static void AddSection(
            TableLayoutPanel table,
            string title)
        {
            Label label = new Label();
            label.Text = title;
            label.Font = new Font(
                "맑은 고딕",
                10F,
                FontStyle.Bold);
            label.AutoSize = true;
            label.Padding = new Padding(0, 14, 0, 6);

            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(label, 0, row);
            table.SetColumnSpan(label, 2);
        }

        private static void AddInformationRow(
            TableLayoutPanel table,
            string labelText,
            string informationText)
        {
            Label informationLabel = new Label();
            informationLabel.Text = informationText;
            informationLabel.AutoSize = true;
            informationLabel.MaximumSize = new Size(535, 0);
            informationLabel.Padding = new Padding(3, 5, 3, 8);

            AddControlRow(table, labelText, informationLabel);
        }

        private static TextBox AddTextRow(
            TableLayoutPanel table,
            string labelText)
        {
            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            AddControlRow(table, labelText, textBox);
            return textBox;
        }

        private static ComboBox AddComboRow(
            TableLayoutPanel table,
            string labelText)
        {
            ComboBox comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDown;
            comboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            comboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
            comboBox.Dock = DockStyle.Fill;
            AddControlRow(table, labelText, comboBox);
            return comboBox;
        }

        private static TextBox AddPathRow(
            TableLayoutPanel table,
            string labelText,
            EventHandler browseClick)
        {
            TableLayoutPanel pathPanel = new TableLayoutPanel();
            pathPanel.Dock = DockStyle.Fill;
            pathPanel.AutoSize = true;
            pathPanel.ColumnCount = 2;
            pathPanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100F));
            pathPanel.ColumnStyles.Add(
                new ColumnStyle(SizeType.AutoSize));

            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;

            Button browseButton = new Button();
            browseButton.Text = "찾아보기";
            browseButton.AutoSize = true;
            browseButton.Margin = new Padding(6, 0, 0, 0);
            browseButton.Click += browseClick;

            pathPanel.Controls.Add(textBox, 0, 0);
            pathPanel.Controls.Add(browseButton, 1, 0);

            AddControlRow(table, labelText, pathPanel);
            return textBox;
        }

        private static TextBox AddMultilineRow(
            TableLayoutPanel table,
            string labelText,
            int height)
        {
            TextBox textBox = new TextBox();
            textBox.Multiline = true;
            textBox.ScrollBars = ScrollBars.Vertical;
            textBox.Height = height;
            textBox.Dock = DockStyle.Fill;
            AddControlRow(table, labelText, textBox);
            return textBox;
        }

        private static NumericUpDown AddNumberRow(
            TableLayoutPanel table,
            string labelText,
            decimal minimum,
            decimal maximum,
            int decimalPlaces)
        {
            NumericUpDown numeric = new NumericUpDown();
            numeric.Minimum = minimum;
            numeric.Maximum = maximum;
            numeric.DecimalPlaces = decimalPlaces;
            numeric.ThousandsSeparator = true;
            numeric.Dock = DockStyle.Left;
            numeric.Width = 190;
            AddControlRow(table, labelText, numeric);
            return numeric;
        }

        private static CheckBox AddCheckRow(
            TableLayoutPanel table,
            string text)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.AutoSize = true;
            checkBox.Padding = new Padding(0, 6, 0, 6);

            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(checkBox, 0, row);
            table.SetColumnSpan(checkBox, 2);
            return checkBox;
        }

        private static void AddControlRow(
            TableLayoutPanel table,
            string labelText,
            System.Windows.Forms.Control control)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Padding = new Padding(0, 5, 0, 5);

            control.Margin = new Padding(3, 4, 3, 4);

            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private void LoadValues()
        {
            txtGeneratedTypeName.Text =
                _settings.GeneratedTypeName;

            cboSourceFamilyName.Text =
                _settings.SourceRoundColumnFamilyName;

            PopulateTypeItems(
                cboSourceFamilyName.Text,
                _settings.SourceRoundColumnTypeName);

            SelectFirstLoadedTypeWhenNeeded();

            txtFamilyRfaPath.Text =
                ResolvePreferredRfaPath();

            txtDiameterParameterName.Text =
                _settings.DiameterParameterName;

            SetNumber(
                numDiameter,
                _settings.DiameterMm);

            chkEnableCondition1.Checked =
                _settings.EnableCondition1;

            txtCondition1Names.Text = ToMultiline(
                _settings.Condition1StandardNames);

            txtCondition1Ratios.Text =
                _settings.Condition1Ratios;

            txtCondition1BNames.Text = ToMultiline(
                _settings.Condition1BStandardNames);

            txtCondition1BRatios.Text =
                _settings.Condition1BRatios;

            txtCondition1ColumnFallbackNames.Text = ToMultiline(
                _settings.Condition1ColumnFallbackStandardNames);

            SetNumber(
                numCondition1ColumnTouchTolerance,
                _settings.Condition1ColumnTouchToleranceMm);

            txtCondition1SpecialBeamNames.Text = ToMultiline(
                _settings.Condition1SpecialBeamStandardNames);

            txtCondition1SideMemberNames.Text = ToMultiline(
                _settings.Condition1SideMemberStandardNames);

            SetNumber(
                numCondition1SpecialNoSideCount,
                _settings.Condition1SpecialNoSideCount);

            SetNumber(
                numCondition1SpecialBothSidesCountPerSide,
                _settings.Condition1SpecialBothSidesCountPerSide);

            SetNumber(
                numCondition1SpecialSingleSideCount,
                _settings.Condition1SpecialSingleSideCount);

            SetNumber(
                numCondition1SideDetectionTolerance,
                _settings.Condition1SideDetectionToleranceMm);

            chkEnableCondition2.Checked =
                _settings.EnableCondition2;

            txtCondition2TypeNameKeywords.Text = ToMultiline(
                _settings.Condition2TypeNameKeywords);

            SetNumber(
                numCondition2Offset,
                _settings.Condition2OffsetMm);

            chkEnableCondition3.Checked =
                _settings.EnableCondition3;

            txtCondition3Names.Text = ToMultiline(
                _settings.Condition3StandardNames);

            SetNumber(
                numCondition3Interval,
                _settings.Condition3IntervalMm);

            chkUseExistingColumns.Checked =
                _settings.UseExistingColumnsAsSupports;

            chkUseWalls.Checked =
                _settings.UseWallsAsSupports;

            SetNumber(
                numWallTolerance,
                _settings.WallTouchToleranceMm);

            SetNumber(
                numWallHorizontalExtra,
                _settings.WallHorizontalExtraMm);

            chkIncludeFloors.Checked =
                _settings.IncludeFloorsAsLowerSupports;

            chkIncludeFoundations.Checked =
                _settings.IncludeStructuralFoundationsAsLowerSupports;

            txtFoundationNames.Text = ToMultiline(
                _settings.StructuralFoundationStandardNames);

            SetNumber(
                numLowerSupportSearchDepth,
                _settings.LowerSupportSearchDepthMm);

            chkEnableBoundaryLowerSupportSearch.Checked =
                _settings.EnableBoundaryLowerSupportSearch;

            SetNumber(
                numBoundarySearchMaximumDistance,
                _settings.BoundarySearchMaximumDistanceMm);

            SetNumber(
                numBoundarySearchStep,
                _settings.BoundarySearchStepMm);

            SetNumber(
                numBoundarySupportTopTolerance,
                _settings.BoundarySupportTopDifferenceToleranceMm);

            chkMoveSupportToBoundaryFoundPoint.Checked =
                _settings.MoveSupportToBoundaryFoundPoint;

            chkEnableLowestFloorClassification.Checked =
                _settings.EnableLowestFloorClassification;

            LoadActualLowestLevelItems();

            SetNumber(
                numActualLowestLevelTolerance,
                _settings.ActualLowestLevelElevationToleranceMm);

            txtLowestFloorFoundationNames.Text = ToMultiline(
                _settings.OtherFloorMarkerStandardNames);

            txtLowestFloorFoundationSuffixes.Text = ToMultiline(
                _settings.OtherFloorMarkerSuffixes);

            SetNumber(
                numLowestFloorTouchTolerance,
                _settings.LowestFloorTouchToleranceMm);

            txtFloorClassificationParameterName.Text =
                _settings.FloorClassificationParameterName;

            txtLowestFloorClassificationValue.Text =
                _settings.LowestFloorClassificationValue;

            txtOtherFloorClassificationValue.Text =
                _settings.OtherFloorClassificationValue;

            chkEnableFloorClassificationCountParameters.Checked =
                _settings.EnableFloorClassificationCountParameters;

            chkResetFloorClassificationCountParameters.Checked =
                _settings.ResetFloorClassificationCountParametersBeforeApply;

            txtLowestFloorCountParameterName.Text =
                _settings.LowestFloorCountParameterName;

            txtOtherFloorCountParameterName.Text =
                _settings.OtherFloorCountParameterName;

            SetNumber(
                numColumnTolerance,
                _settings.ExistingColumnTouchToleranceMm);

            SetNumber(
                numDuplicateTolerance,
                _settings.DuplicatePointToleranceMm);

            SetNumber(
                numDuplicateVerticalTolerance,
                _settings.DuplicateVerticalToleranceMm);

            chkUseActiveViewOnly.Checked =
                _settings.UseActiveViewOnly;

            chkEnableHeightParameterRules.Checked =
                _settings.EnableHeightParameterRules;

            chkResetHeightRuleParameters.Checked =
                _settings.ResetHeightRuleParametersBeforeApply;

            SetNumber(
                numHeightRuleRounding,
                _settings.HeightRuleRoundingMm);

            LoadHeightParameterRuleRows();

            chkEnableViewColorOverride.Checked =
                _settings.EnableViewColorOverride;

            chkApplyColorToExistingSupports.Checked =
                _settings.ApplyColorToExistingSupports;

            selectedViewColorRed =
                ClampColorComponent(_settings.ViewColorRed);

            selectedViewColorGreen =
                ClampColorComponent(_settings.ViewColorGreen);

            selectedViewColorBlue =
                ClampColorComponent(_settings.ViewColorBlue);

            if (cboColorClassificationMode != null)
            {
                cboColorClassificationMode.SelectedIndex =
                    _settings.ColorClassificationMode ==
                        JackSupportColorClassificationMode.HeightParameterRule
                        ? 1
                        : 0;
            }

            chkUseSeparateFloorColors.Checked =
                _settings.UseSeparateFloorColors;

            selectedLowestFloorColorRed =
                ClampColorComponent(_settings.LowestFloorColorRed);

            selectedLowestFloorColorGreen =
                ClampColorComponent(_settings.LowestFloorColorGreen);

            selectedLowestFloorColorBlue =
                ClampColorComponent(_settings.LowestFloorColorBlue);

            selectedOtherFloorColorRed =
                ClampColorComponent(_settings.OtherFloorColorRed);

            selectedOtherFloorColorGreen =
                ClampColorComponent(_settings.OtherFloorColorGreen);

            selectedOtherFloorColorBlue =
                ClampColorComponent(_settings.OtherFloorColorBlue);

            chkEnableUnmatchedHeightColor.Checked =
                _settings.EnableUnmatchedHeightColor;

            selectedUnmatchedHeightColorRed =
                ClampColorComponent(_settings.UnmatchedHeightColorRed);

            selectedUnmatchedHeightColorGreen =
                ClampColorComponent(_settings.UnmatchedHeightColorGreen);

            selectedUnmatchedHeightColorBlue =
                ClampColorComponent(_settings.UnmatchedHeightColorBlue);

            chkEnableBtsColumnBasedOutline.Checked =
                _settings.EnableBtsColumnBasedOutline;

            selectedBtsColumnOutlineColorRed =
                ClampColorComponent(
                    _settings.BtsColumnBasedOutlineRed);

            selectedBtsColumnOutlineColorGreen =
                ClampColorComponent(
                    _settings.BtsColumnBasedOutlineGreen);

            selectedBtsColumnOutlineColorBlue =
                ClampColorComponent(
                    _settings.BtsColumnBasedOutlineBlue);

            SetNumber(
                numBtsColumnBasedOutlineLineWeight,
                _settings.BtsColumnBasedOutlineLineWeight);

            if (chkSelectBtsBeamSupports != null)
                chkSelectBtsBeamSupports.Checked = true;

            if (chkSelectRcBeamSupports != null)
                chkSelectRcBeamSupports.Checked = true;

            if (chkSelectPcColumnSupports != null)
                chkSelectPcColumnSupports.Checked = true;

            if (chkSelectRcColumnSupports != null)
                chkSelectRcColumnSupports.Checked = true;

            if (chkSelectOtherSupports != null)
                chkSelectOtherSupports.Checked = false;

            UpdateViewColorPreviews();
        }

        private void ReadValuesFromControls()
        {
            _settings.GeneratedTypeName =
                txtGeneratedTypeName.Text.Trim();

            _settings.SourceRoundColumnFamilyName =
                cboSourceFamilyName.Text.Trim();

            _settings.SourceRoundColumnTypeName =
                cboSourceTypeName.Text.Trim();

            _settings.FamilyRfaPath =
                txtFamilyRfaPath.Text.Trim();

            _settings.DiameterParameterName =
                txtDiameterParameterName.Text.Trim();

            _settings.DiameterMm =
                Convert.ToDouble(numDiameter.Value);

            _settings.EnableCondition1 =
                chkEnableCondition1.Checked;

            _settings.Condition1StandardNames =
                NormalizeListText(txtCondition1Names.Text);

            _settings.Condition1Ratios =
                NormalizeListText(txtCondition1Ratios.Text);

            _settings.Condition1BStandardNames =
                NormalizeListText(txtCondition1BNames.Text);

            _settings.Condition1BRatios =
                NormalizeListText(txtCondition1BRatios.Text);

            _settings.Condition1ColumnFallbackStandardNames =
                NormalizeListText(
                    txtCondition1ColumnFallbackNames.Text);

            _settings.Condition1ColumnTouchToleranceMm =
                Convert.ToDouble(
                    numCondition1ColumnTouchTolerance.Value);

            _settings.Condition1SpecialBeamStandardNames =
                NormalizeListText(
                    txtCondition1SpecialBeamNames.Text);

            _settings.Condition1SideMemberStandardNames =
                NormalizeListText(
                    txtCondition1SideMemberNames.Text);

            _settings.Condition1SpecialNoSideCount =
                Convert.ToInt32(
                    numCondition1SpecialNoSideCount.Value);

            _settings.Condition1SpecialBothSidesCountPerSide =
                Convert.ToInt32(
                    numCondition1SpecialBothSidesCountPerSide.Value);

            _settings.Condition1SpecialSingleSideCount =
                Convert.ToInt32(
                    numCondition1SpecialSingleSideCount.Value);

            _settings.Condition1SideDetectionToleranceMm =
                Convert.ToDouble(
                    numCondition1SideDetectionTolerance.Value);

            // 특수 보는 벽체 유무와 관계없이 생성한다.
            _settings.Condition1ExcludeWhenWallTouches = false;

            _settings.EnableCondition2 =
                chkEnableCondition2.Checked;

            _settings.Condition2TypeNameKeywords =
                NormalizeListText(txtCondition2TypeNameKeywords.Text);

            _settings.Condition2OffsetMm =
                Convert.ToDouble(numCondition2Offset.Value);

            _settings.EnableCondition3 =
                chkEnableCondition3.Checked;

            _settings.Condition3StandardNames =
                NormalizeListText(txtCondition3Names.Text);

            _settings.Condition3IntervalMm =
                Convert.ToDouble(numCondition3Interval.Value);

            _settings.UseExistingColumnsAsSupports =
                chkUseExistingColumns.Checked;

            _settings.UseWallsAsSupports =
                chkUseWalls.Checked;

            // 이전 설정 XML과의 호환을 위해 함께 저장함.
            _settings.Condition3WallExclusionMode =
                _settings.UseWallsAsSupports
                    ? JackSupportWallExclusionMode.EntireBeam
                    : JackSupportWallExclusionMode.None;

            _settings.ExcludeWallAtCondition3Point =
                _settings.UseWallsAsSupports;

            _settings.WallTouchToleranceMm =
                Convert.ToDouble(numWallTolerance.Value);

            _settings.WallHorizontalExtraMm =
                Convert.ToDouble(numWallHorizontalExtra.Value);

            _settings.IncludeFloorsAsLowerSupports =
                chkIncludeFloors.Checked;

            _settings.IncludeStructuralFoundationsAsLowerSupports =
                chkIncludeFoundations.Checked;

            _settings.StructuralFoundationStandardNames =
                NormalizeListText(txtFoundationNames.Text);

            _settings.LowerSupportSearchDepthMm =
                Convert.ToDouble(numLowerSupportSearchDepth.Value);

            _settings.EnableBoundaryLowerSupportSearch =
                chkEnableBoundaryLowerSupportSearch.Checked;

            _settings.BoundarySearchMaximumDistanceMm =
                Convert.ToDouble(
                    numBoundarySearchMaximumDistance.Value);

            _settings.BoundarySearchStepMm =
                Convert.ToDouble(
                    numBoundarySearchStep.Value);

            _settings.BoundarySupportTopDifferenceToleranceMm =
                Convert.ToDouble(
                    numBoundarySupportTopTolerance.Value);

            _settings.MoveSupportToBoundaryFoundPoint =
                chkMoveSupportToBoundaryFoundPoint.Checked;

            _settings.EnableLowestFloorClassification =
                chkEnableLowestFloorClassification.Checked;

            _settings.ActualLowestLevelNames =
                GetSelectedActualLowestLevelNames();

            _settings.ActualLowestLevelElevationToleranceMm =
                Convert.ToDouble(
                    numActualLowestLevelTolerance.Value);

            _settings.OtherFloorMarkerStandardNames =
                NormalizeListText(
                    txtLowestFloorFoundationNames.Text);

            _settings.OtherFloorMarkerSuffixes =
                NormalizeListText(
                    txtLowestFloorFoundationSuffixes.Text);

            // 구버전 XML 호환용 속성도 같은 값으로 유지한다.
            _settings.LowestFloorFoundationStandardNames =
                _settings.OtherFloorMarkerStandardNames;

            _settings.LowestFloorFoundationSuffixes =
                _settings.OtherFloorMarkerSuffixes;

            _settings.LowestFloorTouchToleranceMm =
                Convert.ToDouble(numLowestFloorTouchTolerance.Value);

            _settings.FloorClassificationParameterName =
                txtFloorClassificationParameterName.Text.Trim();

            _settings.LowestFloorClassificationValue =
                txtLowestFloorClassificationValue.Text.Trim();

            _settings.OtherFloorClassificationValue =
                txtOtherFloorClassificationValue.Text.Trim();

            _settings.EnableFloorClassificationCountParameters =
                chkEnableFloorClassificationCountParameters.Checked;

            _settings.ResetFloorClassificationCountParametersBeforeApply =
                chkResetFloorClassificationCountParameters.Checked;

            _settings.LowestFloorCountParameterName =
                txtLowestFloorCountParameterName.Text.Trim();

            _settings.OtherFloorCountParameterName =
                txtOtherFloorCountParameterName.Text.Trim();

            _settings.ExistingColumnTouchToleranceMm =
                Convert.ToDouble(numColumnTolerance.Value);

            _settings.DuplicatePointToleranceMm =
                Convert.ToDouble(numDuplicateTolerance.Value);

            _settings.DuplicateVerticalToleranceMm =
                Convert.ToDouble(numDuplicateVerticalTolerance.Value);

            _settings.UseActiveViewOnly =
                chkUseActiveViewOnly.Checked;

            _settings.EnableHeightParameterRules =
                chkEnableHeightParameterRules.Checked;

            _settings.ResetHeightRuleParametersBeforeApply =
                chkResetHeightRuleParameters.Checked;

            _settings.HeightRuleRoundingMm =
                Convert.ToDouble(numHeightRuleRounding.Value);

            _settings.HeightParameterRules =
                ReadHeightParameterRuleRows();

            _settings.EnableViewColorOverride =
                chkEnableViewColorOverride.Checked;

            _settings.ApplyColorToExistingSupports =
                chkApplyColorToExistingSupports.Checked;

            _settings.ViewColorRed =
                ClampColorComponent(selectedViewColorRed);

            _settings.ViewColorGreen =
                ClampColorComponent(selectedViewColorGreen);

            _settings.ViewColorBlue =
                ClampColorComponent(selectedViewColorBlue);

            _settings.ColorClassificationMode =
                cboColorClassificationMode != null &&
                cboColorClassificationMode.SelectedIndex == 1
                    ? JackSupportColorClassificationMode.HeightParameterRule
                    : JackSupportColorClassificationMode.FloorClassification;

            _settings.UseSeparateFloorColors =
                chkUseSeparateFloorColors.Checked;

            _settings.LowestFloorColorRed =
                ClampColorComponent(selectedLowestFloorColorRed);

            _settings.LowestFloorColorGreen =
                ClampColorComponent(selectedLowestFloorColorGreen);

            _settings.LowestFloorColorBlue =
                ClampColorComponent(selectedLowestFloorColorBlue);

            _settings.OtherFloorColorRed =
                ClampColorComponent(selectedOtherFloorColorRed);

            _settings.OtherFloorColorGreen =
                ClampColorComponent(selectedOtherFloorColorGreen);

            _settings.OtherFloorColorBlue =
                ClampColorComponent(selectedOtherFloorColorBlue);

            _settings.EnableUnmatchedHeightColor =
                chkEnableUnmatchedHeightColor.Checked;

            _settings.UnmatchedHeightColorRed =
                ClampColorComponent(selectedUnmatchedHeightColorRed);

            _settings.UnmatchedHeightColorGreen =
                ClampColorComponent(selectedUnmatchedHeightColorGreen);

            _settings.UnmatchedHeightColorBlue =
                ClampColorComponent(selectedUnmatchedHeightColorBlue);

            _settings.EnableBtsColumnBasedOutline =
                chkEnableBtsColumnBasedOutline.Checked;

            _settings.BtsColumnBasedOutlineRed =
                ClampColorComponent(
                    selectedBtsColumnOutlineColorRed);

            _settings.BtsColumnBasedOutlineGreen =
                ClampColorComponent(
                    selectedBtsColumnOutlineColorGreen);

            _settings.BtsColumnBasedOutlineBlue =
                ClampColorComponent(
                    selectedBtsColumnOutlineColorBlue);

            _settings.BtsColumnBasedOutlineLineWeight =
                Convert.ToInt32(
                    numBtsColumnBasedOutlineLineWeight.Value);
        }

        private void ValidateSettings(
            JackSupportSettingsAction action)
        {
            bool hasFamily =
                !string.IsNullOrWhiteSpace(
                    _settings.SourceRoundColumnFamilyName);

            bool hasType =
                !string.IsNullOrWhiteSpace(
                    _settings.SourceRoundColumnTypeName);

            if (hasFamily != hasType)
            {
                throw new InvalidOperationException(
                    "지정 패밀리를 직접 사용할 때는 패밀리명과 유형명을 모두 선택하거나 입력해 주십시오.");
            }

            if (!hasFamily &&
                string.IsNullOrWhiteSpace(
                    _settings.GeneratedTypeName))
            {
                throw new InvalidOperationException(
                    "기존 잭서포트를 식별하려면 자동 생성 유형명 또는 지정 패밀리/유형이 필요합니다.");
            }

            bool validateGeneration =
                action == JackSupportSettingsAction.Generate ||
                action == JackSupportSettingsAction.SaveOnly;

            bool validateJudgment =
                action != JackSupportSettingsAction.ApplyUniformColor;

            if (validateGeneration)
            {
                if (!_settings.EnableCondition1 &&
                    !_settings.EnableCondition2 &&
                    !_settings.EnableCondition3)
                {
                    throw new InvalidOperationException(
                        "특수 보 잭서포트, Drop Caps 기둥 잭서포트, RC보하부 잭서포트 중 하나 이상을 사용으로 설정해 주십시오.");
                }

                if (_settings.EnableCondition1)
                {
                    IList<string> condition1ANames =
                        _settings.GetCondition1Names();

                    IList<string> condition1BNames =
                        _settings.GetCondition1BNames();

                    if (condition1ANames.Count == 0 &&
                        condition1BNames.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "특수 보 잭서포트의 A 대상 또는 B 대상 표준부재명을 하나 이상 입력해 주십시오.");
                    }

                    IList<string> duplicatedNames =
                        condition1ANames
                            .Where(name => condition1BNames.Any(other =>
                                string.Equals(
                                    name,
                                    other,
                                    StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                    if (duplicatedNames.Count > 0)
                    {
                        throw new InvalidOperationException(
                            "특수 보 잭서포트의 A 대상과 B 대상에 같은 표준부재명이 중복되어 있습니다: " +
                            string.Join(", ", duplicatedNames));
                    }

                    if (_settings.GetCondition1ColumnFallbackNames().Count == 0)
                    {
                        throw new InvalidOperationException(
                            "특수 보 하부 바닥·기초가 없을 때 사용할 단부 기둥 표준부재명을 하나 이상 입력해 주십시오.");
                    }

                    if (_settings.GetCondition1SpecialBeamNames().Count == 0 ||
                        _settings.GetCondition1SideMemberNames().Count == 0)
                    {
                        throw new InvalidOperationException(
                            "특수 대상 보/측면 접촉 부재 특수 생성 규칙의 대상 표준부재명을 입력해 주십시오.");
                    }
                }

                if (_settings.EnableCondition3)
                {
                    if (_settings.GetCondition3Names().Count == 0)
                    {
                        throw new InvalidOperationException(
                            "RC보하부 잭서포트의 대상 표준부재명을 하나 이상 입력해 주십시오.");
                    }

                    if (_settings.Condition3IntervalMm <= 0.0)
                    {
                        throw new InvalidOperationException(
                            "RC보하부 잭서포트의 남은 구간 기준 간격은 0보다 커야 합니다.");
                    }
                }

                if (_settings.EnableCondition3 &&
                    !_settings.IncludeFloorsAsLowerSupports &&
                    !_settings.IncludeStructuralFoundationsAsLowerSupports)
                {
                    throw new InvalidOperationException(
                        "RC보하부 잭서포트를 사용할 때는 하부 지지체에서 바닥 또는 구조기초를 하나 이상 선택해야 합니다.");
                }

                if (_settings.EnableCondition1 &&
                    !_settings.IncludeFloorsAsLowerSupports &&
                    !_settings.IncludeStructuralFoundationsAsLowerSupports &&
                    _settings.GetCondition1ColumnFallbackNames().Count == 0)
                {
                    throw new InvalidOperationException(
                        "특수 보 잭서포트는 바닥·구조기초 또는 단부 기둥 기준 중 하나가 필요합니다.");
                }

                if (_settings.IncludeStructuralFoundationsAsLowerSupports &&
                    _settings.GetStructuralFoundationNames().Count == 0)
                {
                    throw new InvalidOperationException(
                        "구조기초를 사용할 경우 구조기초 표준부재명을 하나 이상 입력해 주십시오.");
                }

                if (_settings.EnableBoundaryLowerSupportSearch)
                {
                    if (_settings.BoundarySearchMaximumDistanceMm <= 0.0)
                    {
                        throw new InvalidOperationException(
                            "슬래브 경계부 주변 검색의 최대거리는 0보다 커야 합니다.");
                    }

                    if (_settings.BoundarySearchStepMm <= 0.0 ||
                        _settings.BoundarySearchStepMm >
                            _settings.BoundarySearchMaximumDistanceMm)
                    {
                        throw new InvalidOperationException(
                            "슬래브 경계부 검색 간격은 0보다 크고 최대 검색거리 이하여야 합니다.");
                    }
                }
            }

            if (validateJudgment &&
                _settings.EnableCondition2 &&
                _settings.GetCondition2FamilyNameKeywords().Count == 0)
            {
                throw new InvalidOperationException(
                    "Drop Caps 기둥 4개 세트 판정을 위해 패밀리명 포함 문구를 하나 이상 입력해 주십시오.");
            }

            if (validateJudgment &&
                _settings.EnableLowestFloorClassification)
            {
                if (_settings.GetActualLowestLevelNames().Count == 0)
                {
                    throw new InvalidOperationException(
                        "최하층 구분을 사용할 경우 실제 최하층 레벨을 하나 이상 선택해 주십시오.");
                }

                if (_settings.GetOtherFloorMarkerNames().Count == 0 &&
                    _settings.GetOtherFloorMarkerSuffixes().Count == 0)
                {
                    throw new InvalidOperationException(
                        "최하층 구분을 사용할 경우 그외층 판정 표준부재명 또는 접미어를 하나 이상 입력해 주십시오.");
                }

                if (string.IsNullOrWhiteSpace(
                    _settings.FloorClassificationParameterName))
                {
                    throw new InvalidOperationException(
                        "최하층 구분 값을 입력할 인스턴스 매개변수명을 입력해 주십시오.");
                }

                if (string.IsNullOrWhiteSpace(
                        _settings.LowestFloorClassificationValue) ||
                    string.IsNullOrWhiteSpace(
                        _settings.OtherFloorClassificationValue))
                {
                    throw new InvalidOperationException(
                        "최하층 입력값과 그외층 입력값을 모두 입력해 주십시오.");
                }

                if (_settings.EnableFloorClassificationCountParameters)
                {
                    if (string.IsNullOrWhiteSpace(
                            _settings.LowestFloorCountParameterName) ||
                        string.IsNullOrWhiteSpace(
                            _settings.OtherFloorCountParameterName))
                    {
                        throw new InvalidOperationException(
                            "최하층/그외층 수량 입력을 사용할 경우 두 수량 인스턴스 매개변수명을 모두 입력해 주십시오.");
                    }

                    if (string.Equals(
                        _settings.LowestFloorCountParameterName,
                        _settings.OtherFloorCountParameterName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "최하층 수량 매개변수와 그외층 수량 매개변수는 서로 다른 이름이어야 합니다.");
                    }
                }
            }

            if (validateJudgment &&
                _settings.EnableViewColorOverride &&
                _settings.ColorClassificationMode ==
                    JackSupportColorClassificationMode.FloorClassification &&
                _settings.UseSeparateFloorColors &&
                !_settings.EnableLowestFloorClassification)
            {
                throw new InvalidOperationException(
                    "최하층/그외층 색상 분리 적용을 사용하려면 [최하층 구분] 탭의 기능을 체크해 주십시오.");
            }

            if (validateJudgment)
            {
                ValidateHeightParameterRules();

                if (_settings.EnableViewColorOverride &&
                    _settings.ColorClassificationMode ==
                        JackSupportColorClassificationMode.HeightParameterRule &&
                    !_settings.EnableHeightParameterRules)
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 색상을 적용하려면 [높이별 데이터] 탭의 기능을 사용으로 설정해 주십시오.");
                }
            }
        }

        private void UpdateEnabledStates()
        {
            bool directFamilyMode =
                !string.IsNullOrWhiteSpace(cboSourceFamilyName.Text) ||
                !string.IsNullOrWhiteSpace(cboSourceTypeName.Text);

            txtGeneratedTypeName.Enabled = !directFamilyMode;
            numDiameter.Enabled = !directFamilyMode;
            txtDiameterParameterName.Enabled = !directFamilyMode;

            bool condition1Enabled = chkEnableCondition1.Checked;
            txtCondition1Names.Enabled = condition1Enabled;
            txtCondition1Ratios.Enabled = condition1Enabled;
            txtCondition1BNames.Enabled = condition1Enabled;
            txtCondition1BRatios.Enabled = condition1Enabled;
            txtCondition1ColumnFallbackNames.Enabled = condition1Enabled;
            numCondition1ColumnTouchTolerance.Enabled = condition1Enabled;
            txtCondition1SpecialBeamNames.Enabled = condition1Enabled;
            txtCondition1SideMemberNames.Enabled = condition1Enabled;
            numCondition1SpecialNoSideCount.Enabled = condition1Enabled;
            numCondition1SpecialBothSidesCountPerSide.Enabled = condition1Enabled;
            numCondition1SpecialSingleSideCount.Enabled = condition1Enabled;
            numCondition1SideDetectionTolerance.Enabled = condition1Enabled;

            bool condition2Enabled = chkEnableCondition2.Checked;
            txtCondition2TypeNameKeywords.Enabled = condition2Enabled;
            numCondition2Offset.Enabled = condition2Enabled;

            bool condition3Enabled = chkEnableCondition3.Checked;
            txtCondition3Names.Enabled = condition3Enabled;
            numCondition3Interval.Enabled = condition3Enabled;
            chkUseExistingColumns.Enabled = condition3Enabled;
            chkUseWalls.Enabled = condition3Enabled;

            bool wallEnabled =
                condition3Enabled &&
                chkUseWalls.Checked;

            numWallTolerance.Enabled = wallEnabled;
            numWallHorizontalExtra.Enabled = wallEnabled;

            txtFoundationNames.Enabled =
                chkIncludeFoundations.Checked;

            bool boundarySearchEnabled =
                chkEnableBoundaryLowerSupportSearch != null &&
                chkEnableBoundaryLowerSupportSearch.Checked;

            if (numBoundarySearchMaximumDistance != null)
                numBoundarySearchMaximumDistance.Enabled =
                    boundarySearchEnabled;

            if (numBoundarySearchStep != null)
                numBoundarySearchStep.Enabled =
                    boundarySearchEnabled;

            if (numBoundarySupportTopTolerance != null)
                numBoundarySupportTopTolerance.Enabled =
                    boundarySearchEnabled;

            if (chkMoveSupportToBoundaryFoundPoint != null)
                chkMoveSupportToBoundaryFoundPoint.Enabled =
                    boundarySearchEnabled;

            bool lowestFloorEnabled =
                chkEnableLowestFloorClassification != null &&
                chkEnableLowestFloorClassification.Checked;

            if (lstActualLowestLevels != null)
                lstActualLowestLevels.Enabled = lowestFloorEnabled;

            if (numActualLowestLevelTolerance != null)
                numActualLowestLevelTolerance.Enabled = lowestFloorEnabled;

            if (txtLowestFloorFoundationNames != null)
                txtLowestFloorFoundationNames.Enabled = lowestFloorEnabled;

            if (txtLowestFloorFoundationSuffixes != null)
                txtLowestFloorFoundationSuffixes.Enabled = lowestFloorEnabled;

            if (numLowestFloorTouchTolerance != null)
                numLowestFloorTouchTolerance.Enabled = lowestFloorEnabled;

            if (txtFloorClassificationParameterName != null)
                txtFloorClassificationParameterName.Enabled = lowestFloorEnabled;

            if (txtLowestFloorClassificationValue != null)
                txtLowestFloorClassificationValue.Enabled = lowestFloorEnabled;

            if (txtOtherFloorClassificationValue != null)
                txtOtherFloorClassificationValue.Enabled = lowestFloorEnabled;

            bool floorCountEnabled =
                lowestFloorEnabled &&
                chkEnableFloorClassificationCountParameters != null &&
                chkEnableFloorClassificationCountParameters.Checked;

            if (chkEnableFloorClassificationCountParameters != null)
                chkEnableFloorClassificationCountParameters.Enabled =
                    lowestFloorEnabled;

            if (chkResetFloorClassificationCountParameters != null)
                chkResetFloorClassificationCountParameters.Enabled =
                    floorCountEnabled;

            if (txtLowestFloorCountParameterName != null)
                txtLowestFloorCountParameterName.Enabled =
                    floorCountEnabled;

            if (txtOtherFloorCountParameterName != null)
                txtOtherFloorCountParameterName.Enabled =
                    floorCountEnabled;

            bool heightRuleEnabled =
                chkEnableHeightParameterRules.Checked;

            chkResetHeightRuleParameters.Enabled =
                heightRuleEnabled;

            numHeightRuleRounding.Enabled =
                heightRuleEnabled;

            gridHeightParameterRules.Enabled =
                heightRuleEnabled;

            btnAddHeightRule.Enabled =
                heightRuleEnabled;

            btnDeleteHeightRule.Enabled =
                heightRuleEnabled;

            bool colorEnabled =
                chkEnableViewColorOverride != null &&
                chkEnableViewColorOverride.Checked;

            bool heightColorMode =
                cboColorClassificationMode != null &&
                cboColorClassificationMode.SelectedIndex == 1;

            bool floorColorMode = !heightColorMode;

            bool separateFloorColors =
                colorEnabled &&
                floorColorMode &&
                chkUseSeparateFloorColors != null &&
                chkUseSeparateFloorColors.Checked;

            bool commonColorEnabled = colorEnabled;

            bool classifiedColorEnabled =
                separateFloorColors &&
                lowestFloorEnabled;

            if (cboColorClassificationMode != null)
                cboColorClassificationMode.Enabled = colorEnabled;

            if (chkApplyColorToExistingSupports != null)
                chkApplyColorToExistingSupports.Enabled = colorEnabled;

            if (chkUseSeparateFloorColors != null)
                chkUseSeparateFloorColors.Enabled =
                    colorEnabled && floorColorMode;

            if (btnChooseViewColor != null)
                btnChooseViewColor.Enabled = commonColorEnabled;

            if (pnlViewColorPreview != null)
                pnlViewColorPreview.Enabled = commonColorEnabled;

            if (lblViewColorRgb != null)
                lblViewColorRgb.Enabled = commonColorEnabled;

            if (btnChooseLowestFloorColor != null)
                btnChooseLowestFloorColor.Enabled = classifiedColorEnabled;

            if (pnlLowestFloorColorPreview != null)
                pnlLowestFloorColorPreview.Enabled = classifiedColorEnabled;

            if (lblLowestFloorColorRgb != null)
                lblLowestFloorColorRgb.Enabled = classifiedColorEnabled;

            if (btnChooseOtherFloorColor != null)
                btnChooseOtherFloorColor.Enabled = classifiedColorEnabled;

            if (pnlOtherFloorColorPreview != null)
                pnlOtherFloorColorPreview.Enabled = classifiedColorEnabled;

            if (lblOtherFloorColorRgb != null)
                lblOtherFloorColorRgb.Enabled = classifiedColorEnabled;

            bool unmatchedColorEnabled =
                colorEnabled &&
                heightColorMode &&
                heightRuleEnabled &&
                chkEnableUnmatchedHeightColor != null &&
                chkEnableUnmatchedHeightColor.Checked;

            if (chkEnableUnmatchedHeightColor != null)
                chkEnableUnmatchedHeightColor.Enabled =
                    colorEnabled &&
                    heightColorMode &&
                    heightRuleEnabled;

            if (btnChooseUnmatchedHeightColor != null)
                btnChooseUnmatchedHeightColor.Enabled = unmatchedColorEnabled;

            if (pnlUnmatchedHeightColorPreview != null)
                pnlUnmatchedHeightColorPreview.Enabled = unmatchedColorEnabled;

            if (lblUnmatchedHeightColorRgb != null)
                lblUnmatchedHeightColorRgb.Enabled = unmatchedColorEnabled;

            bool btsOutlineEnabled =
                colorEnabled &&
                chkEnableBtsColumnBasedOutline != null &&
                chkEnableBtsColumnBasedOutline.Checked;

            if (chkEnableBtsColumnBasedOutline != null)
                chkEnableBtsColumnBasedOutline.Enabled = colorEnabled;

            if (btnChooseBtsColumnOutlineColor != null)
                btnChooseBtsColumnOutlineColor.Enabled = btsOutlineEnabled;

            if (pnlBtsColumnOutlineColorPreview != null)
                pnlBtsColumnOutlineColorPreview.Enabled = btsOutlineEnabled;

            if (lblBtsColumnOutlineColorRgb != null)
                lblBtsColumnOutlineColorRgb.Enabled = btsOutlineEnabled;

            if (numBtsColumnBasedOutlineLineWeight != null)
                numBtsColumnBasedOutlineLineWeight.Enabled = btsOutlineEnabled;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (TrySaveSettings(
                JackSupportSettingsAction.SaveOnly,
                true))
            {
                RequestedAction =
                    JackSupportSettingsAction.SaveOnly;
            }
        }

        private void BtnRun_Click(object sender, EventArgs e)
        {
            if (!TrySaveSettings(
                JackSupportSettingsAction.Generate,
                false))
            {
                return;
            }

            RequestedAction = JackSupportSettingsAction.Generate;

            RaiseExternalRequest(
                new JackSupportExternalRequest
                {
                    RequestType =
                        JackSupportExternalRequestType.Generate
                });

            StartFamilySelectionRefreshTimer();
        }

        private void BtnApplyJudgmentColors_Click(
            object sender,
            EventArgs e)
        {
            if (!TrySaveSettings(
                JackSupportSettingsAction.ApplyJudgmentColors,
                false))
            {
                return;
            }

            RequestedAction =
                JackSupportSettingsAction.ApplyJudgmentColors;

            RaiseExternalRequest(
                new JackSupportExternalRequest
                {
                    RequestType =
                        JackSupportExternalRequestType
                            .ApplyJudgmentColors
                });
        }

        private void BtnApplyUniformColor_Click(
            object sender,
            EventArgs e)
        {
            if (!TrySaveSettings(
                JackSupportSettingsAction.ApplyUniformColor,
                false))
            {
                return;
            }

            RequestedAction =
                JackSupportSettingsAction.ApplyUniformColor;

            RaiseExternalRequest(
                new JackSupportExternalRequest
                {
                    RequestType =
                        JackSupportExternalRequestType
                            .ApplyUniformColor
                });
        }

        private void BtnSelectGeneratedSupports_Click(
            object sender,
            EventArgs e)
        {
            if (!TrySaveSettings(
                JackSupportSettingsAction.SaveOnly,
                false))
            {
                return;
            }

            RaiseExternalRequest(
                new JackSupportExternalRequest
                {
                    RequestType =
                        JackSupportExternalRequestType
                            .SelectSupports,
                    SelectionOptions =
                        BuildSelectionOptions()
                });
        }

        private void BtnIsolateGeneratedSupports_Click(
            object sender,
            EventArgs e)
        {
            if (!TrySaveSettings(
                JackSupportSettingsAction.SaveOnly,
                false))
            {
                return;
            }

            RaiseExternalRequest(
                new JackSupportExternalRequest
                {
                    RequestType =
                        JackSupportExternalRequestType
                            .IsolateSupports,
                    SelectionOptions =
                        BuildSelectionOptions()
                });
        }

        private void BtnRestoreGeneratedSupports_Click(
            object sender,
            EventArgs e)
        {
            RaiseExternalRequest(
                new JackSupportExternalRequest
                {
                    RequestType =
                        JackSupportExternalRequestType
                            .RestoreTemporaryIsolation
                });
        }

        private void BtnClose_Click(
            object sender,
            EventArgs e)
        {
            Close();
        }

        private void BtnLatestResult_Click(
            object sender,
            EventArgs e)
        {
            string resultText;

            if (!JackSupportResultStore.TryLoad(
                out resultText))
            {
                MessageBox.Show(
                    this,
                    "저장된 최신 실행 결과가 없습니다.\n\n" +
                    "먼저 잭서포트 자동 생성을 한 번 실행해 주십시오.",
                    "잭서포트 최신 결과",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            JackSupportResultDialog.Show(
                this,
                "잭서포트 최신 결과",
                resultText);
        }

        private bool TrySaveSettings(
            JackSupportSettingsAction action,
            bool showSuccessMessage)
        {
            try
            {
                ReadValuesFromControls();
                NormalizeIncompleteFamilySelectionForRfaLoad();
                ValidateSettings(action);
                JackSupportSettingsStore.Save(_settings);

                if (showSuccessMessage)
                {
                    MessageBox.Show(
                        this,
                        "설정을 저장했습니다.",
                        "잭서포트",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                string title;

                switch (action)
                {
                    case JackSupportSettingsAction.Generate:
                        title = "자동 생성 준비 오류";
                        break;

                    case JackSupportSettingsAction.ApplyJudgmentColors:
                        title = "판정색상 일괄 적용 준비 오류";
                        break;

                    case JackSupportSettingsAction.ApplyUniformColor:
                        title = "기존 잭서포트 처리 준비 오류";
                        break;

                    default:
                        title = "설정 저장 오류";
                        break;
                }

                MessageBox.Show(
                    this,
                    ex.Message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }
        }

        private void RaiseExternalRequest(
            JackSupportExternalRequest request)
        {
            if (_externalEventHandler == null ||
                _externalEvent == null)
            {
                MessageBox.Show(
                    this,
                    "현재 옵션창은 모델리스 실행기로 열리지 않았습니다.\n\n" +
                    "창을 닫고 Revit 리본의 잭서포트 버튼으로 다시 열어 주십시오.",
                    "잭서포트",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            _externalEventHandler.SetRequest(request);
            _externalEvent.Raise();
        }

        private JackSupportSelectionOptions BuildSelectionOptions()
        {
            JackSupportSelectionOptions options =
                new JackSupportSelectionOptions();

            options.IncludeBtsBeamSupports =
                chkSelectBtsBeamSupports != null &&
                chkSelectBtsBeamSupports.Checked;

            options.IncludeRcBeamSupports =
                chkSelectRcBeamSupports != null &&
                chkSelectRcBeamSupports.Checked;

            options.IncludePcColumnSupports =
                chkSelectPcColumnSupports != null &&
                chkSelectPcColumnSupports.Checked;

            options.IncludeRcColumnSupports =
                chkSelectRcColumnSupports != null &&
                chkSelectRcColumnSupports.Checked;

            options.IncludeOtherSupports =
                chkSelectOtherSupports != null &&
                chkSelectOtherSupports.Checked;

            return options;
        }

        protected override bool ProcessCmdKey(
            ref Message message,
            Keys keyData)
        {
            if ((keyData & Keys.KeyCode) == Keys.Enter)
            {
                TextBoxBase textBox =
                    ActiveControl as TextBoxBase;

                if (textBox != null && textBox.Multiline)
                {
                    return base.ProcessCmdKey(
                        ref message,
                        keyData);
                }

                // Enter로 자동 생성 버튼이 실행되지 않도록 소비한다.
                return true;
            }

            return base.ProcessCmdKey(
                ref message,
                keyData);
        }

        private void CboSourceFamilyName_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            string previousType =
                cboSourceTypeName == null
                    ? string.Empty
                    : cboSourceTypeName.Text;

            PopulateTypeItems(
                cboSourceFamilyName.Text,
                previousType);

            SelectFirstLoadedTypeWhenNeeded();

            UpdateEnabledStates();
        }

        private void FamilySelectionChanged(
            object sender,
            EventArgs e)
        {
            UpdateEnabledStates();
        }

        private void BtnClearFamilySelection_Click(
            object sender,
            EventArgs e)
        {
            cboSourceFamilyName.Text = string.Empty;
            PopulateTypeItems(string.Empty, string.Empty);
            UpdateEnabledStates();
        }

        private void BtnBrowseRfa_Click(
            object sender,
            EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "잭서포트 구조기둥 패밀리 선택";
                dialog.Filter =
                    "Revit Family (*.rfa)|*.rfa|모든 파일 (*.*)|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                string currentPath = txtFamilyRfaPath.Text.Trim();

                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    try
                    {
                        string directory =
                            Path.GetDirectoryName(currentPath);

                        if (!string.IsNullOrWhiteSpace(directory) &&
                            Directory.Exists(directory))
                        {
                            dialog.InitialDirectory = directory;
                        }

                        dialog.FileName =
                            Path.GetFileName(currentPath);
                    }
                    catch
                    {
                        // 현재 경로가 잘못되어도 찾아보기는 계속 진행함.
                    }
                }

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                txtFamilyRfaPath.Text = dialog.FileName;

                if (string.IsNullOrWhiteSpace(
                    cboSourceFamilyName.Text))
                {
                    cboSourceFamilyName.Text =
                        Path.GetFileNameWithoutExtension(
                            dialog.FileName);
                }

                PopulateTypeItems(
                    cboSourceFamilyName.Text,
                    cboSourceTypeName.Text);

                SelectFirstLoadedTypeWhenNeeded();

                UpdateEnabledStates();
            }
        }

        private void ConditionCheckChanged(
            object sender,
            EventArgs e)
        {
            UpdateEnabledStates();
        }

        private void LowerSupportCheckChanged(
            object sender,
            EventArgs e)
        {
            UpdateEnabledStates();
        }


        private void LoadHeightParameterRuleRows()
        {
            gridHeightParameterRules.Rows.Clear();

            if (_settings.HeightParameterRules == null)
                return;

            foreach (JackSupportHeightParameterRule rule in
                _settings.HeightParameterRules)
            {
                if (rule == null)
                    continue;

                int rowIndex =
                    gridHeightParameterRules.Rows.Add(
                        rule.MinimumHeightMm,
                        rule.MaximumHeightMm,
                        rule.ParameterName,
                        rule.Value,
                        BuildRgbText(
                            rule.ColorRed,
                            rule.ColorGreen,
                            rule.ColorBlue),
                        rule.ColorRed,
                        rule.ColorGreen,
                        rule.ColorBlue);

                SetHeightRuleRowColor(
                    gridHeightParameterRules.Rows[rowIndex],
                    rule.ColorRed,
                    rule.ColorGreen,
                    rule.ColorBlue);
            }
        }

        private List<JackSupportHeightParameterRule>
            ReadHeightParameterRuleRows()
        {
            List<JackSupportHeightParameterRule> rules =
                new List<JackSupportHeightParameterRule>();

            foreach (DataGridViewRow row in
                gridHeightParameterRules.Rows)
            {
                if (row == null || row.IsNewRow)
                    continue;

                string minimumText =
                    Convert.ToString(
                        row.Cells["MinimumHeightMm"].Value).Trim();

                string maximumText =
                    Convert.ToString(
                        row.Cells["MaximumHeightMm"].Value).Trim();

                string parameterName =
                    Convert.ToString(
                        row.Cells["ParameterName"].Value).Trim();

                string valueText =
                    Convert.ToString(
                        row.Cells["Value"].Value).Trim();

                bool completelyEmpty =
                    string.IsNullOrWhiteSpace(minimumText) &&
                    string.IsNullOrWhiteSpace(maximumText) &&
                    string.IsNullOrWhiteSpace(parameterName) &&
                    string.IsNullOrWhiteSpace(valueText);

                if (completelyEmpty)
                    continue;

                double minimum;
                double maximum;
                double value;

                if (!double.TryParse(minimumText, out minimum))
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 최소 높이는 숫자로 입력해 주십시오.");
                }

                if (!double.TryParse(maximumText, out maximum))
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 최대 높이는 숫자로 입력해 주십시오.");
                }

                if (string.IsNullOrWhiteSpace(valueText))
                {
                    value = 1.0;
                }
                else if (!double.TryParse(valueText, out value))
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 입력값은 숫자로 입력해 주십시오.");
                }

                int colorRed = ReadGridColorComponent(
                    row,
                    "ColorRed",
                    0);

                int colorGreen = ReadGridColorComponent(
                    row,
                    "ColorGreen",
                    120);

                int colorBlue = ReadGridColorComponent(
                    row,
                    "ColorBlue",
                    255);

                rules.Add(
                    new JackSupportHeightParameterRule
                    {
                        MinimumHeightMm = minimum,
                        MaximumHeightMm = maximum,
                        ParameterName = parameterName,
                        Value = value,
                        ColorRed = colorRed,
                        ColorGreen = colorGreen,
                        ColorBlue = colorBlue
                    });
            }

            return rules;
        }

        private void ValidateHeightParameterRules()
        {
            if (!_settings.EnableHeightParameterRules)
                return;

            IList<JackSupportHeightParameterRule> rules =
                _settings.GetValidHeightParameterRules();

            if (rules.Count == 0)
            {
                throw new InvalidOperationException(
                    "높이별 데이터 기능을 사용할 경우 높이 구간 규칙을 하나 이상 입력해 주십시오.");
            }

            foreach (JackSupportHeightParameterRule rule in
                _settings.HeightParameterRules)
            {
                if (rule == null)
                    continue;

                if (rule.MinimumHeightMm < 0.0)
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 최소 높이는 0 이상이어야 합니다.");
                }

                if (rule.MaximumHeightMm <=
                    rule.MinimumHeightMm)
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 최대 높이는 최소 높이보다 커야 합니다.");
                }

                if (string.IsNullOrWhiteSpace(
                    rule.ParameterName))
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 매개변수명을 입력해 주십시오.");
                }
            }

            List<JackSupportHeightParameterRule> ordered =
                rules
                    .OrderBy(rule => rule.MinimumHeightMm)
                    .ThenBy(rule => rule.MaximumHeightMm)
                    .ToList();

            for (int i = 1; i < ordered.Count; i++)
            {
                JackSupportHeightParameterRule previous =
                    ordered[i - 1];

                JackSupportHeightParameterRule current =
                    ordered[i];

                if (current.MinimumHeightMm <
                    previous.MaximumHeightMm)
                {
                    throw new InvalidOperationException(
                        "높이별 데이터 규칙의 구간이 서로 겹칩니다.\n\n" +
                        previous.MinimumHeightMm + " ~ " +
                        previous.MaximumHeightMm + "mm\n" +
                        current.MinimumHeightMm + " ~ " +
                        current.MaximumHeightMm + "mm");
                }
            }
        }

        private void BtnAddHeightRule_Click(
            object sender,
            EventArgs e)
        {
            double minimum = 0.0;

            if (gridHeightParameterRules.Rows.Count > 0)
            {
                DataGridViewRow lastRow =
                    gridHeightParameterRules.Rows[
                        gridHeightParameterRules.Rows.Count - 1];

                double previousMaximum;

                if (double.TryParse(
                    Convert.ToString(
                        lastRow.Cells["MaximumHeightMm"].Value),
                    out previousMaximum))
                {
                    minimum = previousMaximum;
                }
            }

            System.Drawing.Color defaultColor =
                GetDefaultHeightRuleColor(
                    gridHeightParameterRules.Rows.Count);

            int rowIndex =
                gridHeightParameterRules.Rows.Add(
                    minimum,
                    minimum + 1000.0,
                    string.Empty,
                    1.0,
                    BuildRgbText(
                        defaultColor.R,
                        defaultColor.G,
                        defaultColor.B),
                    defaultColor.R,
                    defaultColor.G,
                    defaultColor.B);

            SetHeightRuleRowColor(
                gridHeightParameterRules.Rows[rowIndex],
                defaultColor.R,
                defaultColor.G,
                defaultColor.B);

            gridHeightParameterRules.CurrentCell =
                gridHeightParameterRules.Rows[rowIndex]
                    .Cells["ParameterName"];

            gridHeightParameterRules.BeginEdit(true);
        }

        private void BtnDeleteHeightRule_Click(
            object sender,
            EventArgs e)
        {
            List<DataGridViewRow> selectedRows =
                gridHeightParameterRules.SelectedRows
                    .Cast<DataGridViewRow>()
                    .OrderByDescending(row => row.Index)
                    .ToList();

            if (selectedRows.Count == 0 &&
                gridHeightParameterRules.CurrentRow != null)
            {
                selectedRows.Add(
                    gridHeightParameterRules.CurrentRow);
            }

            foreach (DataGridViewRow row in selectedRows)
            {
                if (!row.IsNewRow)
                    gridHeightParameterRules.Rows.Remove(row);
            }
        }

        private void BtnAddHeightRuleExamples_Click(
            object sender,
            EventArgs e)
        {
            int firstRow =
                gridHeightParameterRules.Rows.Add(
                    4000.0,
                    5000.0,
                    "ABCD",
                    1.0,
                    BuildRgbText(0, 120, 255),
                    0,
                    120,
                    255);

            SetHeightRuleRowColor(
                gridHeightParameterRules.Rows[firstRow],
                0,
                120,
                255);

            int secondRow =
                gridHeightParameterRules.Rows.Add(
                    5000.0,
                    6000.0,
                    "FGHI",
                    1.0,
                    BuildRgbText(0, 180, 120),
                    0,
                    180,
                    120);

            SetHeightRuleRowColor(
                gridHeightParameterRules.Rows[secondRow],
                0,
                180,
                120);
        }

        private void GridHeightParameterRules_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 ||
                e.ColumnIndex < 0 ||
                gridHeightParameterRules.Columns[e.ColumnIndex].Name !=
                    "RuleColor")
            {
                return;
            }

            DataGridViewRow row =
                gridHeightParameterRules.Rows[e.RowIndex];

            int red = ReadGridColorComponent(
                row,
                "ColorRed",
                0);
            int green = ReadGridColorComponent(
                row,
                "ColorGreen",
                120);
            int blue = ReadGridColorComponent(
                row,
                "ColorBlue",
                255);

            ChooseColor(
                ref red,
                ref green,
                ref blue);

            row.Cells["ColorRed"].Value = red;
            row.Cells["ColorGreen"].Value = green;
            row.Cells["ColorBlue"].Value = blue;

            SetHeightRuleRowColor(
                row,
                red,
                green,
                blue);
        }

        private static int ReadGridColorComponent(
            DataGridViewRow row,
            string columnName,
            int defaultValue)
        {
            if (row == null ||
                string.IsNullOrWhiteSpace(columnName) ||
                !row.DataGridView.Columns.Contains(columnName))
            {
                return ClampColorComponent(defaultValue);
            }

            int value;

            if (!int.TryParse(
                Convert.ToString(
                    row.Cells[columnName].Value),
                out value))
            {
                value = defaultValue;
            }

            return ClampColorComponent(value);
        }

        private static void SetHeightRuleRowColor(
            DataGridViewRow row,
            int red,
            int green,
            int blue)
        {
            if (row == null ||
                row.DataGridView == null ||
                !row.DataGridView.Columns.Contains("RuleColor"))
            {
                return;
            }

            red = ClampColorComponent(red);
            green = ClampColorComponent(green);
            blue = ClampColorComponent(blue);

            DataGridViewCell cell =
                row.Cells["RuleColor"];

            cell.Value = BuildRgbText(
                red,
                green,
                blue);

            cell.Style.BackColor =
                System.Drawing.Color.FromArgb(
                    red,
                    green,
                    blue);

            double brightness =
                red * 0.299 +
                green * 0.587 +
                blue * 0.114;

            cell.Style.ForeColor =
                brightness < 140.0
                    ? System.Drawing.Color.White
                    : System.Drawing.Color.Black;
        }

        private static string BuildRgbText(
            int red,
            int green,
            int blue)
        {
            return
                "RGB " +
                ClampColorComponent(red) + "," +
                ClampColorComponent(green) + "," +
                ClampColorComponent(blue);
        }

        private static System.Drawing.Color
            GetDefaultHeightRuleColor(
                int index)
        {
            System.Drawing.Color[] colors =
            {
                System.Drawing.Color.FromArgb(0, 120, 255),
                System.Drawing.Color.FromArgb(0, 180, 120),
                System.Drawing.Color.FromArgb(255, 170, 0),
                System.Drawing.Color.FromArgb(180, 90, 255),
                System.Drawing.Color.FromArgb(255, 90, 120)
            };

            int normalizedIndex = Math.Abs(index) % colors.Length;
            return colors[normalizedIndex];
        }

        private void HeightParameterRuleControlChanged(
            object sender,
            EventArgs e)
        {
            UpdateEnabledStates();
        }

        private void BtnChooseViewColor_Click(
            object sender,
            EventArgs e)
        {
            ChooseColor(
                ref selectedViewColorRed,
                ref selectedViewColorGreen,
                ref selectedViewColorBlue);

            UpdateViewColorPreviews();
        }

        private void BtnChooseLowestFloorColor_Click(
            object sender,
            EventArgs e)
        {
            ChooseColor(
                ref selectedLowestFloorColorRed,
                ref selectedLowestFloorColorGreen,
                ref selectedLowestFloorColorBlue);

            UpdateViewColorPreviews();
        }

        private void BtnChooseOtherFloorColor_Click(
            object sender,
            EventArgs e)
        {
            ChooseColor(
                ref selectedOtherFloorColorRed,
                ref selectedOtherFloorColorGreen,
                ref selectedOtherFloorColorBlue);

            UpdateViewColorPreviews();
        }

        private void BtnChooseUnmatchedHeightColor_Click(
            object sender,
            EventArgs e)
        {
            ChooseColor(
                ref selectedUnmatchedHeightColorRed,
                ref selectedUnmatchedHeightColorGreen,
                ref selectedUnmatchedHeightColorBlue);

            UpdateViewColorPreviews();
        }

        private void BtnChooseBtsColumnOutlineColor_Click(
            object sender,
            EventArgs e)
        {
            ChooseColor(
                ref selectedBtsColumnOutlineColorRed,
                ref selectedBtsColumnOutlineColorGreen,
                ref selectedBtsColumnOutlineColorBlue);

            UpdateViewColorPreviews();
        }

        private void ChooseColor(
            ref int red,
            ref int green,
            ref int blue)
        {
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.AnyColor = true;
                dialog.SolidColorOnly = false;

                dialog.Color =
                    System.Drawing.Color.FromArgb(
                        ClampColorComponent(red),
                        ClampColorComponent(green),
                        ClampColorComponent(blue));

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                red = dialog.Color.R;
                green = dialog.Color.G;
                blue = dialog.Color.B;
            }
        }

        private void UpdateViewColorPreviews()
        {
            UpdateColorPreview(
                pnlViewColorPreview,
                lblViewColorRgb,
                ref selectedViewColorRed,
                ref selectedViewColorGreen,
                ref selectedViewColorBlue);

            UpdateColorPreview(
                pnlLowestFloorColorPreview,
                lblLowestFloorColorRgb,
                ref selectedLowestFloorColorRed,
                ref selectedLowestFloorColorGreen,
                ref selectedLowestFloorColorBlue);

            UpdateColorPreview(
                pnlOtherFloorColorPreview,
                lblOtherFloorColorRgb,
                ref selectedOtherFloorColorRed,
                ref selectedOtherFloorColorGreen,
                ref selectedOtherFloorColorBlue);

            UpdateColorPreview(
                pnlUnmatchedHeightColorPreview,
                lblUnmatchedHeightColorRgb,
                ref selectedUnmatchedHeightColorRed,
                ref selectedUnmatchedHeightColorGreen,
                ref selectedUnmatchedHeightColorBlue);

            UpdateColorPreview(
                pnlBtsColumnOutlineColorPreview,
                lblBtsColumnOutlineColorRgb,
                ref selectedBtsColumnOutlineColorRed,
                ref selectedBtsColumnOutlineColorGreen,
                ref selectedBtsColumnOutlineColorBlue);
        }

        private static void UpdateColorPreview(
            System.Windows.Forms.Panel previewPanel,
            Label rgbLabel,
            ref int red,
            ref int green,
            ref int blue)
        {
            red = ClampColorComponent(red);
            green = ClampColorComponent(green);
            blue = ClampColorComponent(blue);

            if (previewPanel != null)
            {
                previewPanel.BackColor =
                    System.Drawing.Color.FromArgb(
                        red,
                        green,
                        blue);
            }

            if (rgbLabel != null)
            {
                rgbLabel.Text =
                    "RGB " +
                    red + ", " +
                    green + ", " +
                    blue;
            }
        }

        private void ViewColorControlChanged(
            object sender,
            EventArgs e)
        {
            UpdateEnabledStates();
        }

        private sealed class JackSupportLevelListItem
        {
            public string LevelName { get; private set; }
            private string DisplayText { get; set; }

            public JackSupportLevelListItem(
                string levelName,
                string displayText)
            {
                LevelName = levelName ?? string.Empty;
                DisplayText = displayText ?? string.Empty;
            }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private static int ClampColorComponent(int value)
        {
            if (value < 0)
                return 0;

            if (value > 255)
                return 255;

            return value;
        }

        private static void SetNumber(
            NumericUpDown control,
            double value)
        {
            decimal decimalValue = Convert.ToDecimal(value);

            if (decimalValue < control.Minimum)
                decimalValue = control.Minimum;

            if (decimalValue > control.Maximum)
                decimalValue = control.Maximum;

            control.Value = decimalValue;
        }

        private static string ToMultiline(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace(";", Environment.NewLine);
        }

        private static string NormalizeListText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Replace("\r\n", ";")
                .Replace("\n", ";")
                .Replace("\r", ";")
                .Replace(",", ";");
        }
    }
}

// =========================================================
// 코드 제목: 공개용 잭서포트 통합 설정 및 모델리스 실행 옵션창
// 파일명: JackSupportSettingsForm.cs
// =========================================================
