using System.IO;

namespace MoaView.Infrastructure.Configuration;

/// <summary>
/// 모든 데이터는 %LOCALAPPDATA%\MoaView 아래에 둔다(NFR-06, 04 아키텍처 6절). 실행 파일 폴더에는 쓰지 않는다.
/// </summary>
public sealed class AppPaths
{
    public AppPaths()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoaView"))
    {
    }

    public AppPaths(string root)
    {
        Root = root;
        DatabaseFile = Path.Combine(root, "moaview.db");
        WebView2UserDataFolder = Path.Combine(root, "WebView2");
        LogsFolder = Path.Combine(root, "logs");
        IconsFolder = Path.Combine(root, "icons");
    }

    public string Root { get; }
    public string DatabaseFile { get; }
    public string WebView2UserDataFolder { get; }
    public string LogsFolder { get; }
    public string IconsFolder { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(WebView2UserDataFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(IconsFolder);
    }
}
