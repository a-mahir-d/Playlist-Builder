using System.Collections.ObjectModel;
using Playlist_Builder.Helpers;
using Playlist_Builder.Models;

namespace Playlist_Builder;

[QueryProperty(nameof(IncomingPlaylists), "Playlists")]
public partial class CreatePlaylistPage : ContentPage
{
    private ObservableCollection<Playlist> _playlists;
    public ObservableCollection<Playlist> IncomingPlaylists
    {
        get => _playlists;
        set 
        { 
            _playlists = value; 
            OnPropertyChanged();
        }
    }
    
    public CreatePlaylistPage()
    {
        InitializeComponent();
    }
    
    private async void PickFolderButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var folderPath = await AndroidFileService.PickFolderAsync();
            if (folderPath == null) return;
            var success = await AndroidFileService.GetPersistablePerm(folderPath);
            if (success) DownloadFolderEntry.Text = folderPath.ToString()!;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Hata", ex.Message);
        }
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        var name = PlaylistNameEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name) || IconPicker.SelectedIndex < 0)
        {
            await DisplayAlert("Hata", "İsim ve icon seçin.");
            return;
        }

        if (_playlists.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("Hata", "Bu isimde bir çalma listesi zaten var.");
            return;
        }
        
        var downloadFolder = DownloadFolderEntry.Text;
        if (string.IsNullOrWhiteSpace(downloadFolder))
        {
            await DisplayAlert("Hata", "İndirme klasörü boş olamaz.");
            return;
        }

        var newPlaylist = new Playlist
        {
            Name = name,
            Icon = IconPicker.SelectedItem.ToString() ?? "🎵",
            TotalSongs = 0,
            TotalHours = 0,
            DownloadFolder = downloadFolder
        };
        
        var (success, message) = AndroidFileService.CreatePlaylistFolder(downloadFolder, newPlaylist.Name);
        if (!success)
        {
            await DisplayAlert("Hata", message);
            return;
        }

        if (message != "")
        {
            var part = message.Split("-->")[1].Trim();
            var pieces = part.Split(',');
            var totalSongs = int.Parse(pieces[0]);
            var totalDurationInSeconds = int.Parse(pieces[1]);

            newPlaylist.TotalSongs = totalSongs;
            newPlaylist.TotalHours = totalDurationInSeconds / 3600.0;
        }

        _playlists.Add(newPlaylist);
        FileHelper.SavePlaylists(_playlists);

        ReturnToMainPage();
    }

    private async void CancelButton_Clicked(object sender, EventArgs e)
    {
        ReturnToMainPage();
    }

    private async void ReturnToMainPage()
    {
        KeyboardHelper.HideKeyboard();
        await Shell.Current.GoToAsync("..");
    }

    private async Task DisplayAlert(string title, string message)
    {
        if (this.Parent is ContentPage page)
        {
            await page.DisplayAlertAsync(title, message, "Tamam");
        }
    }
}