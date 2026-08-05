Revit 2023 Structural Automation

구조 BIM 모델링, 가시성 제어 및 모델 품질 검토를 지원하기 위해생성형 AI를 개발 보조 도구로 활용하여 구현한 Revit 2023 자동화 사례 모음입니다.

이 저장소는 개발자 포트폴리오가 아니라,구조 BIM 실무자가 반복 업무와 오류 발생 지점을 분석하고업무 기준을 C#·Revit API 기능으로 구현한 과정을 보여주는 포트폴리오입니다.

프로젝트 개요

구조 BIM 업무에서는 다음과 같은 반복 작업이 자주 발생합니다.

특정 층과 카테고리만 남겨 모델 검토

그룹 유형별 객체 확인 및 임시 분리

구조부재 조건에 따른 가설 객체 배치

하부 지지체와 기존 부재의 간섭 확인

생성 객체의 높이·층·수량 데이터 입력

모델 검토 결과의 시각화 및 재검증

본 프로젝트는 이러한 작업을 단순 명령 실행으로 끝내는 것이 아니라,실제 업무 규칙과 예외조건을 설정으로 관리하고 결과를 다시 확인할 수 있도록 구성했습니다.

대표 기능

기능

주요 내용

문서

층별 부재 보기

층·카테고리 복수 선택, 검색, 임시 분리 및 원복

기능 설명

그룹별 보기

모델 그룹 유형 수집, 검색, 복수 선택, 중첩 그룹 처리

기능 설명

잭서포트 자동 생성

구조부재 형상 분석, 하부 지지체 탐색, 자동 배치 및 데이터 입력

기능 설명

1. 층별 부재 보기

선택한 층과 카테고리에 해당하는 요소만 현재 뷰에 임시로 표시하는 기능입니다.

주요 기능

Revit 레벨 목록 자동 수집

카테고리 복수 선택

저장된 최근 선택값 복원

선택 요소만 현재 뷰에 임시 분리

기존 임시 숨기기·분리 상태 해제

다양한 요소의 레벨 정보 보완 판정

설정값을 사용자 AppData 경로에 저장

관련 파일

기능 설명 문서

FloorCategoryVisibilityCommand.cs

FloorCategoryVisibilityService.cs

FloorCategoryVisibilityForm.cs

2. 그룹별 보기

모델 그룹을 유형별로 정리하고, 선택한 그룹의 인스턴스와 구성 요소만 현재 뷰에 표시하는 기능입니다.

주요 기능

모델 그룹 유형과 인스턴스 수 자동 수집

그룹이 배치된 레벨 요약

검색어를 이용한 그룹 필터링

검색 중에도 기존 체크 상태 유지

그룹 유형 복수 선택

중첩 그룹 구성 요소 재귀 수집

임시 분리 및 전체 원복

지원하지 않는 뷰 예외 처리

관련 파일

기능 설명 문서

GroupVisibilityCommand.cs

GroupVisibilityService.cs

GroupVisibilityForm.cs

3. 잭서포트 자동 생성

보, 기둥, 바닥, 구조기초 및 기존 지지 객체를 분석하여설정된 업무 기준에 맞는 위치와 높이에 잭서포트를 자동 생성하는 기능입니다.

주요 기능

유형명의 마지막 _ 뒤 문자열을 이용한 대상 부재 판정

대상 그룹별 생성 위치와 개수 설정

특수 기둥 외곽 네 방향 생성점 계산

기존 구조기둥·벽체 점유 구간 제외

남은 보 구간별 잭서포트 균등 배치

바닥·구조기초 Solid 기반 하부 지지체 탐색

슬래브 경계부 주변 위치 보정

하부 지지체가 없을 때 양단 기둥 높이 보간

XY와 상·하단 높이를 함께 비교하는 중복 생성 방지

최하층·그외층 분류 및 수량 매개변수 입력

높이 구간별 데이터 매개변수 입력

현재 뷰 그래픽 색상 재지정

기존 잭서포트 재판정 및 색상 일괄 적용

생성 원인별 모델 선택·임시 분리

모델리스 옵션창과 ExternalEvent 실행

관련 문서

잭서포트 자동 생성 기능 설명

관련 소스

CreateJackSupportCommand.cs

JackSupportSettingsCommand.cs

JackSupportSettings.cs

JackSupportSettingsForm.cs

JackSupportExternalEventHandler.cs

JackSupportGeometryHelper.cs

JackSupportFamilyService.cs

JackSupportClassificationService.cs

JackSupportBatchColorService.cs

JackSupportSelectionService.cs

프로젝트 구조

Revit2023-structural-automation/
├─ README.md
├─ docs/
│  ├─ FloorVisibility.md
│  ├─ GroupVisibility.md
│  └─ JackSupport.md
└─ src/
   ├─ FloorVisibility/
   │  ├─ FloorCategoryVisibilityCommand.cs
   │  ├─ FloorCategoryVisibilityService.cs
   │  └─ FloorCategoryVisibilityForm.cs
   ├─ GroupVisibility/
   │  ├─ GroupVisibilityCommand.cs
   │  ├─ GroupVisibilityService.cs
   │  └─ GroupVisibilityForm.cs
   └─ JackSupport/
      ├─ CreateJackSupportCommand.cs
      ├─ JackSupportSettingsCommand.cs
      ├─ JackSupportSettings.cs
      ├─ JackSupportSettingsForm.cs
      ├─ JackSupportExternalEventHandler.cs
      ├─ JackSupportGeometryHelper.cs
      ├─ JackSupportFamilyService.cs
      ├─ JackSupportClassificationService.cs
      ├─ JackSupportBatchColorService.cs
      └─ JackSupportSelectionService.cs

개발 방식

수작업 프로세스 분석
→ 반복 작업과 오류 발생 지점 정리
→ 업무 기준과 예외조건 정의
→ 사용자 흐름 및 설정창 설계
→ 생성형 AI를 활용한 C#·Revit API 구현
→ 실제 Revit 모델 테스트
→ 오류 원인 분석 및 조건 보완
→ 결과창과 사용자 인터페이스 개선
→ 팀 사용 검증 및 반복 개선

생성형 AI는 코드 작성, 리팩터링 및 오류 분석을 지원하는 도구로 활용했습니다.

실제 업무 기준 정의, 예외조건 판단, 모델 테스트 및 최종 검증은구조 BIM 실무 경험을 기반으로 수행했습니다.

담당 역할

구조 BIM 수작업 프로세스 분석

자동화 대상 선정

기능 요구사항 및 예외조건 정의

Revit 모델 요소 판정 기준 설계

사용자 옵션과 실행 흐름 설계

생성형 AI를 활용한 C# 코드 구현

실제 프로젝트 모델 기반 테스트

오류 사례 분석 및 로직 개선

사용자 편의 기능과 결과 검토 방식 개선

기능 매뉴얼 작성 및 팀 공유

적용 효과

가시성 검토 기능

반복적인 수동 숨기기·분리 작업 감소

층과 카테고리 또는 그룹 기준의 모델 검토 일관성 향상

중첩 그룹과 다양한 레벨 정보의 검토 누락 감소

잭서포트 자동 생성

수작업 기준 약 4시간의 모델링 업무를 약 10분 수준으로 단축

작업자별 배치 기준 차이 감소

하부 지지체와 생성 높이 검토 자동화

동일 위치 중복 생성 방지

층·높이·수량 데이터 입력 자동화

생성 원인별 검토와 결과 통계 확인 가능

기술 환경

Autodesk Revit 2023

Revit API

C#

.NET Framework 4.8

Windows Forms

Visual Studio

생성형 AI 활용

공개 범위

이 저장소의 코드는 포트폴리오 공개를 위해 재구성한 예시 코드입니다.

다음 정보는 포함하지 않습니다.

회사명과 실제 현장명

실제 프로젝트 모델

회사 내부 부재 코드

사내 매개변수 체계

실제 업무용 RFA 패밀리

내부 서버 및 설치 경로

실제 프로젝트 설정 XML

사용자와 프로젝트 식별정보

공개용 소스에서는 실제 업무 코드를 다음과 같은 일반 예시값으로 대체했습니다.

SPECIAL_BEAM
SIDE_MEMBER
COLUMN_SUPPORT
FOUNDATION_SUPPORT
OTHER_FLOOR_MARKER

참고 사항

저장소에는 대표 기능의 문서와 핵심 소스 파일만 포함합니다.

실제 애드인의 전체 빌드 구성과 배포 파일은 포함하지 않습니다.

일부 클래스는 서로 의존하므로 단일 파일만으로 실행되지 않습니다.

Revit 2023 참조 DLL과 전체 프로젝트 구성을 포함한 공개용 컴파일은 별도 검증이 필요합니다.

프로젝트별 패밀리와 모델링 기준에 따라 설정 조정이 필요합니다.
