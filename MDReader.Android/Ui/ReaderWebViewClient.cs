using Android.App;
using Android.Content;
using Android.Webkit;
using Android.Widget;
using MDReader.Core;
using MDReader.Mobile.Services;
using AndroidUri = Android.Net.Uri;

namespace MDReader.Mobile.Ui;

public sealed class ReaderWebViewClient(Activity activity, Action pageLoaded, Func<(string? Tree, string? Folder)> context) : WebViewClient
{
    public override void OnPageFinished(WebView? view, string? url)
    {
        base.OnPageFinished(view, url);
        pageLoaded();
    }

    public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
    {
        var uri = request?.Url;
        if (uri?.Host != "files.mdreader") return base.ShouldInterceptRequest(view, request);
        var (tree, folder) = context();
        try
        {
            if (tree is not null && folder is not null && AndroidUri.Parse(tree) is { } treeUri)
            {
                var stream = new DocumentFolderService(activity.ContentResolver!).OpenImage(treeUri, folder, uri.Path ?? "");
                if (stream is not null)
                    return new WebResourceResponse(LocalResourcePath.MimeType(uri.Path ?? ""), null, 200, "OK",
                        new Dictionary<string, string> { ["X-Content-Type-Options"] = "nosniff" }, stream);
            }
        }
        catch (Exception ex) when (ex is IOException or Java.IO.IOException or Java.Lang.SecurityException or ArgumentException) { }
        return new WebResourceResponse("text/plain", "utf-8", 404, "Not Found", null, new MemoryStream());
    }

    public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
    {
        var uri = request?.Url;
        if (uri is null) return true;
        if (uri.ToString() == "about:blank") return false;
        if (uri.Scheme == "https" && uri.Host == "reader.mdreader" && uri.Path == "/index.html" && !string.IsNullOrEmpty(uri.Fragment)) return false;
        if ((uri.Scheme is "https" or "http") && uri.Host is not ("files.mdreader" or "reader.mdreader"))
        {
            try { activity.StartActivity(new Intent(Intent.ActionView, uri)); }
            catch (ActivityNotFoundException) { Toast.MakeText(activity, "Nenhum navegador disponível.", ToastLength.Long)?.Show(); }
        }
        return true;
    }
}
