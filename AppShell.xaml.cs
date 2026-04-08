namespace Playlist_Builder;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        Routing.RegisterRoute("CreatePlaylistPage", typeof(CreatePlaylistPage));
        Routing.RegisterRoute("PlaylistPage", typeof(PlaylistPage));
    }
}