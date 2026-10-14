<#
.SYNOPSIS
  MoaView를 Velopack 설치 패키지로 만든다(D-17). 결과는 Releases\ 폴더(.gitignore 대상).

.PARAMETER Version
  패키지 버전(SemVer). 생략하면 src\Directory.Build.props의 <Version>을 쓴다.

.PARAMETER Upload
  GitHub Releases에 업로드까지 한다. GITHUB_TOKEN 환경 변수 또는 gh 로그인 토큰이 필요하다.

.EXAMPLE
  pwsh tools/release/pack.ps1
  pwsh tools/release/pack.ps1 -Version 0.2.0 -Upload
#>
param(
    [string]$Version,
    [switch]$Upload
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

if (-not $Version) {
    $props = [xml](Get-Content "src\Directory.Build.props")
    $Version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
Write-Host "MoaView v$Version 패키징" -ForegroundColor Cyan

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    dotnet tool install -g vpk | Out-Null
}

$publishDir = Join-Path $root "publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet test src\MoaView.slnx -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "테스트 실패" }

# 프레임워크 의존 배포. .NET Desktop Runtime과 WebView2 런타임은 설치기가 --framework 옵션으로 받아 설치한다.
dotnet publish src\MoaView.App -c Release -r win-x64 --self-contained false -o $publishDir -p:Version=$Version -p:PublishReadyToRun=false --nologo
if ($LASTEXITCODE -ne 0) { throw "publish 실패" }

vpk pack `
    --packId MoaView `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe MoaView.exe `
    --packTitle MoaView `
    --packAuthors "Minwoo Nam" `
    --icon src\MoaView.App\Assets\moaview.ico `
    --framework net10.0-x64-desktop,webview2 `
    --runtime win-x64 `
    --channel win `
    --outputDir Releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack 실패" }

Get-ChildItem Releases | Select-Object Name, @{n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize

if ($Upload) {
    $token = $env:GITHUB_TOKEN
    if (-not $token) { $token = gh auth token }
    vpk upload github --repoUrl https://github.com/southmw/MoaView --publish --releaseName "MoaView v$Version" --tag "v$Version" --token $token --outputDir Releases --channel win
    if ($LASTEXITCODE -ne 0) { throw "업로드 실패" }
}
