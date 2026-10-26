using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Database;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Views.InputMethods;
using Android.Webkit;
using MDReader.Core;
using MDReader.Mobile.Services;
using MDReader.Mobile.Ui;
using AndroidUri = Android.Net.Uri;
using Path = System.IO.Path;
using Orientation = Android.Widget.Orientation;

namespace MDReader.Mobile;

[Activity(Name = "com.pedrohda.mdreader.MainActivity", Label = "@string/app_name", MainLauncher = true,
    Exported = true, Theme = "@style/AppTheme", LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
[IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault },
    DataSchemes = new[] { "content" }, DataMimeTypes = new[] { "text/markdown", "text/x-markdown", "text/plain", "application/octet-stream" })]
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault },
    DataMimeTypes = new[] { "text/markdown", "text/x-markdown", "text/plain", "application/octet-stream" })]
public sealed class MainActivity : Activity
{
    private const int OpenDocumentRequest = 100, OpenFolderRequest = 101;
    private readonly CancellationTokenSource _lifetime = new();
    private ReaderStateStore _store = null!;
    private AndroidMarkdownFileReader _reader = null!;
    private DocumentFolderService _folders = null!;
    private Button _open = null!;
    private ImageButton _reload = null!;
    private TextView _fileName = null!, _status = null!, _rawText = null!, _searchCount = null!;
    private EditText _query = null!;
    private View _empty = null!;
    private ScrollView _rawScroll = null!;
    private RadioGroup _tabs = null!;
    private ProgressBar _loading = null!;
    private WebView _preview = null!;
    private AndroidUri? _currentUri;
    private RecentDocument? _entry;
    private OpenedMarkdownDocument? _document;
    private bool _busy, _resumed, _destroyed;
    private (AndroidUri Uri, ActivityFlags? Flags, string? Tree, string? Folder)? _pending;
    private System.Threading.Timer? _searchTimer, _reloadTimer, _saveTimer;
    private ContentObserver? _observer;
    private int _rawMatch = -1;
    private ReaderSettings Settings => _store.State.Settings;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _store = new ReaderStateStore(Path.Combine(FilesDir!.AbsolutePath, "reader-state.json"));
        _reader = new AndroidMarkdownFileReader(ContentResolver!);
        _folders = new DocumentFolderService(ContentResolver!);
        SetContentView(Resource.Layout.main);
        _open = FindViewById<Button>(Resource.Id.open_file)!;
        _reload = FindViewById<ImageButton>(Resource.Id.reload_file)!;
        _fileName = FindViewById<TextView>(Resource.Id.file_name)!;
        _status = FindViewById<TextView>(Resource.Id.status)!;
        _rawText = FindViewById<TextView>(Resource.Id.raw_content)!;
        _searchCount = FindViewById<TextView>(Resource.Id.search_count)!;
        _query = FindViewById<EditText>(Resource.Id.search_query)!;
        _empty = FindViewById(Resource.Id.empty_state)!;
        _rawScroll = FindViewById<ScrollView>(Resource.Id.raw_scroll)!;
        _tabs = FindViewById<RadioGroup>(Resource.Id.view_tabs)!;
        _loading = FindViewById<ProgressBar>(Resource.Id.loading)!;
        _preview = FindViewById<WebView>(Resource.Id.preview_browser)!;
        FindViewById(Resource.Id.root)!.SetOnApplyWindowInsetsListener(new SystemBarInsetsListener());
        _preview.Settings.JavaScriptEnabled = false;
        _preview.Settings.AllowFileAccess = false;
        _preview.Settings.AllowContentAccess = false;
        _preview.Settings.DomStorageEnabled = false;
        _preview.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
        _preview.SetWebViewClient(new ReaderWebViewClient(this, RestorePosition, () => (_entry?.TreeUri, _entry?.FolderId)));
        _preview.SetFindListener(new ReaderFindListener((index, count) =>
        {
            if (_tabs.CheckedRadioButtonId == Resource.Id.preview_tab)
                _searchCount.Text = string.IsNullOrEmpty(_query.Text) ? "" : $"{index}/{count}";
        }));
        _open.Click += (_, _) => SelectFile();
        _reload.Click += async (_, _) => await ReloadAsync();
        FindViewById<ImageButton>(Resource.Id.more_options)!.Click += (_, _) => ShowMenu();
        FindViewById<ImageButton>(Resource.Id.search_previous)!.Click += (_, _) => FindNext(false);
        FindViewById<ImageButton>(Resource.Id.search_next)!.Click += (_, _) => FindNext(true);
        _query.TextChanged += (_, _) =>
        {
            _searchTimer?.Dispose();
            _searchTimer = new System.Threading.Timer(_ => RunOnUiThread(() => Search()), null, 250, Timeout.Infinite);
        };
        _query.EditorAction += (_, e) => { if (e.ActionId == ImeAction.Search) { FindNext(true); e.Handled = true; } };
        _tabs.CheckedChange += (_, _) => { UpdateTab(); Search(); };
        _preview.ScrollChange += (_, _) => ScheduleSave();
        _rawScroll.ScrollChange += (_, _) => ScheduleSave();
        ApplyAppearance();
        if (savedInstanceState?.GetBoolean("raw-selected") == true) _tabs.Check(Resource.Id.raw_tab);
        if (!HandleIntent(Intent))
        {
            var previous = savedInstanceState?.GetString("document-uri") ?? _store.State.LastDocument
                ?? GetPreferences(FileCreationMode.Private)?.GetString("document-uri", null);
            if (previous is not null && AndroidUri.Parse(previous) is { } uri)
            {
                var history = _store.Find(previous);
                _ = LoadAsync(uri, tree: history?.TreeUri, folder: history?.FolderId);
            }
        }
    }

    private void SelectFile()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        try { StartActivityForResult(intent, OpenDocumentRequest); }
        catch (ActivityNotFoundException) { ShowStatus("Nenhum seletor de arquivos disponível.", true); }
    }

    private void SelectFolder()
    {
        var intent = new Intent(Intent.ActionOpenDocumentTree);
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        try { StartActivityForResult(intent, OpenFolderRequest); }
        catch (ActivityNotFoundException) { ShowStatus("Nenhum seletor de pastas disponível.", true); }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data?.Data is not { } uri) return;
        if (requestCode == OpenDocumentRequest) _ = LoadAsync(uri, data.Flags);
        else if (requestCode == OpenFolderRequest)
        {
            PersistPermission(uri, data.Flags);
            Settings.DocumentTreeUri = uri.ToString();
            _store.Save();
            _ = BrowseFolderAsync(uri, DocumentsContract.GetTreeDocumentId(uri)!);
        }
    }

    private async Task BrowseFolderAsync(AndroidUri tree, string folderId)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var entries = (await Task.Run(() => _folders.List(tree, folderId)))
                .Where(e => e.IsDirectory || MarkdownContentReader.HasMarkdownExtension(e.Name)).ToList();
            if (_destroyed) return;
            if (entries.Count == 0) { ShowStatus("Nenhum Markdown ou subpasta encontrado."); return; }
            new AlertDialog.Builder(this)!.SetTitle("Documentos da pasta")!
                .SetItems(entries.Select(e => e.Name + (e.IsDirectory ? "/" : "")).ToArray(), async (_, args) =>
                {
                    var chosen = entries[args.Which];
                    if (chosen.IsDirectory) await BrowseFolderAsync(tree, chosen.Id);
                    else await LoadAsync(chosen.Uri, tree: tree.ToString(), folder: folderId);
                })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
        }
        catch (Exception ex) { ShowStatus(FriendlyMessage(ex), true); }
        finally { if (!_destroyed) SetBusy(false); }
    }

    private async Task LoadAsync(AndroidUri uri, ActivityFlags? flags = null, string? tree = null, string? folder = null)
    {
        if (_destroyed) return;
        if (_busy) { _pending = (uri, flags, tree, folder); return; }
        SavePosition();
        SetBusy(true);
        ShowStatus("Carregando...");
        try
        {
            var token = _lifetime.Token;
            var document = await Task.Run(() => _reader.ReadAsync(uri, token, Settings), token);
            if (_destroyed || token.IsCancellationRequested) return;
            _document = document;
            _currentUri = uri;
            _entry = _store.Open(uri.ToString()!, document.FileName);
            _entry.TreeUri = tree;
            _entry.FolderId = folder;
            if (flags is { } granted) PersistPermission(uri, granted);
            _store.Save();
            _rawText.Text = document.Content;
            _fileName.Text = document.FileName + (_entry.Favorite ? " ★" : "");
            _preview.LoadDataWithBaseURL("https://reader.mdreader/index.html", document.Html, "text/html", "UTF-8", null);
            _empty.Visibility = ViewStates.Gone;
            _rawMatch = -1;
            UpdateTab();
            Observe(uri);
            ShowStatus(string.IsNullOrWhiteSpace(document.Content) ? "Arquivo carregado, mas ele está vazio." : "Documento carregado.");
        }
        catch (System.OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!_destroyed) ShowStatus(FriendlyMessage(ex), true); }
        finally
        {
            if (!_destroyed)
            {
                SetBusy(false);
                if (_pending is { } pending)
                {
                    _pending = null;
                    _ = LoadAsync(pending.Uri, pending.Flags, pending.Tree, pending.Folder);
                }
            }
        }
    }

    private Task ReloadAsync() => _currentUri is null ? Task.CompletedTask : LoadAsync(_currentUri, tree: _entry?.TreeUri, folder: _entry?.FolderId);

    private void PersistPermission(AndroidUri uri, ActivityFlags flags)
    {
        if (!flags.HasFlag(ActivityFlags.GrantPersistableUriPermission)) return;
        try { ContentResolver!.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission); }
        catch (Java.Lang.SecurityException) { }
    }

    private void Observe(AndroidUri uri)
    {
        if (_observer is not null) { ContentResolver!.UnregisterContentObserver(_observer); _observer.Dispose(); }
        _observer = new DocumentObserver(new Handler(Looper.MainLooper!), () =>
        {
            if (!Settings.AutoReload || _destroyed) return;
            _reloadTimer?.Dispose();
            _reloadTimer = new System.Threading.Timer(_ => RunOnUiThread(async () => { if (!_destroyed) await ReloadAsync(); }), null, 700, Timeout.Infinite);
        });
        try { ContentResolver!.RegisterContentObserver(uri, false, _observer); }
        catch (Java.Lang.SecurityException) { }
    }

    private void ShowMenu()
    {
        var menu = new PopupMenu(this, FindViewById(Resource.Id.more_options));
        menu.Menu!.Add(0, 1, 0, "Abrir pasta");
        menu.Menu!.Add(0, 2, 1, "Recentes e favoritos");
        menu.Menu!.Add(0, 3, 2, _entry?.Favorite == true ? "Remover dos favoritos" : "Favoritar")!.SetEnabled(_entry is not null);
        menu.Menu!.Add(0, 4, 3, "Sumário")!.SetEnabled(_document?.Headings.Count > 0);
        menu.Menu!.Add(0, 5, 4, "Aparência");
        menu.Menu!.Add(0, 6, 5, "Voltar ao topo")!.SetEnabled(_document is not null);
        menu.MenuItemClick += (_, args) =>
        {
            switch (args.Item!.ItemId)
            {
                case 1: SelectFolder(); break;
                case 2: ShowHistory(); break;
                case 3:
                    _entry!.Favorite = !_entry.Favorite; _store.Save();
                    _fileName.Text = _document!.FileName + (_entry.Favorite ? " ★" : ""); break;
                case 4: ShowContents(); break;
                case 5: ShowAppearance(); break;
                case 6: _preview.ScrollTo(0, 0); _rawScroll.ScrollTo(0, 0); break;
            }
            args.Handled = true;
        };
        menu.Show();
    }

    private void ShowHistory()
    {
        var entries = _store.Recent.ToArray();
        if (entries.Length == 0) { ShowStatus("Nenhum documento recente."); return; }
        new AlertDialog.Builder(this)!.SetTitle("Recentes e favoritos")!
            .SetItems(entries.Select(e => e.ToString()).ToArray(), async (_, args) =>
            {
                var selected = entries[args.Which];
                if (AndroidUri.Parse(selected.Location) is { } uri) await LoadAsync(uri, tree: selected.TreeUri, folder: selected.FolderId);
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }

    private void ShowContents()
    {
        var headings = _document?.Headings.ToArray() ?? Array.Empty<HeadingInfo>();
        new AlertDialog.Builder(this)!.SetTitle("Sumário")!.SetItems(headings.Select(h => h.ToString()).ToArray(), (_, args) =>
        {
            _tabs.Check(Resource.Id.preview_tab);
            _preview.LoadUrl("https://reader.mdreader/index.html#" + AndroidUri.Encode(headings[args.Which].Id));
        })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }

    private void ShowAppearance()
    {
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var padding = (int)(20 * Resources!.DisplayMetrics!.Density);
        layout.SetPadding(padding, padding / 2, padding, padding / 2);
        var dark = new Switch(this) { Text = "Modo escuro", Checked = Settings.DarkMode };
        var auto = new Switch(this) { Text = "Atualização automática", Checked = Settings.AutoReload };
        var fontLabel = new TextView(this) { Text = "Fonte: " + Settings.FontSize };
        var font = new SeekBar(this) { Max = 20, Progress = Settings.FontSize - 12 };
        font.ProgressChanged += (_, e) => fontLabel.Text = "Fonte: " + (e.Progress + 12);
        var zoomLabel = new TextView(this) { Text = "Zoom: " + Settings.ZoomPercent + "%" };
        var zoom = new SeekBar(this) { Max = 5, Progress = (Settings.ZoomPercent - 75) / 25 };
        zoom.ProgressChanged += (_, e) => zoomLabel.Text = "Zoom: " + (75 + e.Progress * 25) + "%";
        var width = new Spinner(this);
        width.Adapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerDropDownItem, new[] { "Largura confortável", "Largura ampla", "Largura completa" });
        width.SetSelection(Settings.ContentWidth switch { 1100 => 1, 0 => 2, _ => 0 });
        foreach (var view in new View[] { dark, auto, fontLabel, font, zoomLabel, zoom, width }) layout.AddView(view);
        new AlertDialog.Builder(this)!.SetTitle("Aparência")!.SetView(layout)!
            .SetPositiveButton("Aplicar", async (_, _) =>
            {
                SavePosition();
                Settings.DarkMode = dark.Checked; Settings.AutoReload = auto.Checked;
                Settings.FontSize = font.Progress + 12; Settings.ZoomPercent = 75 + zoom.Progress * 25;
                Settings.ContentWidth = width.SelectedItemPosition switch { 1 => 1100, 2 => 0, _ => 840 };
                _store.Save(); ApplyAppearance(); await ReloadAsync();
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void ApplyAppearance()
    {
        var foreground = Color.ParseColor(Settings.DarkMode ? "#E9EDF0" : "#202327");
        var background = Color.ParseColor(Settings.DarkMode ? "#202226" : "#FFFFFF");
        var surface = Color.ParseColor(Settings.DarkMode ? "#30363B" : "#F1F5F6");
        void Apply(View view)
        {
            if (view is WebView) return;
            if (view is TextView text) text.SetTextColor(foreground);
            if (view is ImageButton image) image.ImageTintList = ColorStateList.ValueOf(foreground);
            if (view is RadioButton tab)
            {
                var states = new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() };
                tab.BackgroundTintList = new ColorStateList(states, new[] { (int)Color.ParseColor("#176A58"), (int)surface });
                tab.SetTextColor(new ColorStateList(states, new[] { (int)Color.White, (int)foreground }));
            }
            else if (view is Button button) button.BackgroundTintList = ColorStateList.ValueOf(surface);
            if (view is not TextView && view is not ImageButton) view.SetBackgroundColor(background);
            if (view is ViewGroup group) for (var i = 0; i < group.ChildCount; i++) Apply(group.GetChildAt(i)!);
        }
        Apply(FindViewById(Resource.Id.root)!);
        _query.SetHintTextColor(Color.ParseColor(Settings.DarkMode ? "#ADB8C2" : "#59656D"));
        _preview.SetBackgroundColor(background);
        _preview.Settings.TextZoom = Settings.ZoomPercent;
        _rawText.TextSize = Settings.FontSize * Settings.ZoomPercent / 100f;
        _status.SetBackgroundColor(surface);
    }

    private void RestorePosition()
    {
        _preview.PostDelayed(() =>
        {
            if (_destroyed || _entry is null) return;
            var density = Resources!.DisplayMetrics!.Density;
            var previewHeight = Math.Max(0, _preview.ContentHeight * density - _preview.Height);
            _preview.ScrollTo(0, (int)(previewHeight * _entry.PreviewPosition));
            _rawScroll.ScrollTo(0, (int)(Math.Max(0, _rawText.Height - _rawScroll.Height) * _entry.RawPosition));
            Search();
        }, 120);
    }

    private void SavePosition()
    {
        if (_entry is null) return;
        var previewHeight = _preview.ContentHeight * Resources!.DisplayMetrics!.Density - _preview.Height;
        if (previewHeight > 0) _entry.PreviewPosition = ReaderStateStore.ClampPosition(_preview.ScrollY / previewHeight);
        var rawHeight = _rawText.Height - _rawScroll.Height;
        if (rawHeight > 0) _entry.RawPosition = ReaderStateStore.ClampPosition((double)_rawScroll.ScrollY / rawHeight);
    }

    private void ScheduleSave()
    {
        if (_destroyed) return;
        _saveTimer?.Dispose();
        _saveTimer = new System.Threading.Timer(_ => RunOnUiThread(() =>
        {
            if (!_destroyed) { SavePosition(); _store.Save(); }
        }), null, 900, Timeout.Infinite);
    }

    private void Search()
    {
        if (_destroyed) return;
        _rawMatch = -1;
        if (_tabs.CheckedRadioButtonId == Resource.Id.preview_tab) _preview.FindAllAsync(_query.Text ?? "");
        else FindNext(true);
    }

    private void FindNext(bool forward)
    {
        if (_document is null) return;
        if (_tabs.CheckedRadioButtonId == Resource.Id.preview_tab) { _preview.FindNext(forward); return; }
        var query = _query.Text ?? "";
        if (query.Length == 0) { _rawText.Text = _document.Content; _searchCount.Text = ""; return; }
        var text = _document.Content;
        var positions = new List<int>();
        for (var offset = 0; offset <= text.Length - query.Length && positions.Count < 1000;)
        {
            var found = text.IndexOf(query, offset, StringComparison.CurrentCultureIgnoreCase);
            if (found < 0) break;
            positions.Add(found); offset = found + query.Length;
        }
        _rawMatch = positions.Count == 0 ? -1 : (_rawMatch < 0 ? 0 : (_rawMatch + (forward ? 1 : -1) + positions.Count) % positions.Count);
        _searchCount.Text = $"{_rawMatch + 1}/{positions.Count}";
        var highlighted = new SpannableString(text);
        if (_rawMatch >= 0)
        {
            var position = positions[_rawMatch];
            highlighted.SetSpan(new BackgroundColorSpan(Color.Rgb(233, 169, 53)), position, position + query.Length, SpanTypes.ExclusiveExclusive);
            highlighted.SetSpan(new ForegroundColorSpan(Color.Black), position, position + query.Length, SpanTypes.ExclusiveExclusive);
            _rawText.TextFormatted = highlighted;
            _rawText.Post(() => { if (_rawText.Layout is { } layout) _rawScroll.SmoothScrollTo(0, layout.GetLineTop(layout.GetLineForOffset(position))); });
        }
        else _rawText.Text = text;
    }

    private void UpdateTab()
    {
        var raw = _tabs.CheckedRadioButtonId == Resource.Id.raw_tab;
        _preview.Visibility = _currentUri is not null && !raw ? ViewStates.Visible : ViewStates.Gone;
        _rawScroll.Visibility = _currentUri is not null && raw ? ViewStates.Visible : ViewStates.Gone;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _open.Enabled = !busy; _reload.Enabled = !busy && _currentUri is not null;
        _reload.Alpha = _reload.Enabled ? 1 : .35f;
        _loading.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
    }

    private void ShowStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.SetTextColor(Color.ParseColor(error ? (Settings.DarkMode ? "#FFB4AA" : "#A52D23") : (Settings.DarkMode ? "#8BDFC2" : "#365349")));
    }

    private static string FriendlyMessage(Exception ex) => ex switch
    {
        InvalidDataException => ex.Message,
        DecoderFallbackException => "Codificação inválida. Salve o arquivo como UTF-8.",
        FileNotFoundException or Java.IO.FileNotFoundException => "Arquivo não encontrado. Selecione-o novamente.",
        UnauthorizedAccessException or Java.Lang.SecurityException => "Permissão de acesso indisponível. Selecione o arquivo ou a pasta novamente.",
        _ => "Não foi possível ler o documento. Verifique se ele está disponível."
    };

    private bool HandleIntent(Intent? intent)
    {
        var uri = intent?.Action == Intent.ActionView ? intent.Data : null;
        if (intent?.Action == Intent.ActionSend)
        {
            uri = OperatingSystem.IsAndroidVersionAtLeast(33)
                ? intent.GetParcelableExtra(Intent.ExtraStream, Java.Lang.Class.FromType(typeof(AndroidUri))) as AndroidUri
                : intent.GetParcelableExtra(Intent.ExtraStream) as AndroidUri;
        }
        if (uri?.Scheme != "content") return false;
        _ = LoadAsync(uri, intent!.Flags);
        return true;
    }

    protected override void OnNewIntent(Intent? intent) { base.OnNewIntent(intent); Intent = intent; HandleIntent(intent); }
    protected override void OnResume()
    {
        base.OnResume();
        if (_resumed && !_busy && Settings.AutoReload && _currentUri is not null) _ = ReloadAsync();
        _resumed = true;
    }
    protected override void OnPause() { SavePosition(); _store.Save(); base.OnPause(); }
    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutString("document-uri", _currentUri?.ToString());
        outState.PutBoolean("raw-selected", _tabs.CheckedRadioButtonId == Resource.Id.raw_tab);
        base.OnSaveInstanceState(outState);
    }
    protected override void OnDestroy()
    {
        _destroyed = true; _lifetime.Cancel();
        _searchTimer?.Dispose(); _reloadTimer?.Dispose(); _saveTimer?.Dispose();
        if (_observer is not null) { ContentResolver!.UnregisterContentObserver(_observer); _observer.Dispose(); }
        _preview.StopLoading(); (_preview.Parent as ViewGroup)?.RemoveView(_preview); _preview.Destroy();
        _lifetime.Dispose(); base.OnDestroy();
    }

    private sealed class DocumentObserver(Handler handler, Action changed) : ContentObserver(handler)
    {
        public override void OnChange(bool selfChange) { base.OnChange(selfChange); changed(); }
    }
}
