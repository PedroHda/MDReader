using MDReader.Models;
using MDReader.Rendering;
using MDReader.Services;

namespace MDReader.Ui;

public sealed class MainForm : Form
{
    private readonly MarkdownFileReader _fileReader;
    private readonly MarkdownPreviewRenderer _previewRenderer = new();

    private readonly Label _fileNameLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _selectFileButton = new();
    private readonly Button _reloadButton = new();
    private readonly RichTextBox _rawMarkdownTextBox = new();
    private readonly WebBrowser _previewBrowser = new();

    private MarkdownDocument? _currentDocument;

    public MainForm()
    {
        _fileReader = new MarkdownFileReader(new MarkdownFileValidator());

        ConfigureWindow();
        BuildLayout();
        ShowStatus("Selecione um arquivo .md ou .markdown para começar.", StatusType.Info);
    }

    private void ConfigureWindow()
    {
        Text = "Markdown Reader";
        MinimumSize = new Size(820, 560);
        Size = new Size(1040, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 249, 252);

        var appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        if (appIcon is not null)
        {
            Icon = appIcon;
        }
    }

    private void BuildLayout()
    {
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = BackColor
        };

        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        rootLayout.Controls.Add(BuildHeader(), 0, 0);
        rootLayout.Controls.Add(BuildReaderTabs(), 0, 1);
        rootLayout.Controls.Add(BuildStatusBar(), 0, 2);

        Controls.Add(rootLayout);
    }

    private Control BuildHeader()
    {
        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            Text = "Markdown Reader",
            AutoSize = true,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(20, 58, 93),
            Margin = new Padding(0, 0, 0, 8)
        };

        var controlsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 0, 0, 8)
        };

        _selectFileButton.Text = "Selecionar Markdown";
        _selectFileButton.AutoSize = true;
        _selectFileButton.Padding = new Padding(12, 7, 12, 7);
        _selectFileButton.FlatStyle = FlatStyle.System;
        _selectFileButton.Click += SelectFileButton_Click;

        _reloadButton.Text = "Recarregar";
        _reloadButton.AutoSize = true;
        _reloadButton.Enabled = false;
        _reloadButton.Padding = new Padding(12, 7, 12, 7);
        _reloadButton.FlatStyle = FlatStyle.System;
        _reloadButton.Click += ReloadButton_Click;

        controlsLayout.Controls.Add(_selectFileButton);
        controlsLayout.Controls.Add(_reloadButton);

        _fileNameLabel.Text = "Arquivo carregado: nenhum";
        _fileNameLabel.AutoEllipsis = true;
        _fileNameLabel.Dock = DockStyle.Top;
        _fileNameLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        _fileNameLabel.ForeColor = Color.FromArgb(72, 81, 91);
        _fileNameLabel.Margin = new Padding(0);

        headerLayout.Controls.Add(titleLabel, 0, 0);
        headerLayout.Controls.Add(controlsLayout, 0, 1);
        headerLayout.Controls.Add(_fileNameLabel, 0, 2);

        return headerLayout;
    }

    private Control BuildReaderTabs()
    {
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular)
        };

        var rawTab = new TabPage("Texto bruto")
        {
            Padding = new Padding(8),
            BackColor = Color.White
        };

        var previewTab = new TabPage("Visualização")
        {
            Padding = new Padding(8),
            BackColor = Color.White
        };

        ConfigureTextBox(_rawMarkdownTextBox, new Font("Consolas", 10.5F, FontStyle.Regular));
        ConfigurePreviewBrowser(_previewBrowser);

        rawTab.Controls.Add(_rawMarkdownTextBox);
        previewTab.Controls.Add(_previewBrowser);
        tabControl.TabPages.Add(rawTab);
        tabControl.TabPages.Add(previewTab);

        return tabControl;
    }

    private static void ConfigureTextBox(RichTextBox textBox, Font font)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.White;
        textBox.ForeColor = Color.FromArgb(32, 35, 39);
        textBox.Font = font;
        textBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        textBox.WordWrap = true;
        textBox.DetectUrls = true;
    }

    private static void ConfigurePreviewBrowser(WebBrowser browser)
    {
        browser.Dock = DockStyle.Fill;
        browser.AllowWebBrowserDrop = false;
        browser.IsWebBrowserContextMenuEnabled = true;
        browser.ScriptErrorsSuppressed = true;
        browser.WebBrowserShortcutsEnabled = true;
    }

    private Control BuildStatusBar()
    {
        _statusLabel.Dock = DockStyle.Bottom;
        _statusLabel.AutoSize = false;
        _statusLabel.Height = 38;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        _statusLabel.Padding = new Padding(12, 0, 12, 0);
        _statusLabel.Margin = new Padding(0, 12, 0, 0);

        return _statusLabel;
    }

    private void SelectFileButton_Click(object? sender, EventArgs e)
    {
        SelectMarkdownFile();
    }

    private void ReloadButton_Click(object? sender, EventArgs e)
    {
        if (_currentDocument is null)
        {
            ShowStatus("Nenhum arquivo foi carregado para recarregar.", StatusType.Warning);
            return;
        }

        LoadMarkdownFile(_currentDocument.FilePath);
    }

    private void SelectMarkdownFile()
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Selecione um arquivo Markdown",
            Filter = "Arquivos Markdown (*.md;*.markdown)|*.md;*.markdown|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadMarkdownFile(openFileDialog.FileName);
        }
    }

    private void LoadMarkdownFile(string filePath)
    {
        try
        {
            var document = _fileReader.Read(filePath);
            DisplayDocument(document);
        }
        catch (MarkdownFileException ex)
        {
            ShowStatus(ex.Message, StatusType.Error);
            MessageBox.Show(this, ex.Message, "Não foi possível abrir o arquivo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            const string message = "Ocorreu um erro inesperado ao abrir o arquivo.";
            ShowStatus(message, StatusType.Error);
            MessageBox.Show(this, $"{message}{Environment.NewLine}{ex.Message}", "Erro inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DisplayDocument(MarkdownDocument document)
    {
        _currentDocument = document;
        _reloadButton.Enabled = true;
        _fileNameLabel.Text = $"Arquivo carregado: {document.FileName}";
        _rawMarkdownTextBox.Text = document.Content;
        _previewRenderer.Render(_previewBrowser, document.Content, document.FilePath);

        if (document.IsEmpty)
        {
            ShowStatus("Arquivo carregado, mas ele está vazio.", StatusType.Warning);
            return;
        }

        ShowStatus("Arquivo Markdown carregado com sucesso.", StatusType.Success);
    }

    private void ShowStatus(string message, StatusType type)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = type switch
        {
            StatusType.Success => Color.FromArgb(16, 99, 64),
            StatusType.Warning => Color.FromArgb(132, 86, 0),
            StatusType.Error => Color.FromArgb(154, 52, 18),
            _ => Color.FromArgb(53, 65, 82)
        };
        _statusLabel.BackColor = type switch
        {
            StatusType.Success => Color.FromArgb(229, 246, 236),
            StatusType.Warning => Color.FromArgb(255, 247, 214),
            StatusType.Error => Color.FromArgb(255, 236, 232),
            _ => Color.FromArgb(233, 239, 247)
        };
    }

    private enum StatusType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
