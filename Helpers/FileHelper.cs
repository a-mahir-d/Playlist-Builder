using System.Collections.ObjectModel;
using System.Text.Json;
using Playlist_Builder.Models;

namespace Playlist_Builder.Helpers;

public class FileHelper
{
    private static string PlaylistsFilePath => Path.Combine(FileSystem.AppDataDirectory, "playlists.json");
    
    public static ObservableCollection<Playlist> LoadPlaylists()
    {
        ObservableCollection<Playlist> playlists = [];
        if(!File.Exists(PlaylistsFilePath)) return playlists;
        
        var json = File.ReadAllText(PlaylistsFilePath);
        var readPlaylists = JsonSerializer.Deserialize<ObservableCollection<Playlist>>(json);
        if (readPlaylists == null) return playlists;
        foreach (var p in readPlaylists)
        {
            playlists.Add(p);
        }

        return playlists;
    }
    
    public static void SavePlaylists(ObservableCollection<Playlist> playlists)
    {
        var json = JsonSerializer.Serialize(playlists);
        File.WriteAllText(PlaylistsFilePath, json);
    }
}