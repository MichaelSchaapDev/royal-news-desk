using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RoyalNewsDesk.App.Converters;

/// <summary>Shows the element only when the bound value is a non-empty string or non-null object.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
