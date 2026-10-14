using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoaView.App.ViewModels;
using MoaView.App.Views;

namespace MoaView.App.Services;

public sealed class SettingsDialogService(IServiceProvider services, ILogger<SettingsDialogService> logger) : ISettingsDialog
{
    private SettingsWindow? _open;

    public async Task<SettingsResult> ShowAsync()
    {
        if (_open is not null)
        {
            _open.Activate();
            return SettingsResult.None;
        }

        var viewModel = services.GetRequiredService<SettingsViewModel>();
        var window = new SettingsWindow(viewModel) { Owner = Application.Current.MainWindow };
        _open = window;
        try
        {
            await viewModel.LoadAsync();
            window.ShowDialog();
        }
        finally
        {
            _open = null;
        }

        if (viewModel.RestartRequested)
        {
            Restart();
            return SettingsResult.None;
        }

        return new SettingsResult(viewModel.ChangedServiceIds, viewModel.ServicesChanged);
    }

    /// <summary>WebView2 데이터 초기화(FR-SET-04)는 브라우저 프로세스가 폴더를 잡고 있어 다음 시작 때 적용한다.</summary>
    private void Restart()
    {
        var exe = Environment.ProcessPath;
        if (exe is null)
        {
            logger.LogWarning("실행 파일 경로를 알 수 없어 다시 시작하지 못했습니다. 수동으로 다시 실행하세요.");
            Application.Current.Shutdown();
            return;
        }

        logger.LogInformation("사용자 요청으로 다시 시작: {Exe}", exe);
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        Application.Current.Shutdown();
    }
}
