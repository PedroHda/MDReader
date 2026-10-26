using System.Diagnostics;
using System.Text.Json;
using MDReader.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MDReader.Rendering;

public sealed class WebPreviewSession(WebView2 browser) : IDisposable
{
    private readonly string _cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDReader", "cache", Environment.ProcessId.ToString());
    private string? _directory;
    private ReaderSettings _settings = new();
    private double _position;
    private int _revision;
    public bool Ready { get; private set; }
    public event Action<double>? PositionChanged;
    public event Action<int, int>? SearchChanged;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_cache);
        var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDReader", "WebView2");
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
        await browser.EnsureCoreWebView2Async(environment);
        var core = browser.CoreWebView2;
        core.Settings.IsScriptEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.SetVirtualHostNameToFolderMapping("reader.mdreader", _cache, CoreWebView2HostResourceAccessKind.DenyCors);
        core.AddWebResourceRequestedFilter("https://files.mdreader/*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, e) =>
        {
            if (_directory is null || !Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)
                || e.ResourceContext != CoreWebView2WebResourceContext.Image
                || LocalResourcePath.ResolveImage(_directory, uri.AbsolutePath) is null)
            {
                e.Response = core.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
            }
        };
        core.NavigationStarting += (_, e) =>
        {
            if (e.Uri == "about:blank") return;
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)
                && uri.Scheme == "https" && uri.Host == "reader.mdreader" && uri.AbsolutePath == "/index.html")
            {
                if (uri.Fragment.Length > 1)
                {
                    e.Cancel = true;
                    _ = GoToAsync(Uri.UnescapeDataString(uri.Fragment[1..]));
                }
                return;
            }
            e.Cancel = true;
            if (e.IsUserInitiated) OpenExternal(e.Uri);
        };
        core.NewWindowRequested += (_, e) => { e.Handled = true; if (e.IsUserInitiated) OpenExternal(e.Uri); };
        core.WebMessageReceived += (_, e) =>
        {
            if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source) || source.Host != "reader.mdreader") return;
            try
            {
                using var message = JsonDocument.Parse(e.WebMessageAsJson);
                var root = message.RootElement;
                if (root.GetProperty("type").GetString() == "position") PositionChanged?.Invoke(ReaderStateStore.ClampPosition(root.GetProperty("value").GetDouble()));
                else if (root.GetProperty("type").GetString() == "search") SearchChanged?.Invoke(root.GetProperty("index").GetInt32(), root.GetProperty("count").GetInt32());
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
        };
        core.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess || browser.IsDisposed) return;
            try
            {
                await core.ExecuteScriptAsync(ReaderAssets.Script);
                await ApplyAsync(_settings);
                await ExecuteAsync("restore", _position);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        };
        Ready = true;
    }

    public async Task ShowAsync(string html, string directory, ReaderSettings settings, double position)
    {
        if (!Ready) return;
        _directory = directory;
        _settings = settings;
        _position = ReaderStateStore.ClampPosition(position);
        browser.CoreWebView2.SetVirtualHostNameToFolderMapping("files.mdreader", directory, CoreWebView2HostResourceAccessKind.DenyCors);
        await File.WriteAllTextAsync(Path.Combine(_cache, "index.html"), html);
        browser.CoreWebView2.Navigate("https://reader.mdreader/index.html?revision=" + ++_revision);
    }

    public async Task ApplyAsync(ReaderSettings settings)
    {
        _settings = settings;
        if (!Ready || browser.IsDisposed) return;
        browser.ZoomFactor = settings.ZoomPercent / 100d;
        await ExecuteAsync("apply", new { darkMode = settings.DarkMode, fontSize = settings.FontSize, contentWidth = settings.ContentWidth });
    }

    public Task SearchAsync(string query, int direction) => ExecuteAsync("search", query, direction);
    public Task GoToAsync(string id) => ExecuteAsync("goTo", id);

    private async Task ExecuteAsync(string method, params object[] args)
    {
        if (!Ready || browser.IsDisposed) return;
        var arguments = string.Join(",", args.Select(a => JsonSerializer.Serialize(a)));
        await browser.CoreWebView2.ExecuteScriptAsync($"window.mdreader && window.mdreader.{method}({arguments})");
    }

    private static void OpenExternal(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        try { File.Delete(Path.Combine(_cache, "index.html")); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
