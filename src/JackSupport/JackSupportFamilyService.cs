// =========================================================
// 공개용 정리 날짜: 2026-08-05 (KST)
// 파일명: JackSupportFamilyService.cs
// 설명:
// 1) 설정된 구조기둥 패밀리/유형을 조회하고 필요 시 RFA를 자동 로드
// 2) 정확한 하단·상단 높이와 기준 레벨을 적용하여 수직 지지 부재 생성
// 3) 동일한 XY와 동일한 수직 구간의 중복 생성을 방지
// 4) 기존 또는 신규 지지 부재에 높이 구간별 데이터 규칙 적용
// 5) 최하층·그외층 분류값과 수량 집계용 매개변수 입력
// 6) 층 분류 또는 높이 규칙에 따른 활성 뷰 그래픽 재지정
// 7) 생성 원인과 원본 부재 구분값을 유형명에 기록
// 8) 사용자 로컬 절대 경로 대신 AppData 기반 공개용 데이터 경로 사용
// 9) 실제 회사·현장·부재 코드와 내부 패밀리 경로는 공개용 값으로 일반화
// =========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;

using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace REVIT_TAP
{
    public class JackSupportFamilyService
    {
        private static readonly string FixedJackSupportRfaPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "RevitStructuralAutomation",
                "JackSupport",
                "JackSupport.rfa");

        private readonly Document _doc;
        private readonly JackSupportSettings _settings;
        private readonly double _duplicatePlanTolerance;
        private readonly double _duplicateVerticalTolerance;
        private readonly List<FamilyInstance> _createdOrExistingSupports;
        private readonly HashSet<int> _heightRuleProcessedElementIds;
        private readonly HashSet<int> _viewColorProcessedElementIds;
        private readonly HashSet<int> _floorClassificationProcessedElementIds;
        private readonly HashSet<int> _newlyCreatedSupportElementIds;
        private readonly HashSet<int> _heightRuleNoMatchElementIds;
        private readonly Dictionary<int, JackSupportHeightParameterRule>
            _heightRuleMatchByElementId;
        private readonly Dictionary<string, FamilySymbol> _sourceTaggedSymbolCache;
        private ElementId _solidFillPatternId;

        public JackSupportHeightParameterStatistics
            HeightParameterStatistics { get; private set; }

        public JackSupportViewColorStatistics
            ViewColorStatistics { get; private set; }

        public JackSupportFloorClassificationStatistics
            FloorClassificationStatistics { get; private set; }

        public JackSupportFamilyService(
            Document doc,
            JackSupportSettings settings)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");

            if (settings == null)
                throw new ArgumentNullException("settings");

            _doc = doc;
            _settings = settings;
            _duplicatePlanTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.DuplicatePointToleranceMm);

            _duplicateVerticalTolerance =
                JackSupportGeometryHelper.MmToInternal(
                    settings.DuplicateVerticalToleranceMm);

            _createdOrExistingSupports =
                CollectExistingGeneratedSupports();

            _heightRuleProcessedElementIds =
                new HashSet<int>();

            _viewColorProcessedElementIds =
                new HashSet<int>();

            _floorClassificationProcessedElementIds =
                new HashSet<int>();

            _newlyCreatedSupportElementIds =
                new HashSet<int>();

            _heightRuleNoMatchElementIds =
                new HashSet<int>();

            _heightRuleMatchByElementId =
                new Dictionary<int, JackSupportHeightParameterRule>();

            _sourceTaggedSymbolCache =
                new Dictionary<string, FamilySymbol>(
                    StringComparer.OrdinalIgnoreCase);

            _solidFillPatternId = null;

            HeightParameterStatistics =
                new JackSupportHeightParameterStatistics();

            ViewColorStatistics =
                new JackSupportViewColorStatistics();

            FloorClassificationStatistics =
                new JackSupportFloorClassificationStatistics();
        }

        public FamilySymbol GetOrCreateSymbol()
        {
            if (_settings.UsesSpecifiedFamilyType())
                return GetSpecifiedFamilySymbol();

            FamilySymbol fixedRfaSymbol =
                GetFixedRfaFamilySymbol();

            if (fixedRfaSymbol != null)
                return fixedRfaSymbol;

            return GetOrCreateAutomaticSymbol();
        }

        private FamilySymbol GetFixedRfaFamilySymbol()
        {
            if (!File.Exists(FixedJackSupportRfaPath))
            {
                return null;
            }

            Family loadedFamily =
                LoadConfiguredFamily(
                    FixedJackSupportRfaPath);

            FamilySymbol symbol =
                FindBestLoadedFamilySymbol(
                    loadedFamily);

            if (symbol == null)
            {
                symbol =
                    FindRfaNamedFamilySymbol(
                        FixedJackSupportRfaPath);
            }

            if (symbol == null)
            {
                symbol =
                    FindJackSupportNamedSymbol();
            }

            if (symbol == null)
            {
                return null;
            }

            ActivateSymbol(symbol);

            SynchronizeResolvedFamilyType(
                symbol,
                FixedJackSupportRfaPath);

            return symbol;
        }

        public FamilySymbol GetOrCreateSourceTaggedSymbol(
            FamilySymbol baseSymbol,
            string sourceKind,
            string sourceStandardName)
        {
            if (baseSymbol == null)
                throw new ArgumentNullException("baseSymbol");

            string typeName =
                BuildSourceTaggedTypeName(
                    baseSymbol,
                    sourceKind,
                    sourceStandardName);

            FamilySymbol cached;

            if (_sourceTaggedSymbolCache.TryGetValue(
                typeName,
                out cached) &&
                cached != null)
            {
                ActivateSymbol(cached);
                return cached;
            }

            int baseFamilyId =
                baseSymbol.Family == null
                    ? ElementId.InvalidElementId
                        .IntegerValue
                    : baseSymbol.Family.Id.IntegerValue;

            FamilySymbol existing =
                GetStructuralColumnSymbols()
                    .FirstOrDefault(symbol =>
                    {
                        int symbolFamilyId =
                            symbol.Family == null
                                ? ElementId
                                    .InvalidElementId
                                    .IntegerValue
                                : symbol.Family.Id
                                    .IntegerValue;

                        return
                            symbolFamilyId == baseFamilyId &&
                            string.Equals(
                                symbol.Name,
                                typeName,
                                StringComparison
                                    .OrdinalIgnoreCase);
                    });

            if (existing != null)
            {
                ActivateSymbol(existing);
                _sourceTaggedSymbolCache[typeName] =
                    existing;

                return existing;
            }

            ElementType duplicated =
                baseSymbol.Duplicate(typeName);

            FamilySymbol taggedSymbol =
                duplicated as FamilySymbol;

            if (taggedSymbol == null)
            {
                throw new InvalidOperationException(
                    "잭서포트 생성 원인 유형을 만들지 못했습니다.\n\n" +
                    "기준 유형: " + baseSymbol.Name +
                    "\n생성 유형: " + typeName);
            }

            ActivateSymbol(taggedSymbol);

            _sourceTaggedSymbolCache[typeName] =
                taggedSymbol;

            return taggedSymbol;
        }

        public FamilyInstance CreateVerticalColumn(
            XYZ planPoint,
            double bottomZ,
            double topZ,
            FamilySymbol symbol)
        {
            bool wasCreated;

            FamilyInstance instance =
                CreateOrGetVerticalColumn(
                    planPoint,
                    bottomZ,
                    topZ,
                    symbol,
                    out wasCreated);

            return wasCreated
                ? instance
                : null;
        }

        public FamilyInstance CreateOrGetVerticalColumn(
            XYZ planPoint,
            double bottomZ,
            double topZ,
            FamilySymbol symbol,
            out bool wasCreated)
        {
            return CreateOrGetVerticalColumn(
                planPoint,
                bottomZ,
                topZ,
                symbol,
                null,
                null,
                out wasCreated);
        }

        public FamilyInstance CreateOrGetVerticalColumn(
            XYZ planPoint,
            double bottomZ,
            double topZ,
            FamilySymbol symbol,
            Element baseReferenceElement,
            Element topReferenceElement,
            out bool wasCreated)
        {
            wasCreated = false;

            if (planPoint == null)
                throw new ArgumentNullException("planPoint");

            if (symbol == null)
                throw new ArgumentNullException("symbol");

            double requestedHeight = topZ - bottomZ;
            double minimumHeight =
                JackSupportGeometryHelper.MmToInternal(10.0);

            // Revit 구조기둥은 높이가 0이거나 매우 작은 상태로
            // 생성될 경우 "열 높이가 0이 되지 않도록..." 오류를 발생시킨다.
            // 실제 계산 높이가 10mm 이하인 위치는 생성하지 않는다.
            if (requestedHeight <= minimumHeight)
                return null;

            XYZ targetPoint =
                new XYZ(planPoint.X, planPoint.Y, bottomZ);

            IList<Level> levels =
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(level => level.Elevation)
                    .ToList();

            if (levels.Count == 0)
            {
                throw new InvalidOperationException(
                    "프로젝트에 레벨이 없습니다.");
            }

            Level baseLevel =
                ResolvePreferredBaseLevel(
                    levels,
                    baseReferenceElement,
                    bottomZ);

            Level topLevel =
                ResolvePreferredTopLevel(
                    levels,
                    topReferenceElement,
                    topZ);

            FamilyInstance duplicate =
                FindDuplicate(
                    targetPoint,
                    bottomZ,
                    topZ);

            if (duplicate != null)
            {
                TryApplyRequestedSymbol(
                    duplicate,
                    symbol);

                ApplyVerticalConstraints(
                    duplicate,
                    bottomZ,
                    topZ,
                    baseLevel,
                    topLevel);

                _doc.Regenerate();

                ApplyHeightParameterRules(
                    duplicate,
                    GetActualHeightOrFallback(
                        duplicate,
                        topZ - bottomZ));

                if (_settings.ApplyColorToExistingSupports)
                {
                    ApplyViewColorOverride(
                        duplicate,
                        false,
                        null);
                }

                return duplicate;
            }

            XYZ insertionPoint =
                new XYZ(
                    planPoint.X,
                    planPoint.Y,
                    baseLevel.Elevation);

            FamilyInstance instance =
                _doc.Create.NewFamilyInstance(
                    insertionPoint,
                    symbol,
                    baseLevel,
                    StructuralType.Column);

            // 중요:
            // 새 구조기둥은 처음 생성되는 순간 상단과 하단이 같은 레벨로
            // 설정될 수 있다. 이 상태에서 하단 간격띄우기를 먼저 변경하면
            // Revit이 높이 0 또는 음수 높이의 중간 상태를 검사하여
            // "열 높이가 0이 되지 않도록..." 오류를 발생시킨다.
            //
            // 따라서 반드시 상단 구속/높이를 먼저 양수로 만든 후
            // 마지막에 하단 간격띄우기를 설정한다.
            ApplyVerticalConstraints(
                instance,
                bottomZ,
                topZ,
                baseLevel,
                topLevel);

            _doc.Regenerate();
            _createdOrExistingSupports.Add(instance);
            _newlyCreatedSupportElementIds.Add(
                instance.Id.IntegerValue);
            wasCreated = true;

            ApplyHeightParameterRules(
                instance,
                topZ - bottomZ);

            ApplyViewColorOverride(
                instance,
                true,
                null);

            return instance;
        }

        public void ApplyFloorClassification(
            FamilyInstance instance,
            bool isLowestFloor)
        {
            ApplyFloorClassificationInternal(
                instance,
                isLowestFloor,
                false);
        }

        private void ApplyFloorClassificationInternal(
            FamilyInstance instance,
            bool isLowestFloor,
            bool forceColor)
        {
            if (instance == null ||
                !_settings.EnableLowestFloorClassification)
            {
                return;
            }

            int elementId = instance.Id.IntegerValue;

            if (!_floorClassificationProcessedElementIds.Add(elementId))
                return;

            FloorClassificationStatistics.ProcessedCount++;

            if (isLowestFloor)
                FloorClassificationStatistics.LowestFloorCount++;
            else
                FloorClassificationStatistics.OtherFloorCount++;

            ApplyFloorClassificationTextValue(
                instance,
                elementId,
                isLowestFloor);

            ApplyFloorClassificationCountValues(
                instance,
                elementId,
                isLowestFloor);

            bool wasCreated =
                _newlyCreatedSupportElementIds.Contains(
                    elementId);

            ApplyViewColorOverride(
                instance,
                wasCreated,
                isLowestFloor,
                forceColor);
        }

        public void ReapplyExistingSupportJudgment(
            FamilyInstance instance,
            bool isLowestFloor)
        {
            if (instance == null)
                return;

            int elementId = instance.Id.IntegerValue;

            _heightRuleProcessedElementIds.Remove(elementId);
            _viewColorProcessedElementIds.Remove(elementId);
            _floorClassificationProcessedElementIds.Remove(elementId);
            _heightRuleNoMatchElementIds.Remove(elementId);
            _heightRuleMatchByElementId.Remove(elementId);

            double bottomZ;
            double topZ;

            if (JackSupportClassificationService.TryGetSupportVerticalRange(
                instance,
                out bottomZ,
                out topZ) &&
                topZ > bottomZ)
            {
                ApplyHeightParameterRules(
                    instance,
                    topZ - bottomZ);
            }

            if (_settings.EnableLowestFloorClassification)
            {
                ApplyFloorClassificationInternal(
                    instance,
                    isLowestFloor,
                    true);
            }
            else
            {
                ApplyViewColorOverride(
                    instance,
                    true,
                    null,
                    true);
            }
        }

        public void ApplyUniformColorToExistingSupport(
            FamilyInstance instance)
        {
            if (instance == null)
                return;

            int elementId = instance.Id.IntegerValue;
            _viewColorProcessedElementIds.Remove(elementId);

            ApplyExplicitViewColorOverride(
                instance,
                _settings.ViewColorRed,
                _settings.ViewColorGreen,
                _settings.ViewColorBlue,
                "공통 일괄");
        }

        private void ApplyFloorClassificationTextValue(
            FamilyInstance instance,
            int elementId,
            bool isLowestFloor)
        {
            string parameterName =
                _settings.FloorClassificationParameterName;

            string value =
                isLowestFloor
                    ? _settings.LowestFloorClassificationValue
                    : _settings.OtherFloorClassificationValue;

            string error;

            if (TrySetInstanceParameterTextValue(
                instance,
                parameterName,
                value,
                out error))
            {
                FloorClassificationStatistics.WrittenCount++;
                FloorClassificationStatistics
                    .ClassificationTextWrittenCount++;
            }
            else
            {
                FloorClassificationStatistics.FailureCount++;
                FloorClassificationStatistics
                    .ClassificationTextFailureCount++;

                FloorClassificationStatistics.AddError(
                    "ElementId " + elementId +
                    " / " +
                    (isLowestFloor ? "최하층" : "그외층") +
                    " / 분류값 / " + error);
            }
        }

        private void ApplyFloorClassificationCountValues(
            FamilyInstance instance,
            int elementId,
            bool isLowestFloor)
        {
            if (!_settings.EnableFloorClassificationCountParameters)
                return;

            string activeParameterName =
                isLowestFloor
                    ? _settings.LowestFloorCountParameterName
                    : _settings.OtherFloorCountParameterName;

            string inactiveParameterName =
                isLowestFloor
                    ? _settings.OtherFloorCountParameterName
                    : _settings.LowestFloorCountParameterName;

            ApplyFloorClassificationCountValue(
                instance,
                elementId,
                isLowestFloor,
                activeParameterName,
                1);

            if (_settings.ResetFloorClassificationCountParametersBeforeApply)
            {
                ApplyFloorClassificationCountValue(
                    instance,
                    elementId,
                    isLowestFloor,
                    inactiveParameterName,
                    0);
            }
        }

        private void ApplyFloorClassificationCountValue(
            FamilyInstance instance,
            int elementId,
            bool isLowestFloor,
            string parameterName,
            int value)
        {
            string error;

            if (TrySetInstanceParameterTextValue(
                instance,
                parameterName,
                value.ToString(CultureInfo.InvariantCulture),
                out error))
            {
                FloorClassificationStatistics.WrittenCount++;
                FloorClassificationStatistics
                    .CountParameterWrittenCount++;
            }
            else
            {
                FloorClassificationStatistics.FailureCount++;
                FloorClassificationStatistics
                    .CountParameterFailureCount++;

                FloorClassificationStatistics.AddError(
                    "ElementId " + elementId +
                    " / " +
                    (isLowestFloor ? "최하층" : "그외층") +
                    " / 수량 매개변수 '" +
                    parameterName +
                    "' / " + error);
            }
        }

        public static bool IsConfiguredJackSupportElement(
            Document doc,
            Element element,
            JackSupportSettings settings)
        {
            if (doc == null ||
                element == null ||
                settings == null)
            {
                return false;
            }

            FamilySymbol symbol =
                doc.GetElement(element.GetTypeId()) as FamilySymbol;

            if (symbol == null)
                return false;

            string familyName =
                symbol.Family == null
                    ? string.Empty
                    : symbol.Family.Name;

            bool matchesConfiguredFamilyType =
                settings.UsesSpecifiedFamilyType() &&
                string.Equals(
                    familyName,
                    settings.SourceRoundColumnFamilyName,
                    StringComparison.OrdinalIgnoreCase) &&
                (
                    string.Equals(
                        symbol.Name,
                        settings.SourceRoundColumnTypeName,
                        StringComparison.OrdinalIgnoreCase) ||
                    StartsWithTaggedTypeName(
                        symbol.Name,
                        settings.SourceRoundColumnTypeName)
                );

            bool matchesGeneratedType =
                !string.IsNullOrWhiteSpace(
                    settings.GeneratedTypeName) &&
                (
                    string.Equals(
                        symbol.Name,
                        settings.GeneratedTypeName,
                        StringComparison.OrdinalIgnoreCase) ||
                    StartsWithTaggedTypeName(
                        symbol.Name,
                        settings.GeneratedTypeName)
                );

            // 이전 설정으로 생성한 잭서포트도 일괄 처리할 수 있도록
            // 공개용 대표 이름이 포함된 패밀리/유형도 추가로 인식한다.
            bool hasJackSupportName =
                ContainsJackSupportName(familyName) ||
                ContainsJackSupportName(symbol.Name);

            return
                matchesConfiguredFamilyType ||
                matchesGeneratedType ||
                hasJackSupportName;
        }


        private static bool StartsWithTaggedTypeName(
            string actualTypeName,
            string baseTypeName)
        {
            if (string.IsNullOrWhiteSpace(
                    actualTypeName) ||
                string.IsNullOrWhiteSpace(
                    baseTypeName))
            {
                return false;
            }

            return actualTypeName.StartsWith(
                baseTypeName.Trim() + "_",
                StringComparison.OrdinalIgnoreCase
            );
        }

        private string BuildSourceTaggedTypeName(
            FamilySymbol baseSymbol,
            string sourceKind,
            string sourceStandardName)
        {
            string baseTypeName =
                baseSymbol == null
                    ? string.Empty
                    : baseSymbol.Name;

            string cleanBaseName =
                SanitizeTypeNamePart(
                    baseTypeName,
                    "PORTFOLIO_JACK_SUPPORT");

            string cleanSourceKind =
                SanitizeTypeNamePart(
                    sourceKind,
                    "생성원인");

            string cleanStandardName =
                SanitizeTypeNamePart(
                    sourceStandardName,
                    "미지정");

            string typeName =
                cleanBaseName +
                "_" +
                cleanSourceKind +
                "_" +
                cleanStandardName;

            const int maximumLength = 240;

            if (typeName.Length > maximumLength)
            {
                typeName =
                    typeName.Substring(
                        0,
                        maximumLength
                    );
            }

            return typeName.Trim('_', ' ');
        }

        private static string SanitizeTypeNamePart(
            string value,
            string fallback)
        {
            string clean =
                string.IsNullOrWhiteSpace(value)
                    ? fallback
                    : value.Trim();

            char[] invalidCharacters =
            {
                '\\',
                '/',
                ':',
                ';',
                '{',
                '}',
                '[',
                ']',
                '|',
                '<',
                '>',
                '?',
                '*',
                '\r',
                '\n',
                '\t'
            };

            foreach (char invalidCharacter in
                invalidCharacters)
            {
                clean =
                    clean.Replace(
                        invalidCharacter,
                        '_'
                    );
            }

            while (clean.Contains("__"))
            {
                clean =
                    clean.Replace("__", "_");
            }

            clean =
                clean.Trim('_', ' ');

            return string.IsNullOrWhiteSpace(clean)
                ? fallback
                : clean;
        }

        private static void TryApplyRequestedSymbol(
            FamilyInstance instance,
            FamilySymbol requestedSymbol)
        {
            if (instance == null ||
                requestedSymbol == null)
            {
                return;
            }

            try
            {
                ElementId currentTypeId =
                    instance.GetTypeId();

                if (currentTypeId == null ||
                    currentTypeId.IntegerValue !=
                        requestedSymbol.Id
                            .IntegerValue)
                {
                    instance.ChangeTypeId(
                        requestedSymbol.Id
                    );
                }
            }
            catch
            {
                // 기존 중복 객체의 유형 변경이 불가능한 경우
                // 생성 자체와 데이터 입력은 계속 진행한다.
            }
        }

        private static bool ContainsJackSupportName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized =
                value
                    .Trim()
                    .Replace(" ", string.Empty)
                    .ToUpperInvariant();

            return
                normalized.Contains("잭서포트") ||
                normalized.Contains("PORTFOLIO_JACK_SUPPORT") ||
                normalized.Contains("JACK_SUPPORT") ||
                normalized.Contains("JACKSUPPORT") ||
                normalized.Contains("JACKSURPPORT");
        }

        public static IList<FamilyInstance>
            CollectConfiguredJackSupports(
                Document doc,
                JackSupportSettings settings)
        {
            if (doc == null || settings == null)
                return new List<FamilyInstance>();

            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .OfType<FamilyInstance>()
                .Where(instance =>
                    IsConfiguredJackSupportElement(
                        doc,
                        instance,
                        settings))
                .ToList();
        }

        private FamilySymbol GetSpecifiedFamilySymbol()
        {
            if (string.IsNullOrWhiteSpace(
                    _settings.SourceRoundColumnFamilyName) ||
                string.IsNullOrWhiteSpace(
                    _settings.SourceRoundColumnTypeName))
            {
                throw new InvalidOperationException(
                    "지정 패밀리 직접 사용 모드에서는 패밀리명과 유형명이 모두 필요합니다.");
            }

            FamilySymbol symbol =
                FindExactSpecifiedSymbol();

            string rfaPath =
                ResolveJackSupportRfaPath();

            Family loadedFamily = null;

            if (symbol == null &&
                File.Exists(rfaPath))
            {
                loadedFamily =
                    LoadConfiguredFamily(rfaPath);

                symbol = FindExactSpecifiedSymbol();

                if (symbol == null)
                {
                    symbol =
                        FindBestLoadedFamilySymbol(
                            loadedFamily);
                }
            }

            if (symbol == null)
            {
                symbol =
                    FindSpecifiedFamilyFirstSymbol();
            }

            if (symbol == null)
            {
                symbol =
                    FindRfaNamedFamilySymbol(
                        rfaPath);
            }

            if (symbol == null)
            {
                symbol =
                    FindJackSupportNamedSymbol();
            }

            if (symbol == null)
            {
                string rfaInformation =
                    File.Exists(rfaPath)
                        ? rfaPath
                        : "RFA 파일 없음 또는 경로 오류: " +
                          rfaPath;

                throw new InvalidOperationException(
                    "지정한 잭서포트 구조기둥 패밀리 유형을 찾지 못했습니다.\n\n" +
                    "패밀리명: " +
                    _settings.SourceRoundColumnFamilyName +
                    "\n유형명: " +
                    _settings.SourceRoundColumnTypeName +
                    "\nRFA 경로: " +
                    rfaInformation +
                    "\n\n패밀리는 반드시 구조기둥 카테고리여야 합니다." +
                    "\n현재 프로젝트의 구조기둥 유형:" +
                    BuildStructuralColumnSymbolSummary());
            }

            ActivateSymbol(symbol);

            SynchronizeResolvedFamilyType(
                symbol,
                rfaPath);

            // 지정한 패밀리 유형은 그대로 사용하며
            // 지름이나 유형명을 자동 변경하지 않음.
            return symbol;
        }

        private FamilySymbol GetOrCreateAutomaticSymbol()
        {
            FamilySymbol target =
                FindAutomaticTargetSymbol();

            if (target != null)
            {
                ActivateSymbol(target);
                TrySetDiameter(target);
                return target;
            }

            FamilySymbol source =
                FindAutomaticRoundSourceSymbol();

            Family loadedFamily = null;

            string rfaPath =
                ResolveJackSupportRfaPath();

            if (source == null &&
                File.Exists(rfaPath))
            {
                _doc.LoadFamily(
                    rfaPath,
                    new JackSupportFamilyLoadOptions(),
                    out loadedFamily);

                target = FindAutomaticTargetSymbol();

                if (target != null)
                {
                    ActivateSymbol(target);
                    TrySetDiameter(target);
                    return target;
                }

                source = FindAutomaticRoundSourceSymbol();

                if (source == null &&
                    loadedFamily != null)
                {
                    source =
                        loadedFamily
                            .GetFamilySymbolIds()
                            .Select(id =>
                                _doc.GetElement(id) as FamilySymbol)
                            .FirstOrDefault(symbol =>
                                symbol != null &&
                                IsStructuralColumnSymbol(symbol));
                }
            }

            if (source == null)
            {
                throw new InvalidOperationException(
                    "잭서포트 유형을 만들 원형 구조기둥 유형을 찾지 못했습니다.\n\n" +
                    "기본 생성 탭에서 사용할 패밀리명과 유형명을 지정하거나 다음 RFA 파일을 배치해 주십시오.\n" +
                    rfaPath);
            }

            ElementType duplicated =
                source.Duplicate(
                    _settings.GeneratedTypeName);

            target = duplicated as FamilySymbol;

            if (target == null)
            {
                throw new InvalidOperationException(
                    "원형 구조기둥 유형을 복제하지 못했습니다: " +
                    _settings.GeneratedTypeName);
            }

            ActivateSymbol(target);
            TrySetDiameter(target);
            return target;
        }

        private FamilySymbol FindExactSpecifiedSymbol()
        {
            return GetStructuralColumnSymbols()
                .FirstOrDefault(symbol =>
                {
                    string familyName =
                        symbol.Family == null
                            ? string.Empty
                            : symbol.Family.Name;

                    return
                        string.Equals(
                            familyName,
                            _settings.SourceRoundColumnFamilyName,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            symbol.Name,
                            _settings.SourceRoundColumnTypeName,
                            StringComparison.OrdinalIgnoreCase);
                });
        }

        private FamilySymbol FindSpecifiedFamilyFirstSymbol()
        {
            string configuredFamilyName =
                (_settings.SourceRoundColumnFamilyName ??
                 string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(
                configuredFamilyName))
            {
                return null;
            }

            return GetStructuralColumnSymbols()
                .Where(symbol => symbol != null)
                .Where(symbol =>
                {
                    string familyName =
                        symbol.Family == null
                            ? string.Empty
                            : symbol.Family.Name;

                    return string.Equals(
                        familyName,
                        configuredFamilyName,
                        StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(symbol =>
                    string.Equals(
                        symbol.Name,
                        _settings.SourceRoundColumnTypeName,
                        StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : 1)
                .ThenBy(symbol => symbol.Name)
                .FirstOrDefault();
        }

        private FamilySymbol FindBestLoadedFamilySymbol(
            Family loadedFamily)
        {
            if (loadedFamily == null)
            {
                return null;
            }

            List<FamilySymbol> symbols =
                loadedFamily
                    .GetFamilySymbolIds()
                    .Select(id =>
                        _doc.GetElement(id) as FamilySymbol)
                    .Where(symbol =>
                        symbol != null &&
                        IsStructuralColumnSymbol(symbol))
                    .ToList();

            return symbols
                .OrderBy(symbol =>
                    string.Equals(
                        symbol.Name,
                        _settings.SourceRoundColumnTypeName,
                        StringComparison.OrdinalIgnoreCase)
                            ? 0
                            : 1)
                .ThenBy(symbol =>
                    ContainsJackSupportName(symbol.Name)
                        ? 0
                        : 1)
                .ThenBy(symbol => symbol.Name)
                .FirstOrDefault();
        }

        private FamilySymbol FindRfaNamedFamilySymbol(
            string rfaPath)
        {
            string rfaFamilyName =
                string.IsNullOrWhiteSpace(rfaPath)
                    ? string.Empty
                    : Path.GetFileNameWithoutExtension(
                        rfaPath).Trim();

            if (string.IsNullOrWhiteSpace(rfaFamilyName))
            {
                return null;
            }

            return GetStructuralColumnSymbols()
                .Where(symbol =>
                    symbol != null &&
                    symbol.Family != null &&
                    string.Equals(
                        symbol.Family.Name,
                        rfaFamilyName,
                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(symbol => symbol.Name)
                .FirstOrDefault();
        }

        private FamilySymbol FindJackSupportNamedSymbol()
        {
            return GetStructuralColumnSymbols()
                .Where(symbol => symbol != null)
                .Where(symbol =>
                    ContainsJackSupportName(
                        symbol.Family == null
                            ? string.Empty
                            : symbol.Family.Name) ||
                    ContainsJackSupportName(
                        symbol.Name))
                .OrderBy(symbol => symbol.Name)
                .FirstOrDefault();
        }

        private FamilySymbol FindAutomaticTargetSymbol()
        {
            return GetStructuralColumnSymbols()
                .FirstOrDefault(symbol =>
                    string.Equals(
                        symbol.Name,
                        _settings.GeneratedTypeName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private FamilySymbol FindAutomaticRoundSourceSymbol()
        {
            string[] roundKeywords =
            {
                "원형",
                "원기둥",
                "round",
                "circular",
                "circle"
            };

            return GetStructuralColumnSymbols()
                .FirstOrDefault(symbol =>
                {
                    string familyName =
                        symbol.Family == null
                            ? string.Empty
                            : symbol.Family.Name;

                    string text =
                        (familyName + " " + symbol.Name)
                            .ToLowerInvariant();

                    return roundKeywords.Any(
                        keyword => text.Contains(
                            keyword.ToLowerInvariant()));
                });
        }

        private Family LoadConfiguredFamily(
            string rfaPath)
        {
            Family loadedFamily;

            _doc.LoadFamily(
                rfaPath,
                new JackSupportFamilyLoadOptions(),
                out loadedFamily);

            return loadedFamily;
        }

        private string ResolveJackSupportRfaPath()
        {
            if (File.Exists(FixedJackSupportRfaPath))
            {
                return FixedJackSupportRfaPath;
            }

            return _settings == null
                ? FixedJackSupportRfaPath
                : _settings.FamilyRfaPath;
        }

        private void SynchronizeResolvedFamilyType(
            FamilySymbol symbol,
            string rfaPath)
        {
            if (symbol == null || _settings == null)
            {
                return;
            }

            string familyName =
                symbol.Family == null
                    ? string.Empty
                    : symbol.Family.Name;

            TrySetSettingStringProperty(
                _settings,
                "SourceRoundColumnFamilyName",
                familyName);

            TrySetSettingStringProperty(
                _settings,
                "SourceRoundColumnTypeName",
                symbol.Name);

            TrySetSettingStringProperty(
                _settings,
                "FamilyRfaPath",
                rfaPath);

            TrySaveSettings(_settings);
        }

        private static void TrySetSettingStringProperty(
            JackSupportSettings settings,
            string propertyName,
            string value)
        {
            try
            {
                PropertyInfo property =
                    settings
                        .GetType()
                        .GetProperty(
                            propertyName,
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);

                if (property != null &&
                    property.CanWrite &&
                    property.PropertyType == typeof(string))
                {
                    property.SetValue(
                        settings,
                        value ?? string.Empty,
                        null);
                }
            }
            catch
            {
                // 현재 실행에는 선택된 FamilySymbol을 직접 사용하므로
                // 설정 반영 실패가 생성 작업을 막지 않도록 한다.
            }
        }

        private static void TrySaveSettings(
            JackSupportSettings settings)
        {
            try
            {
                Type storeType =
                    settings
                        .GetType()
                        .Assembly
                        .GetType(
                            "REVIT_TAP.JackSupportSettingsStore",
                            false,
                            false);

                if (storeType == null)
                {
                    return;
                }

                MethodInfo saveMethod =
                    storeType
                        .GetMethods(
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic)
                        .FirstOrDefault(method =>
                        {
                            if (!string.Equals(
                                method.Name,
                                "Save",
                                StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }

                            ParameterInfo[] parameters =
                                method.GetParameters();

                            return
                                parameters.Length == 1 &&
                                parameters[0]
                                    .ParameterType
                                    .IsAssignableFrom(
                                        settings.GetType());
                        });

                if (saveMethod != null)
                {
                    saveMethod.Invoke(
                        null,
                        new object[] { settings });
                }
            }
            catch
            {
                // 설정 파일 저장 실패는 현재 생성 작업에 영향 주지 않음
            }
        }

        private string BuildStructuralColumnSymbolSummary()
        {
            List<string> lines =
                GetStructuralColumnSymbols()
                    .Where(symbol => symbol != null)
                    .Select(symbol =>
                    {
                        string familyName =
                            symbol.Family == null
                                ? string.Empty
                                : symbol.Family.Name;

                        return
                            "\n- " +
                            familyName +
                            " / " +
                            symbol.Name;
                    })
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Take(20)
                    .ToList();

            return lines.Count == 0
                ? "\n- 없음"
                : string.Join(
                    string.Empty,
                    lines.ToArray());
        }

        private IList<FamilySymbol> GetStructuralColumnSymbols()
        {
            return
                new FilteredElementCollector(_doc)
                    .OfCategory(
                        BuiltInCategory.OST_StructuralColumns)
                    .WhereElementIsElementType()
                    .OfType<FamilySymbol>()
                    .ToList();
        }

        private static bool IsStructuralColumnSymbol(
            FamilySymbol symbol)
        {
            if (symbol == null ||
                symbol.Category == null)
            {
                return false;
            }

            return symbol.Category.Id.IntegerValue ==
                   (int)BuiltInCategory.OST_StructuralColumns;
        }

        private void ActivateSymbol(FamilySymbol symbol)
        {
            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }
        }

        private void TrySetDiameter(FamilySymbol symbol)
        {
            if (symbol == null ||
                _settings.DiameterMm <= 0.0)
            {
                return;
            }

            double diameter =
                JackSupportGeometryHelper.MmToInternal(
                    _settings.DiameterMm);

            List<string> parameterNames =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(
                _settings.DiameterParameterName))
            {
                parameterNames.Add(
                    _settings.DiameterParameterName);
            }

            parameterNames.AddRange(
                new[]
                {
                    "지름",
                    "직경",
                    "Diameter",
                    "D"
                });

            foreach (string name in parameterNames
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Parameter parameter =
                    symbol.LookupParameter(name);

                if (parameter == null ||
                    parameter.IsReadOnly)
                {
                    continue;
                }

                if (parameter.StorageType ==
                    StorageType.Double)
                {
                    parameter.Set(diameter);
                    return;
                }
            }
        }

        private List<FamilyInstance>
            CollectExistingGeneratedSupports()
        {
            return CollectConfiguredJackSupports(
                _doc,
                _settings)
                .ToList();
        }

        private FamilyInstance FindDuplicate(
            XYZ point,
            double bottomZ,
            double topZ)
        {
            foreach (FamilyInstance instance in
                _createdOrExistingSupports)
            {
                XYZ existingPoint =
                    GetInstancePlanPoint(instance);

                if (existingPoint == null)
                    continue;

                double dx =
                    existingPoint.X - point.X;

                double dy =
                    existingPoint.Y - point.Y;

                double planDistance =
                    Math.Sqrt(dx * dx + dy * dy);

                if (planDistance > _duplicatePlanTolerance)
                    continue;

                double existingBottom;
                double existingTop;

                // 구조기둥의 레벨/간격띄우기 매개변수에서 먼저 정확한 높이 구간을 읽는다.
                // 형상 BoundingBox는 상·하부 캡이나 부속 형상 때문에 실제 구속 높이와 다를 수 있다.
                if (!TryGetColumnConstraintVerticalRange(
                    instance,
                    out existingBottom,
                    out existingTop))
                {
                    if (!JackSupportGeometryHelper.TryGetElementVerticalRange(
                        instance,
                        out existingBottom,
                        out existingTop))
                    {
                        // 기존 높이 구간을 확인할 수 없다는 이유만으로 신규 생성을 막지 않는다.
                        continue;
                    }
                }

                double bottomDifference =
                    Math.Abs(existingBottom - bottomZ);

                double topDifference =
                    Math.Abs(existingTop - topZ);

                bool sameVerticalSpan =
                    bottomDifference <= _duplicateVerticalTolerance &&
                    topDifference <= _duplicateVerticalTolerance;

                // 단순 접촉 또는 일부 겹침은 중복이 아니다.
                // 동일 XY 위치이면서 하단과 상단이 모두 사실상 같은 경우에만 중복으로 처리한다.
                if (sameVerticalSpan)
                    return instance;
            }

            return null;
        }

        private static XYZ GetInstancePlanPoint(
            FamilyInstance instance)
        {
            if (instance == null)
                return null;

            LocationPoint locationPoint =
                instance.Location as LocationPoint;

            if (locationPoint != null)
                return locationPoint.Point;

            return JackSupportGeometryHelper.GetElementCenter(
                instance);
        }

        private bool TryGetColumnConstraintVerticalRange(
            FamilyInstance instance,
            out double bottomZ,
            out double topZ)
        {
            bottomZ = 0.0;
            topZ = 0.0;

            if (instance == null)
                return false;

            try
            {
                Parameter baseLevelParameter =
                    instance.get_Parameter(
                        BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);

                if (baseLevelParameter == null ||
                    baseLevelParameter.StorageType != StorageType.ElementId)
                {
                    return false;
                }

                Level baseLevel =
                    _doc.GetElement(
                        baseLevelParameter.AsElementId()) as Level;

                if (baseLevel == null)
                    return false;

                double baseOffset =
                    GetDoubleParameterValue(
                        instance,
                        BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);

                bottomZ =
                    baseLevel.Elevation +
                    baseOffset;

                Parameter topLevelParameter =
                    instance.get_Parameter(
                        BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);

                if (topLevelParameter != null &&
                    topLevelParameter.StorageType == StorageType.ElementId)
                {
                    Level topLevel =
                        _doc.GetElement(
                            topLevelParameter.AsElementId()) as Level;

                    if (topLevel != null)
                    {
                        double topOffset =
                            GetDoubleParameterValue(
                                instance,
                                BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);

                        topZ =
                            topLevel.Elevation +
                            topOffset;

                        if (topZ > bottomZ)
                            return true;
                    }
                }

                Parameter heightParameter =
                    instance.get_Parameter(
                        BuiltInParameter.FAMILY_HEIGHT_PARAM);

                if (heightParameter != null &&
                    heightParameter.StorageType == StorageType.Double)
                {
                    double height =
                        heightParameter.AsDouble();

                    if (height > 0.0)
                    {
                        topZ = bottomZ + height;
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static double GetDoubleParameterValue(
            Element element,
            BuiltInParameter builtInParameter)
        {
            if (element == null)
                return 0.0;

            Parameter parameter =
                element.get_Parameter(builtInParameter);

            if (parameter == null ||
                parameter.StorageType != StorageType.Double)
            {
                return 0.0;
            }

            return parameter.AsDouble();
        }

        private double GetActualHeightOrFallback(
            FamilyInstance instance,
            double fallbackHeight)
        {
            double bottomZ;
            double topZ;

            if (instance != null &&
                JackSupportGeometryHelper.TryGetElementVerticalRange(
                    instance,
                    out bottomZ,
                    out topZ) &&
                topZ > bottomZ)
            {
                return topZ - bottomZ;
            }

            return fallbackHeight;
        }

        private void ApplyViewColorOverride(
            FamilyInstance instance,
            bool wasCreated,
            bool? isLowestFloor)
        {
            ApplyViewColorOverride(
                instance,
                wasCreated,
                isLowestFloor,
                false);
        }

        private void ApplyViewColorOverride(
            FamilyInstance instance,
            bool wasCreated,
            bool? isLowestFloor,
            bool forceApply)
        {
            if (instance == null)
                return;

            if (!_settings.EnableViewColorOverride &&
                !forceApply)
            {
                return;
            }

            if (!forceApply &&
                !wasCreated &&
                !_settings.ApplyColorToExistingSupports)
            {
                return;
            }

            bool useFloorClassificationColors =
                _settings.ColorClassificationMode ==
                    JackSupportColorClassificationMode
                        .FloorClassification &&
                _settings.UseSeparateFloorColors &&
                _settings.EnableLowestFloorClassification;

            bool useHeightRuleColors =
                _settings.ColorClassificationMode ==
                    JackSupportColorClassificationMode
                        .HeightParameterRule &&
                _settings.EnableHeightParameterRules;

            // 최하층 기준을 선택했을 때는 층 판정이 끝난 뒤 색상을 적용한다.
            if (useFloorClassificationColors &&
                !isLowestFloor.HasValue)
            {
                return;
            }

            int elementId = instance.Id.IntegerValue;

            if (!_viewColorProcessedElementIds.Add(elementId))
                return;

            View activeView = _doc.ActiveView;

            if (activeView == null ||
                activeView.IsTemplate)
            {
                ViewColorStatistics.FailureCount++;
                ViewColorStatistics.AddError(
                    "ElementId " + elementId +
                    " / 현재 활성 뷰가 없거나 뷰 템플릿입니다.");
                return;
            }

            try
            {
                int red = _settings.ViewColorRed;
                int green = _settings.ViewColorGreen;
                int blue = _settings.ViewColorBlue;
                string classificationText = "공통";

                if (useHeightRuleColors)
                {
                    JackSupportHeightParameterRule matchedRule;

                    if (_heightRuleMatchByElementId.TryGetValue(
                        elementId,
                        out matchedRule) &&
                        matchedRule != null)
                    {
                        red = matchedRule.ColorRed;
                        green = matchedRule.ColorGreen;
                        blue = matchedRule.ColorBlue;
                        classificationText =
                            "높이 " +
                            matchedRule.MinimumHeightMm.ToString("0.###") +
                            "~" +
                            matchedRule.MaximumHeightMm.ToString("0.###") +
                            "mm";
                    }
                    else if (_settings.EnableUnmatchedHeightColor &&
                        _heightRuleNoMatchElementIds.Contains(elementId))
                    {
                        red = _settings.UnmatchedHeightColorRed;
                        green = _settings.UnmatchedHeightColorGreen;
                        blue = _settings.UnmatchedHeightColorBlue;
                        classificationText = "높이구간없음";
                    }
                }
                else if (useFloorClassificationColors &&
                    isLowestFloor.HasValue)
                {
                    if (isLowestFloor.Value)
                    {
                        red = _settings.LowestFloorColorRed;
                        green = _settings.LowestFloorColorGreen;
                        blue = _settings.LowestFloorColorBlue;
                        classificationText = "최하층";
                    }
                    else
                    {
                        red = _settings.OtherFloorColorRed;
                        green = _settings.OtherFloorColorGreen;
                        blue = _settings.OtherFloorColorBlue;
                        classificationText = "그외층";
                    }
                }

                Autodesk.Revit.DB.Color fillColor =
                    new Autodesk.Revit.DB.Color(
                        Convert.ToByte(
                            ClampColorComponent(red)),
                        Convert.ToByte(
                            ClampColorComponent(green)),
                        Convert.ToByte(
                            ClampColorComponent(blue)));

                Autodesk.Revit.DB.Color lineColor = fillColor;
                bool isBtsColumnBased =
                    IsBtsColumnBasedSupport(instance);

                if (isBtsColumnBased &&
                    _settings.EnableBtsColumnBasedOutline)
                {
                    lineColor =
                        new Autodesk.Revit.DB.Color(
                            Convert.ToByte(
                                ClampColorComponent(
                                    _settings.BtsColumnBasedOutlineRed)),
                            Convert.ToByte(
                                ClampColorComponent(
                                    _settings.BtsColumnBasedOutlineGreen)),
                            Convert.ToByte(
                                ClampColorComponent(
                                    _settings.BtsColumnBasedOutlineBlue)));
                }

                OverrideGraphicSettings overrides =
                    new OverrideGraphicSettings();

                overrides.SetProjectionLineColor(lineColor);
                overrides.SetCutLineColor(lineColor);

                if (isBtsColumnBased &&
                    _settings.EnableBtsColumnBasedOutline)
                {
                    int lineWeight = Math.Max(
                        1,
                        Math.Min(
                            16,
                            _settings.BtsColumnBasedOutlineLineWeight));

                    overrides.SetProjectionLineWeight(lineWeight);
                    overrides.SetCutLineWeight(lineWeight);
                }

                ElementId solidFillPatternId =
                    GetSolidFillPatternId();

                if (solidFillPatternId != null &&
                    solidFillPatternId != ElementId.InvalidElementId)
                {
                    overrides.SetSurfaceForegroundPatternId(
                        solidFillPatternId);
                    overrides.SetSurfaceForegroundPatternColor(
                        fillColor);
                    overrides.SetCutForegroundPatternId(
                        solidFillPatternId);
                    overrides.SetCutForegroundPatternColor(
                        fillColor);
                }

                activeView.SetElementOverrides(
                    instance.Id,
                    overrides);

                ViewColorStatistics.AppliedCount++;

                if (classificationText == "높이구간없음")
                {
                    ViewColorStatistics.UnmatchedHeightAppliedCount++;
                }
                else if (classificationText.StartsWith(
                    "높이 ",
                    StringComparison.Ordinal))
                {
                    ViewColorStatistics.HeightRuleAppliedCount++;
                }
                else if (classificationText == "최하층")
                {
                    ViewColorStatistics.LowestFloorAppliedCount++;
                }
                else if (classificationText == "그외층")
                {
                    ViewColorStatistics.OtherFloorAppliedCount++;
                }
                else
                {
                    ViewColorStatistics.CommonAppliedCount++;
                }

                if (isBtsColumnBased &&
                    _settings.EnableBtsColumnBasedOutline)
                {
                    ViewColorStatistics.BtsColumnOutlineAppliedCount++;
                }
            }
            catch (Exception ex)
            {
                ViewColorStatistics.FailureCount++;
                ViewColorStatistics.AddError(
                    "ElementId " + elementId +
                    " / 색상 적용 / " +
                    ex.Message);
            }
        }

        private bool IsBtsColumnBasedSupport(
            FamilyInstance instance)
        {
            if (instance == null)
                return false;

            try
            {
                ElementType elementType =
                    _doc.GetElement(instance.GetTypeId())
                    as ElementType;

                string typeName =
                    elementType == null
                        ? string.Empty
                        : elementType.Name ?? string.Empty;

                return typeName.IndexOf(
                    "_단부기둥기준",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyExplicitViewColorOverride(
            FamilyInstance instance,
            int red,
            int green,
            int blue,
            string classificationText)
        {
            if (instance == null)
                return;

            int elementId = instance.Id.IntegerValue;

            if (!_viewColorProcessedElementIds.Add(elementId))
                return;

            View activeView = _doc.ActiveView;

            if (activeView == null || activeView.IsTemplate)
            {
                ViewColorStatistics.FailureCount++;
                ViewColorStatistics.AddError(
                    "ElementId " + elementId +
                    " / 현재 활성 뷰가 없거나 뷰 템플릿입니다.");
                return;
            }

            try
            {
                Autodesk.Revit.DB.Color color =
                    new Autodesk.Revit.DB.Color(
                        Convert.ToByte(ClampColorComponent(red)),
                        Convert.ToByte(ClampColorComponent(green)),
                        Convert.ToByte(ClampColorComponent(blue)));

                OverrideGraphicSettings overrides =
                    new OverrideGraphicSettings();

                Autodesk.Revit.DB.Color lineColor = color;
                bool isBtsColumnBased =
                    IsBtsColumnBasedSupport(instance);

                if (isBtsColumnBased &&
                    _settings.EnableBtsColumnBasedOutline)
                {
                    lineColor =
                        new Autodesk.Revit.DB.Color(
                            Convert.ToByte(
                                ClampColorComponent(
                                    _settings.BtsColumnBasedOutlineRed)),
                            Convert.ToByte(
                                ClampColorComponent(
                                    _settings.BtsColumnBasedOutlineGreen)),
                            Convert.ToByte(
                                ClampColorComponent(
                                    _settings.BtsColumnBasedOutlineBlue)));
                }

                overrides.SetProjectionLineColor(lineColor);
                overrides.SetCutLineColor(lineColor);

                if (isBtsColumnBased &&
                    _settings.EnableBtsColumnBasedOutline)
                {
                    int lineWeight = Math.Max(
                        1,
                        Math.Min(
                            16,
                            _settings.BtsColumnBasedOutlineLineWeight));

                    overrides.SetProjectionLineWeight(lineWeight);
                    overrides.SetCutLineWeight(lineWeight);
                }

                ElementId solidFillPatternId =
                    GetSolidFillPatternId();

                if (solidFillPatternId != null &&
                    solidFillPatternId != ElementId.InvalidElementId)
                {
                    overrides.SetSurfaceForegroundPatternId(
                        solidFillPatternId);
                    overrides.SetSurfaceForegroundPatternColor(color);
                    overrides.SetCutForegroundPatternId(
                        solidFillPatternId);
                    overrides.SetCutForegroundPatternColor(color);
                }

                activeView.SetElementOverrides(
                    instance.Id,
                    overrides);

                ViewColorStatistics.AppliedCount++;
                ViewColorStatistics.CommonAppliedCount++;

                if (isBtsColumnBased &&
                    _settings.EnableBtsColumnBasedOutline)
                {
                    ViewColorStatistics.BtsColumnOutlineAppliedCount++;
                }
            }
            catch (Exception ex)
            {
                ViewColorStatistics.FailureCount++;
                ViewColorStatistics.AddError(
                    "ElementId " + elementId +
                    " / " + classificationText +
                    " / " + ex.Message);
            }
        }

        private ElementId GetSolidFillPatternId()
        {
            if (_solidFillPatternId != null)
                return _solidFillPatternId;

            FillPatternElement solidFill =
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(element =>
                    {
                        FillPattern pattern =
                            element.GetFillPattern();

                        return pattern != null &&
                               pattern.IsSolidFill;
                    });

            _solidFillPatternId =
                solidFill == null
                    ? ElementId.InvalidElementId
                    : solidFill.Id;

            return _solidFillPatternId;
        }

        private static int ClampColorComponent(int value)
        {
            if (value < 0)
                return 0;

            if (value > 255)
                return 255;

            return value;
        }

        private void ApplyHeightParameterRules(
            FamilyInstance instance,
            double heightInternal)
        {
            if (instance == null ||
                !_settings.EnableHeightParameterRules)
            {
                return;
            }

            int elementId = instance.Id.IntegerValue;

            if (_heightRuleProcessedElementIds.Contains(
                elementId))
            {
                return;
            }

            _heightRuleProcessedElementIds.Add(elementId);
            HeightParameterStatistics.ProcessedSupportCount++;

            IList<JackSupportHeightParameterRule> rules =
                _settings.GetValidHeightParameterRules();

            HeightParameterStatistics.EnsureRuleResults(
                rules);

            if (rules.Count == 0)
            {
                _heightRuleMatchByElementId.Remove(elementId);
                HeightParameterStatistics.NoMatchingRuleCount++;
                return;
            }

            double heightMm =
                JackSupportGeometryHelper.InternalToMm(
                    heightInternal);

            double roundingMm =
                _settings.HeightRuleRoundingMm > 0.0
                    ? _settings.HeightRuleRoundingMm
                    : 1.0;

            heightMm =
                Math.Round(
                    heightMm / roundingMm,
                    MidpointRounding.AwayFromZero) *
                roundingMm;

            if (_settings.ResetHeightRuleParametersBeforeApply)
            {
                foreach (string parameterName in
                    rules
                        .Select(rule =>
                            rule.ParameterName.Trim())
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase))
                {
                    string resetError;

                    TrySetInstanceParameterValue(
                        instance,
                        parameterName,
                        0.0,
                        out resetError,
                        false);
                }
            }

            JackSupportHeightParameterRule matchingRule =
                rules.FirstOrDefault(rule =>
                    rule.Matches(heightMm));

            if (matchingRule == null)
            {
                _heightRuleMatchByElementId.Remove(elementId);
                _heightRuleNoMatchElementIds.Add(elementId);
                HeightParameterStatistics.NoMatchingRuleCount++;
                HeightParameterStatistics.AddNoMatchSample(
                    "ElementId " + elementId +
                    " / 높이 " + heightMm.ToString("0.###") +
                    "mm / 유형 " + GetElementTypeDisplayName(instance));
                return;
            }

            _heightRuleNoMatchElementIds.Remove(elementId);
            _heightRuleMatchByElementId[elementId] = matchingRule;
            HeightParameterStatistics.MatchedSupportCount++;

            int matchingRuleIndex =
                rules.IndexOf(matchingRule);

            string error;
            bool writeSucceeded =
                TrySetInstanceParameterValue(
                    instance,
                    matchingRule.ParameterName,
                    matchingRule.Value,
                    out error,
                    true);

            HeightParameterStatistics.RegisterRuleResult(
                matchingRuleIndex,
                matchingRule,
                writeSucceeded);

            if (writeSucceeded)
            {
                HeightParameterStatistics.ValueWrittenCount++;
            }
            else
            {
                HeightParameterStatistics.WriteFailureCount++;
                HeightParameterStatistics.AddError(
                    "ElementId " + elementId +
                    " / 높이 " + heightMm.ToString("0.###") +
                    "mm / " + error);
            }
        }

        private string GetElementTypeDisplayName(
            FamilyInstance instance)
        {
            if (instance == null)
                return string.Empty;

            try
            {
                ElementType type =
                    _doc.GetElement(instance.GetTypeId()) as ElementType;

                if (type == null)
                    return string.Empty;

                string familyName =
                    type.FamilyName ?? string.Empty;

                string typeName =
                    type.Name ?? string.Empty;

                if (string.IsNullOrWhiteSpace(familyName))
                    return typeName;

                if (string.IsNullOrWhiteSpace(typeName))
                    return familyName;

                return familyName + " : " + typeName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool TrySetInstanceParameterValue(
            FamilyInstance instance,
            string parameterName,
            double value,
            out string error,
            bool reportFailure)
        {
            error = string.Empty;

            if (instance == null)
            {
                error = "기둥 인스턴스가 없습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                error = "매개변수명이 비어 있습니다.";
                return false;
            }

            Parameter parameter =
                FindInstanceParameter(
                    instance,
                    parameterName);

            if (parameter == null)
            {
                error =
                    "인스턴스 매개변수 '" +
                    parameterName +
                    "'을 찾지 못했습니다.";

                if (reportFailure)
                {
                    HeightParameterStatistics
                        .MissingParameterCount++;
                }

                return false;
            }

            if (parameter.IsReadOnly)
            {
                error =
                    "매개변수 '" +
                    parameterName +
                    "'은 읽기 전용입니다.";

                if (reportFailure)
                {
                    HeightParameterStatistics
                        .ReadOnlyParameterCount++;
                }

                return false;
            }

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.Integer:
                        parameter.Set(
                            Convert.ToInt32(
                                Math.Round(
                                    value,
                                    MidpointRounding.AwayFromZero)));
                        return true;

                    case StorageType.Double:
                        parameter.Set(value);
                        return true;

                    case StorageType.String:
                        parameter.Set(
                            value.ToString(
                                "0.################",
                                System.Globalization
                                    .CultureInfo.InvariantCulture));
                        return true;

                    default:
                        error =
                            "매개변수 '" +
                            parameterName +
                            "'은 지원하지 않는 저장 형식입니다.";

                        if (reportFailure)
                        {
                            HeightParameterStatistics
                                .UnsupportedStorageTypeCount++;
                        }

                        return false;
                }
            }
            catch (Exception ex)
            {
                error =
                    "매개변수 '" +
                    parameterName +
                    "' 입력 실패: " +
                    ex.Message;

                return false;
            }
        }

        private bool TrySetInstanceParameterTextValue(
            FamilyInstance instance,
            string parameterName,
            string value,
            out string error)
        {
            error = string.Empty;

            if (instance == null)
            {
                error = "잭서포트 인스턴스가 없습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                error = "최하층 구분 매개변수명이 비어 있습니다.";
                return false;
            }

            Parameter parameter =
                FindInstanceParameter(
                    instance,
                    parameterName);

            if (parameter == null)
            {
                error =
                    "인스턴스 매개변수 '" +
                    parameterName +
                    "'을 찾지 못했습니다.";

                FloorClassificationStatistics.MissingParameterCount++;
                return false;
            }

            if (parameter.IsReadOnly)
            {
                error =
                    "매개변수 '" +
                    parameterName +
                    "'은 읽기 전용입니다.";

                FloorClassificationStatistics.ReadOnlyParameterCount++;
                return false;
            }

            string input = value ?? string.Empty;

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        parameter.Set(input);
                        return true;

                    case StorageType.Integer:
                        int integerValue;

                        if (!int.TryParse(
                            input,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out integerValue) &&
                            !int.TryParse(
                                input,
                                NumberStyles.Integer,
                                CultureInfo.CurrentCulture,
                                out integerValue))
                        {
                            error =
                                "정수 매개변수 '" +
                                parameterName +
                                "'에 입력할 값 '" +
                                input +
                                "'을 정수로 변환하지 못했습니다.";
                            return false;
                        }

                        parameter.Set(integerValue);
                        return true;

                    case StorageType.Double:
                        double doubleValue;

                        if (!double.TryParse(
                            input,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out doubleValue) &&
                            !double.TryParse(
                                input,
                                NumberStyles.Float,
                                CultureInfo.CurrentCulture,
                                out doubleValue))
                        {
                            error =
                                "숫자 매개변수 '" +
                                parameterName +
                                "'에 입력할 값 '" +
                                input +
                                "'을 숫자로 변환하지 못했습니다.";
                            return false;
                        }

                        parameter.Set(doubleValue);
                        return true;

                    default:
                        error =
                            "매개변수 '" +
                            parameterName +
                            "'은 지원하지 않는 저장 형식입니다.";

                        FloorClassificationStatistics
                            .UnsupportedStorageTypeCount++;

                        return false;
                }
            }
            catch (Exception ex)
            {
                error =
                    "매개변수 '" +
                    parameterName +
                    "' 입력 실패: " +
                    ex.Message;

                return false;
            }
        }

        private static Parameter FindInstanceParameter(
            FamilyInstance instance,
            string parameterName)
        {
            if (instance == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            Parameter exact =
                instance.LookupParameter(parameterName);

            if (exact != null)
                return exact;

            foreach (Parameter parameter in
                instance.Parameters)
            {
                if (parameter == null ||
                    parameter.Definition == null)
                {
                    continue;
                }

                if (string.Equals(
                    parameter.Definition.Name,
                    parameterName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return parameter;
                }
            }

            return null;
        }

        private void ApplyVerticalConstraints(
            FamilyInstance instance,
            double bottomZ,
            double topZ,
            Level baseLevel,
            Level topLevel)
        {
            if (instance == null ||
                baseLevel == null ||
                topLevel == null)
            {
                return;
            }

            Parameter topLevelParameter =
                instance.get_Parameter(
                    BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);

            // 구조기둥 생성 직후에는 상단과 하단이 같은 레벨일 수 있다.
            // 하단을 먼저 변경하면 중간 상태에서 높이 0 또는 음수 높이로
            // 판단될 수 있으므로 상단 구속 또는 높이를 먼저 확정한다.
            if (topLevelParameter != null &&
                !topLevelParameter.IsReadOnly)
            {
                topLevelParameter.Set(topLevel.Id);

                SetDoubleParameter(
                    instance,
                    BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM,
                    topZ - topLevel.Elevation);
            }
            else
            {
                SetDoubleParameter(
                    instance,
                    BuiltInParameter.FAMILY_HEIGHT_PARAM,
                    topZ - bottomZ);
            }

            SetElementIdParameter(
                instance,
                BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                baseLevel.Id);

            SetDoubleParameter(
                instance,
                BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM,
                bottomZ - baseLevel.Elevation);
        }

        private Level ResolvePreferredBaseLevel(
            IList<Level> levels,
            Element referenceElement,
            double bottomZ)
        {
            Level preferred = TryResolveElementLevel(
                referenceElement,
                false);

            return preferred ?? FindBaseLevel(levels, bottomZ);
        }

        private Level ResolvePreferredTopLevel(
            IList<Level> levels,
            Element referenceElement,
            double topZ)
        {
            Level preferred = TryResolveElementLevel(
                referenceElement,
                true);

            return preferred ?? FindTopLevel(levels, topZ);
        }

        private Level TryResolveElementLevel(
            Element element,
            bool preferTopLevel)
        {
            if (element == null)
                return null;

            BuiltInParameter[] parameterCandidates =
                preferTopLevel
                    ? new[]
                    {
                        BuiltInParameter.FAMILY_TOP_LEVEL_PARAM,
                        BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                        BuiltInParameter.FAMILY_LEVEL_PARAM,
                        BuiltInParameter.SCHEDULE_LEVEL_PARAM,
                        BuiltInParameter.LEVEL_PARAM
                    }
                    : new[]
                    {
                        BuiltInParameter.FAMILY_BASE_LEVEL_PARAM,
                        BuiltInParameter.FAMILY_LEVEL_PARAM,
                        BuiltInParameter.LEVEL_PARAM,
                        BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                        BuiltInParameter.SCHEDULE_LEVEL_PARAM
                    };

            foreach (BuiltInParameter builtInParameter in
                parameterCandidates)
            {
                try
                {
                    Parameter parameter =
                        element.get_Parameter(builtInParameter);

                    if (parameter == null ||
                        parameter.StorageType != StorageType.ElementId)
                    {
                        continue;
                    }

                    ElementId levelId = parameter.AsElementId();
                    Level level = _doc.GetElement(levelId) as Level;

                    if (level != null)
                        return level;
                }
                catch
                {
                    // 해당 요소가 지원하지 않는 레벨 매개변수는 다음 후보를 확인한다.
                }
            }

            try
            {
                ElementId levelId = element.LevelId;
                Level directLevel = _doc.GetElement(levelId) as Level;

                if (directLevel != null)
                    return directLevel;
            }
            catch
            {
                // LevelId가 없는 요소는 좌표 기준 보조 판정을 사용한다.
            }

            return null;
        }

        private static Level FindBaseLevel(
            IList<Level> levels,
            double z)
        {
            Level result =
                levels
                    .Where(level =>
                        level.Elevation <= z)
                    .OrderByDescending(level =>
                        level.Elevation)
                    .FirstOrDefault();

            return result ?? levels.First();
        }

        private static Level FindTopLevel(
            IList<Level> levels,
            double z)
        {
            Level result =
                levels
                    .Where(level =>
                        level.Elevation >= z)
                    .OrderBy(level =>
                        level.Elevation)
                    .FirstOrDefault();

            return result ?? levels.Last();
        }

        private static void SetElementIdParameter(
            Element element,
            BuiltInParameter builtInParameter,
            ElementId value)
        {
            Parameter parameter =
                element.get_Parameter(
                    builtInParameter);

            if (parameter != null &&
                !parameter.IsReadOnly)
            {
                parameter.Set(value);
            }
        }

        private static void SetDoubleParameter(
            Element element,
            BuiltInParameter builtInParameter,
            double value)
        {
            Parameter parameter =
                element.get_Parameter(
                    builtInParameter);

            if (parameter != null &&
                !parameter.IsReadOnly)
            {
                parameter.Set(value);
            }
        }
    }

    public class JackSupportFloorClassificationStatistics
    {
        public int ProcessedCount { get; set; }
        public int LowestFloorCount { get; set; }
        public int OtherFloorCount { get; set; }
        public int WrittenCount { get; set; }
        public int FailureCount { get; set; }
        public int ClassificationTextWrittenCount { get; set; }
        public int ClassificationTextFailureCount { get; set; }
        public int CountParameterWrittenCount { get; set; }
        public int CountParameterFailureCount { get; set; }
        public int MissingParameterCount { get; set; }
        public int ReadOnlyParameterCount { get; set; }
        public int UnsupportedStorageTypeCount { get; set; }
        public List<string> Errors { get; private set; }

        public JackSupportFloorClassificationStatistics()
        {
            Errors = new List<string>();
        }

        public void AddError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (Errors.Count < 50)
                Errors.Add(text);
        }
    }

    public class JackSupportViewColorStatistics
    {
        public int AppliedCount { get; set; }
        public int CommonAppliedCount { get; set; }
        public int LowestFloorAppliedCount { get; set; }
        public int OtherFloorAppliedCount { get; set; }
        public int HeightRuleAppliedCount { get; set; }
        public int UnmatchedHeightAppliedCount { get; set; }
        public int BtsColumnOutlineAppliedCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> Errors { get; private set; }

        public JackSupportViewColorStatistics()
        {
            Errors = new List<string>();
        }

        public void AddError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (Errors.Count < 50)
                Errors.Add(text);
        }
    }

    public class JackSupportHeightRuleResultItem
    {
        public int RuleIndex { get; set; }
        public double MinimumHeightMm { get; set; }
        public double MaximumHeightMm { get; set; }
        public string ParameterName { get; set; }
        public double ParameterValue { get; set; }
        public int ColorRed { get; set; }
        public int ColorGreen { get; set; }
        public int ColorBlue { get; set; }
        public int MatchedCount { get; set; }
        public int ValueWrittenCount { get; set; }
        public int WriteFailureCount { get; set; }

        public JackSupportHeightRuleResultItem()
        {
            ParameterName = string.Empty;
        }

        public string BuildDisplayText()
        {
            string rangeText =
                FormatNumber(MinimumHeightMm) +
                "~" +
                FormatNumber(MaximumHeightMm) +
                "mm";

            string valueText =
                string.IsNullOrWhiteSpace(ParameterName)
                    ? string.Empty
                    : " / " +
                      ParameterName.Trim() +
                      "=" +
                      FormatNumber(ParameterValue);

            if (WriteFailureCount > 0 ||
                MatchedCount != ValueWrittenCount)
            {
                return
                    rangeText +
                    ": 입력 " +
                    ValueWrittenCount +
                    "개 / 일치 " +
                    MatchedCount +
                    "개 / 실패 " +
                    WriteFailureCount +
                    "개" +
                    valueText;
            }

            return
                rangeText +
                ": " +
                ValueWrittenCount +
                "개" +
                valueText;
        }

        private static string FormatNumber(
            double value)
        {
            return value.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
        }
    }

    public class JackSupportHeightParameterStatistics
    {
        public int ProcessedSupportCount { get; set; }
        public int MatchedSupportCount { get; set; }
        public int NoMatchingRuleCount { get; set; }
        public int ValueWrittenCount { get; set; }
        public int WriteFailureCount { get; set; }
        public int MissingParameterCount { get; set; }
        public int ReadOnlyParameterCount { get; set; }
        public int UnsupportedStorageTypeCount { get; set; }
        public List<string> Errors { get; private set; }
        public List<string> NoMatchSamples { get; private set; }
        public List<JackSupportHeightRuleResultItem>
            RuleResults { get; private set; }

        public JackSupportHeightParameterStatistics()
        {
            Errors = new List<string>();
            NoMatchSamples = new List<string>();
            RuleResults =
                new List<JackSupportHeightRuleResultItem>();
        }

        public void EnsureRuleResults(
            IList<JackSupportHeightParameterRule> rules)
        {
            if (rules == null)
                return;

            if (RuleResults.Count == rules.Count)
            {
                bool sameRules = true;

                for (int index = 0;
                    index < rules.Count;
                    index++)
                {
                    JackSupportHeightParameterRule rule =
                        rules[index];

                    JackSupportHeightRuleResultItem item =
                        RuleResults[index];

                    if (rule == null ||
                        item == null ||
                        Math.Abs(
                            item.MinimumHeightMm -
                            rule.MinimumHeightMm) > 0.0001 ||
                        Math.Abs(
                            item.MaximumHeightMm -
                            rule.MaximumHeightMm) > 0.0001 ||
                        !string.Equals(
                            item.ParameterName,
                            rule.ParameterName ?? string.Empty,
                            StringComparison.OrdinalIgnoreCase) ||
                        Math.Abs(
                            item.ParameterValue -
                            rule.Value) > 0.0001)
                    {
                        sameRules = false;
                        break;
                    }
                }

                if (sameRules)
                    return;
            }

            RuleResults.Clear();

            for (int index = 0;
                index < rules.Count;
                index++)
            {
                JackSupportHeightParameterRule rule =
                    rules[index];

                if (rule == null)
                    continue;

                RuleResults.Add(
                    new JackSupportHeightRuleResultItem
                    {
                        RuleIndex = index,
                        MinimumHeightMm =
                            rule.MinimumHeightMm,
                        MaximumHeightMm =
                            rule.MaximumHeightMm,
                        ParameterName =
                            rule.ParameterName ??
                            string.Empty,
                        ParameterValue =
                            rule.Value,
                        ColorRed = rule.ColorRed,
                        ColorGreen = rule.ColorGreen,
                        ColorBlue = rule.ColorBlue
                    });
            }
        }

        public void RegisterRuleResult(
            int ruleIndex,
            JackSupportHeightParameterRule rule,
            bool writeSucceeded)
        {
            if (rule == null)
                return;

            JackSupportHeightRuleResultItem item =
                RuleResults.FirstOrDefault(
                    result =>
                        result != null &&
                        result.RuleIndex == ruleIndex);

            if (item == null)
            {
                item =
                    new JackSupportHeightRuleResultItem
                    {
                        RuleIndex = ruleIndex,
                        MinimumHeightMm =
                            rule.MinimumHeightMm,
                        MaximumHeightMm =
                            rule.MaximumHeightMm,
                        ParameterName =
                            rule.ParameterName ??
                            string.Empty,
                        ParameterValue =
                            rule.Value,
                        ColorRed = rule.ColorRed,
                        ColorGreen = rule.ColorGreen,
                        ColorBlue = rule.ColorBlue
                    };

                RuleResults.Add(item);
            }

            item.MatchedCount++;

            if (writeSucceeded)
            {
                item.ValueWrittenCount++;
            }
            else
            {
                item.WriteFailureCount++;
            }
        }

        public IList<string> BuildRuleResultLines()
        {
            return RuleResults
                .Where(item => item != null)
                .OrderBy(item => item.RuleIndex)
                .Select(item =>
                    item.BuildDisplayText())
                .ToList();
        }

        public void AddError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (Errors.Count < 50)
                Errors.Add(text);
        }

        public void AddNoMatchSample(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (NoMatchSamples.Count < 100)
                NoMatchSamples.Add(text);
        }
    }

    public class JackSupportFamilyLoadOptions :
        IFamilyLoadOptions
    {
        public bool OnFamilyFound(
            bool familyInUse,
            out bool overwriteParameterValues)
        {
            overwriteParameterValues = false;
            return true;
        }

        public bool OnSharedFamilyFound(
            Family sharedFamily,
            bool familyInUse,
            out FamilySource source,
            out bool overwriteParameterValues)
        {
            source = FamilySource.Project;
            overwriteParameterValues = false;
            return true;
        }
    }
}

// =========================================================
// 코드 제목: 잭서포트 패밀리 생성·중복 판정·데이터 및 색상 적용 서비스
// 파일명: JackSupportFamilyService.cs
// =========================================================
