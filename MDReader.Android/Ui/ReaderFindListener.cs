using Android.Webkit;

namespace MDReader.Mobile.Ui;

public sealed class ReaderFindListener(Action<int, int> completed) : Java.Lang.Object, WebView.IFindListener
{
    public void OnFindResultReceived(int activeMatchOrdinal, int numberOfMatches, bool isDoneCounting)
    {
        if (isDoneCounting) completed(numberOfMatches == 0 ? 0 : activeMatchOrdinal + 1, numberOfMatches);
    }
}
