using System.Globalization;

namespace Playlist_Builder.Converters;

public class HourConverter : IValueConverter
{
    #nullable enable
    public object Convert(object? value, Type? targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double hours) return "0 saat";
        if (hours <= 0)
            return "0 sa. 0 dk.";

        var totalHours = (int)Math.Floor(hours);
        var minutes = (int)Math.Round((hours - totalHours) * 60);

        return $"{totalHours} sa. {minutes} dk.";

    }

    public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}