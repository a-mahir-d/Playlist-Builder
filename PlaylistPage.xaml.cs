using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playlist_Builder.Converters;
using Playlist_Builder.Helpers;
using Playlist_Builder.Models;

namespace Playlist_Builder;

[QueryProperty(nameof(SelectedPlaylist), "SelectedPlaylist")]
public partial class PlaylistPage : ContentPage
{
    private Playlist _playlist;
    public Playlist SelectedPlaylist
    {
        get => _playlist;
        set 
        { 
            _playlist = value; 
            OnPropertyChanged();
            LoadSongs();
        }
    }
    
    private List<Song> _songs;
    public Playlist Playlist => _playlist;
    public ObservableCollection<Song> FilteredSongs { get; set; }
    public List<Song> Songs { get; set; }
    public string PcIp = "";
    
    public PlaylistPage()
    {
        InitializeComponent();

        BindingContext = new
        {
            Page = this,
            Playlist = _playlist
        };
    }

    private void LoadSongs()
    {
        _songs = AndroidFileService.ReadSongsFile(_playlist.DownloadFolder, _playlist.Name);

        if (Songs == null || Songs.Count == 0)
        {
            Songs = [];
        }

        FilteredSongs = [.. Songs];
    }
    
    private void PlaylistSearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = e.NewTextValue?.ToLower() ?? string.Empty;

        FilteredSongs.Clear();

        var filtered = string.IsNullOrEmpty(searchText)
            ? Songs
            : Songs.Where(s => s.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) || s.Artist.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));

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
            
            if (Songs.Count > 0)
            {
                var existingSongNames = new HashSet<string>(Songs.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
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
            
            var artist = await DisplayPrompt("Yeni şarkı�", "Şarkıcı/Grup adını girin:");
            if (string.IsNullOrWhiteSpace(artist))
                return;
            
            var youtubeUrl = await DisplayPrompt("Yeni şarkı", "Şarkının YouTube URL'ini girin:");
            if (string.IsNullOrWhiteSpace(youtubeUrl))
                return;
            
            newSongs.Add(new Song { Name = songName, Artist = artist, YoutubeUrl = youtubeUrl, DurationInSeconds = 0 });
        }
        
        var success = AndroidFileService.AddSongsToPlaylist(newSongs, _playlist.DownloadFolder, _playlist.Name);
        if (!success)
        {
            await DisplayAlert("Hata", "Şarkılar eklenemedi.");
            return;
        }

        success = FileHelper.AddSongsToPlaylist(_playlist, 0, newSongs.Count);
        if (!success)
        {
            await DisplayAlert("Hata", "Playlist bilgisi güncellenemedi.");
            return;
        }


        Songs.AddRange(newSongs);
        RewriteFilteredSongs();
    }
    
    private async void OnPlaylistDeleteTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;

        var answer = await DisplayAlertWithAnswer("Uyarı", "Çalma listesi silinecektir. Onaylıyor musunuz?");
        if (!answer) return;

        AndroidFileService.DeletePlaylistFolder(_playlist.DownloadFolder, _playlist.Name);
        FileHelper.DeletePlaylist(_playlist.Name);
        ReturnToMainPage();
    }
    
    private async void OnSongDeleteTapped(object sender, TappedEventArgs e)
    {
        if (LoadingOverlay.IsVisible) return;

        if (sender is not Label label) return;

        if (label.BindingContext is not Song song) return;

        var answer = await DisplayAlertWithAnswer("Uyarı", $"{song.Name} silinecektir. Onaylıyor musunuz?");
        if(!answer) return;

        AndroidFileService.DeleteSong(_playlist.DownloadFolder, _playlist.Name, song.Name, song.IsDownloaded);
        
        FileHelper.RemoveSongFromPlaylist(_playlist, song.DurationInSeconds);
        PlaylistTotalHoursLabel.Text = new HourConverter().Convert(_playlist.TotalHours, typeof(string), null, CultureInfo.CurrentCulture).ToString() ?? string.Empty;
        Songs.Remove(song);
        RewriteFilteredSongs();
    }
    
    private async void RewriteFilteredSongs()
    {
        FilteredSongs.Clear();
        if (PlaylistSearchBar.Text == "")
        {
            FilteredSongs = [.._songs];
        }
        else
        {
            var searchText = PlaylistSearchBar.Text.ToLower();
            foreach (var s in Songs)
            {
                if (s.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) || s.Artist.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))
                {
                    FilteredSongs.Add(s);
                }
            }
        }
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