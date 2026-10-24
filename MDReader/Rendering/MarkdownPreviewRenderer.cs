using MDReader.Core;

namespace MDReader.Rendering;

public sealed class MarkdownPreviewRenderer
{
    private readonly MarkdownHtmlRenderer _renderer = new();

    public void Render(WebBrowser target, string markdown, string filePath)
    {
        target.DocumentText = BuildHtmlDocument(markdown, filePath);
    }

    internal string BuildHtmlDocument(string markdown, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var baseHref = string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : new Uri(directory + Path.DirectorySeparatorChar).AbsoluteUri;

        return _renderer.Render(markdown, baseHref, allowEmbeddedHtml: true);
    }
}
