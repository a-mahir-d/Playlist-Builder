namespace Playlist_Builder.Models;

public class Song
{
    public required string Name { get; set; }
    public required string Artist { get; set; }
    public required string YoutubeUrl { get; set; }
    public int DurationInSeconds { get; set; }
    public bool IsDownloaded { get; set; }
}