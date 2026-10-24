using System.Text;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Webkit;
using MDReader.Mobile.Services;
using MDReader.Mobile.Ui;
using AndroidUri = Android.Net.Uri;

namespace MDReader.Mobile;

[Activity(Name = "com.pedrohda.mdreader.MainActivity", Label = "@string/app_name", MainLauncher = true,
    Exported = true, Theme = "@style/AppTheme",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize)]
public sealed class MainActivity : Activity
{
    private const int OpenDocumentRequest = 100;
    private const string DocumentUriKey = "document-uri";
    private readonly CancellationTokenSource _lifetime = new();
    private AndroidMarkdownFileReader _fileReader = null!;
    private Button _openButton = null!;
    private ImageButton _reloadButton = null!;
    private TextView _fileName = null!;
    private TextView _status = null!;
    private TextView _rawText = null!;
    private View _emptyState = null!;
    private ScrollView _rawScroll = null!;
    private RadioGroup _tabs = null!;
    private ProgressBar _loading = null!;
    private WebView _preview = null!;
    private AndroidUri? _currentUri;
    private bool _busy;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.main);
        _fileReader = new AndroidMarkdownFileReader(ContentResolver!);
        _openButton = FindViewById<Button>(Resource.Id.open_file)!;
        _reloadButton = FindViewById<ImageButton>(Resource.Id.reload_file)!;
        _fileName = FindViewById<TextView>(Resource.Id.file_name)!;
        _status = FindViewById<TextView>(Resource.Id.status)!;
        _rawText = FindViewById<TextView>(Resource.Id.raw_content)!;
        _emptyState = FindViewById(Resource.Id.empty_state)!;
        _rawScroll = FindViewById<ScrollView>(Resource.Id.raw_scroll)!;
        _tabs = FindViewById<RadioGroup>(Resource.Id.view_tabs)!;
        _loading = FindViewById<ProgressBar>(Resource.Id.loading)!;
        _preview = FindViewById<WebView>(Resource.Id.preview_browser)!;
        FindViewById(Resource.Id.root)!.SetOnApplyWindowInsetsListener(new SystemBarInsetsListener());
        ConfigurePreview();

        _openButton.Click += (_, _) => SelectDocument();
        _reloadButton.Click += async (_, _) =>
        {
            if (_currentUri is not null)
            {
                await LoadDocumentAsync(_currentUri);
            }
        };
        _tabs.CheckedChange += (_, _) => UpdateVisibleTab();
        if (savedInstanceState?.GetBoolean("raw-selected") == true)
        {
            _tabs.Check(Resource.Id.raw_tab);
        }

        var previousUri = savedInstanceState?.GetString(DocumentUriKey)
            ?? GetPreferences(FileCreationMode.Private)?.GetString(DocumentUriKey, null);
        if (!string.IsNullOrWhiteSpace(previousUri) && AndroidUri.Parse(previousUri) is { } uri)
        {
            _ = LoadDocumentAsync(uri);
        }
    }

    private void ConfigurePreview()
    {
        _preview.Settings.JavaScriptEnabled = false;
        _preview.Settings.AllowFileAccess = false;
        _preview.Settings.AllowContentAccess = false;
        _preview.Settings.DomStorageEnabled = false;
        _preview.Settings.MixedContentMode = MixedContentHandling.NeverAllow;
        _preview.Settings.SetSupportMultipleWindows(false);
        _preview.SetWebViewClient(new ReaderWebViewClient(this));
    }

    private void SelectDocument()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        // Providers do not agree on a MIME type for Markdown; validate the returned display name.
        intent.SetType("*/*");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
        try
        {
            StartActivityForResult(intent, OpenDocumentRequest);
        }
        catch (ActivityNotFoundException)
        {
            ShowStatus("Nenhum seletor de arquivos disponível neste dispositivo.", isError: true);
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == OpenDocumentRequest && resultCode == Result.Ok && data?.Data is { } uri)
        {
            _ = LoadDocumentAsync(uri, data.Flags);
        }
    }

    private async Task LoadDocumentAsync(AndroidUri uri, ActivityFlags? grantedFlags = null)
    {
        if (_busy || _lifetime.IsCancellationRequested)
        {
            return;
        }

        SetBusy(true);
        ShowStatus(GetString(Resource.String.loading));
        try
        {
            var token = _lifetime.Token;
            var document = await Task.Run(() => _fileReader.ReadAsync(uri, token), token);
            if (IsFinishing || IsDestroyed || token.IsCancellationRequested)
            {
                return;
            }

            _currentUri = uri;
            _fileName.Text = document.FileName;
            _rawText.Text = document.Content;
            _rawScroll.ScrollTo(0, 0);
            _preview.LoadDataWithBaseURL("https://mdreader.invalid/", document.Html, "text/html", "UTF-8", null);
            _emptyState.Visibility = ViewStates.Gone;
            UpdateVisibleTab();
            if (grantedFlags is not null)
            {
                RememberDocument(uri, grantedFlags.Value);
            }

            ShowStatus(string.IsNullOrWhiteSpace(document.Content)
                ? "Arquivo carregado, mas ele está vazio."
                : "Arquivo Markdown carregado com sucesso.");
        }
        catch (System.OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("MDReader", ex.ToString());
            if (!IsFinishing && !IsDestroyed)
            {
                ShowStatus(GetFriendlyMessage(ex), isError: true);
            }
        }
        finally
        {
            if (!IsFinishing && !IsDestroyed)
            {
                SetBusy(false);
            }
        }
    }

    private void RememberDocument(AndroidUri uri, ActivityFlags grantedFlags)
    {
        var preferences = GetPreferences(FileCreationMode.Private)!;
        var previousUri = preferences.GetString(DocumentUriKey, null);
        var persistentAccess = false;
        try
        {
            if (grantedFlags.HasFlag(ActivityFlags.GrantPersistableUriPermission))
            {
                ContentResolver!.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission);
                persistentAccess = true;
            }
        }
        catch (Java.Lang.SecurityException)
        {
            // Some providers grant access only for the current session.
        }

        var editor = preferences.Edit()!;
        if (persistentAccess)
        {
            editor.PutString(DocumentUriKey, uri.ToString());
        }
        else
        {
            editor.Remove(DocumentUriKey);
        }

        editor.Apply();
        try
        {
            if (previousUri is not null && previousUri != uri.ToString() && AndroidUri.Parse(previousUri) is { } previous)
            {
                ContentResolver!.ReleasePersistableUriPermission(previous, ActivityFlags.GrantReadUriPermission);
            }
        }
        catch (Java.Lang.SecurityException)
        {
            // The old grant may already have been revoked by its provider.
        }
    }

    private void UpdateVisibleTab()
    {
        var hasDocument = _currentUri is not null;
        var rawSelected = _tabs.CheckedRadioButtonId == Resource.Id.raw_tab;
        _preview.Visibility = hasDocument && !rawSelected ? ViewStates.Visible : ViewStates.Gone;
        _rawScroll.Visibility = hasDocument && rawSelected ? ViewStates.Visible : ViewStates.Gone;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _openButton.Enabled = !busy;
        _reloadButton.Enabled = !busy && _currentUri is not null;
        _reloadButton.Alpha = _reloadButton.Enabled ? 1f : 0.35f;
        _loading.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
    }

    private void ShowStatus(string message, bool isError = false)
    {
        _status.Text = message;
        _status.SetTextColor(Color.ParseColor(isError ? "#A52D23" : "#365349"));
        _status.SetBackgroundColor(Color.ParseColor(isError ? "#FCECE9" : "#EFF6F2"));
    }

    private static string GetFriendlyMessage(Exception exception) => exception switch
    {
        InvalidDataException => exception.Message,
        DecoderFallbackException => "Não foi possível interpretar o texto. Salve o arquivo como UTF-8 e tente novamente.",
        FileNotFoundException or Java.IO.FileNotFoundException => "O arquivo não foi encontrado. Selecione-o novamente.",
        UnauthorizedAccessException or Java.Lang.SecurityException => "O acesso ao arquivo não está mais disponível. Selecione-o novamente.",
        IOException or Java.IO.IOException => "Não foi possível ler o arquivo. Verifique se ele está disponível e tente novamente.",
        _ => "Não foi possível abrir o documento. Selecione um arquivo Markdown válido."
    };

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutString(DocumentUriKey, _currentUri?.ToString());
        outState.PutBoolean("raw-selected", _tabs.CheckedRadioButtonId == Resource.Id.raw_tab);
        base.OnSaveInstanceState(outState);
    }

    protected override void OnDestroy()
    {
        _lifetime.Cancel();
        _preview.StopLoading();
        (_preview.Parent as ViewGroup)?.RemoveView(_preview);
        _preview.Destroy();
        _lifetime.Dispose();
        base.OnDestroy();
    }
}
