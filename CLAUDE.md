# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 상태

MoaView(구 MyOTTPlayer, 2026-10-13 개명)는 Netflix, TVING, Wavve, YouTube, 치지직을 하나의 Windows 데스크톱 앱에서 전환하며 시청하는 "통합 브라우저 셸"입니다. **Phase 0(PoC)은 2026-10-13에 Go 판정, Phase 1(MVP 셸)·Phase 2(데이터·계정)는 구현 완료(실사용 검증 일부 남음), Phase 3(품질·편의)은 진행 중입니다.** Phase 3에서 완료한 것은 Suspend/LRU, 미니 플레이어, PiP, 배율 저장, 개명·아이콘, 설정 화면(2026-10-14: 일반·서비스·계정·재생·단축키·데이터·정보 탭), 사용자 정의 서비스 추가·수정·삭제, 사이드바 정렬(위/아래 버튼)·활성화 토글, 다크/라이트/시스템 테마, 파비콘 캐시, 미니 창 위치·크기 저장이고, Phase 3에 남은 것은 Could 항목(드래그 정렬, DB 복원 UI, 다중 계정)뿐입니다. 다음은 사용자 실측(Phase 1·2 검증 항목)과 Phase 4(배포·안정화)입니다. 진행 상황은 [Doc/07_개발_계획.md](Doc/07_개발_계획.md) 체크리스트, PoC에서 확인된 사실은 [Doc/09_PoC_결과.md](Doc/09_PoC_결과.md)에 있습니다.

설계가 바뀌면 코드와 함께 `Doc/` 문서를 갱신합니다. 문서는 번호 접두사를 붙인 한국어 마크다운입니다.

## 명령

```bash
dotnet build src/MoaView.slnx                 # .NET 10 SDK는 .slnx 형식을 만든다
dotnet test src/MoaView.slnx                  # Core.Tests(순수 로직), Infrastructure.Tests(Provider·저장소·DPAPI)
dotnet test src/tests/MoaView.Infrastructure.Tests --filter "FullyQualifiedName~ProviderTests.Netflix_detects_login_page"   # 단일 테스트
dotnet run --project src/MoaView.App          # 본 앱 실행. 로그: %LOCALAPPDATA%\MoaView\logs\app-<날짜>.log
dotnet ef migrations add <Name> --project src/MoaView.Infrastructure --startup-project src/MoaView.App --output-dir Persistence/Migrations   # 시작 프로젝트는 반드시 App(라이브러리만으로는 Windows SDK 어셈블리를 못 찾음)
pwsh tools/release/pack.ps1 [-Version 0.2.0] [-Upload]   # Velopack 패키징(Releases\, gitignore). 릴리스는 태그 v* 푸시 → GitHub Actions(release.yml)가 업로드
```

릴리스 버전은 `src/Directory.Build.props`의 `Version`과 태그를 맞춥니다. 패키징은 프레임워크 의존(`--framework net10.0-x64-desktop,webview2`, `--runtime win-x64`)이라 설치기가 .NET Desktop Runtime과 WebView2 런타임을 받아 설치합니다. 패키지를 추가·변경하면 `THIRD-PARTY-NOTICES.md`를 함께 갱신하고, FluentAssertions는 8.x부터 상용이라 7.x에 고정합니다.

빌드 전 실행 중인 `MoaView.exe`가 있으면 exe가 잠겨 실패하므로 먼저 종료합니다. Debug 빌드는 Debug 레벨 로그(탐색·URL 변경·저장 예약)를 남기고 Release는 Information부터입니다. 개발 PC의 회사망은 netflix.com을 차단하므로(사용자 확인) 앱을 자동 실행해 검증할 때 Netflix를 첫 서비스로 두면 탐색이 멈춰 이벤트가 전혀 오지 않습니다. DB의 `Startup.LastServiceId`를 4(YouTube)로 바꿔 검증하고, Netflix 실측은 사용자가 테더링이나 집 네트워크에서 합니다. 솔루션 파일과 공통 빌드 속성(`Nullable`, TFM 변수 `WindowsTargetFramework`)은 `src/MoaView.slnx`, `src/Directory.Build.props`에 있습니다(저장소 루트에는 문서·CLAUDE.md·tools만 둠). DPAPI 엔트로피 상수는 개명 전 값(`MyOTTPlayer.Credential.v1`)을 유지해야 저장된 비밀번호가 풀립니다. `UseWPF`를 켠 프로젝트는 WPF SDK의 암시적 using에 `System.IO`가 빠지므로 `Path`·`Directory`를 쓰는 파일은 `using System.IO;`를 명시합니다.

Phase 0 PoC 앱(`src/MyOTTPlayer.Poc`)과 그 로그·캡처(`src/Report`)는 2026-10-13에 삭제했습니다. 결과는 [Doc/09_PoC_결과.md](Doc/09_PoC_결과.md)에만 남아 있고, 재사용할 코드는 `Infrastructure/WebView/ExtensionInstaller.cs`(아직 미호출)와 `App/Scripts/drm-probe.js`, `video-info.js`로 옮겨 두었습니다.

PoC에서 확정된 기술 사항: `WebView2CompositionControl`을 쓰려면 TFM이 `net10.0-windows10.0.19041.0`(Windows SDK 프로젝션 포함)이어야 합니다. 치지직 그리드 확장은 Native Messaging으로 커넥터와 통신하며, 폴더 로드 시 manifest에 `key`를 넣어야 스토어 ID와 같아져 페이지가 확장을 찾습니다(`Infrastructure/WebView/ExtensionInstaller.cs`, CRX3 서명 검증 포함).

빌드와 테스트가 통과한 뒤에 커밋합니다. 커밋은 영어 Conventional Commits(예: `feat(player): add fullscreen sync with WebView2`). 브랜치는 `main` / `develop` / `feature/*`.

## 기술 스택

.NET 10 WPF(App·Infrastructure는 `net10.0-windows10.0.19041.0`, Core는 `net10.0`), `Microsoft.Web.WebView2`, `WPF-UI`(Fluent 스타일 UI 라이브러리, D-13), `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting`(Generic Host DI), EF Core 10 + SQLite, `System.Security.Cryptography.ProtectedData`(DPAPI, Windows TFM 공유 프레임워크에 포함되어 별도 패키지 없음), Serilog, xUnit + FluentAssertions. 배포는 Velopack 또는 MSIX. 상세는 [Doc/03_기술_검토.md](Doc/03_기술_검토.md) 5절.

## 솔루션 구조

```text
src/MoaView.slnx, src/Directory.Build.props   솔루션과 공통 빌드 속성
tools/icon/                      앱 아이콘 생성 스크립트(GDI+ 렌더 + ICO 조립). 아이콘을 바꿀 때만 사용
src/MoaView.App/             WPF 실행 프로젝트(net10.0-windows10.0.19041.0, AssemblyName MoaView, 아이콘 Assets/moaview.ico)
  Program.cs                     진입점(csproj StartupObject). Velopack 훅(VelopackApp.Build().Run())을 WPF보다 먼저 처리한 뒤 App을 띄움
  App.xaml.cs                    중복 실행 방지(SingleInstanceGuard), Generic Host, DI, Serilog, 전역 예외 처리(UI 예외 안내 3회, AppDomain 예외는 logs\crash-*.log), WebView2 런타임 확인(없으면 WebView2Bootstrapper로 설치 시도)
  Services/UpdateService         Velopack + GitHub Releases 자동 업데이트(D-17). 설치 버전(IsInstalled)에서만 동작. MainViewModel이 시작 10초 뒤 확인·다운로드 후 배너, 설정 > 정보에서 수동 확인
  Views/MainWindow               FluentWindow + 사이드바 + 배너 + HostArea, 단축키(PreviewKeyDown)
  Views/OAuthPopupWindow         소셜 로그인 팝업(요청 서비스와 같은 프로필)
  Views/AccountsWindow, AccountEditDialog   계정 관리(서비스 아이콘 우클릭 → 계정 관리, 설정 > 계정)
  Views/SettingsWindow, ServiceEditDialog   설정 창(Ctrl+, / 사이드바 ⚙, 탭 7개, 변경 즉시 저장)과 서비스 추가·수정 대화상자(사이드바 ＋, 설정 > 서비스, 컨텍스트 메뉴 "서비스 편집...")
  ViewModels/MainViewModel       서비스 목록·선택·전체화면·배너·컨텍스트 메뉴 명령. ReloadServicesAsync는 설정 창이 닫힌 뒤 사이드바를 다시 읽고 수정·삭제·숨김된 서비스의 WebView만 해제
  ViewModels/SettingsViewModel   설정 창 상태. 서비스 변경은 ChangedServiceIds·ServicesChanged에 모아 SettingsResult로 메인에 전달
  Controls/ServiceHostControl    서비스 1개 = WebView2 1개. 이벤트 처리, ProcessFailed 복구, 보안 설정
  Services/ServiceHostManager    호스트 생성·전환(보이는 상태에서 생성), 30초마다 Suspend·LRU 유지 관리, URL·배율 디바운스 저장
  Services/WindowService         전체화면(F11/영상)과 미니 플레이어(Ctrl+M) 창 변형. 미니는 MinWidth/MinHeight도 임시로 낮추고(아니면 640×400으로 잘림), 해제 때 위치·크기를 Window.MiniPlacement에 저장
  Services/FaviconCache          WebView2 FaviconChanged → GetFaviconAsync(PNG) → icons\{code}.png. 허용 호스트의 비로그인 페이지만, 7일마다 갱신. ServiceHostManager가 호출하고 ServiceIconChanged로 사이드바 갱신
  Services/LoginAssistService    로그인 페이지 감지 → 자동 입력 → (옵트인) 자동 제출 → 판정. LoginGuard는 Core
  Services/ThemeService          다크(기본)/라이트/시스템 테마(WPF-UI ApplicationThemeManager). App 시작 때 창 생성 전에 적용. XAML의 색은 DynamicResource 브러시를 써야 라이트에서도 맞음
  Services/SettingsDialogService 설정 창 모달 표시. "모든 서비스 데이터 초기화"는 Data.ResetWebViewOnStart 플래그를 세우고 다시 시작(App이 시작 때 WebView2 폴더 삭제)
  Scripts/*.js                   포함 리소스. autofill.js(휴리스틱 자동 입력), form-analyze.js(Ctrl+Shift+L 개발용), logged-out-check.js(로그아웃 상태 판정), hover-probe.js(마우스를 올려야 열리는 메뉴 탐침), login-submitted-check.js(제출 표식 확인), drm-probe.js·video-info.js(PoC에서 옮긴 진단, 미사용). 인자는 ScriptLoader가 JSON으로 주입
src/MoaView.Core/            순수 .NET(net10.0). Entities(StreamingService, Account, AppSetting), Abstractions(저장소·Provider·창 인터페이스), Models(HostPattern, SettingKeys 등)
src/MoaView.Infrastructure/  Persistence(AppDbContext, Migrations, DbSeeder, DatabaseInitializer, DatabaseMaintenance(VACUUM INTO 백업), Repositories), Security/DpapiCredentialProtector, WebView(WebView2EnvironmentProvider, ExtensionInstaller(미호출)), Providers(서비스별 + Generic + Resolver), Configuration(AppPaths, PlayerOptions, LoginOptions), DependencyInjection
src/tests/MoaView.Core.Tests/    HostPattern 등 순수 로직
src/tests/MoaView.Infrastructure.Tests/   Provider 정책, 저장소·Seed(SQLite in-memory + 실제 마이그레이션), DPAPI
```

의존 방향: App → Core, Infrastructure. Infrastructure → Core. Core는 아무것도 참조하지 않습니다. 저장소는 `IDbContextFactory<AppDbContext>`로 컨텍스트를 호출마다 짧게 열고 닫습니다. 자동 입력 셀렉터는 코드에 두지 않습니다. 휴리스틱이 안 맞는 서비스만 DB `LoginFormSpecJson`에 셀렉터를 넣고, 구조는 앱에서 Ctrl+Shift+L로 뽑은 로그로 확인합니다.

## 핵심 아키텍처

- **재생 방식**: 각 서비스의 공식 웹 플레이어를 WebView2에 띄웁니다. 스트림 URL 추출, DRM 우회, 다운로드·녹화, 광고 차단은 **범위 밖이며 구현하지 않습니다**(D-02, NFR-11).
- **IStreamingProvider**: 서비스별 동작(로그인 페이지 판별, 로그인 폼 셀렉터, 새 창 정책)을 Provider로 캡슐화합니다. 기본 5종은 전용 Provider, 사용자 정의 서비스는 `GenericProvider`(DB `AllowedHosts`가 비어 있으면 홈 URL의 호스트·하위 도메인을 허용 호스트로 씀. 이 값이 새 창 정책·LastUrl 저장·자동 입력 허용에 모두 쓰이므로 비워 두면 안 됨). 사이트 구조가 바뀌면 해당 Provider 또는 DB의 `LoginFormSpecJson`만 고칩니다. 셀렉터는 코드가 아니라 DB/JSON에 두는 것이 원칙입니다.
- **ServiceHostControl**: 서비스(·계정)당 WebView2 인스턴스 1개. 처음 선택할 때 지연 생성, 비활성 시 `TrySuspendAsync`, 전환은 Visibility만 바꿉니다. `NavigationCompleted`(로그인 감지), `SourceChanged`·`DOMContentLoaded`(SPA 라우팅 감지, 페이지 안정 판정), `ContainsFullScreenElementChanged`(전체화면 연동), `NewWindowRequested`(OAuth는 앱 내 대화상자, 외부 링크는 기본 브라우저), `PermissionRequested`(자동 재생만 허용), `LaunchingExternalUriScheme`(모두 취소), `DownloadStarting`(허용하되 로그), `WebResourceResponseReceived`(로그인 POST 관찰, 호스트만 로그), `ProcessFailed`(재생성 후 마지막 URL 복원)를 처리합니다.
- **WebView2EnvironmentProvider**: `CoreWebView2Environment`는 앱 전체에서 싱글톤 1개. 프로필은 `svc_{code}` 또는 `svc_{code}_{accountId}`로 분리해 쿠키를 격리합니다. UserDataFolder는 `%LOCALAPPDATA%\MoaView\WebView2`.
- **WindowService**: 영상 전체화면(HTML5 Fullscreen API)과 앱 전체화면(F11)을 하나의 진입점으로 합칩니다. Esc는 영상 전체화면이면 JS `exitFullscreen()`, 앱 전체화면이면 창 상태 복원.
- **셸 UI 구성 (D-10)**: 커스텀 타이틀바와 사이드바뿐이며 상단 도구막대, 주소 표시줄, 탐색 버튼은 없습니다. 뒤로/앞으로는 Alt+←/→ 단축키와 마우스 측면 버튼으로만, 홈·새로고침은 단축키와 서비스 아이콘 컨텍스트 메뉴(계정 관리·서비스 편집·홈·새로고침·로그아웃)로 제공합니다. 사이드바 하단에는 미니 플레이어·전체화면·서비스 추가·설정(Ctrl+,) 버튼만 두고, 계정 전환은 컨텍스트 메뉴와 설정 > 계정에 둡니다. 설정 창은 확인 버튼 없이 변경 즉시 저장하고, 닫힐 때 `SettingsResult`로 바뀐 서비스 Id를 돌려주면 `MainViewModel.ReloadServicesAsync`가 해당 WebView만 해제·재생성합니다.
- **WPF Airspace 제약**: WebView2(HWND)는 위에 WPF 요소를 겹칠 수 없습니다. 오버레이 UI를 만들지 말고, 배너·알림은 타이틀바와 WebView 사이에 공간을 차지하는 방식으로 배치합니다(평소에는 숨김). `WebView2CompositionControl`은 DRM 렌더링 PoC(P7)를 통과한 뒤에만 고려합니다.
- **단축키**: WebView2에 포커스가 있어도 WPF WebView2 컨트롤이 `AcceleratorKeyPressed`를 WPF 키 이벤트로 올려 주므로, `MainWindow`의 `PreviewKeyDown`에서 앱 단축키를 처리합니다(직접 구독하지 않음). Space·화살표 등 플레이어용 키는 건드리지 않고 웹으로 넘깁니다. 단축키 목록은 [Doc/06_UI_UX_설계.md](Doc/06_UI_UX_설계.md) 4절.

## 보안 규칙 ([Doc/10_보안_점검.md](Doc/10_보안_점검.md))

- 로그에는 URL의 쿼리·프래그먼트를 남기지 않습니다(세션 토큰 유출). 로그는 `ServiceHostControl.SanitizeForLog`를 거칩니다. DB(`LastUrl`)는 `PersistableUrl`을 거쳐 허용 호스트 안·비로그인 페이지에 한해 프래그먼트만 제거하고 저장합니다(쿼리는 유지, 예: YouTube `?v=`).
- 기본 브라우저로 넘기는 URL은 http(s)만 허용합니다. 페이지의 외부 프로토콜 실행은 모두 취소하고, 권한 요청은 자동 재생만 허용합니다.
- WebView2 설정: 호스트 객체·웹 메시지는 항상 비활성(자동 입력·판정 결과는 `ExecuteScriptAsync` 반환값으로 받음), 브라우저 비밀번호 저장·자동 완성 비활성, Release에서 DevTools 비활성. OAuth 팝업(`OAuthPopupWindow`)에도 같은 설정을 적용합니다.
- 주입 스크립트에 값을 넣을 때는 반드시 JSON 직렬화하고, 복호화한 비밀번호는 호출 직전에만 만들어 로그·예외·배너에 넣지 않습니다.

## 자격증명과 자동 로그인 (D-03, D-09)

- 비밀번호는 `ProtectedData`(DPAPI, `CurrentUser`, 앱 고유 entropy)로 암호화해 SQLite `Accounts.EncryptedPassword`에 저장합니다. 평문 저장 금지. 다른 PC·계정으로 DB를 옮기면 복호화되지 않는 것이 의도된 동작입니다.
- 복호화한 비밀번호는 `ExecuteScriptAsync`에 넘기기 직전에만 메모리에 두고, 로그·예외 메시지에 남기지 않습니다. 자동 입력 스크립트는 현재 URL이 해당 서비스 허용 호스트(`AllowedHosts`)이거나 Provider가 로그인 페이지로 판정한 URL일 때만 실행합니다.
- 로그인 흐름: 세션 유지(쿠키) → 만료 감지 → 자동 입력(기본 켬) → 자동 제출(기본 **끔**, 계정별 옵션) → **LoginGuard**(같은 계정 10분에 1회, 연속 2회 실패 시 자동 제출 중단 후 배너 안내). 계정 잠금 예방을 위한 장치이므로 완화하지 않습니다. 2단계 로그인(이메일 먼저)의 1단계 '다음' 클릭은 비밀번호 제출이 아니므로 자동 제출 설정·LoginGuard와 무관하게 수행합니다(의도된 동작).
- 만료 감지 수단: 로그인 URL 패턴(Provider `IsLoginPage`), `logged-out-check.js`(DB `loggedOutMarker`가 있으면 그것으로, 없으면 로그인/로그아웃 링크 휴리스틱으로 항상 실행), `hover-probe.js`(마우스를 올려야 열리는 메뉴를 잠깐 열어 확인), 쿠키 서명 학습(`Login.CookieSignature.{code}`, 로그인 POST 관찰 후 늘어난 쿠키 **이름만** AppSettings에 저장, 값은 저장하지 않음).
- 지원 우선순위: TVING·Wavve 자체 ID 먼저, 그다음 Netflix, 치지직. 소셜/제휴 로그인은 이후 순차 지원. YouTube는 쿠키 유지만.

## 데이터

- 모든 데이터는 `%LOCALAPPDATA%\MoaView\` 아래(`moaview.db`, `WebView2\`, `logs\`, `icons\`). 실행 파일 폴더에 쓰지 않고, 외부로 전송하지 않습니다.
- EF Core Code-First 마이그레이션. `DbContext`는 `IDbContextFactory<AppDbContext>`로 주입합니다(WPF ViewModel의 긴 수명 때문). 시각은 UTC ISO-8601 TEXT. 스키마는 [Doc/05_데이터베이스_설계.md](Doc/05_데이터베이스_설계.md).
- 기본 서비스 5종은 첫 실행 시 Seed. `IsBuiltIn` 서비스는 삭제 불가, 숨김만 가능.

## 공개 배포 대비 원칙 (D-08, 개인용 단계에서도 적용)

- 테스트 계정·개인 경로를 코드나 Seed에 넣지 않습니다. 개발용 계정은 User Secrets.
- 서비스 브랜드 로고를 앱 리소스에 넣지 않습니다. 실행 중 파비콘을 받아 `icons\{code}.png`에 캐시하고(`FaviconCache`), 없으면 이니셜 아이콘입니다. 서비스 삭제나 홈 URL 변경 시 캐시 파일을 지웁니다.
- 텔레메트리·원격 로그 없음. UI 문자열은 `.resx`로 분리(1차는 한국어)하고 NuGet 라이선스는 `THIRD-PARTY-NOTICES.md`에 기록(둘 다 미구현, Phase 4 할 일).

## 알려진 제약

- **모든 재생은 앱 안에서만 합니다(D-07).** 외부 브라우저·공식 앱으로 넘기는 "외부 실행" 기능은 없습니다. Netflix 4K는 PlayReady SL3000 하드웨어 DRM이 필요해 WebView2에서는 사실상 불가능하므로, "4K 지원"은 "앱은 4K 렌더링과 하드웨어 디코딩을 제공하고, 실제 화질은 서비스가 허용하는 최대치"로 정의합니다. 재생이 안 되는 서비스는 지원 목록에서 제외합니다.
- PoC에서 확인된 사실: WebView2에 PlayReady SL3000(하드웨어)과 Widevine L3 CDM이 있고 Netflix·TVING 1080p DRM 재생이 됩니다. DRM 재생 중 서비스 하나가 WebView2 프로세스 약 1 GB를 쓰므로 Suspend와 유지 개수 제한이 필수입니다. 숨겨진(Collapsed) WebView는 페이지 로드가 약 40초 걸리므로 미리 로드는 보이는 상태에서 해야 합니다(R-20). 단축키 입력은 사용자 제스처로 인정되어 `ExecuteScriptAsync`로 PiP를 열 수 있습니다.
- 치지직은 PC의 Edge·Chrome에서 720p60 이상을 보려면 그리드(브라우저 확장 + 네이버 라이브 스트리밍 커넥터)가 필요하고, 없으면 720p입니다(PoC 실측. 1080p 선택 여부는 미확인). **UA 우회는 불허(D-14)** 이며, 공식 확장을 `AddBrowserExtensionAsync`로 정식 로드하는 방법만 시도하고 안 되면 720p로 봅니다. 상세는 [Doc/03_기술_검토.md](Doc/03_기술_검토.md) 3.1절.
- 1차는 서비스당 계정 1개만 쓰지만 스키마와 프로필 이름 규칙은 다중 계정을 수용합니다(D-11). 미니 플레이어는 메인 창을 작은 Topmost 창으로 변형하는 방식이고, PIP는 Chromium 내장 PiP를 씁니다(D-12).
- `app.manifest`에 PerMonitorV2 DPI를 선언하고, `--disable-gpu` 같은 브라우저 인자를 쓰지 않습니다.
- 미결 사항과 결정 기록(D-01~D-15)은 [Doc/08_리스크_및_결정사항.md](Doc/08_리스크_및_결정사항.md)에서 관리합니다. 2026-10-13 기준 미결은 Q-05(추가 서비스, 보류)뿐입니다.
