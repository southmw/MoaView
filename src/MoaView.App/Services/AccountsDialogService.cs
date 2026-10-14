using System.Windows;
using Microsoft.Extensions.Logging;
using MoaView.App.ViewModels;
using MoaView.App.Views;
using MoaView.Core.Abstractions;
using MoaView.Core.Entities;

namespace MoaView.App.Services;

public sealed class AccountsDialogService(
    IAccountRepository accounts,
    ICredentialProtector protector,
    IStreamingProviderResolver providers,
    ILoggerFactory loggerFactory) : IAccountsDialog
{
    private AccountsWindow? _open;

    public async Task ShowAsync(StreamingService service)
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        var viewModel = new AccountsViewModel(service, accounts, protector,
            providers.Resolve(service).PrimaryLoginProvider, loggerFactory.CreateLogger<AccountsViewModel>());
        // 설정 창에서 열면 설정 창 위에 떠야 하므로 활성 창을 소유자로 한다.
        var window = new AccountsWindow(viewModel)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow,
        };
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
    }
}
