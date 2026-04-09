using System.Globalization;

namespace Playlist_Builder.Converters;

public class SecondsToDurationConverter : IValueConverter
{
    #nullable enable
    public object Convert(object? value, Type? targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int seconds) return "00:00";
        if (seconds <= 0)
        {
            return "?";
        }

        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";

    }

    public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}