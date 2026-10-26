using MDReader.Core;
using MDReader.Models;
using MDReader.Rendering;
using MDReader.Services;
using Microsoft.Web.WebView2.WinForms;

namespace MDReader.Ui;

public sealed class MainForm : Form
{
    private readonly MarkdownFileReader _reader = new(new MarkdownFileValidator());
    private readonly MarkdownPreviewRenderer _renderer = new();
    private readonly ReaderStateStore _store;
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly WebPreviewSession _preview;
    private readonly RichTextBox _raw = new() { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = true, HideSelection = false, BorderStyle = BorderStyle.None };
    private readonly ListBox _headings = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
    private readonly ListBox _recent = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
    private readonly TabControl _readingTabs = new() { Dock = DockStyle.Fill };
    private readonly ToolStrip _tools = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 6, 8, 6) };
    private readonly ToolStrip _searchBar = new() { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 3, 8, 5) };
    private readonly ToolStripTextBox _query = new() { AutoSize = false, Width = 240, MaxLength = 256 };
    private readonly ToolStripLabel _searchCount = new();
    private readonly ToolStripButton _favorite = new() { ToolTipText = "Favoritar documento", AccessibleName = "Favoritar documento" };
    private readonly ToolStripButton _dark = new() { CheckOnClick = true, ToolTipText = "Modo escuro", AccessibleName = "Modo escuro" };
    private readonly ToolStripButton _reload = new() { Text = "Recarregar", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, ToolTipText = "Recarregar (F5)" };
    private readonly ToolStripButton _open = new() { Text = "Abrir", DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, ToolTipText = "Abrir Markdown (Ctrl+O)" };
    private readonly NumericUpDown _fontSize = new() { Minimum = 12, Maximum = 32, Width = 48 };
    private readonly ToolStripComboBox _zoom = new() { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = 72 };
    private readonly ToolStripComboBox _width = new() { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = 110 };
    private readonly ToolStripMenuItem _autoReload = new("Atualização automática") { CheckOnClick = true };
    private readonly ToolStripMenuItem _recentMenu = new("Recentes e favoritos");
    private readonly StatusStrip _statusBar = new();
    private readonly ToolStripStatusLabel _fileLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _status = new();
    private readonly MenuStrip _menu = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 250 };
    private readonly System.Windows.Forms.Timer _watchTimer = new() { Interval = 650 };
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 1500 };
    private FileSystemWatcher? _watcher;
    private MarkdownDocument? _document;
    private RecentDocument? _entry;
    private string? _pendingPath;
    private bool _busy, _applying, _closing;
    private readonly string? _initialPath;
    private ReaderSettings Settings => _store.State.Settings;

    public MainForm(string? initialPath = null)
    {
        _initialPath = initialPath;
        _store = new ReaderStateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDReader", "reader-state.json"));
        _preview = new WebPreviewSession(_browser);
        Text = "MDReader";
        Size = new Size(1180, 820);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AllowDrop = true;
        BuildLayout();
        WireEvents();
        ApplyAppearance();
        RefreshHistory();
        SetStatus(_store.LastError ?? "Abra um arquivo Markdown.");
    }

    private void BuildLayout()
    {
        var file = new ToolStripMenuItem("Arquivo");
        ((ToolStripMenuItem)file.DropDownItems.Add("Abrir...", null, async (_, _) => await SelectFileAsync())).ShortcutKeys = Keys.Control | Keys.O;
        file.DropDownItems.Add(_recentMenu);
        ((ToolStripMenuItem)file.DropDownItems.Add("Recarregar", null, async (_, _) => await ReloadAsync())).ShortcutKeys = Keys.F5;
        file.DropDownItems.Add(_autoReload);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Registrar no menu Abrir com", null, (_, _) =>
        {
            try { WindowsFileIntegration.RegisterOpenWith(); SetStatus("Registrado em Abrir com para .md e .markdown."); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException) { SetStatus("Não foi possível registrar Abrir com.", true); }
        });
        file.DropDownItems.Add("Sair", null, (_, _) => Close());
        _menu.Items.Add(file);
        var view = new ToolStripMenuItem("Exibir");
        ((ToolStripMenuItem)view.DropDownItems.Add("Pesquisar", null, (_, _) => _query.Focus())).ShortcutKeys = Keys.Control | Keys.F;
        view.DropDownItems.Add("Topo do documento", null, async (_, _) =>
        {
            _raw.SelectionStart = 0;
            _raw.ScrollToCaret();
            await _preview.GoToAsync("reader-top");
        });
        view.DropDownItems.Add("WebView2 Runtime", null, (_, _) =>
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true }));
        _menu.Items.Add(view);
        MainMenuStrip = _menu;

        _tools.Items.AddRange(new ToolStripItem[] { _open, _reload, new ToolStripSeparator(), _favorite, _dark, new ToolStripSeparator(), new ToolStripLabel("Fonte") });
        _tools.Items.Add(new ToolStripControlHost(_fontSize));
        _zoom.Items.AddRange(new object[] { "75%", "100%", "125%", "150%", "175%", "200%" });
        _width.Items.AddRange(new object[] { "Confortável", "Ampla", "Completa" });
        _tools.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("Zoom"), _zoom, new ToolStripLabel("Largura"), _width });
        _query.TextBox.PlaceholderText = "Buscar no documento";
        var previous = new ToolStripButton { ToolTipText = "Resultado anterior (Shift+F3)", AccessibleName = "Resultado anterior", Tag = '\uE70E' };
        var next = new ToolStripButton { ToolTipText = "Próximo resultado (F3)", AccessibleName = "Próximo resultado", Tag = '\uE70D' };
        var clear = new ToolStripButton { ToolTipText = "Limpar busca", AccessibleName = "Limpar busca", Tag = '\uE711' };
        previous.Click += async (_, _) => await SearchAsync(-1);
        next.Click += async (_, _) => await SearchAsync(1);
        clear.Click += (_, _) => _query.Clear();
        _searchBar.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("Buscar"), _query, previous, next, clear, _searchCount });
        _readingTabs.TabPages.Add(new TabPage("Visualização") { Controls = { _browser } });
        _readingTabs.TabPages.Add(new TabPage("Texto bruto") { Controls = { _raw } });
        var navigation = new TabControl { Dock = DockStyle.Fill };
        navigation.TabPages.Add(new TabPage("Sumário") { Controls = { _headings } });
        navigation.TabPages.Add(new TabPage("Recentes") { Controls = { _recent } });
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1140, 650), SplitterDistance = 230, Panel1MinSize = 180, Panel2MinSize = 450 };
        split.Panel1.Controls.Add(navigation);
        split.Panel2.Controls.Add(_readingTabs);
        _statusBar.Items.AddRange(new ToolStripItem[] { _fileLabel, _status });
        Controls.Add(split);
        Controls.Add(_searchBar);
        Controls.Add(_tools);
        Controls.Add(_menu);
        Controls.Add(_statusBar);
    }

    private void WireEvents()
    {
        Shown += async (_, _) =>
        {
            try { await _preview.InitializeAsync(); }
            catch (Exception ex) when (ex is Microsoft.Web.WebView2.Core.WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException or InvalidOperationException)
            {
                _readingTabs.SelectedIndex = 1;
                SetStatus("WebView2 indisponível. Instale o runtime pelo menu Exibir.", true);
            }
            var path = _initialPath ?? _store.State.LastDocument;
            if (path is not null) await LoadAsync(path);
        };
        _open.Click += async (_, _) => await SelectFileAsync();
        _reload.Click += async (_, _) => await ReloadAsync();
        _query.TextChanged += (_, _) => { _searchTimer.Stop(); _searchTimer.Start(); };
        _query.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await SearchAsync(e.Shift ? -1 : 1); }
            if (e.KeyCode == Keys.Escape) { _query.Clear(); e.SuppressKeyPress = true; }
        };
        _searchTimer.Tick += async (_, _) => { _searchTimer.Stop(); await SearchAsync(0); };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SavePosition(); _store.Save(); };
        _watchTimer.Tick += async (_, _) => { _watchTimer.Stop(); if (Settings.AutoReload) await ReloadAsync(); };
        _headings.SelectedIndexChanged += async (_, _) =>
        {
            if (_headings.SelectedItem is HeadingInfo heading)
            {
                _readingTabs.SelectedIndex = 0;
                await _preview.GoToAsync(heading.Id);
            }
        };
        _recent.DoubleClick += async (_, _) => { if (_recent.SelectedItem is RecentDocument entry) await LoadAsync(entry.Location); };
        _favorite.Click += (_, _) =>
        {
            if (_entry is null) return;
            _entry.Favorite = !_entry.Favorite;
            _store.Save();
            RefreshHistory();
            ApplyAppearance();
        };
        _dark.CheckedChanged += async (_, _) => { if (!_applying) { Settings.DarkMode = _dark.Checked; await SettingsChangedAsync(); } };
        _fontSize.ValueChanged += async (_, _) => { if (!_applying) { Settings.FontSize = (int)_fontSize.Value; await SettingsChangedAsync(); } };
        _zoom.SelectedIndexChanged += async (_, _) => { if (!_applying && _zoom.SelectedItem is string zoom) { Settings.ZoomPercent = int.Parse(zoom.TrimEnd('%')); await SettingsChangedAsync(); } };
        _width.SelectedIndexChanged += async (_, _) => { if (!_applying) { Settings.ContentWidth = _width.SelectedIndex switch { 1 => 1100, 2 => 0, _ => 840 }; await SettingsChangedAsync(); } };
        _autoReload.CheckedChanged += (_, _) => { if (!_applying) { Settings.AutoReload = _autoReload.Checked; _store.Save(); } };
        _readingTabs.SelectedIndexChanged += async (_, _) => await SearchAsync(0);
        _preview.PositionChanged += position => { if (_entry is not null) _entry.PreviewPosition = position; ScheduleSave(); };
        _preview.SearchChanged += (index, count) => { if (_readingTabs.SelectedIndex == 0) _searchCount.Text = string.IsNullOrEmpty(_query.Text) ? "" : $"{index}/{count}"; };
        _raw.VScroll += (_, _) => ScheduleSave();
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += async (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) await LoadAsync(files[0]); };
        FormClosing += (_, _) =>
        {
            _closing = true;
            SavePosition();
            _store.Save();
            _lifetime.Cancel();
            _watcher?.Dispose();
            _searchTimer.Dispose(); _watchTimer.Dispose(); _saveTimer.Dispose();
            _preview.Dispose();
        };
    }

    private async Task SelectFileAsync()
    {
        using var dialog = new OpenFileDialog { Title = "Abrir Markdown", Filter = "Markdown (*.md;*.markdown)|*.md;*.markdown|Todos os arquivos|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) await LoadAsync(dialog.FileName);
    }

    private Task ReloadAsync() => _document is null ? Task.CompletedTask : LoadAsync(_document.FilePath);

    private async Task LoadAsync(string path)
    {
        if (_closing) return;
        if (_busy) { _pendingPath = path; return; }
        _busy = true;
        _open.Enabled = false; _reload.Enabled = false;
        SavePosition();
        SetStatus("Carregando...");
        try
        {
            var document = await _reader.ReadAsync(path, _lifetime.Token);
            var rendered = await Task.Run(() => _renderer.Render(document.Content, Settings), _lifetime.Token);
            if (_closing) return;
            _document = document;
            _entry = _store.Open(document.FilePath, document.FileName);
            _raw.Text = document.Content;
            _raw.SelectionStart = Math.Clamp((int)(_entry.RawPosition * _raw.TextLength), 0, _raw.TextLength);
            _raw.ScrollToCaret();
            _headings.Items.Clear();
            foreach (var heading in rendered.Headings) _headings.Items.Add(heading);
            await _preview.ShowAsync(rendered.Html, Path.GetDirectoryName(document.FilePath)!, Settings, _entry.PreviewPosition);
            _fileLabel.Text = document.FileName;
            _fileLabel.ToolTipText = document.FilePath;
            Text = "MDReader - " + document.FileName;
            RefreshHistory();
            ApplyAppearance();
            ConfigureWatcher(document.FilePath);
            SetStatus(document.IsEmpty ? "Arquivo vazio." : _preview.Ready ? "Documento carregado." : "Texto carregado. WebView2 indisponível.");
        }
        catch (OperationCanceledException) when (_closing) { }
        catch (Exception ex) when (ex is MarkdownFileException or IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { if (!_closing) SetStatus(ex.Message, true); }
        finally
        {
            _busy = false;
            if (!_closing)
            {
                _open.Enabled = true; _reload.Enabled = _document is not null;
                if (_pendingPath is { } pending) { _pendingPath = null; _ = LoadAsync(pending); }
            }
        }
    }

    private void ConfigureWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
        { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        void Changed(object? sender, FileSystemEventArgs e)
        {
            if (_closing || !IsHandleCreated) return;
            try { BeginInvoke(() => { if (!_closing) { _watchTimer.Stop(); _watchTimer.Start(); } }); }
            catch (InvalidOperationException) { }
        }
        _watcher.Changed += Changed; _watcher.Created += Changed; _watcher.Deleted += Changed; _watcher.Renamed += (_, e) => Changed(this, e);
        _watcher.EnableRaisingEvents = true;
    }

    private async Task SettingsChangedAsync()
    {
        ApplyAppearance();
        _store.Save();
        try { await _preview.ApplyAsync(Settings); }
        catch (InvalidOperationException) { }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    private void ApplyAppearance()
    {
        _applying = true;
        Settings.Normalize();
        _dark.Checked = Settings.DarkMode;
        _fontSize.Value = Settings.FontSize;
        _zoom.SelectedItem = Settings.ZoomPercent + "%";
        _width.SelectedIndex = Settings.ContentWidth switch { 1100 => 1, 0 => 2, _ => 0 };
        _autoReload.Checked = Settings.AutoReload;
        var background = Settings.DarkMode ? Color.FromArgb(32, 34, 38) : Color.White;
        var surface = Settings.DarkMode ? Color.FromArgb(43, 48, 53) : Color.FromArgb(243, 246, 247);
        var foreground = Settings.DarkMode ? Color.FromArgb(233, 237, 240) : Color.FromArgb(32, 35, 39);
        void ColorControls(Control parent)
        {
            parent.BackColor = parent is RichTextBox or ListBox ? background : surface;
            parent.ForeColor = foreground;
            foreach (Control child in parent.Controls) ColorControls(child);
        }
        ColorControls(this);
        if (_raw.Font.Name != "Consolas" || _raw.Font.Size != Settings.FontSize)
        {
            var previousFont = _raw.Font;
            _raw.Font = new Font("Consolas", Settings.FontSize);
            if (!ReferenceEquals(previousFont, Font)) previousFont.Dispose();
        }
        _raw.ZoomFactor = Settings.ZoomPercent / 100f;
        _browser.DefaultBackgroundColor = background;
        foreach (var strip in new ToolStrip[] { _tools, _searchBar, _menu, _statusBar })
        {
            strip.BackColor = surface; strip.ForeColor = foreground;
            foreach (ToolStripItem item in strip.Items) { item.BackColor = surface; item.ForeColor = foreground; }
        }
        _open.Tag = '\uE8E5'; _reload.Tag = '\uE72C'; _favorite.Tag = _entry?.Favorite == true ? '\uE735' : '\uE734'; _dark.Tag = '\uE708';
        foreach (var strip in new[] { _tools, _searchBar })
            foreach (ToolStripItem item in strip.Items)
                if (item.Tag is char glyph) { var previous = item.Image; item.Image = ReaderIcons.Create(glyph, foreground); previous?.Dispose(); }
        _favorite.Enabled = _document is not null;
        _favorite.ToolTipText = _entry?.Favorite == true ? "Remover dos favoritos" : "Favoritar documento";
        _applying = false;
    }

    private void RefreshHistory()
    {
        _recent.Items.Clear(); _recentMenu.DropDownItems.Clear();
        foreach (var document in _store.Recent)
        {
            _recent.Items.Add(document);
            var captured = document;
            _recentMenu.DropDownItems.Add(document.ToString(), null, async (_, _) => await LoadAsync(captured.Location));
        }
        _recentMenu.Enabled = _recent.Items.Count > 0;
    }

    private async Task SearchAsync(int direction)
    {
        if (_closing) return;
        if (_readingTabs.SelectedIndex == 0)
        {
            try { await _preview.SearchAsync(_query.Text, direction); }
            catch (InvalidOperationException) { }
            catch (System.Runtime.InteropServices.COMException) { }
            return;
        }
        if (string.IsNullOrEmpty(_query.Text)) { _searchCount.Text = ""; _raw.SelectionLength = 0; return; }
        var text = _raw.Text;
        if (text.Length == 0) { _searchCount.Text = "Nenhum resultado"; return; }
        var start = direction < 0 ? Math.Max(0, _raw.SelectionStart - 1) : Math.Min(text.Length, _raw.SelectionStart + (direction == 0 ? 0 : _raw.SelectionLength));
        var found = direction < 0 ? text.LastIndexOf(_query.Text, Math.Min(start, Math.Max(0, text.Length - 1)), StringComparison.CurrentCultureIgnoreCase)
            : text.IndexOf(_query.Text, start, StringComparison.CurrentCultureIgnoreCase);
        if (found < 0) found = direction < 0 ? text.LastIndexOf(_query.Text, StringComparison.CurrentCultureIgnoreCase) : text.IndexOf(_query.Text, StringComparison.CurrentCultureIgnoreCase);
        if (found >= 0) { _raw.Select(found, _query.Text.Length); _raw.ScrollToCaret(); }
        _searchCount.Text = found >= 0 ? "Encontrado" : "Nenhum resultado";
    }

    private void SavePosition()
    {
        if (_entry is not null && _raw.TextLength > 0)
            _entry.RawPosition = ReaderStateStore.ClampPosition((double)_raw.GetCharIndexFromPosition(Point.Empty) / _raw.TextLength);
    }

    private void ScheduleSave() { if (!_closing) { _saveTimer.Stop(); _saveTimer.Start(); } }
    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? (Settings.DarkMode ? Color.Salmon : Color.Firebrick) : (Settings.DarkMode ? Color.LightGreen : Color.DarkGreen);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { _query.Focus(); return true; }
        if (keyData == Keys.F3 || keyData == (Keys.Shift | Keys.F3)) { _ = SearchAsync(keyData.HasFlag(Keys.Shift) ? -1 : 1); return true; }
        if (keyData == (Keys.Control | Keys.Shift | Keys.D)) { _dark.Checked = !_dark.Checked; return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
