using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playlist_Builder.Models;

namespace Playlist_Builder;

public partial class PlaylistPage : ContentPage
{
    private readonly Playlist _playlist;
    private List<Song> _songs;
    
    
    public Playlist Playlist => _playlist;
    public ObservableCollection<Song> FilteredSongs { get; set; }
    public List<Song> Songs { get; set; }
    public string PcIp = "";
    
    public PlaylistPage(Playlist playlist)
    {
        InitializeComponent();
        
        _playlist = playlist;

        _songs = AndroidFileService.ReadSongsFile(_playlist.DownloadFolder, _playlist.Name);

        if (Songs == null || Songs.Count == 0)
        {
            Songs = [];
        }

        FilteredSongs = [.. Songs];

        BindingContext = new
        {
            Page = this,
            Playlist = _playlist
        };
    }
}