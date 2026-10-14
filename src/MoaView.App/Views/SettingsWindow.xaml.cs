using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MoaView.App.ViewModels;
using Wpf.Ui.Controls;

namespace MoaView.App.Views;

public partial class SettingsWindow : FluentWindow
{
    private readonly ILogger<SettingsWindow> _logger;
    private Point _dragStart;
    private ServiceRowViewModel? _dragCandidate;

    public SettingsWindow(SettingsViewModel viewModel, ILogger<SettingsWindow> logger)
    {
        _logger = logger;
        InitializeComponent();
        DataContext = viewModel;
    }

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    // ---------------------------------------------------------------- 서비스 목록 드래그 정렬 (FR-SVC-04, D-18)

    private void OnServiceListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragCandidate = RowAt(e.OriginalSource);
        _logger.LogDebug("드래그 후보: {Row} (원본 {Source})", _dragCandidate?.Name, e.OriginalSource.GetType().Name);
    }

    private void OnServiceListMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var row = _dragCandidate;
        _dragCandidate = null;
        _logger.LogDebug("드래그 시작: {Row}", row.Name);
        var result = DragDrop.DoDragDrop(ServiceListView, row, DragDropEffects.Move);
        _logger.LogDebug("드래그 끝: {Row} → {Result}", row.Name, result);
    }

    private void OnServiceListDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ServiceRowViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnServiceListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ServiceRowViewModel)) is not ServiceRowViewModel row)
        {
            return;
        }

        // 놓은 자리의 행 위치로. 행 밖(목록 아래 빈 공간)에 놓으면 맨 끝으로.
        var target = RowAt(e.OriginalSource);
        var index = target is null ? ViewModel.Services.Count - 1 : ViewModel.Services.IndexOf(target);
        _logger.LogDebug("드롭: {Row} → {Index} (대상 {Target})", row.Name, index, target?.Name);
        await ViewModel.MoveToAsync(row, index);
    }

    private ServiceRowViewModel? RowAt(object source) =>
        source is DependencyObject element
        && ItemsControl.ContainerFromElement(ServiceListView, element) is System.Windows.Controls.ListViewItem item
            ? item.DataContext as ServiceRowViewModel
            : null;
}
