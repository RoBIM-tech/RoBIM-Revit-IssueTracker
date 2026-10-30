# BIM 이슈 검토

[일반 사용자 설명서](docs/user/README.md) · [개발자 설명서](docs/developer/README.md) · [전용 스킬](.codex/skills/robim-issue-tracker/SKILL.md)

RoBIM 독립 Revit Add-in의 TEST 시작 예제입니다. 지원: Windows x64, Revit 2026.5+ / .NET 10. pyRevit이 필요하지 않습니다.

## 개발

1. RoBIM-Libraries와 이 저장소를 clone합니다. 중앙 카탈로그 등록을 원할 때만 RoBIM-Add-in-Manager도 clone합니다. Libraries에서 `./pack.ps1`을 실행합니다.
2. `src/Command.cs`를 수정합니다. 현재 예제는 읽기 전용 알림입니다. 실제 문서 수정은 미리보기 승인 후 Revit 명령의 Transaction에서 수행합니다.
3. `./build.ps1 -CommonFeed <Libraries clone>/artifacts/packages`를 실행합니다. 선택적으로 -ManagerPath를 제공해 중앙 등록과의 일치도 검사합니다.
4. `artifacts/<version>/RoBIM.Feature.IssueTracker-Revit2026.zip`와 `.sha256`을 Manager의 도구 등록 → 독립 Revit ZIP 가져오기로 확인합니다.
5. TEST 도구에서 설치하기를 누릅니다. 별도 기능 목록 보기 없이 준비하며 기본 리본 위치를 자동 추가합니다. 기존 사용자 배치는 유지하고 실행 중 Revit에 자동 적용을 요청합니다. 최초 고정 Host 설치는 Revit 종료가 필요합니다.
6. 실제 Revit에서 실행·취소·오류 복원·Reload를 검증하고 사용자 설명서와 개발자 설명서를 완성합니다.

## 제작·배포 계약

[버튼 제작 계약](https://github.com/RoBIM-tech/RoBIM-Workspace/blob/main/docs/developer/BUTTON_CONTRACT.md)에서 매니페스트 필드, 공용 패키지 버전별 소스, 최초 설치/Reload 검증과 Release 게시를 확인합니다.

## Manager 등록

기본은 `robim-addon` 토픽과 기본 브랜치의 robim-package.json 및 TEST Release를 통한 동적 검색입니다. Manager 검색 대상과 접근 권한을 확인하세요. -ManagerPath를 지정하면 기존 중앙 카탈로그도 갱신되므로 Manager PR을 별도로 제출합니다.

모듈 `issue-tracker`, 명령 `run`, 패키지 `robim-issue-tracker`의 ID를 버전·표시 이름 변경 때문에 바꾸지 않습니다. 이 저장소는 한 명령만 소유합니다. 다른 버튼은 새 저장소로 만들고 Workspace 소유권 맵에 등록합니다. Manager에서 저장소 스크립트를 실행하지 않습니다.

## TEST → 실사용

`package/robim-release.json`과 모듈 버전을 함께 올립니다. `robim-package.json`의 channel 및 중앙에 등록한 경우 Manager channel도 함께 갱신합니다. 실사용은 prerelease가 없는 새 버전, `channel: stable`, `validation.by`, `validation.hostBuild`, HTTPS `validation.evidence`가 필요합니다. main 병합이나 빌드 성공만으로 실사용으로 승격하지 않습니다. GitHub TEST Release는 prerelease로 게시하고 새 배포 버전의 ZIP/SHA-256을 제공합니다.

공용 DLL은 `RoBIM.Common [0.1.0]`을 사용합니다. 갱신 시 csproj와 배포 메타데이터의 libraries를 함께 갱신하고 다시 검증합니다. DLL 직접 덮어쓰기는 하지 않습니다.
