using Android.App;
using Android.Content;
using Android.Webkit;

namespace MDReader.Mobile.Ui;

public sealed class ReaderWebViewClient(Activity activity) : WebViewClient
{
    public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
    {
        var uri = request?.Url;
        if (uri is null)
        {
            return true;
        }

        if (uri.ToString() == "about:blank")
        {
            return false;
        }

        if (uri.Host == "mdreader.invalid")
        {
            if (uri.Path == "/" && !string.IsNullOrEmpty(uri.Fragment))
            {
                return false;
            }

            Toast.MakeText(activity, "Selecione o documento pelo botão Abrir arquivo.", ToastLength.Long)?.Show();
            return true;
        }

        if (uri.Scheme is "https" or "http")
        {
            try
            {
                activity.StartActivity(new Intent(Intent.ActionView, uri));
            }
            catch (ActivityNotFoundException)
            {
                Toast.MakeText(activity, "Nenhum navegador disponível para abrir o link.", ToastLength.Long)?.Show();
            }
        }

        return true;
    }
}
