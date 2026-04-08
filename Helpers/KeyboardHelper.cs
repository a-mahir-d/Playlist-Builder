using Android.Views.InputMethods;

namespace Playlist_Builder.Helpers;

public static class KeyboardHelper
{
    public static void HideKeyboard()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
            return;


        if (activity.GetSystemService(Android.Content.Context.InputMethodService) is not InputMethodManager inputMethodManager)
            return;

        var token = activity.CurrentFocus?.WindowToken;

        if (token != null)
        {
            inputMethodManager.HideSoftInputFromWindow(token, HideSoftInputFlags.None);
        }
    }
}