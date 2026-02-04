using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinManager.Models;

namespace WinManager.Converters;

/// <summary>
/// Shows a view only when SelectedSection matches ConverterParameter.
/// </summary>
public class SectionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AppSection current && parameter is string param && Enum.TryParse<AppSection>(param, out var target))
        {
            return current == target ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

