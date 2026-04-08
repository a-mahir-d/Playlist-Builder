namespace Playlist_Builder.Models;

public class Playlist
{
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public int TotalSongs { get; set; }
    public double TotalHours { get; set; }
    public string DownloadFolder { get; set; }
}