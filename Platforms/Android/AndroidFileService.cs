using System.Text;
using System.Text.Json;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.DocumentFile.Provider;
using Playlist_Builder.Models;
using Environment = System.Environment;
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
            var existing = activity.ContentResolver?.PersistedUriPermissions;
            if (existing != null && existing.Any(p => p.Uri?.Equals(folderUri) == true && p.IsReadPermission && p.IsWritePermission))
            {
                return Task.FromResult(true);
            }
            
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

    public static async Task<List<Song>> PickAndProcessJsonFile()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Lütfen bir JSON dosyası seçin",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, ["application/json"] }
                })
            };

            var result = await FilePicker.Default.PickAsync(options);
            if (result == null) return [];

            await using var stream = await result.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var jsonContent = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<List<Song>>(jsonContent) ?? [];
        }
        catch
        {
            return [];
        }
    }
    
    public static bool AddSongsToPlaylist(List<Song> newSongs, string playlistDownloadFolderPath, string playlistName)
    {
        var context = Platform.CurrentActivity;
        if (context == null) return false;
    
        var uriPath = ParseToUri(playlistDownloadFolderPath);
        if (uriPath == null) return false;
        
        var root = DocumentFile.FromTreeUri(context, uriPath);
        if (root == null) return false;

        var playlistDir = root.FindFile(playlistName);
        if (playlistDir == null) return false;

        var files = playlistDir.ListFiles();
        if (files == null) return false;

        var songsFile = playlistDir.FindFile("songs.json");
        if (songsFile?.Uri == null) return false;
        
        List<Song> existingSongs = [];
        using var stream = context.ContentResolver?.OpenInputStream(songsFile.Uri);
        if (stream == null) return false;
        
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var list = JsonSerializer.Deserialize<List<Song>>(json);
        if (list != null)
            existingSongs = list;
        
        songsFile.Delete();
        
        var newSongsFile = playlistDir.CreateFile("application/json", "songs.json");
        if (newSongsFile?.Uri == null) return false;

        try
        {
            json = JsonSerializer.Serialize(existingSongs.Concat(newSongs).ToList());

            using var newStream = context.ContentResolver?.OpenOutputStream(newSongsFile.Uri);
            if (newStream == null) return false;

            using var writer = new StreamWriter(newStream);
            writer.Write(json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }
    
    public static (bool, string) SaveOpus(byte[] opusBytes, Song song, Playlist playlist)
    {
        var context = Platform.CurrentActivity;
        if (context == null || string.IsNullOrEmpty(playlist.DownloadFolder)) return (false, "SaveSong: Context bulunamadı");

        var treeUri = Uri.Parse(playlist.DownloadFolder);
        var root = DocumentFile.FromTreeUri(context, treeUri);
        if (root == null) return (false, "SaveSong: İndirme klasörü bulunamadı");

        var playlistDir = root.FindFile(playlist.Name) ?? root.CreateDirectory(playlist.Name);
        if (playlistDir == null) return (false, "SaveSong: Çalma listesi klasörü bulunamadı");
        
        var(success, errorMessage) = SaveOpus(opusBytes, playlistDir, $"{song.Name}");
        return !success ? (false, "SaveSong: " + errorMessage) : (true, "");
    }
    
    private static (bool, string) SaveOpus(byte[] opusBytes, DocumentFile playlistDir, string fileName)
    {
        try
        {
            var context = Platform.CurrentActivity;
            if (context == null) return (false, "SaveOpus: Context bulunamadı");

            var opusFile = playlistDir.CreateFile("audio/opus", fileName + ".opus");
            if (opusFile?.Uri == null) return (false, "SaveOpus: .opus oluşturulamadı");

            using var stream = context.ContentResolver?.OpenOutputStream(opusFile.Uri);
            if (stream == null) return (false, "SaveOpus: stream bulunamadı");

            stream.Write(opusBytes, 0, opusBytes.Length);
            stream.Flush();
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, "SaveOpus: " + ex.Message);
        }
    }
    
    public static bool SaveToDownloads(Context context, string filePath, string displayName)
    {
        try
        {
            var sdk = (int)Build.VERSION.SdkInt;
            if (sdk >= 29)
            {
                var values = new ContentValues();
                values.Put(MediaStore.IMediaColumns.DisplayName, displayName);
                values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
                values.Put("relative_path", "Download/");

#pragma warning disable CA1416
                var uri = context.ContentResolver?.Insert(MediaStore.Downloads.ExternalContentUri, values);
#pragma warning restore CA1416
                if (uri == null) return false;

                using var input = System.IO.File.OpenRead(filePath);
                using var output = context.ContentResolver?.OpenOutputStream(uri);
                if (output == null) return false;

                input.CopyTo(output);
                return true;
            }
        }
        catch
        {
            // ignored
        }

        return false;
    }
    
    public static bool UpdateNewlyDownloadedSongs(List<Song> songs, List<Song> songsToBeUpdated, string playlistDownloadFolderPath, string playlistName)
    {
        var context = Platform.CurrentActivity;
        if (context == null) return false;
        
        var playlistDownloadFolderUri = ParseToUri(playlistDownloadFolderPath);
        if (playlistDownloadFolderUri == null) return false;

        var root = DocumentFile.FromTreeUri(context, playlistDownloadFolderUri);
        if (root == null) return false;

        var playlistDir = root.FindFile(playlistName);
        if (playlistDir == null) return false;

        var songsFile = playlistDir.FindFile("songs.json");
        if (songsFile?.Uri == null) return false;

        foreach (var updatedSong in songsToBeUpdated)
        {
            var originalSong = songs.FirstOrDefault(s => s.Name.Equals(updatedSong.Name));
            if(originalSong == null) continue;
            
            originalSong.YoutubeUrl = updatedSong.YoutubeUrl;
            originalSong.DurationInSeconds = updatedSong.DurationInSeconds;
            originalSong.IsDownloaded = true;
        }

        try
        {
            using var stream = context.ContentResolver?.OpenOutputStream(songsFile.Uri, "rwt");
            if (stream == null) return false;

            using var writer = new StreamWriter(stream, Encoding.UTF8);
            var updatedJson = JsonSerializer.Serialize(songs);
            writer.Write(updatedJson);
            writer.Flush();

            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public static void DeletePlaylistFolder(string playlistDownloadFolderPath, string playlistName)
    {
        var context = Platform.CurrentActivity;
        if (context == null) return;
        
        var uriPath = ParseToUri(playlistDownloadFolderPath);
        if (uriPath == null) return;

        var root = DocumentFile.FromTreeUri(context, uriPath);
        if (root == null) return;

        var playlistDir = root.FindFile(playlistName);
        if (playlistDir == null) return;

        if (playlistDir.Uri != null && !playlistDir.Uri.Equals(root.Uri))
        {
            playlistDir.Delete();
        }
    }
    
    public static void DeleteSong(string playlistDownloadFolderPath, string playlistName, string songName, bool isDownloaded)
    {
        var context = Platform.CurrentActivity;
        if (context == null) return;
        
        var uriPath = ParseToUri(playlistDownloadFolderPath);
        if (uriPath == null) return;

        var root = DocumentFile.FromTreeUri(context, uriPath);
        if (root == null) return;

        var playlistDir = root.FindFile(playlistName);
        if (playlistDir == null) return;

        if (isDownloaded)
        {
            var songFile = root.FindFile(songName);
            songFile?.Delete();
        }

        var songs = ReadSongsFile(playlistDownloadFolderPath, playlistName);
        var songToBeDeleted = songs.FirstOrDefault(s => s.Name == songName);
        if(songToBeDeleted == null) return;
        
        songs.Remove(songToBeDeleted);
        
        var songsFile = playlistDir.FindFile("songs.json");
        if (songsFile?.Uri == null) return;
        
        songsFile.Delete();

        var jsonFile = playlistDir.CreateFile("application/json", "songs.json");
        if (jsonFile?.Uri == null) return;

        try
        {
            var json = JsonSerializer.Serialize(songs);

            using var newStream = context.ContentResolver?.OpenOutputStream(jsonFile.Uri);
            if (newStream == null) return;

            using var writer = new StreamWriter(newStream);
            writer.Write(json);
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    private static Uri? ParseToUri(string path)
    {
        return Uri.Parse(path);
    }
    
    public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (_tcs == null) return;

        if (requestCode == 1001 && resultCode == Result.Ok && data?.Data != null)
        {
            _tcs.SetResult(data.Data);
        }
        else
        {
            _tcs.SetResult(null);
        }

        _tcs = null;
    }
    
}