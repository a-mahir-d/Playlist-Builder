using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Playlist_Builder.Models;

namespace Playlist_Builder.Helpers;

public class FileHelper
{
    private static string PlaylistsFilePath => Path.Combine(FileSystem.AppDataDirectory, "playlists.json");
    private static readonly JsonSerializerOptions writeIntentedJsonSerializerOption = new() { WriteIndented = true };
    
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
    
    public static bool AddSongsToPlaylist(Playlist playlist, int durationInSeconds, int songCount)
    {
        playlist.TotalSongs += songCount;
        playlist.TotalHours += durationInSeconds / 3600.0;
        UpdatePlaylist(playlist);
        return true;
    }
    
    public static void UpdatePlaylist(Playlist playlist)
    {
        try
        {
            var filePath = Path.Combine(FileSystem.AppDataDirectory, "playlists.json");

            List<Playlist> playlists = [];

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                playlists = JsonSerializer.Deserialize<List<Playlist>>(json) ?? [];
            }

            var existing = playlists.FirstOrDefault(p => p.Name == playlist.Name);
            if (existing != null)
            {
                existing.TotalSongs = playlist.TotalSongs;
                existing.TotalHours = playlist.TotalHours;
                existing.DownloadFolder = playlist.DownloadFolder;
            }
            else
            {
                playlists.Add(playlist);
            }

            var updatedJson = JsonSerializer.Serialize(playlists, writeIntentedJsonSerializerOption);
            File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine("JSON güncelleme hatası: " + ex.Message);
        }
    }
    
    public static void DeletePlaylist(string playlistName)
    {
        try
        {
            var filePath = Path.Combine(FileSystem.AppDataDirectory, "playlists.json");

            List<Playlist> playlists = [];

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                playlists = JsonSerializer.Deserialize<List<Playlist>>(json) ?? [];
            }

            var existing = playlists.FirstOrDefault(p => p.Name == playlistName);
            if (existing == null) return;

            playlists.Remove(existing);

            var updatedJson = JsonSerializer.Serialize(playlists, writeIntentedJsonSerializerOption);
            File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine("JSON güncelleme hatası: " + ex.Message);
        }
    }
    
    public static void RemoveSongFromPlaylist(Playlist playlist, int durationInSeconds)
    {
        playlist.TotalSongs--;
        playlist.TotalHours -= durationInSeconds / 3600.0;
        UpdatePlaylist(playlist);
    }
}