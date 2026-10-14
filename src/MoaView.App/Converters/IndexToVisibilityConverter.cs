using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MoaView.App.Converters;

/// <summary>설정 창의 탭 패널 전환용: 선택 인덱스가 ConverterParameter와 같을 때만 Visible.</summary>
public sealed class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && parameter is string text && int.TryParse(text, CultureInfo.InvariantCulture, out var expected) && index == expected
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
