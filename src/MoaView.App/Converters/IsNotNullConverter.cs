using System.Globalization;
using System.Windows.Data;

namespace MoaView.App.Converters;

/// <summary>값이 null이 아니면 true. DataTrigger에서 "있음/없음"을 가를 때 쓴다.</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
