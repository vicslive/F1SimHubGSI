using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace F1SimHubLive.Picker.Services;

/// <summary>
/// Converts a MultiViewer team colour hex string (no leading '#', e.g. "E80020")
/// to a WPF SolidColorBrush. Returns DimGray when the hex is missing or unparseable
/// so the row still renders.
/// </summary>
internal sealed class HexToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Fallback = new(Color.FromRgb(0x40, 0x40, 0x40));

    static HexToBrushConverter() { Fallback.Freeze(); }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string hex = (value as string ?? "").Trim().TrimStart('#');
        if (hex.Length != 6) return Fallback;
        if (!byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return Fallback;

        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
