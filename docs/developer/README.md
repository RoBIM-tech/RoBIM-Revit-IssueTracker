# BIM 이슈 검토 (Issue Tracker) 개발자 설명서

## 구조 개요

- **명령 엔트리**: `Command.cs` (`IExternalCommand`)
  - 싱글톤 패턴(`IssueTrackerWindow.Instance`)을 통해 단일 모델리스 창 유지.
  - `IssueExternalEventHandler` 및 `ExternalEvent`를 생성하여 모델리스 UI와 Revit 메인 스레드 간 비동기 작업 중계.
- **UI 레이어**: `IssueTrackerWindow.cs`, `CustomFieldsDialog.cs`
  - WPF 기반 UI, `RoBIM.Wpf`의 `RoBimTheme` 스타일 적용.
  - 좌측 이슈 목록(DataGrid) 및 상태/검색 필터.
  - 우측 이슈 상세/편집 폼 및 동적 커스텀 필드 렌더링.
- **Revit 연동 이벤트**: `IssueExternalEventHandler.cs`
  - `PickElements`: `uidoc.Selection.PickObjects()`
  - `GetSelectedElements`: `uidoc.Selection.GetElementIds()`
  - `HighlightElements`: `uidoc.Selection.SetElementIds()` & `uidoc.ShowElements()`
  - `CaptureView`: `doc.ExportImage()`를 활용한 액티브 뷰포트 고품질 이미지 익스포트.
- **데이터 저장소**: `IssueStorage.cs`, `Models.cs`
  - 워크셰어링 중앙 파일 경로(`doc.GetWorksharingCentralModelPath()`) 파악 및 하위 `issue/` 디렉토리 자동 생성.
  - 이슈 파일(`ISSUE-0001.json`), 설정 파일(`fields_schema.json`), 스냅샷 이미지 파일 저장 및 파싱.

## 빌드 및 검증

```powershell
# 빌드 및 아티팩트(ZIP) 생성
.\RoBIM-Revit-IssueTracker\build.ps1 -CommonFeed .\RoBIM-Libraries\artifacts\packages

# 버튼 메타데이터 유효성 검사
python .\RoBIM-Revit-IssueTracker\scripts\validate_button.py --source .\RoBIM-Revit-IssueTracker --host revit --repository RoBIM-tech/RoBIM-Revit-IssueTracker
```
