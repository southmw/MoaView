using Velopack;

namespace MoaView.App;

/// <summary>
/// 진입점. Velopack 훅(설치·제거·업데이트 직후 실행되는 인스턴스)을 WPF가 뜨기 전에 처리해야 해서 App.xaml의 자동 Main 대신 쓴다
/// (csproj의 StartupObject). 훅 호출이면 VelopackApp이 여기서 프로세스를 끝낸다.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
