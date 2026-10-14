# MoaView

Netflix, TVING, Wavve, YouTube, 치지직을 하나의 Windows 데스크톱 앱에서 전환하며 보는 **통합 브라우저 셸**입니다. 각 서비스의 공식 웹 플레이어를 WebView2(Microsoft Edge 엔진)로 띄우고, 사이드바·단축키·전체화면·미니 플레이어·계정 자동 입력 같은 편의 기능을 셸이 맡습니다.

스트림 추출, DRM 우회, 다운로드·녹화, 광고 차단은 **범위 밖이며 구현하지 않습니다.** 재생 가능 여부와 최대 화질은 각 서비스가 WebView2에 허용하는 범위를 따릅니다.

## 상태 (2026-10-14)

| 단계 | 내용 | 상태 |
|:---:|------|:---:|
| 0 | 기술 검증(PoC): DRM 재생, 로그인, 전체화면, PiP | 완료 (Go) |
| 1 | MVP 셸: 사이드바, WebView2 통합, 전체화면, 단축키 | 완료 (실사용 검증 일부 남음) |
| 2 | 데이터·계정: SQLite, DPAPI 자격증명, 자동 입력·제출, LoginGuard | 완료 (TVING·Wavve 셀렉터 실측 남음) |
| 3 | 품질·편의: 설정 화면, 사용자 정의 서비스, 테마, Suspend/LRU, 미니 플레이어, 파비콘 캐시 | 완료 (Could 항목 제외) |
| 4 | 배포·안정화: 중복 실행 방지, 크래시 로그 완료. 설치 패키지, 라이선스 고지, `.resx` 분리 남음 | 진행 중 |

상세 계획과 체크리스트는 [doc/07_개발_계획.md](doc/07_개발_계획.md), 결정 기록은 [doc/08_리스크_및_결정사항.md](doc/08_리스크_및_결정사항.md)에 있습니다.

## 요구 사항

- Windows 10 1809 이상 (Windows 11 권장)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (빌드), .NET 10 Desktop Runtime (실행)
- [Microsoft Edge WebView2 런타임](https://developer.microsoft.com/microsoft-edge/webview2/) (Windows 11에는 기본 포함)

## 빌드와 실행

```bash
dotnet build src/MoaView.slnx
dotnet test src/MoaView.slnx
dotnet run --project src/MoaView.App
```

모든 데이터는 `%LOCALAPPDATA%\MoaView\` 아래에만 둡니다(`moaview.db`, `WebView2\`, `logs\`, `icons\`). 실행 파일 폴더에는 쓰지 않고, 외부로 전송하지 않습니다. 비밀번호는 Windows DPAPI(현재 사용자)로 암호화해 저장하므로 다른 PC나 계정으로 DB를 옮기면 복원되지 않습니다.

## 구조

```text
doc/        설계 문서(번호 접두사, 한국어). 01 개요 → 10 보안 점검
src/
  MoaView.App/             WPF 실행 프로젝트(Views, ViewModels, Controls, Services, Scripts)
  MoaView.Core/            엔티티·추상화·순수 로직(.NET 10, 의존성 없음)
  MoaView.Infrastructure/  EF Core SQLite, DPAPI, WebView2 환경, 서비스별 Provider
  tests/                   Core.Tests, Infrastructure.Tests (xUnit)
tools/icon/ 앱 아이콘 생성 스크립트
```

의존 방향은 App → Core, Infrastructure이고 Infrastructure → Core입니다. 개발 규칙과 아키텍처 요점은 [CLAUDE.md](CLAUDE.md)에 정리되어 있습니다.

## 단축키

F11 전체화면, Esc 해제, Ctrl+1~9 서비스 전환, Ctrl+Tab 다음 서비스, Alt+←/→ 뒤로/앞으로, F5 새로고침, Alt+Home 홈, Ctrl+M 미니 플레이어, Ctrl+P PiP, Ctrl+B 사이드바, Ctrl+, 설정. 전체 목록은 설정 > 단축키 탭과 [doc/06_UI_UX_설계.md](doc/06_UI_UX_설계.md) 4절.

## 라이선스

[MIT](LICENSE). 사용하는 서드파티 구성 요소와 라이선스는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 있습니다.

Netflix, TVING, Wavve, YouTube, 치지직은 각 회사의 상표이며 MoaView는 이들과 제휴·승인 관계가 없습니다. 브랜드 로고는 저장소에 포함하지 않으며, 아이콘은 실행 중에 각 사이트의 파비콘을 받아 로컬에만 캐시합니다.
