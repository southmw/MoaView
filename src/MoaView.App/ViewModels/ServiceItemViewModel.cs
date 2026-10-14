using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using MoaView.Core.Entities;

namespace MoaView.App.ViewModels;

/// <summary>
/// 사이드바 항목 하나. 실행 중에 캐시한 파비콘(Service.IconPath)이 있으면 그것을, 없으면 이니셜과 강조 색으로 그린다(D-08, FR-SVC-06).
/// </summary>
public sealed partial class ServiceItemViewModel : ObservableObject
{
    private static readonly Brush FallbackBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));

    public ServiceItemViewModel(StreamingService service, int index, MainViewModel owner)
    {
        Service = service;
        Index = index;
        Owner = owner;
        AccentBrush = CreateBrush(service.AccentColor);
        RefreshIcon();
    }

    public StreamingService Service { get; }

    /// <summary>컨텍스트 메뉴 명령(계정 관리, 로그아웃 등)이 메인 ViewModel에 있어서 참조한다.</summary>
    public MainViewModel Owner { get; }

    /// <summary>0부터 시작. 사이드바 순서이자 Ctrl+{Index+1} 단축키.</summary>
    public int Index { get; }

    public string Code => Service.Code;

    public string Name => Service.Name;

    public string Initial => Service.Name.Length > 0 ? Service.Name[..1].ToUpperInvariant() : "?";

    public string HotkeyText => Index < 9 ? $"Ctrl+{Index + 1}" : string.Empty;

    public Brush AccentBrush { get; }

    /// <summary>캐시된 파비콘. null이면 이니셜 아이콘을 그린다.</summary>
    [ObservableProperty]
    public partial ImageSource? IconSource { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>WebView가 생성된 서비스는 점(●)으로 표시한다(06 UI/UX 2.1절).</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    /// <summary>Service.IconPath의 파일을 다시 읽는다. 파일을 잠그지 않도록 메모리에 올린 뒤 닫는다.</summary>
    public void RefreshIcon()
    {
        var path = Service.IconPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            IconSource = null;
            return;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            IconSource = image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
        {
            IconSource = null;
        }
    }

    private static Brush CreateBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return FallbackBrush;
        }

        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return FallbackBrush;
        }
    }
}
