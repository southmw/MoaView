# 09. PoC 결과 (Phase 0)

> 검증 항목 정의: [03 기술 검토](03_기술_검토.md) 6절. 판단 기준: [07 개발 계획](07_개발_계획.md) Phase 0.

| 항목 | 내용 |
|------|------|
| 상태 | **Go 판정** (2026-10-13). 일부 항목은 Phase 1과 병행해 계속 확인 |
| PoC 앱 | `src/MyOTTPlayer.Poc/`(WPF 코드 비하인드 검증 도구). **Go 판정 후 2026-10-13 삭제.** 실행 로그(`src/Report/logs`, 13개)와 캡처 3장도 함께 삭제했고, 이 문서가 유일한 기록 |
| 보존한 코드 | `src/MoaView.Infrastructure/WebView/ExtensionInstaller.cs`(스토어 CRX 내려받기·CRX3 서명 검증·`key` 주입, 미호출), `src/MoaView.App/Scripts/drm-probe.js`(EME·코덱 진단), `video-info.js`(영상 해상도·프레임 드롭) |
| 데이터 | `%LOCALAPPDATA%\MyOTTPlayer.Poc\` 는 삭제됨 |

## 1. 검증 환경

| 항목 | 값 | 확인일 |
|------|----|--------|
| OS | Windows 11 Enterprise 10.0.26300 | 2026-10-13 |
| .NET | SDK 10.0.401 / 런타임 10.0.12 | 2026-10-13 |
| WebView2 런타임 | 154.0.4258.62 (Evergreen) | 2026-10-13 |
| WebView2 SDK | 1.0.4258.31 | 2026-10-13 |
| WPF-UI | 4.3.0 | 2026-10-13 |
| WPF 렌더링 | Tier 2 (하드웨어 가속) | 2026-10-13 |
| 모니터 | 1920×1080, DPI 배율 1.00 (4K 모니터 테스트는 별도 필요) | 2026-10-13 |
| 네트워크 | 회사망. **netflix.com 차단**(TLS 리셋·타임아웃). Netflix 검증(P1)은 휴대폰 테더링으로 수행 | 2026-10-13 |
| GPU / 드라이버 | (기록 필요) | |
| 네이버 라이브 스트리밍 커넥터 | **설치됨** (치지직 안내 링크로 `NLiveConnectorSetup.exe` 내려받아 설치). `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\nliveconnector_1.0.0.0` 등록. 호스트 매니페스트의 `allowed_origins`에 확장 ID `jedbgfnh…` 포함 | 2026-10-13 |

## 2. 사전 확인 (앱 구성)

| 항목 | 결과 | 비고 |
|------|:----:|------|
| .NET 10 + WPF-UI 4.3.0 빌드·실행 | **통과** | FluentWindow, Mica, 다크 테마 정상. 경고 0 |
| `CoreWebView2Environment` 생성(앱 전체 공유) | **통과** | 약 30 ms |
| 서비스별 프로필 분리(`svc_{code}`) | **통과** | 5개 프로필 생성, 각자 초기화 |
| `WebView2CompositionControl` 로드 | **통과(수정 후)** | 처음엔 `Microsoft.Windows.SDK.NET` 어셈블리 없음으로 크래시. TFM을 `net10.0-windows10.0.19041.0`으로 바꿔 해결. **본 앱도 같은 TFM 필요** |
| 스토어 CRX 내려받기 → 폴더 로드 | **통과** | 아래 P5-b 참고 |

## 3. 검증 결과

결과 표기: **통과** / **조건부** / **실패** / **보류** / 미실행

| # | 검증 항목 | 결과 | 확인 내용 | 확인일 |
|---|-----------|:----:|-----------|--------|
| P1 | Netflix DRM 재생 | **통과** | (휴대폰 테더링) 로그인 후 `/watch/83226075` 재생 성공(HWND 컨트롤). "영상 정보": **1920×1080**, 10초 재생 동안 298 프레임 중 드롭 0. 이 PC 모니터가 1080p라 화질 상한은 디스플레이 기준이며, 4K는 4K 디스플레이에서 별도 확인. EME 조회(4.3절): PlayReady 3000(하드웨어 DRM)·HEVC OK, Widevine L3. 회사망에서는 netflix.com이 차단되어 `ERR_CONNECTION_RESET` | 2026-10-13 |
| P2 | TVING VOD·실시간 | **통과 (VOD)** | 로그인(account/login → 프로필 선택) 후 `/player/E004615366` 재생. "영상 정보": **1920×1080**, 101프레임 드롭 0(HWND). 실시간 채널은 미확인 | 2026-10-13 |
| P3 | Wavve VOD·실시간 | **조건부 통과 (VOD)** | 프로필 선택 후 `/player/stream/vod?programid=K02_T2026-0092` 재생. "영상 정보" 시점(114초)에는 **854×480**, 5초 뒤 PiP 진입 시 **1280×720**으로 올라감(적응형 화질 상승 중). 1080p 선택 가능 여부는 플레이어 화질 메뉴에서 확인 필요(요금제·콘텐츠 제한일 수 있음). 실시간 채널 미확인 | 2026-10-13 |
| P4 | YouTube 4K + Google 로그인 | **부분 통과** | **Google 로그인 성공**(accounts.google.com → 패스키 등록 권유 → SetSID → 로그인된 youtube.com). 임베디드 브라우저 차단 없음(R-04 해소). 2160p 재생은 미확인 | 2026-10-13 |
| P5-a | 치지직 (그리드 미설치) + 네이버 로그인 | **조건부 통과** | 그리드 없이 **1280×720** 재생(프레임 드롭 1075 중 3). 네이버 로그인(nid.naver.com) 성공, 캡차 없음. 그리드 안내의 확장 링크는 Edge 추가 기능 스토어로 가고, WebView2 안에서는 "사용자 브라우저와 호환되지 않음"으로 설치 불가(캡처 111047) | 2026-10-13 |
| P5-b | 치지직 그리드 정식 사용 | **통과 (통신 경로)** / 1080p 미확인 | (1) 스토어 공식 CRX(네이버 동영상 플러그인 v1.0.2.4) 내려받아 `key` 주입 후 `AddBrowserExtensionAsync` → **스토어와 같은 ID로 로드 성공**, 재시작 후에도 프로필에 유지. (2) 커넥터 설치 후 "그리드 연결 테스트": 페이지 → 확장 `sendMessage` 응답 `"install"` OK → 확장 → 커넥터 `connectNative("nliveconnector_1.0.0.0")` → **커넥터 응답 `{"cmd":"ping","svc":"nliveconnector"}`가 페이지까지 돌아옴**(실패 시 `errCode 1000`). 테스트 중 `NLiveConnectorLauncher.exe` 프로세스가 뜨는 것도 확인. 즉 **WebView2가 Edge의 NativeMessagingHosts 등록을 읽고 Native Messaging이 동작함**. (3) 남은 것: 실제 라이브에서 1080p 선택 가능 여부와 그리드 안내 사라짐(수동) | 2026-10-13 |
| P6 | HTML5 전체화면 ↔ 창 전체화면 | **부분 통과** | F11 앱 전체화면이 모니터 전체(1920×1080)를 덮음. 플레이어 전체화면 버튼 연동(`ContainsFullScreenElementChanged`)은 미확인 | 2026-10-13 |
| P7 | CompositionControl에서 DRM 렌더링 | **부분 통과** | TFM 수정 후 Composition 모드로 치지직(비DRM) 720p 재생 확인. DRM 콘텐츠는 P1~P3 이후 확인 | 2026-10-13 |
| P8 | WebView 5개 동시 유지 메모리 | **측정 완료(1회)** | 아래 4.1절. Suspend 효과는 미측정 | 2026-10-13 |
| P9 | 프로필 분리·세션 유지 | 미실행 | 프로필 분리는 동작. 재시작 후 로그인 유지, 쿠키 삭제 격리는 미확인 | 2026-10-13 |
| P10 | 미니 플레이어·PiP | **통과** | Wavve 재생 중 Ctrl+P → `requestPictureInPicture()` 성공(`ok 1280x720`), 다시 Ctrl+P로 `exited`. 단축키 입력이 사용자 제스처로 인정되어 `ExecuteScriptAsync`에서 PiP를 열 수 있음. 미니 플레이어 진입·해제도 재생 중 정상(13:46:09~11) | 2026-10-13 |

## 4. 측정값

### 4.1 메모리 (P8)

| 상태 | 앱 프로세스 | WebView2 프로세스 수 | WebView2 합계 | 비고 |
|------|------------:|---------------------:|--------------:|------|
| 서비스 0개 (환경만) | 266 MB | - | - | HWND 모드 |
| 서비스 5개 로드, Netflix 활성(오류 페이지) | 294 MB | 12 | 1,727 MB | GPU 352, Browser 194, Renderer 42~302 ×6, Utility 20~57 ×3 |
| 서비스 1개(치지직 재생), Composition | 351 MB | 6 | 884 MB | GPU 303, Browser 139, Renderer 352 |
| 위 + 확장 1개 로드 | 357 MB | 7 | 979 MB | 확장 서비스 워커용 Renderer 42 MB 추가 |
| **Netflix 1080p DRM 재생 중** (서비스 1개, HWND) | 294 MB | 8 | **1,512 MB** | Renderer 591, GPU 392, Utility 189(미디어/CDM 추정)+66+53+29+23, Browser 169 |
| TVING(숨김, 재생 직후) + Wavve 재생 중 (서비스 2개, HWND) | 290 MB | 12 | **1,987 MB** | Renderer 361+369+46, GPU 518, Utility 145+93+93+64+59+29+23, Browser 187 |
| 비활성 4개 Suspend 후 | | | | 미측정 |

→ 서비스 5개를 그대로 두면 WebView2 쪽이 1.7 GB에 이릅니다. NFR-04(셸 오버헤드 200 MB 이하)는 앱 프로세스 기준으로는 300 MB 안팎이라 Mica·WPF-UI 비용을 포함해 재검토가 필요합니다. Suspend와 LRU(최대 유지 개수)는 필수로 보입니다.

### 4.2 전환 시간 (NFR-03)

| 서비스 | 최초 생성·초기화 (ms) | 재전환 (ms) |
|--------|----------------------:|------------:|
| Netflix (앱 첫 WebView) | 5,431 (1회차) / 374 (2회차) | 0~3 |
| TVING | 65~77 | 2 |
| Wavve | 103~104 | 2~4 |
| YouTube | 65~152 | 3~4 |
| 치지직 | 72~91 (HWND) / 675~844 (Composition) | 0~3 |

→ 로드된 서비스 간 전환은 수 ms로 NFR-03(1초 이내)을 충족합니다. 첫 WebView 생성만 수 초가 걸릴 수 있습니다.

### 4.3 DRM·코덱 진단 (EME, Netflix 로그인 페이지에서 실행, HWND 컨트롤)

`navigator.requestMediaKeySystemAccess`로 키 시스템과 robustness를 조회한 결과입니다("DRM 진단" 버튼 / `--drm-probe`. PoC 기능, 삭제됨. 스크립트만 `drm-probe.js`로 보존).

| 키 시스템 | robustness | H.264 | HEVC | 의미 |
|-----------|-----------|:-----:|:----:|------|
| com.widevine.alpha | SW_SECURE_CRYPTO / SW_SECURE_DECODE | OK | 불가 | Widevine **L3**(소프트웨어). HEVC는 Widevine과 조합 불가 |
| com.widevine.alpha | HW_SECURE_* (L1) | 불가 | 불가 | Windows PC 브라우저의 일반적 상태 |
| com.microsoft.playready(.recommendation) | 150 / 2000 | OK | OK | PlayReady 소프트웨어 |
| com.microsoft.playready.recommendation | **3000** | **OK** | **OK** | **PlayReady 하드웨어 DRM(SL3000)**. Netflix 4K의 전제 조건 |
| com.microsoft.playready.recommendation.3000, .hardware | - | OK | OK | 위와 같음 |
| com.apple.fps | - | 불가 | 불가 | 해당 없음 |

| 디코딩 능력 (mediaCapabilities, media-source) | 결과 |
|------|------|
| H.264 1080p | supported, smooth, powerEfficient |
| **HEVC 10-bit 4K HDR(PQ, BT.2020)** | **supported, smooth, powerEfficient** (하드웨어 디코딩) |
| VP9 4K60 | supported, smooth, powerEfficient |
| AV1 4K60 | supported, smooth, powerEfficient |
| MSE isTypeSupported | h264, hevc, hevc10, av1, vp9, vp9 10-bit 모두 true |

→ **WebView2의 DRM·코덱 능력은 Edge 브라우저와 같습니다.** 03 기술 검토 2.2절의 "WebView2 임베디드 환경 DRM 지원 미확인"은 해소됐고, Netflix 4K의 소프트웨어 조건(PlayReady SL3000, HEVC 하드웨어 디코딩)은 충족합니다. 남은 조건은 Netflix 측 요구사항(프리미엄 요금제, 모든 디스플레이 HDCP 2.2, Netflix가 WebView2 UA를 Edge로 인정하는지)입니다. 이 PC의 모니터는 1920×1080이라 4K 실측은 4K 디스플레이에서 따로 해야 합니다.

## 5. 관찰 사항

- **Netflix 연결**: 개발 PC가 있는 **회사망은 netflix.com을 차단**합니다(TLS 리셋 또는 타임아웃). 13:35~13:46의 Netflix 재생 테스트(P1 통과)는 휴대폰 테더링으로 한 것입니다. WebView2와 무관하며, 막힌 상태에서는 탐색 시작 뒤 아무 이벤트도 오지 않아 검은 화면만 보입니다. 자동 검증은 YouTube·치지직으로 하고, Netflix 실측은 테더링이나 집 네트워크에서 합니다.
- **치지직 그리드 구조** (CRX 분석): 확장은 `background.js`(서비스 워커)가 페이지에서 `chrome.runtime.sendMessage(확장ID, {svc, ver, param})`로 받은 요청을 `chrome.runtime.connectNative(svc + "_" + ver)`로 커넥터에 전달하고, 응답을 `content.js` → `window.postMessage`로 페이지에 돌려줍니다. `externally_connectable`이 `*.naver.com`이라 **페이지가 확장을 스토어 ID로 직접 찾습니다**. 그래서 폴더 로드 시 manifest에 `key`를 넣어 ID를 맞추는 것이 필수입니다.
- **WebView2 확장 제한** (공식 스펙): 스토어 설치 불가, 폴더 로드만 가능. `_`로 시작하는 폴더(`_metadata`) 있으면 `E_ACCESSDENIED` → 삭제 필요. 확장 파일을 바꾸면 프로필에서 제거됨. 확장 UI(아이콘·팝업)는 표시할 곳이 없음. **Native Messaging 지원 여부는 문서에 없음**. Teams(WebView2 기반)에서는 "지원하지 않음"이라는 MS 답변(2024)이 있어 가능성은 낮게 봐야 합니다.
- **숨겨진(Collapsed) WebView의 페이지 로드가 40초 넘게 걸림**: TVING·Wavve를 보이지 않는 상태로 두면 `NavigationCompleted`가 약 40초 뒤에 옵니다(11:29:03 시작 → 11:29:45 완료, 두 실행에서 재현). 같은 TVING 홈을 **보이는 상태**에서 열면 3.4초(13:41:25 → 13:41:29). 본 앱에서 "미리 로드"를 하려면 보이는 상태에서 로드한 뒤 숨겨야 합니다.
- **DRM 재생 중 메모리**: Netflix 1080p 재생만으로 WebView2 프로세스 합계가 1.5 GB(렌더러 591 MB, GPU 392 MB). 서비스 5개를 모두 살려 두면 3 GB를 넘길 수 있어 Suspend·LRU가 필수입니다.
- **커넥터 설치 파일 다운로드**: 치지직의 커넥터 링크는 `NewWindowRequested`(appdown.pstatic.net의 exe)로 들어오고, 같은 창에서 열면 WebView2 기본 다운로드 UI로 받아집니다(9 MB). 본 앱에서는 외부 도메인 새 창을 기본 브라우저로 보내는 정책(FR-NAV-04)이라 이 흐름이 브라우저에서 이어집니다.
- **확장 영속성**: 프로필에 로드한 확장은 앱을 재시작해도 남아 있습니다(확장 목록에 네이버 동영상 플러그인 유지). 같은 ID로 다시 로드하면 교체됩니다.
- **치지직 재로그인**: 11:10에 로그인했는데 11:28에 다시 로그인 화면을 거쳤습니다. 세션 만료인지 사용자가 직접 누른 것인지 불명확. P9에서 재시작 후 로그인 유지 여부를 따로 확인.
- **세션 유지 차이(P9 관련)**: 11:30에 로그인한 TVING은 13:44 재시작 시 **다시 로그인 페이지**로 갔고, 같은 시각 로그인한 Wavve는 13:45에 **프로필 선택으로 바로 진입**(세션 유지). 프로필 쿠키 영속화 자체는 Wavve로 증명됐으므로, TVING은 서비스 측 세션 수명이 짧은 것으로 보입니다(R-16, D-09의 자동 입력 필요성 뒷받침).
- **Wavve 적응형 화질**: 재생 114초 시점 480p → 119초 시점 720p. 측정은 재생 시작 후 충분히 기다린 뒤(또는 화질을 수동 고정한 뒤) 해야 합니다. Wavve 페이지에는 `<video>`가 2개(본편 + 0×0의 광고/보조 플레이어).
- **새 창 요청**: 치지직 그리드 안내 링크가 `NewWindowRequested`로 들어옴(사용자 시작). 본 앱에서는 외부 도메인(microsoftedge.microsoft.com)이라 기본 브라우저로 보내는 정책(FR-NAV-04)이 맞음.
- **Composition 컨트롤 비용**: 치지직 초기화가 HWND 72~91 ms → Composition 675~844 ms로 늘고, 앱 프로세스 메모리도 약 60 MB 더 큼.

## 6. 다음 단계

1. 치지직 라이브를 실제로 틀고 화질 메뉴에 **1080p가 생기는지**, 그리드 설치 안내가 사라졌는지 확인 (P5-b 마지막 조건). "영상 정보" 버튼으로 `size`가 1920x1080인지 기록.
2. TVING·Wavve에서 실제 영상 재생 후 "영상 정보"로 해상도 기록(P2, P3). 둘 중 하나만 되어도 Go 기준 충족.
3. YouTube 2160p와 Google 로그인(P4), 재생 중 PiP(P10), 플레이어 전체화면 버튼 연동(P6).
4. 5개 로드 → "비활성 Suspend" → "메모리"로 Suspend 효과 측정(P8).
5. 앱 재시작 후 로그인 유지(P9).

## 7. Go / No-Go 판단

| 기준 | 결과 |
|------|------|
| DRM 서비스(Netflix/TVING/Wavve) 중 2개 이상 재생 | **3/3 재생 성공** (Netflix 1080p, TVING 1080p, Wavve 720p 이상) |
| 판단 | **Go** (2026-10-13). WebView2 통합 브라우저 셸 방식으로 Phase 1 진행 |

**Go 이후 계속 확인할 항목** (Phase 1과 병행, 결과는 이 문서에 추가)
- P3 Wavve 1080p 선택 가능 여부, P2·P3 실시간 채널
- P4 YouTube 2160p (4K 디스플레이 필요), P5-b 치지직 1080p 선택 여부
- P6 플레이어 전체화면 버튼 → 창 전체화면 연동
- P7 Composition 컨트롤에서 DRM 재생(검은 화면 여부)
- P8 5개 로드 후 Suspend 효과, P9 재시작 후 세션 유지(서비스별)

## 8. 설계에 반영할 사항 (확정분)

| 항목 | 반영 |
|------|------|
| DRM 전제 | WebView2에 PlayReady SL3000(하드웨어)과 Widevine L3가 있음. R-01(DRM 재생 불가)은 가능성 하향. "4K 지원" 정의(D-07)에서 소프트웨어 쪽 전제는 충족 |
| DRM 진단 화면 | 설정 > 정보의 DRM 진단은 `Scripts/drm-probe.js`(PoC에서 옮김)를 쓰면 됨(키 시스템별 robustness, HEVC/AV1/VP9 디코딩, HDCP는 별도) |
| TFM | 본 앱 App·Infrastructure는 `net10.0-windows10.0.19041.0` + `SupportedOSPlatformVersion 10.0.17763.0` (Composition 컨트롤과 Windows SDK 프로젝션 때문) |
| 메모리 | Suspend + 최대 유지 개수(LRU)는 Should가 아니라 **Must**로 올리는 것을 검토 (FR-PLY-05) |
| 그리드 확장 | 확장 설치는 "스토어 CRX 내려받기 → `_metadata` 제거 → `key` 주입 → 폴더 로드" 절차로 자동화 가능(`ExtensionInstaller.cs`로 보존). 본 앱에 넣을지는 P5-b 결과를 본 뒤 결정 |
| 새 창 정책 | 외부 도메인 새 창은 기본 브라우저로(FR-NAV-04) 유지 |
