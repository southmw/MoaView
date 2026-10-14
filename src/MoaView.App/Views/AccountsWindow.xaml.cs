using MoaView.App.ViewModels;
using Wpf.Ui.Controls;

namespace MoaView.App.Views;

public partial class AccountsWindow : FluentWindow
{
    public AccountsWindow(AccountsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
