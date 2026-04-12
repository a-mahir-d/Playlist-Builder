using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Playlist_Builder.Converters;
using Playlist_Builder.Helpers;
using Playlist_Builder.Models;

namespace Playlist_Builder;

[QueryProperty(nameof(SelectedPlaylist), "SelectedPlaylist")]
public partial class PlaylistPage : ContentPage
{
    public Playlist Playlist  { get; set; }
    public Playlist SelectedPlaylist
    {
        get => Playlist;
        set 
        { 
            Playlist = value; 
            OnPropertyChanged();
            LoadSongs();
        }
    }
    
    private List<Song> _songs;
    public ObservableCollection<Song> FilteredSongs { get; set; }
    private string _backendIp = "";
    
    public PlaylistPage()
    {
        InitializeComponent();
        _songs = [];
        FilteredSongs = [];
        BindingContext = this;
    }

    private void LoadSongs()
    {
        if (Playlist == null) return;
        
        _songs = AndroidFileService.ReadSongsFile(Playlist.DownloadFolder, Playlist.Name);
        FilteredSongs.Clear();
        foreach (var song in _songs)
        {
            FilteredSongs.Add(song);
        }
        
        OnPropertyChanged(nameof(Playlist));
        OnPropertyChanged(nameof(SelectedPlaylist));
    }
    
    private void PlaylistSearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = e.NewTextValue?.ToLower() ?? string.Empty;

        FilteredSongs.Clear();

        var filtered = string.IsNullOrEmpty(searchText)
            ? _songs
            : _songs.Where(s => s.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) || s.Artist.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));

        foreach (var song in filtered)
            FilteredSongs.Add(song);
    }

    private void PlaylistSearchBar_Focused(object sender, FocusEventArgs e)
    {
        if (!LoadingOverlay.IsVisible) return;
        PlaylistSearchBar.Unfocus();
    }
    
    private async void OnSongAddTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;

        List<Song> newSongs = [];
        var answer = await DisplayAlertWithAnswer("Yeni şarkı", "Json dosyası ile ekleme yapmak ister misiniz?");
        if (answer)
        {
            var jsonSongs = await AndroidFileService.PickAndProcessJsonFile();
            if (jsonSongs.Count == 0) return;
            
            if (_songs.Count > 0)
            {
                var existingSongNames = new HashSet<string>(_songs.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var jsonSong in jsonSongs)
                {
                    if (existingSongNames.Contains(jsonSong.Name)) continue;
                    jsonSong.DurationInSeconds = 0;
                    newSongs.Add(jsonSong);
                }
            }
            else
            {
                foreach (var jsonSong in jsonSongs)
                {
                    jsonSong.DurationInSeconds = 0;
                    newSongs.Add(jsonSong);
                }
            }

            if (newSongs.Count == 0)
            {
                await DisplayAlert("Hata", "Bu şarkılar zaten listenizde bulunmakta");
                return;
            }
        }
        else
        {
            var songName = await DisplayPrompt("Yeni şarkı", "Şarkı adını girin:");
            if (string.IsNullOrWhiteSpace(songName))
                return;

            if (_songs.Select(s => s.Name).Contains(songName))
            {
                await DisplayAlert("Hata", "Bu şarkı zaten listenizde bulunmakta");
                return;
            }
            
            var artist = await DisplayPrompt("Yeni şarkı", "Şarkıcı/Grup adını girin:");
            if (string.IsNullOrWhiteSpace(artist))
                return;
            
            var youtubeUrl = await DisplayPrompt("Yeni şarkı", "Şarkının YouTube URL'ini girin:");
            if (string.IsNullOrWhiteSpace(youtubeUrl))
                return;
            
            newSongs.Add(new Song { Name = songName, Artist = artist, YoutubeUrl = youtubeUrl, DurationInSeconds = 0 });
        }
        
        var success = AndroidFileService.AddSongsToPlaylist(newSongs, Playlist.DownloadFolder, Playlist.Name);
        if (!success)
        {
            await DisplayAlert("Hata", "Şarkılar eklenemedi.");
            return;
        }

        success = FileHelper.AddSongsToPlaylist(Playlist, 0, newSongs.Count);
        if (!success)
        {
            await DisplayAlert("Hata", "Playlist bilgisi güncellenemedi.");
            return;
        }


        _songs.AddRange(newSongs);
        RewriteFilteredSongs();
    }
    
    private async void OnPlaylistDeleteTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;

        var answer = await DisplayAlertWithAnswer("Uyarı", "Çalma listesi silinecektir. Onaylıyor musunuz?");
        if (!answer) return;

        AndroidFileService.DeletePlaylistFolder(Playlist.DownloadFolder, Playlist.Name);
        FileHelper.DeletePlaylist(Playlist.Name);
        ReturnToMainPage();
    }
    
    private async void OnSongDeleteTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;

        if (sender is not Label label) return;

        if (label.BindingContext is not Song song) return;

        var answer = await DisplayAlertWithAnswer("Uyarı", $"{song.Name} silinecektir. Onaylıyor musunuz?");
        if(!answer) return;

        AndroidFileService.DeleteSong(Playlist.DownloadFolder, Playlist.Name, song.Name, song.IsDownloaded);
        
        FileHelper.RemoveSongFromPlaylist(Playlist, song.DurationInSeconds);
        PlaylistTotalHoursLabel.Text = new HourConverter().Convert(Playlist.TotalHours, typeof(string), null, CultureInfo.CurrentCulture).ToString() ?? string.Empty;
        _songs.Remove(song);
        RewriteFilteredSongs();
    }
    
    private async void OnSongDownloadTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;
        if (sender is not Border border) return;
        if (border.BindingContext is not Song song) return;
        if (song.IsDownloaded) return;
        
        List<Song> songsToBeDownloaded = [song];
        DownloadSongs(songsToBeDownloaded);
    }
    
    private async void OnDownloadAllTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;
        
        var songsToBeDownloaded = _songs.Select(s => s).Where(s=> !s.IsDownloaded).ToList();
        if (songsToBeDownloaded.Count == 0)
        {
            await DisplayAlert("Bilgi", "Listenizdeki tüm şarkılar indirilmiş.");
            return;
        }
        DownloadSongs(songsToBeDownloaded);
    }

    private async void DownloadSongs(List<Song> songs)
    {
        if(songs.Count == 0) return;
        
        LoadingOverlay.IsVisible = true;
        try 
        {
            var notDownloadedSongsInfo = "";
            var durationInSecondsCounter = 0;
            var songsCounter = 0;
            ProgressLabel.Text = $"İlerleme: 0/{songs.Count}";
            ErrorLabel.Text = "Hata: 0";
            
            List<(Song song, string reason)> notDownloadedSongs = [];
            foreach (var song in songs)
            {
                if (_backendIp == "")
                {
                    _backendIp = await DisplayPrompt("İndirme", "8000 portunda backend servisini çalıştıran cihazınızın IPv4 adresini giriniz:");
                    if (string.IsNullOrWhiteSpace(_backendIp))
                    {
                        _backendIp = "";
                        notDownloadedSongs.Add((song, "Ip bilgisi boş olamaz"));
                        ErrorLabel.Text = $"{notDownloadedSongs.Count}";
                        continue;
                    }
                }
                
                var(opusBytes, durationInSeconds, errorMessage) = await DownloadHelper.DownloadOpusAsync(_backendIp, song.YoutubeUrl);
                if (opusBytes.Length == 0 || durationInSeconds == 0)
                {
                    notDownloadedSongs.Add((song, errorMessage));
                    ErrorLabel.Text = $"{notDownloadedSongs.Count}";
                    continue;
                }
                
                song.DurationInSeconds = durationInSeconds;
                (var success, errorMessage) = AndroidFileService.SaveOpus(opusBytes, song, Playlist);
                if (!success)
                {
                    LoadingOverlay.IsVisible = false;
                    await DisplayAlert("Hata", errorMessage);
                    return;
                }
                
                durationInSecondsCounter += song.DurationInSeconds;
                songsCounter++;
                ProgressLabel.Text = $"İlerleme: {songsCounter}/{songs.Count}";
            }
            
            Playlist.TotalHours += durationInSecondsCounter / 3600.0;
            var downloadReport = $"İndirme başarısı: {songsCounter}/{songs.Count}";
            
            if (notDownloadedSongs.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("İndirilemeyen Şarkılar");
                sb.AppendLine("----------------------------");
                foreach (var (song, reason) in notDownloadedSongs)
                {
                    sb.AppendLine($"{song.Name}_{song.Artist} - {reason}");
                }

                notDownloadedSongsInfo = sb.ToString();
            }
            
            var successfulSongs = songs.Where(s => notDownloadedSongs.All(nds => nds.song.Name != s.Name)).ToList();
            foreach (var sSong in successfulSongs)
            {
                var index = _songs.FindIndex(s => s.Name == sSong.Name && s.Artist == sSong.Artist);
                if (index == -1) continue;
                _songs[index].IsDownloaded = true;
                _songs[index].DurationInSeconds = sSong.DurationInSeconds;
            }
            
            var successStatus = AndroidFileService.UpdateNewlyDownloadedSongs(_songs, songs, Playlist.DownloadFolder, Playlist.Name);
            if (!successStatus)
            {
                LoadingOverlay.IsVisible = false;
                await DisplayAlert("Hata", "songs.json güncellenemedi");
                return;
            }
            
            FileHelper.UpdatePlaylist(Playlist);
            
            PlaylistTotalHoursLabel.Text = new HourConverter().Convert(Playlist.TotalHours, typeof(string), null, CultureInfo.CurrentCulture).ToString() ?? string.Empty;
            RewriteFilteredSongs();

            LoadingOverlay.IsVisible = false;
            
            if (downloadReport != "")
            {
                await DisplayAlert("İndirme Raporu", downloadReport);
            }

            if (notDownloadedSongsInfo == "") return;
            
            var answer = await DisplayAlertWithAnswer("Eksik İndirmeler", "İndirilemeyen şarkıların listesini .txt dosyası olarak almak ister misiniz?");
            if (!answer) return;
                
            var path = Path.Combine(FileSystem.CacheDirectory, $"FailedDownloads_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, notDownloadedSongsInfo);

            var context = Platform.CurrentActivity;
            if(context == null) return;
            
            try
            {
                successStatus = AndroidFileService.SaveToDownloads(context, path, "Indirilemeyen_Sarkilar.txt" );

                if (successStatus)
                {
                    await DisplayAlert("Bilgi", $"Dosya indirilenler klas�r�ne kaydedildi");
                }
                else
                {
                    await HandleDownloadTxtFail(notDownloadedSongsInfo);
                }

            }
            catch
            {
                await HandleDownloadTxtFail(notDownloadedSongsInfo);
            }
        }
        catch (Exception ex)
        {
            LoadingOverlay.IsVisible = false;
            await DisplayAlert("Hata", ex.Message);
        }
    }
    
    private void RewriteFilteredSongs()
    {
        if (_songs == null) return;
        var searchText = PlaylistSearchBar?.Text?.ToLower() ?? string.Empty;
    
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SongsCollectionView.ItemsSource = null; 

            FilteredSongs.Clear();
            var filtered = string.IsNullOrWhiteSpace(searchText)
                ? _songs
                : _songs.Where(s => (s.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) || 
                                    (s.Artist?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));

            foreach (var s in filtered)
                FilteredSongs.Add(s);
            
            SongsCollectionView.ItemsSource = FilteredSongs;
        });
    }
    
    private async Task HandleDownloadTxtFail(string content)
    {
        await Clipboard.Default.SetTextAsync(content);

        await DisplayAlert("Dosya indirmesi olu�turulamad�", "İndirilemeyen şarkıların listesi clipboard'a kopyalandı."
        );
    }
    
    private async Task DisplayAlert(string title, string message)
    {
        await DisplayAlertAsync(title, message, "Tamam");
    }
    
    private async Task<bool> DisplayAlertWithAnswer(string title, string message)
    {
        return await DisplayAlertAsync(title, message, "Evet", "Hayır");
    }
    
    private async Task<string> DisplayPrompt(string title, string message)
    {
        return await DisplayPromptAsync(title, message);
    }
    
    private async void ReturnToMainPage()
    {
        await Shell.Current.GoToAsync("..");
    }
}