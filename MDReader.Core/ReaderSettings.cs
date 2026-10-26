namespace MDReader.Core;

public sealed class ReaderSettings
{
    public bool DarkMode { get; set; }
    public int FontSize { get; set; } = 16;
    public int ZoomPercent { get; set; } = 100;
    public int ContentWidth { get; set; } = 840;
    public bool AutoReload { get; set; } = true;
    public string? DocumentTreeUri { get; set; }

    public void Normalize()
    {
        FontSize = Math.Clamp(FontSize, 12, 32);
        ZoomPercent = Math.Clamp(ZoomPercent, 75, 200);
        ContentWidth = ContentWidth is 0 or 840 or 1100 ? ContentWidth : 840;
    }
}

public sealed class RecentDocument
{
    public string Location { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public double PreviewPosition { get; set; }
    public double RawPosition { get; set; }
    public DateTimeOffset LastOpened { get; set; }
    public string? FolderId { get; set; }
    public string? TreeUri { get; set; }

    public override string ToString() => (Favorite ? "★ " : string.Empty) + FileName;
}

public sealed class ReaderState
{
    public ReaderSettings Settings { get; set; } = new();
    public List<RecentDocument> Documents { get; set; } = new();
    public string? LastDocument { get; set; }
}
