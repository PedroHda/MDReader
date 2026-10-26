using MDReader.Core;

namespace MDReader.Rendering;

public sealed class MarkdownPreviewRenderer
{
    private readonly MarkdownHtmlRenderer _renderer = new();
    public RenderedMarkdownDocument Render(string markdown, ReaderSettings settings) =>
        _renderer.RenderDocument(markdown, "https://files.mdreader/", settings);
}
