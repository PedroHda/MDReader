using Android.Views;

namespace MDReader.Mobile.Ui;

public sealed class SystemBarInsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
{
    public WindowInsets OnApplyWindowInsets(View? view, WindowInsets? insets)
    {
        if (insets is null)
        {
            return null!;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            if (bars is not null)
            {
                view?.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }
        }
        else
        {
            view?.SetPadding(insets.SystemWindowInsetLeft, insets.SystemWindowInsetTop,
                insets.SystemWindowInsetRight, insets.SystemWindowInsetBottom);
        }

        return insets;
    }
}
