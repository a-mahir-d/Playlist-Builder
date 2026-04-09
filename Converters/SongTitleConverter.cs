using System.Globalization;
using Playlist_Builder.Models;

namespace Playlist_Builder.Converters;

public class SongTitleConverter : IValueConverter
{
    #nullable enable
    public object Convert(object? value, Type? targetType, object? parameter, CultureInfo culture)
    {
        if (value is Song song)
        {
            return $"{song.Name} by {song.Artist}";
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}