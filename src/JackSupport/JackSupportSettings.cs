// =========================================================
// 파일명: JackSupportSettings.cs
// 공개용 설명:
// 1) 잭서포트 자동 생성 기능의 설정 모델
// 2) XML 설정 저장·불러오기 및 버전 마이그레이션 지원
// 3) 대상 부재명, 생성 비율, 간격과 허용오차를 외부 설정으로 관리
// 4) 하부 지지체, 중복 판정, 층 분류 및 높이별 데이터 규칙 설정
// 5) 활성 뷰 그래픽 재지정 색상과 결과 분류 기준 설정
// 6) 실제 프로젝트의 부재 코드·패밀리명·매개변수명은 예시값으로 일반화
// 7) 사용자 로컬 절대 경로 대신 AppData 기반 공개용 데이터 경로 사용
// =========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace REVIT_TAP
{
    public enum JackSupportWallExclusionMode
    {
        None = 0,
        AtSupportPoint = 1,
        EntireBeam = 2
    }

    public enum JackSupportColorClassificationMode
    {
        FloorClassification = 0,
        HeightParameterRule = 1
    }

    [Serializable]
    public class JackSupportHeightParameterRule
    {
        public double MinimumHeightMm { get; set; }
        public double MaximumHeightMm { get; set; }
        public string ParameterName { get; set; }
        public double Value { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }

        public JackSupportHeightParameterRule()
        {
            MinimumHeightMm = 0.0;
            MaximumHeightMm = 0.0;
            ParameterName = string.Empty;
            Value = 1.0;
            ColorRed = 0;
            ColorGreen = 120;
            ColorBlue = 255;
        }

        public bool IsValid()
        {
            return
                MinimumHeightMm >= 0.0 &&
                MaximumHeightMm > MinimumHeightMm &&
                !string.IsNullOrWhiteSpace(ParameterName);
        }

        public bool Matches(double heightMm)
        {
            return
                heightMm >= MinimumHeightMm &&
                heightMm < MaximumHeightMm;
        }
    }

    [Serializable]
    public class JackSupportSettings
    {
        public int SettingsVersion { get; set; }

        // 자동 생성 모드에서 사용할 유형명
        public string GeneratedTypeName { get; set; }

        // 지정 패밀리 직접 사용 모드
        // 기존 XML 호환을 위해 속성명은 유지함.
        public string SourceRoundColumnFamilyName { get; set; }
        public string SourceRoundColumnTypeName { get; set; }

        // 지정 패밀리가 프로젝트에 없거나 자동 생성용 원형 패밀리가 없을 때 불러올 RFA
        public string FamilyRfaPath { get; set; }

        // 자동 생성 모드에서만 사용
        public string DiameterParameterName { get; set; }
        public double DiameterMm { get; set; }

        public bool EnableCondition1 { get; set; }

        // 특수 보 보 잭서포트 A 그룹
        // 기존 XML 호환을 위해 A 그룹은 기존 속성명을 그대로 사용함.
        public string Condition1StandardNames { get; set; }
        public string Condition1Ratios { get; set; }

        // 특수 보 보 잭서포트 B 그룹
        public string Condition1BStandardNames { get; set; }
        public string Condition1BRatios { get; set; }

        // 하부 바닥/기초가 없을 때 보 양단의 COLUMN_SUPPORT 구조기둥을 하부 기준으로 사용한다.
        public string Condition1ColumnFallbackStandardNames { get; set; }
        public double Condition1ColumnTouchToleranceMm { get; set; }

        // SPECIAL_BEAM와 SIDE_MEMBER의 긴 측면 접촉 방향별 생성 개수
        public string Condition1SpecialBeamStandardNames { get; set; }
        public string Condition1SideMemberStandardNames { get; set; }
        public int Condition1SpecialNoSideCount { get; set; }
        public int Condition1SpecialBothSidesCountPerSide { get; set; }
        public int Condition1SpecialSingleSideCount { get; set; }
        public double Condition1SideDetectionToleranceMm { get; set; }

        // 이전 설정 XML 호환용. 버전 14부터 특수 보 보에는 벽체 제외를 적용하지 않음.
        public bool Condition1ExcludeWhenWallTouches { get; set; }

        public bool EnableCondition2 { get; set; }

        // 버전 16 이전 설정 XML 호환용. 버전 17부터 실행 조건에는 사용하지 않음.
        public string Condition2Suffixes { get; set; }

        // 구조기둥의 패밀리명 전체에서 아래 문구 중 하나라도 포함되면 대상.
        // 기존 XML 호환을 위해 속성명은 그대로 유지한다.
        public string Condition2TypeNameKeywords { get; set; }
        public double Condition2OffsetMm { get; set; }

        public bool EnableCondition3 { get; set; }
        public string Condition3StandardNames { get; set; }
        public double Condition3IntervalMm { get; set; }
        public bool UseExistingColumnsAsSupports { get; set; }
        public bool UseWallsAsSupports { get; set; }

        // 이전 설정 XML과의 호환을 위해 유지하며 현재 실행 로직에서는 사용하지 않음.
        public JackSupportWallExclusionMode Condition3WallExclusionMode { get; set; }

        public bool IncludeFloorsAsLowerSupports { get; set; }
        public bool IncludeStructuralFoundationsAsLowerSupports { get; set; }
        public string StructuralFoundationStandardNames { get; set; }
        public double LowerSupportSearchDepthMm { get; set; }

        // 슬래브 경계부에서 원래 생성점 바로 아래에 바닥이 없을 때
        // 주변을 단계적으로 검색하여 가장 가까운 유효 하부 지지체 위치를 사용한다.
        public bool EnableBoundaryLowerSupportSearch { get; set; }
        public double BoundarySearchMaximumDistanceMm { get; set; }
        public double BoundarySearchStepMm { get; set; }
        public double BoundarySupportTopDifferenceToleranceMm { get; set; }
        public bool MoveSupportToBoundaryFoundPoint { get; set; }

        // 최하층/그외층 잭서포트 분류
        public bool EnableLowestFloorClassification { get; set; }

        // 실제 최하층으로 강제 판정할 Revit 레벨명 목록.
        // 이 레벨에 속하는 잭서포트는 하부에 그외층 판정 부재가 닿아도 최하층이다.
        public string ActualLowestLevelNames { get; set; }
        public double ActualLowestLevelElevationToleranceMm { get; set; }

        // 실제 최하층이 아닌 위치에서 아래 표준부재명 객체에 닿으면 그외층으로 판정한다.
        public string OtherFloorMarkerStandardNames { get; set; }
        public string OtherFloorMarkerSuffixes { get; set; }
        public double LowestFloorTouchToleranceMm { get; set; }

        // 구버전 XML 호환용. 버전 15부터 위 OtherFloorMarker 속성으로 이관한다.
        public string LowestFloorFoundationStandardNames { get; set; }
        public string LowestFloorFoundationSuffixes { get; set; }

        public string FloorClassificationParameterName { get; set; }
        public string LowestFloorClassificationValue { get; set; }
        public string OtherFloorClassificationValue { get; set; }

        // 최하층/그외층 수량 집계용 매개변수
        // 최하층인 경우 LowestFloorCountParameterName = 1,
        // 그외층인 경우 OtherFloorCountParameterName = 1을 입력한다.
        public bool EnableFloorClassificationCountParameters { get; set; }
        public bool ResetFloorClassificationCountParametersBeforeApply { get; set; }
        public string LowestFloorCountParameterName { get; set; }
        public string OtherFloorCountParameterName { get; set; }

        public double WallTouchToleranceMm { get; set; }
        public double WallHorizontalExtraMm { get; set; }
        public double WallPointProbeLengthMm { get; set; }
        public double ExistingColumnTouchToleranceMm { get; set; }

        // 중복 판정:
        // 1) XY 위치가 DuplicatePointToleranceMm 이내이고
        // 2) 기존/신규 하단 높이 차이가 DuplicateVerticalToleranceMm 이내이며
        // 3) 기존/신규 상단 높이 차이가 DuplicateVerticalToleranceMm 이내일 때만 중복
        public double DuplicatePointToleranceMm { get; set; }
        public double DuplicateVerticalToleranceMm { get; set; }

        public bool UseActiveViewOnly { get; set; }

        // 생성된 잭서포트 높이별 데이터 입력
        public bool EnableHeightParameterRules { get; set; }
        public bool ResetHeightRuleParametersBeforeApply { get; set; }
        public double HeightRuleRoundingMm { get; set; }
        public List<JackSupportHeightParameterRule> HeightParameterRules { get; set; }

        // 현재 활성 뷰 표시 색상
        // Revit 요소 자체 재료를 바꾸는 것이 아니라 View Override를 적용함.
        public bool EnableViewColorOverride { get; set; }
        public bool ApplyColorToExistingSupports { get; set; }
        public int ViewColorRed { get; set; }
        public int ViewColorGreen { get; set; }
        public int ViewColorBlue { get; set; }

        // 판정색상 적용 기준: 최하층·그외층 또는 높이별 데이터
        public JackSupportColorClassificationMode ColorClassificationMode { get; set; }

        // COLUMN_SUPPORT 기둥 길이를 하부 기준으로 사용한 특수 보 잭서포트의 외곽선 표시
        public bool EnableBtsColumnBasedOutline { get; set; }
        public int BtsColumnBasedOutlineRed { get; set; }
        public int BtsColumnBasedOutlineGreen { get; set; }
        public int BtsColumnBasedOutlineBlue { get; set; }
        public int BtsColumnBasedOutlineLineWeight { get; set; }

        // 최하층/그외층 색상 분리
        // EnableLowestFloorClassification이 켜져 있을 때 판정 결과에 따라 적용한다.
        public bool UseSeparateFloorColors { get; set; }
        public int LowestFloorColorRed { get; set; }
        public int LowestFloorColorGreen { get; set; }
        public int LowestFloorColorBlue { get; set; }
        public int OtherFloorColorRed { get; set; }
        public int OtherFloorColorGreen { get; set; }
        public int OtherFloorColorBlue { get; set; }

        // 높이별 데이터 규칙에 일치하지 않는 객체 전용 색상
        // 최하층/그외층 색상보다 우선 적용한다.
        public bool EnableUnmatchedHeightColor { get; set; }
        public int UnmatchedHeightColorRed { get; set; }
        public int UnmatchedHeightColorGreen { get; set; }
        public int UnmatchedHeightColorBlue { get; set; }

        // 이전 설정 XML과의 호환을 위해 유지함.
        public bool ExcludeWallAtCondition3Point { get; set; }
        public double FloorSearchDepthMm { get; set; }

        public JackSupportSettings()
        {
            SettingsVersion = 20;

            GeneratedTypeName = "PORTFOLIO_JACK_SUPPORT";

            // 둘 다 비어 있으면 기존 자동 생성 모드로 실행
            SourceRoundColumnFamilyName = string.Empty;
            SourceRoundColumnTypeName = string.Empty;

            FamilyRfaPath = Path.Combine(
                JackSupportSettingsStore.DefaultDataFolder,
                "JackSupport.rfa");
            DiameterParameterName = "지름";
            DiameterMm = 100.0;

            EnableCondition1 = true;
            Condition1StandardNames = "BEAM_TYPE_A;BEAM_TYPE_B";
            Condition1Ratios = "0.25;0.75";
            Condition1BStandardNames = string.Empty;
            Condition1BRatios = "0.25;0.5;0.75";
            Condition1ColumnFallbackStandardNames = "COLUMN_SUPPORT";
            Condition1ColumnTouchToleranceMm = 300.0;
            Condition1SpecialBeamStandardNames = "SPECIAL_BEAM";
            Condition1SideMemberStandardNames = "SIDE_MEMBER";
            Condition1SpecialNoSideCount = 1;
            Condition1SpecialBothSidesCountPerSide = 1;
            Condition1SpecialSingleSideCount = 3;
            Condition1SideDetectionToleranceMm = 300.0;
            Condition1ExcludeWhenWallTouches = false;

            EnableCondition2 = true;
            Condition2Suffixes = "CAP";
            Condition2TypeNameKeywords = "Column Cap";
            Condition2OffsetMm = 600.0;

            EnableCondition3 = true;
            Condition3StandardNames = "RC_BEAM";
            Condition3IntervalMm = 3000.0;
            UseExistingColumnsAsSupports = true;
            UseWallsAsSupports = true;
            Condition3WallExclusionMode = JackSupportWallExclusionMode.EntireBeam;

            IncludeFloorsAsLowerSupports = true;
            IncludeStructuralFoundationsAsLowerSupports = true;
            StructuralFoundationStandardNames = "FOUNDATION_SUPPORT";
            LowerSupportSearchDepthMm = 30000.0;

            EnableBoundaryLowerSupportSearch = true;
            BoundarySearchMaximumDistanceMm = 300.0;
            BoundarySearchStepMm = 50.0;
            BoundarySupportTopDifferenceToleranceMm = 100.0;
            MoveSupportToBoundaryFoundPoint = true;

            EnableLowestFloorClassification = false;
            ActualLowestLevelNames = string.Empty;
            ActualLowestLevelElevationToleranceMm = 1000.0;
            OtherFloorMarkerStandardNames = "OTHER_FLOOR_MARKER";
            OtherFloorMarkerSuffixes = string.Empty;
            LowestFloorFoundationStandardNames = "OTHER_FLOOR_MARKER";
            LowestFloorFoundationSuffixes = string.Empty;
            LowestFloorTouchToleranceMm = 20.0;
            FloorClassificationParameterName = "SUPPORT_FLOOR_CLASS";
            LowestFloorClassificationValue = "최하층";
            OtherFloorClassificationValue = "그외층";

            EnableFloorClassificationCountParameters = false;
            ResetFloorClassificationCountParametersBeforeApply = true;
            LowestFloorCountParameterName = "SUPPORT_LOWEST_FLOOR_QTY";
            OtherFloorCountParameterName = "SUPPORT_OTHER_FLOOR_QTY";

            WallTouchToleranceMm = 10.0;
            WallHorizontalExtraMm = 20.0;
            WallPointProbeLengthMm = 300.0;
            ExistingColumnTouchToleranceMm = 50.0;
            DuplicatePointToleranceMm = 50.0;
            DuplicateVerticalToleranceMm = 50.0;

            UseActiveViewOnly = false;

            EnableHeightParameterRules = false;
            ResetHeightRuleParametersBeforeApply = true;
            HeightRuleRoundingMm = 1.0;
            HeightParameterRules =
                new List<JackSupportHeightParameterRule>();

            EnableViewColorOverride = true;
            ApplyColorToExistingSupports = true;
            ViewColorRed = 0;
            ViewColorGreen = 200;
            ViewColorBlue = 0;
            ColorClassificationMode =
                JackSupportColorClassificationMode.FloorClassification;

            EnableBtsColumnBasedOutline = true;
            BtsColumnBasedOutlineRed = 255;
            BtsColumnBasedOutlineGreen = 255;
            BtsColumnBasedOutlineBlue = 0;
            BtsColumnBasedOutlineLineWeight = 6;

            UseSeparateFloorColors = true;

            // 최하층은 빨간색, 그외층은 기존 녹색을 기본값으로 사용
            LowestFloorColorRed = 255;
            LowestFloorColorGreen = 0;
            LowestFloorColorBlue = 0;

            OtherFloorColorRed = 0;
            OtherFloorColorGreen = 200;
            OtherFloorColorBlue = 0;

            EnableUnmatchedHeightColor = true;
            UnmatchedHeightColorRed = 255;
            UnmatchedHeightColorGreen = 0;
            UnmatchedHeightColorBlue = 255;

            ExcludeWallAtCondition3Point = true;
            FloorSearchDepthMm = 30000.0;
        }

        public bool UsesSpecifiedFamilyType()
        {
            return
                !string.IsNullOrWhiteSpace(SourceRoundColumnFamilyName) ||
                !string.IsNullOrWhiteSpace(SourceRoundColumnTypeName);
        }

        public IList<string> GetCondition1Names()
        {
            return SplitList(Condition1StandardNames);
        }

        public IList<double> GetCondition1RatioValues()
        {
            List<double> result = new List<double>();

            foreach (string token in SplitList(Condition1Ratios))
            {
                double value;

                if (double.TryParse(token, out value) &&
                    value > 0.0 &&
                    value < 1.0)
                {
                    result.Add(value);
                }
            }

            if (result.Count == 0)
            {
                result.Add(0.25);
                result.Add(0.75);
            }

            return result
                .Distinct()
                .OrderBy(value => value)
                .ToList();
        }

        public IList<string> GetCondition1BNames()
        {
            return SplitList(Condition1BStandardNames);
        }

        public IList<string> GetCondition1ColumnFallbackNames()
        {
            return SplitList(Condition1ColumnFallbackStandardNames);
        }

        public IList<string> GetCondition1SpecialBeamNames()
        {
            return SplitList(Condition1SpecialBeamStandardNames);
        }

        public IList<string> GetCondition1SideMemberNames()
        {
            return SplitList(Condition1SideMemberStandardNames);
        }

        public IList<double> GetCondition1BRatioValues()
        {
            List<double> result = new List<double>();

            foreach (string token in SplitList(Condition1BRatios))
            {
                double value;

                if (double.TryParse(token, out value) &&
                    value > 0.0 &&
                    value < 1.0)
                {
                    result.Add(value);
                }
            }

            if (result.Count == 0)
            {
                result.Add(0.25);
                result.Add(0.5);
                result.Add(0.75);
            }

            return result
                .Distinct()
                .OrderBy(value => value)
                .ToList();
        }

        public IList<string> GetActualLowestLevelNames()
        {
            return SplitList(ActualLowestLevelNames);
        }

        public IList<string> GetOtherFloorMarkerNames()
        {
            return SplitList(OtherFloorMarkerStandardNames);
        }

        public IList<string> GetOtherFloorMarkerSuffixes()
        {
            return SplitList(OtherFloorMarkerSuffixes);
        }

        // 구버전 호출부 호환용
        public IList<string> GetLowestFloorFoundationNames()
        {
            return GetOtherFloorMarkerNames();
        }

        public IList<string> GetLowestFloorFoundationSuffixes()
        {
            return GetOtherFloorMarkerSuffixes();
        }

        public IList<string> GetCondition2FamilyNameKeywords()
        {
            return SplitList(Condition2TypeNameKeywords);
        }

        // 구버전 호출부 호환용.
        public IList<string> GetCondition2TypeNameKeywords()
        {
            return GetCondition2FamilyNameKeywords();
        }

        // 구버전 호출부 호환용.
        public IList<string> GetCondition2SuffixList()
        {
            return GetCondition2FamilyNameKeywords();
        }

        public IList<string> GetCondition3Names()
        {
            return SplitList(Condition3StandardNames);
        }

        public IList<string> GetStructuralFoundationNames()
        {
            return SplitList(StructuralFoundationStandardNames);
        }

        public IList<JackSupportHeightParameterRule>
            GetValidHeightParameterRules()
        {
            if (HeightParameterRules == null)
                return new List<JackSupportHeightParameterRule>();

            return HeightParameterRules
                .Where(rule => rule != null && rule.IsValid())
                .ToList();
        }

        private static IList<string> SplitList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            return text
                .Split(
                    new[] { ';', ',', '\r', '\n', '\t' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static class JackSupportSettingsStore
    {
        public static readonly string DefaultDataFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "RevitStructuralAutomation",
                "JackSupport");

        public static readonly string SettingsFilePath =
            Path.Combine(DefaultDataFolder, "JackSupportSettings.xml");

        public static JackSupportSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    JackSupportSettings defaults = new JackSupportSettings();
                    Save(defaults);
                    return defaults;
                }

                XmlSerializer serializer =
                    new XmlSerializer(typeof(JackSupportSettings));

                JackSupportSettings loaded;

                using (FileStream stream = File.OpenRead(SettingsFilePath))
                {
                    loaded = serializer.Deserialize(stream) as JackSupportSettings;
                }

                if (loaded == null)
                    return new JackSupportSettings();

                bool changed = ApplyMigration(loaded);

                if (changed)
                    Save(loaded);

                return loaded;
            }
            catch
            {
                return new JackSupportSettings();
            }
        }

        public static void Save(JackSupportSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            settings.SettingsVersion = 20;
            settings.ExcludeWallAtCondition3Point = settings.UseWallsAsSupports;
            settings.Condition3WallExclusionMode =
                settings.UseWallsAsSupports
                    ? JackSupportWallExclusionMode.EntireBeam
                    : JackSupportWallExclusionMode.None;
            settings.FloorSearchDepthMm = settings.LowerSupportSearchDepthMm;
            settings.LowestFloorFoundationStandardNames =
                settings.OtherFloorMarkerStandardNames;
            settings.LowestFloorFoundationSuffixes =
                settings.OtherFloorMarkerSuffixes;

            Directory.CreateDirectory(DefaultDataFolder);

            XmlSerializer serializer =
                new XmlSerializer(typeof(JackSupportSettings));

            using (FileStream stream = File.Create(SettingsFilePath))
            {
                serializer.Serialize(stream, settings);
            }
        }

        private static bool ApplyMigration(JackSupportSettings settings)
        {
            bool changed = false;

            if (settings.SettingsVersion < 2)
            {
                settings.UseActiveViewOnly = false;
                changed = true;
            }

            if (settings.SettingsVersion < 4)
            {
                settings.EnableCondition1 = true;
                settings.EnableCondition2 = true;
                settings.EnableCondition3 = true;

                settings.IncludeFloorsAsLowerSupports = true;
                settings.IncludeStructuralFoundationsAsLowerSupports = true;
                settings.StructuralFoundationStandardNames = "FOUNDATION_SUPPORT";

                settings.LowerSupportSearchDepthMm =
                    settings.FloorSearchDepthMm > 0.0
                        ? settings.FloorSearchDepthMm
                        : 30000.0;

                settings.Condition3WallExclusionMode =
                    settings.ExcludeWallAtCondition3Point
                        ? JackSupportWallExclusionMode.EntireBeam
                        : JackSupportWallExclusionMode.None;

                settings.WallHorizontalExtraMm = 20.0;
                settings.WallPointProbeLengthMm = 300.0;
                settings.SettingsVersion = 4;
                changed = true;
            }

            if (settings.SettingsVersion < 5)
            {
                settings.UseWallsAsSupports =
                    settings.Condition3WallExclusionMode != JackSupportWallExclusionMode.None ||
                    settings.ExcludeWallAtCondition3Point;

                settings.SettingsVersion = 5;
                changed = true;
            }

            if (settings.SettingsVersion < 6)
            {
                // 기존 SourceRoundColumnFamilyName/TypeName 값이 있으면
                // 이제부터 해당 패밀리/유형을 직접 사용하는 설정으로 해석함.
                settings.SettingsVersion = 6;
                changed = true;
            }

            if (settings.SettingsVersion < 7)
            {
                settings.EnableHeightParameterRules = false;
                settings.ResetHeightRuleParametersBeforeApply = true;
                settings.HeightRuleRoundingMm = 1.0;
                settings.HeightParameterRules =
                    new List<JackSupportHeightParameterRule>();
                settings.SettingsVersion = 7;
                changed = true;
            }

            if (settings.SettingsVersion < 8)
            {
                settings.EnableViewColorOverride = true;
                settings.ApplyColorToExistingSupports = true;
                settings.ViewColorRed = 0;
                settings.ViewColorGreen = 200;
                settings.ViewColorBlue = 0;
                settings.SettingsVersion = 8;
                changed = true;
            }

            
            if (settings.SettingsVersion < 9)
            {
                // 기존 버전은 동일 XY에서 높이 구간이 접촉하거나 겹치기만 해도
                // 중복으로 제외했으므로, 버전 9부터 하단·상단이 모두 같은 경우에만
                // 중복으로 판정하도록 수직 높이 허용오차를 별도로 둔다.
                settings.DuplicateVerticalToleranceMm = 50.0;
                settings.SettingsVersion = 9;
                changed = true;
            }

            if (settings.SettingsVersion < 10)
            {
                settings.Condition1BStandardNames = string.Empty;
                settings.Condition1BRatios = "0.25;0.5;0.75";

                settings.EnableLowestFloorClassification = false;
                settings.LowestFloorFoundationStandardNames = "FOUNDATION_SUPPORT";
                settings.LowestFloorFoundationSuffixes = "MF;SF";
                settings.LowestFloorTouchToleranceMm = 20.0;
                settings.FloorClassificationParameterName = "SUPPORT_FLOOR_CLASS";
                settings.LowestFloorClassificationValue = "최하층";
                settings.OtherFloorClassificationValue = "그외층";

                settings.SettingsVersion = 10;
                changed = true;
            }

            if (settings.SettingsVersion < 11)
            {
                settings.EnableFloorClassificationCountParameters = false;
                settings.ResetFloorClassificationCountParametersBeforeApply = true;
                settings.LowestFloorCountParameterName = "SUPPORT_LOWEST_FLOOR_QTY";
                settings.OtherFloorCountParameterName = "SUPPORT_OTHER_FLOOR_QTY";

                settings.SettingsVersion = 11;
                changed = true;
            }

            if (settings.SettingsVersion < 12)
            {
                settings.UseSeparateFloorColors = true;

                settings.LowestFloorColorRed = 255;
                settings.LowestFloorColorGreen = 0;
                settings.LowestFloorColorBlue = 0;

                // 기존 공통 색상을 그외층 색상으로 승계
                settings.OtherFloorColorRed =
                    ClampColorComponent(settings.ViewColorRed);
                settings.OtherFloorColorGreen =
                    ClampColorComponent(settings.ViewColorGreen);
                settings.OtherFloorColorBlue =
                    ClampColorComponent(settings.ViewColorBlue);

                settings.SettingsVersion = 12;
                changed = true;
            }

            if (settings.SettingsVersion < 13)
            {
                settings.Condition1ExcludeWhenWallTouches = true;

                settings.EnableUnmatchedHeightColor = true;
                settings.UnmatchedHeightColorRed = 255;
                settings.UnmatchedHeightColorGreen = 0;
                settings.UnmatchedHeightColorBlue = 255;

                settings.SettingsVersion = 13;
                changed = true;
            }

            if (settings.SettingsVersion < 14)
            {
                // 특수 보 보 잭서포트는 벽체 유무와 관계없이 생성한다.
                // 기존 XML의 벽체 제외 설정은 강제로 해제한다.
                settings.Condition1ExcludeWhenWallTouches = false;
                settings.SettingsVersion = 14;
                changed = true;
            }

            if (settings.SettingsVersion < 15)
            {
                // 기존에 최하층 판정용으로 입력해 둔 표준부재명 값을
                // 새 그외층 판정 부재 설정으로 승계한다.
                settings.OtherFloorMarkerStandardNames =
                    string.IsNullOrWhiteSpace(
                        settings.LowestFloorFoundationStandardNames)
                        ? "OTHER_FLOOR_MARKER"
                        : settings.LowestFloorFoundationStandardNames;

                settings.OtherFloorMarkerSuffixes =
                    settings.LowestFloorFoundationSuffixes ?? string.Empty;

                settings.ActualLowestLevelNames = string.Empty;
                settings.ActualLowestLevelElevationToleranceMm = 1000.0;
                settings.SettingsVersion = 15;
                changed = true;
            }

            if (settings.SettingsVersion < 16)
            {
                settings.EnableBoundaryLowerSupportSearch = true;
                settings.BoundarySearchMaximumDistanceMm = 300.0;
                settings.BoundarySearchStepMm = 50.0;
                settings.BoundarySupportTopDifferenceToleranceMm = 100.0;
                settings.MoveSupportToBoundaryFoundPoint = true;
                settings.SettingsVersion = 16;
                changed = true;
            }

            if (settings.SettingsVersion < 17)
            {
                // 기존 PC 접미어 방식은 사용하지 않고,
                // Column Cap 포함 문구 방식으로 전환한다.
                settings.Condition2TypeNameKeywords = "Column Cap";
                settings.SettingsVersion = 17;
                changed = true;
            }

            if (settings.SettingsVersion < 18)
            {
                // 버전 18부터 Column Cap 포함 문구는
                // 구조기둥 유형명이 아니라 패밀리명에서 검색한다.
                if (string.IsNullOrWhiteSpace(
                    settings.Condition2TypeNameKeywords))
                {
                    settings.Condition2TypeNameKeywords =
                        "Column Cap";
                }

                settings.SettingsVersion = 18;
                changed = true;
            }

            if (settings.SettingsVersion < 19)
            {
                settings.Condition1ColumnFallbackStandardNames = "COLUMN_SUPPORT";
                settings.Condition1ColumnTouchToleranceMm = 300.0;
                settings.Condition1SpecialBeamStandardNames = "SPECIAL_BEAM";
                settings.Condition1SideMemberStandardNames = "SIDE_MEMBER";
                settings.Condition1SpecialNoSideCount = 1;
                settings.Condition1SpecialBothSidesCountPerSide = 1;
                settings.Condition1SpecialSingleSideCount = 3;
                settings.Condition1SideDetectionToleranceMm = 300.0;

                settings.ColorClassificationMode =
                    JackSupportColorClassificationMode.FloorClassification;

                settings.EnableBtsColumnBasedOutline = true;
                settings.BtsColumnBasedOutlineRed = 255;
                settings.BtsColumnBasedOutlineGreen = 255;
                settings.BtsColumnBasedOutlineBlue = 0;
                settings.BtsColumnBasedOutlineLineWeight = 6;

                if (settings.HeightParameterRules != null)
                {
                    int colorIndex = 0;
                    int[,] defaultColors =
                    {
                        { 0, 120, 255 },
                        { 0, 180, 120 },
                        { 255, 170, 0 },
                        { 180, 90, 255 },
                        { 255, 90, 120 }
                    };

                    foreach (JackSupportHeightParameterRule rule in
                        settings.HeightParameterRules)
                    {
                        if (rule == null)
                            continue;

                        rule.ColorRed =
                            defaultColors[colorIndex % 5, 0];
                        rule.ColorGreen =
                            defaultColors[colorIndex % 5, 1];
                        rule.ColorBlue =
                            defaultColors[colorIndex % 5, 2];
                        colorIndex++;
                    }
                }

                settings.SettingsVersion = 19;
                changed = true;
            }

            if (settings.SettingsVersion < 20)
            {
                // SPECIAL_BEAM/SIDE_MEMBER 생성 규칙 기본값을 명확히 고정한다.
                // SIDE_MEMBER 판정은 구조프레임 유형명의 마지막 '_' 뒤
                // 표준부재명과 정확히 일치하는 경우만 사용한다.
                if (string.IsNullOrWhiteSpace(
                    settings.Condition1SpecialBeamStandardNames))
                {
                    settings.Condition1SpecialBeamStandardNames =
                        "SPECIAL_BEAM";
                }

                if (string.IsNullOrWhiteSpace(
                    settings.Condition1SideMemberStandardNames))
                {
                    settings.Condition1SideMemberStandardNames =
                        "SIDE_MEMBER";
                }

                settings.Condition1SpecialNoSideCount =
                    Math.Max(
                        1,
                        settings.Condition1SpecialNoSideCount);

                settings.Condition1SpecialBothSidesCountPerSide =
                    Math.Max(
                        1,
                        settings.Condition1SpecialBothSidesCountPerSide);

                settings.Condition1SpecialSingleSideCount =
                    Math.Max(
                        3,
                        settings.Condition1SpecialSingleSideCount);

                if (settings.Condition1SideDetectionToleranceMm <= 0.0)
                {
                    settings.Condition1SideDetectionToleranceMm =
                        300.0;
                }

                settings.SettingsVersion = 20;
                changed = true;
            }

            if (settings.HeightParameterRules == null)
            {
                settings.HeightParameterRules =
                    new List<JackSupportHeightParameterRule>();
                changed = true;
            }

            if (settings.HeightRuleRoundingMm <= 0.0)
            {
                settings.HeightRuleRoundingMm = 1.0;
                changed = true;
            }

            foreach (JackSupportHeightParameterRule rule in
                settings.HeightParameterRules)
            {
                if (rule == null)
                    continue;

                int ruleRed = ClampColorComponent(rule.ColorRed);
                int ruleGreen = ClampColorComponent(rule.ColorGreen);
                int ruleBlue = ClampColorComponent(rule.ColorBlue);

                if (rule.ColorRed != ruleRed)
                {
                    rule.ColorRed = ruleRed;
                    changed = true;
                }

                if (rule.ColorGreen != ruleGreen)
                {
                    rule.ColorGreen = ruleGreen;
                    changed = true;
                }

                if (rule.ColorBlue != ruleBlue)
                {
                    rule.ColorBlue = ruleBlue;
                    changed = true;
                }
            }

            int normalizedRed = ClampColorComponent(settings.ViewColorRed);
            int normalizedGreen = ClampColorComponent(settings.ViewColorGreen);
            int normalizedBlue = ClampColorComponent(settings.ViewColorBlue);

            if (settings.ViewColorRed != normalizedRed)
            {
                settings.ViewColorRed = normalizedRed;
                changed = true;
            }

            if (settings.ViewColorGreen != normalizedGreen)
            {
                settings.ViewColorGreen = normalizedGreen;
                changed = true;
            }

            if (settings.ViewColorBlue != normalizedBlue)
            {
                settings.ViewColorBlue = normalizedBlue;
                changed = true;
            }

            int normalizedLowestRed =
                ClampColorComponent(settings.LowestFloorColorRed);
            int normalizedLowestGreen =
                ClampColorComponent(settings.LowestFloorColorGreen);
            int normalizedLowestBlue =
                ClampColorComponent(settings.LowestFloorColorBlue);

            int normalizedOtherRed =
                ClampColorComponent(settings.OtherFloorColorRed);
            int normalizedOtherGreen =
                ClampColorComponent(settings.OtherFloorColorGreen);
            int normalizedOtherBlue =
                ClampColorComponent(settings.OtherFloorColorBlue);

            int normalizedUnmatchedRed =
                ClampColorComponent(settings.UnmatchedHeightColorRed);
            int normalizedUnmatchedGreen =
                ClampColorComponent(settings.UnmatchedHeightColorGreen);
            int normalizedUnmatchedBlue =
                ClampColorComponent(settings.UnmatchedHeightColorBlue);

            if (settings.LowestFloorColorRed != normalizedLowestRed)
            {
                settings.LowestFloorColorRed = normalizedLowestRed;
                changed = true;
            }

            if (settings.LowestFloorColorGreen != normalizedLowestGreen)
            {
                settings.LowestFloorColorGreen = normalizedLowestGreen;
                changed = true;
            }

            if (settings.LowestFloorColorBlue != normalizedLowestBlue)
            {
                settings.LowestFloorColorBlue = normalizedLowestBlue;
                changed = true;
            }

            if (settings.OtherFloorColorRed != normalizedOtherRed)
            {
                settings.OtherFloorColorRed = normalizedOtherRed;
                changed = true;
            }

            if (settings.OtherFloorColorGreen != normalizedOtherGreen)
            {
                settings.OtherFloorColorGreen = normalizedOtherGreen;
                changed = true;
            }

            if (settings.OtherFloorColorBlue != normalizedOtherBlue)
            {
                settings.OtherFloorColorBlue = normalizedOtherBlue;
                changed = true;
            }

            if (settings.UnmatchedHeightColorRed != normalizedUnmatchedRed)
            {
                settings.UnmatchedHeightColorRed = normalizedUnmatchedRed;
                changed = true;
            }

            if (settings.UnmatchedHeightColorGreen != normalizedUnmatchedGreen)
            {
                settings.UnmatchedHeightColorGreen = normalizedUnmatchedGreen;
                changed = true;
            }

            if (settings.UnmatchedHeightColorBlue != normalizedUnmatchedBlue)
            {
                settings.UnmatchedHeightColorBlue = normalizedUnmatchedBlue;
                changed = true;
            }

            int normalizedBtsOutlineRed =
                ClampColorComponent(settings.BtsColumnBasedOutlineRed);
            int normalizedBtsOutlineGreen =
                ClampColorComponent(settings.BtsColumnBasedOutlineGreen);
            int normalizedBtsOutlineBlue =
                ClampColorComponent(settings.BtsColumnBasedOutlineBlue);

            if (settings.BtsColumnBasedOutlineRed != normalizedBtsOutlineRed)
            {
                settings.BtsColumnBasedOutlineRed = normalizedBtsOutlineRed;
                changed = true;
            }

            if (settings.BtsColumnBasedOutlineGreen != normalizedBtsOutlineGreen)
            {
                settings.BtsColumnBasedOutlineGreen = normalizedBtsOutlineGreen;
                changed = true;
            }

            if (settings.BtsColumnBasedOutlineBlue != normalizedBtsOutlineBlue)
            {
                settings.BtsColumnBasedOutlineBlue = normalizedBtsOutlineBlue;
                changed = true;
            }

            if (settings.BtsColumnBasedOutlineLineWeight < 1 ||
                settings.BtsColumnBasedOutlineLineWeight > 16)
            {
                settings.BtsColumnBasedOutlineLineWeight = 6;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.GeneratedTypeName))
            {
                settings.GeneratedTypeName = "PORTFOLIO_JACK_SUPPORT";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.FamilyRfaPath))
            {
                settings.FamilyRfaPath =
                    Path.Combine(
                        DefaultDataFolder,
                        "JackSupport.rfa");
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.DiameterParameterName))
            {
                settings.DiameterParameterName = "지름";
                changed = true;
            }

            if (settings.DiameterMm <= 0.0)
            {
                settings.DiameterMm = 100.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Condition1StandardNames))
            {
                settings.Condition1StandardNames = "BEAM_TYPE_A;BEAM_TYPE_B";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Condition1Ratios))
            {
                settings.Condition1Ratios = "0.25;0.75";
                changed = true;
            }

            if (settings.Condition1BStandardNames == null)
            {
                settings.Condition1BStandardNames = string.Empty;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Condition1BRatios))
            {
                settings.Condition1BRatios = "0.25;0.5;0.75";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(
                settings.Condition1ColumnFallbackStandardNames))
            {
                settings.Condition1ColumnFallbackStandardNames = "COLUMN_SUPPORT";
                changed = true;
            }

            if (settings.Condition1ColumnTouchToleranceMm <= 0.0)
            {
                settings.Condition1ColumnTouchToleranceMm = 300.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(
                settings.Condition1SpecialBeamStandardNames))
            {
                settings.Condition1SpecialBeamStandardNames = "SPECIAL_BEAM";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(
                settings.Condition1SideMemberStandardNames))
            {
                settings.Condition1SideMemberStandardNames = "SIDE_MEMBER";
                changed = true;
            }

            if (settings.Condition1SpecialNoSideCount < 0)
            {
                settings.Condition1SpecialNoSideCount = 1;
                changed = true;
            }

            if (settings.Condition1SpecialBothSidesCountPerSide < 0)
            {
                settings.Condition1SpecialBothSidesCountPerSide = 1;
                changed = true;
            }

            if (settings.Condition1SpecialSingleSideCount < 0)
            {
                settings.Condition1SpecialSingleSideCount = 3;
                changed = true;
            }

            if (settings.Condition1SideDetectionToleranceMm <= 0.0)
            {
                settings.Condition1SideDetectionToleranceMm = 300.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Condition2Suffixes))
            {
                settings.Condition2Suffixes = "CAP";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(
                settings.Condition2TypeNameKeywords))
            {
                settings.Condition2TypeNameKeywords = "Column Cap";
                changed = true;
            }

            if (settings.Condition2OffsetMm < 0.0)
            {
                settings.Condition2OffsetMm = 600.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Condition3StandardNames))
            {
                settings.Condition3StandardNames = "RC_BEAM";
                changed = true;
            }

            if (settings.Condition3IntervalMm <= 0.0)
            {
                settings.Condition3IntervalMm = 3000.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.StructuralFoundationStandardNames))
            {
                settings.StructuralFoundationStandardNames = "FOUNDATION_SUPPORT";
                changed = true;
            }

            if (settings.ActualLowestLevelNames == null)
            {
                settings.ActualLowestLevelNames = string.Empty;
                changed = true;
            }

            if (settings.ActualLowestLevelElevationToleranceMm < 0.0)
            {
                settings.ActualLowestLevelElevationToleranceMm = 1000.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.OtherFloorMarkerStandardNames) &&
                string.IsNullOrWhiteSpace(settings.OtherFloorMarkerSuffixes))
            {
                settings.OtherFloorMarkerStandardNames = "OTHER_FLOOR_MARKER";
                changed = true;
            }

            if (settings.OtherFloorMarkerSuffixes == null)
            {
                settings.OtherFloorMarkerSuffixes = string.Empty;
                changed = true;
            }

            if (settings.LowestFloorFoundationStandardNames !=
                settings.OtherFloorMarkerStandardNames)
            {
                settings.LowestFloorFoundationStandardNames =
                    settings.OtherFloorMarkerStandardNames;
                changed = true;
            }

            if (settings.LowestFloorFoundationSuffixes !=
                settings.OtherFloorMarkerSuffixes)
            {
                settings.LowestFloorFoundationSuffixes =
                    settings.OtherFloorMarkerSuffixes;
                changed = true;
            }

            if (settings.LowestFloorTouchToleranceMm < 0.0)
            {
                settings.LowestFloorTouchToleranceMm = 20.0;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.FloorClassificationParameterName))
            {
                settings.FloorClassificationParameterName = "SUPPORT_FLOOR_CLASS";
                changed = true;
            }

            if (settings.LowestFloorClassificationValue == null)
            {
                settings.LowestFloorClassificationValue = "최하층";
                changed = true;
            }

            if (settings.OtherFloorClassificationValue == null)
            {
                settings.OtherFloorClassificationValue = "그외층";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(
                settings.LowestFloorCountParameterName))
            {
                settings.LowestFloorCountParameterName =
                    "SUPPORT_LOWEST_FLOOR_QTY";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(
                settings.OtherFloorCountParameterName))
            {
                settings.OtherFloorCountParameterName =
                    "SUPPORT_OTHER_FLOOR_QTY";
                changed = true;
            }

            if (settings.LowerSupportSearchDepthMm <= 0.0)
            {
                settings.LowerSupportSearchDepthMm =
                    settings.FloorSearchDepthMm > 0.0
                        ? settings.FloorSearchDepthMm
                        : 30000.0;
                changed = true;
            }

            if (settings.BoundarySearchMaximumDistanceMm < 0.0)
            {
                settings.BoundarySearchMaximumDistanceMm = 300.0;
                changed = true;
            }

            if (settings.BoundarySearchStepMm <= 0.0)
            {
                settings.BoundarySearchStepMm = 50.0;
                changed = true;
            }

            if (settings.BoundarySupportTopDifferenceToleranceMm < 0.0)
            {
                settings.BoundarySupportTopDifferenceToleranceMm = 100.0;
                changed = true;
            }

            if (settings.WallHorizontalExtraMm < 0.0)
            {
                settings.WallHorizontalExtraMm = 20.0;
                changed = true;
            }

            if (settings.WallPointProbeLengthMm <= 0.0)
            {
                settings.WallPointProbeLengthMm = 300.0;
                changed = true;
            }

            if (settings.DuplicatePointToleranceMm < 0.0)
            {
                settings.DuplicatePointToleranceMm = 50.0;
                changed = true;
            }

            if (settings.DuplicateVerticalToleranceMm <= 0.0)
            {
                settings.DuplicateVerticalToleranceMm = 50.0;
                changed = true;
            }

            settings.ExcludeWallAtCondition3Point = settings.UseWallsAsSupports;
            settings.Condition3WallExclusionMode =
                settings.UseWallsAsSupports
                    ? JackSupportWallExclusionMode.EntireBeam
                    : JackSupportWallExclusionMode.None;
            settings.FloorSearchDepthMm = settings.LowerSupportSearchDepthMm;

            return changed;
        }

        private static int ClampColorComponent(int value)
        {
            if (value < 0)
                return 0;

            if (value > 255)
                return 255;

            return value;
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 자동 생성 공개용 설정 모델 및 XML 저장소
// 파일명: JackSupportSettings.cs
// =========================================================
