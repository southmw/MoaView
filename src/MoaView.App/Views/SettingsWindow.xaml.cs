using MoaView.App.ViewModels;
using Wpf.Ui.Controls;

namespace MoaView.App.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
