namespace MoaView.Core.Models;

/// <summary>AppSettings 테이블의 키(05 데이터베이스 설계 3.3절).</summary>
public static class SettingKeys
{
    /// <summary>"last" 또는 "fixed".</summary>
    public const string StartupServiceMode = "Startup.ServiceMode";

    /// <summary>고정 시작 서비스 Id.</summary>
    public const string StartupServiceId = "Startup.ServiceId";

    /// <summary>마지막으로 보던 서비스 Id(FR-SVC-07).</summary>
    public const string LastServiceId = "Startup.LastServiceId";

    /// <summary>창 위치·크기·상태 JSON(FR-FS-05).</summary>
    public const string WindowPlacement = "Window.Placement";

    /// <summary>미니 플레이어 창의 마지막 위치·크기(FR-PLY-07). WindowPlacement 레코드, State는 쓰지 않는다.</summary>
    public const string MiniPlacement = "Window.MiniPlacement";

    /// <summary>"dark"(기본) / "light" / "system".</summary>
    public const string Theme = "UI.Theme";

    public const string SidebarCollapsed = "UI.SidebarCollapsed";

    public const string SuspendAfterSeconds = "Player.SuspendAfterSeconds";

    public const string MaxAliveHosts = "Player.MaxAliveHosts";

    public const string LockEnabled = "Security.LockEnabled";

    /// <summary>
    /// 서비스별 로그인 쿠키 서명 접두사. 전체 키는 "Login.CookieSignature.{serviceCode}", 값은 쿠키 이름 JSON 배열(값은 저장하지 않음).
    /// </summary>
    public const string CookieSignaturePrefix = "Login.CookieSignature.";

    /// <summary>true면 다음 시작 때 WebView2 데이터 폴더(모든 서비스의 쿠키·캐시)를 지운 뒤 플래그를 내린다(FR-SET-04).</summary>
    public const string ResetWebViewOnStart = "Data.ResetWebViewOnStart";

    public const string StartupModeLast = "last";
    public const string StartupModeFixed = "fixed";

    public const string ThemeDark = "dark";
    public const string ThemeLight = "light";
    public const string ThemeSystem = "system";
}

/// <summary>Window.Placement 값. 좌표는 WPF DIP 단위.</summary>
public sealed record WindowPlacement(double X, double Y, double Width, double Height, string State);
