using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
}