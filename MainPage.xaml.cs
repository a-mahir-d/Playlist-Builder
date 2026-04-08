using System.Collections.ObjectModel;
using Playlist_Builder.Helpers;
using Playlist_Builder.Models;

namespace Playlist_Builder;

public partial class MainPage : ContentPage
{
    private ObservableCollection<Playlist> Playlists { get; set; }

    public MainPage()
    {
        InitializeComponent();
        Playlists = FileHelper.LoadPlaylists();
        this.BindingContext = this;
    }

    private async void CreatePlaylistButton_Clicked(object sender, EventArgs e)
    {
        var page = new CreatePlaylistPage(Playlists);
        var mainPage = new MainPage();
        // route to page, MainPage should be returnable
    }

    private async void OnPlaylistTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border { BindingContext: Playlist tappedPlaylist }) return;
        
        var page = new PlaylistPage(tappedPlaylist);
        var mainPage = new MainPage();
        // route to page, MainPage should be returnable
    }

    private async Task DisplayAlert(string title, string message)
    {
        if (this.Parent is ContentPage page)
        {
            await page.DisplayAlertAsync(title, message, "Tamam");
        }
    }
}