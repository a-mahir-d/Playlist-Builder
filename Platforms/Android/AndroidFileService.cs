using System.Text;
using System.Text.Json;
using Android.Content;
using AndroidX.DocumentFile.Provider;
using Playlist_Builder.Models;
using Uri = Android.Net.Uri;

namespace Playlist_Builder;

public partial class AndroidFileService
{
    private static TaskCompletionSource<Uri> _tcs;

    #nullable enable
    public static Task<Uri?> PickFolderAsync()
    {
        var activity = Platform.CurrentActivity ?? throw new NullReferenceException("Platform.CurrentActivity null.");
        _tcs = new TaskCompletionSource<Uri?>();

        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                        ActivityFlags.GrantWriteUriPermission |
                        ActivityFlags.GrantPersistableUriPermission |
                        ActivityFlags.GrantPrefixUriPermission);

        activity.StartActivityForResult(intent, 1001);

        return _tcs.Task;
    }
    
    public static Task<bool> GetPersistablePerm(Uri folderUri)
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
            return Task.FromResult(false);

        try
        {
            // Daha önce izin verilmiş mi kontrol et
            var existing = activity.ContentResolver?.PersistedUriPermissions;
            if (existing != null && existing.Any(p => p.Uri?.Equals(folderUri) == true && p.IsReadPermission && p.IsWritePermission))
            {
                return Task.FromResult(true);
            }

            // Yoksa izin al
            activity.ContentResolver?.TakePersistableUriPermission(
                folderUri,
                ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission
            );

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    private static (int, int, string) GetExistingPlaylistInfo(Uri songsFileUri)
    {
        try
        {
            var context = Platform.CurrentActivity;
            if (context == null)
                return (0, 0, "GetExistingPlaylistInfo: Context bulunamadı");

            List<Song> songs = [];

            using (var stream = context.ContentResolver?.OpenInputStream(songsFileUri))
            {
                if (stream != null)
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var json = reader.ReadToEnd();
                    songs = JsonSerializer.Deserialize<List<Song>>(json) ?? [];
                }
            }

            var totalDurationInSeconds = songs.Sum(s => s.DurationInSeconds);
            var totalSongs = songs.Count;
            return (totalSongs, totalDurationInSeconds, "");
        }
        catch (Exception ex)
        {
            return (0, 0, "GetExistingPlaylistInfo: " + ex.Message);
        }
    }
    
    public static List<Song> ReadSongsFile(string playlistDownloadFolderPath, string playlistName)
    {
        var context = Platform.CurrentActivity;
        if (context == null) return [];
    
        var uriPath = ParseToUri(playlistDownloadFolderPath);
        if (uriPath == null) return [];
        
        var root = DocumentFile.FromTreeUri(context, uriPath);
        if (root == null) return [];

        var playlistDir = root.FindFile(playlistName);
        if (playlistDir == null) return [];

        var files = playlistDir.ListFiles();
        if (files == null) return [];

        var songsFile = playlistDir.FindFile("songs.json");
        if (songsFile?.Uri == null)
            return [];

        List<Song> songs = [];
        using var stream = context.ContentResolver?.OpenInputStream(songsFile.Uri);
        if (stream == null) return [];
        
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var list = JsonSerializer.Deserialize<List<Song>>(json);
        if (list != null)
            songs = list;

        return songs;
    }
    
    public static (bool, string) CreatePlaylistFolder(string treePath, string playlistName)
    {
        var context = Platform.CurrentActivity;
        if (context == null) return (false, "CreatePlaylistFolder: Context bulunamadı");

        var treeUri = ParseToUri(treePath);
        if (treeUri == null) return (false, "İndirme klasörü Uri'ye çevrilemedi");
        
        var docFile = DocumentFile.FromTreeUri(context, treeUri);
        if (docFile == null) return (false, "CreatePlaylistFolder: İndirme klasörü bulunamadı");

        var files = docFile.ListFiles();
        if (files != null)
        {
            foreach (var file in files)
            {
                if(file.Name != playlistName) continue;
                
                var songsFile = file.FindFile("songs.json");
                if (songsFile?.Uri == null) return (false, "CreatePlaylistFolder: Çalma listesi klasörü zaten var ama songs.json yok");

                var (totalSongs, totalDurationInSeconds, errorMessage) = GetExistingPlaylistInfo(songsFile.Uri);
                return errorMessage != "" ? (false, errorMessage) : (true, $"CreatePlaylistFolder: Klasör zaten var --> {totalSongs},{totalDurationInSeconds}");
            }
        }

        var dir = docFile.CreateDirectory(playlistName);
        if (dir == null) return (false, "CreatePlaylistFolder: Çalma listesi klasörü oluşturulamadı");

        var jsonFile = dir.CreateFile("application/json", "songs.json");
        if (jsonFile == null) return (false, "CreatePlaylistFolder: songs.json oluşturulamadı");

        try
        {
            if (jsonFile?.Uri == null) return (false, "CreatePlaylistFolder: songs.json oluşturulamadı");

            var empty = new List<Song>();
            var json = JsonSerializer.Serialize(empty);

            using var stream = context.ContentResolver?.OpenOutputStream(jsonFile.Uri);
            if (stream == null) return (false, "CreatePlaylistFolder: songs.json' yazılamadı");

            using var writer = new StreamWriter(stream);
            writer.Write(json);
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"CreatePlaylistFolder: {ex}");
        }
    }

    private static Uri? ParseToUri(string path)
    {
        return Uri.Parse(path);
    }
}