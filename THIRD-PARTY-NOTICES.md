# Third-Party Notices

MoaView는 아래 오픈소스·서드파티 구성 요소를 사용합니다. 각 구성 요소의 저작권과 라이선스는 해당 프로젝트에 있으며, 전문은 링크한 저장소 또는 NuGet 패키지 안의 라이선스 파일을 참고하세요. 이 목록은 `src/*/*.csproj`의 패키지 참조를 기준으로 하며, 패키지를 추가·변경할 때 함께 갱신합니다.

MoaView 자체의 라이선스는 [LICENSE](LICENSE)(MIT)입니다.

## 실행 파일에 포함되는 구성 요소

| 구성 요소 | 버전 | 라이선스 | 용도 | 출처 |
|-----------|------|----------|------|------|
| Microsoft Edge WebView2 SDK (`Microsoft.Web.WebView2`) | 1.0.4258.31 | Microsoft Software License Terms (재배포 허용, 비오픈소스) | 서비스 웹 플레이어 호스팅. WebView2 런타임(Evergreen)은 사용자 PC에 별도 설치되며 Microsoft 라이선스를 따름 | https://www.nuget.org/packages/Microsoft.Web.WebView2 |
| WPF-UI (`WPF-UI`) | 4.3.0 | MIT | Fluent 스타일 컨트롤, 테마, 타이틀바(D-13) | https://github.com/lepoco/wpfui |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM 소스 생성기(ObservableObject, RelayCommand) | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.Hosting / DependencyInjection / Logging / Options / Configuration | 10.0.12 | MIT | Generic Host, DI, 옵션 | https://github.com/dotnet/runtime |
| Microsoft.EntityFrameworkCore, .Relational, .Sqlite, Microsoft.Data.Sqlite | 10.0.12 | MIT | SQLite 데이터 접근, 마이그레이션 | https://github.com/dotnet/efcore |
| SQLitePCLRaw (`bundle_e_sqlite3`, core, provider, lib) | 2.1.12 | Apache-2.0 | SQLite 네이티브 바인딩 | https://github.com/ericsink/SQLitePCL.raw |
| SQLite | (SQLitePCLRaw 번들) | Public Domain | 임베디드 데이터베이스 엔진 | https://sqlite.org/copyright.html |
| Serilog, Serilog.Extensions.Hosting, Serilog.Extensions.Logging | 10.0.0 (Serilog 4.x) | Apache-2.0 | 구조화 로깅 | https://github.com/serilog/serilog |
| Serilog.Sinks.File | 7.0.0 | Apache-2.0 | 파일 로그(일 단위 롤링) | https://github.com/serilog/serilog-sinks-file |
| Velopack | 1.2.161 | MIT | 설치 패키지, 자동 업데이트 | https://github.com/velopack/velopack |
| .NET 10 Runtime, Windows Desktop Runtime (WPF) | 10.0 | MIT | 실행 환경 | https://github.com/dotnet/runtime, https://github.com/dotnet/wpf |

`System.Security.Cryptography.ProtectedData`(DPAPI)는 Windows 전용 .NET 공유 프레임워크에 포함되어 별도 패키지가 아닙니다(MIT).

## 개발·테스트에만 쓰는 구성 요소 (배포물에 포함되지 않음)

| 구성 요소 | 버전 | 라이선스 | 용도 |
|-----------|------|----------|------|
| Microsoft.EntityFrameworkCore.Design (및 전이 의존성 Roslyn, Humanizer, Mono.TextTemplating, Newtonsoft.Json) | 10.0.12 | MIT | `dotnet ef` 마이그레이션 생성. `PrivateAssets=all` |
| xunit, xunit.runner.visualstudio | 2.9.3 / 3.1.5 | Apache-2.0 | 단위 테스트 |
| FluentAssertions | 7.2.2 | Apache-2.0 | 테스트 단언. **8.x부터는 상용 라이선스(Xceed)로 바뀌었으므로 7.x에 고정** |
| Microsoft.NET.Test.Sdk | 18.10.1 | MIT | 테스트 실행기 |
| Velopack CLI (`vpk`) | 1.2.161 | MIT | 패키징 도구(전역 dotnet tool) |

## 서비스 상표

Netflix, TVING, Wavve, YouTube, 치지직(CHZZK)과 그 로고는 각 회사의 상표입니다. MoaView는 이들 서비스와 제휴·승인 관계가 없으며, 브랜드 로고를 앱 리소스에 포함하지 않습니다. 사이드바 아이콘은 실행 중에 각 사이트가 제공하는 파비콘을 받아 사용자 PC에만 캐시합니다(D-08).
