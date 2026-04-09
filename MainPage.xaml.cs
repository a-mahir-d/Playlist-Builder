using System.Collections.ObjectModel;
using Playlist_Builder.Helpers;
using Playlist_Builder.Models;

namespace Playlist_Builder;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Playlist> Playlists { get; set; }

    public MainPage()
    {
        InitializeComponent();
        Playlists = FileHelper.LoadPlaylists();
        this.BindingContext = this;
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }
    
    private void Refresh()
    {
        Playlists.Clear();
        var loaded = FileHelper.LoadPlaylists();
        foreach (var p in loaded)
            Playlists.Add(p);
    }

    private async void CreatePlaylistButton_Clicked(object sender, EventArgs e)
    {
        var navigationParameter = new Dictionary<string, object>
        {
            { "Playlists",  Playlists }
        };
        
        await Shell.Current.GoToAsync(nameof(CreatePlaylistPage), navigationParameter);
    }

    private async void OnPlaylistTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border { BindingContext: Playlist tappedPlaylist }) return;
        
        var navigationParameter = new Dictionary<string, object>
        {
            { "SelectedPlaylist", tappedPlaylist }
        };
        
        await Shell.Current.GoToAsync(nameof(PlaylistPage), navigationParameter);
    }
}