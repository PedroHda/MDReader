using System.Net;
using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MDReader.Core;

public sealed record HeadingInfo(string Id, string Text, int Level)
{
    public override string ToString() => new string(' ', Math.Max(0, Level - 1) * 2) + Text;
}

public sealed record RenderedMarkdownDocument(string Html, IReadOnlyList<HeadingInfo> Headings);

public sealed class MarkdownHtmlRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    public string Render(string markdown, string? baseHref = null, ReaderSettings? settings = null) =>
        RenderDocument(markdown, baseHref, settings).Html;

    public RenderedMarkdownDocument RenderDocument(string markdown, string? baseHref = null, ReaderSettings? settings = null)
    {
        settings ??= new ReaderSettings();
        settings.Normalize();
        var document = Markdown.Parse(markdown, _pipeline);
        var headings = new List<HeadingInfo>();
        foreach (var heading in document.Descendants().OfType<HeadingBlock>())
        {
            var attributes = heading.GetAttributes();
            var id = attributes.Id;
            if (string.IsNullOrEmpty(id) || id is "reader-content" or "reader-top" || headings.Any(h => h.Id == id))
                id = "reader-heading-" + headings.Count;
            attributes.Id = id;
            headings.Add(new HeadingInfo(id, InlineText(heading.Inline), heading.Level));
        }
        // A base de imagens não deve redirecionar as âncoras do próprio documento.
        foreach (var link in document.Descendants().OfType<LinkInline>())
            if (!link.IsImage && link.Url?.StartsWith('#') == true)
                link.Url = "https://reader.mdreader/index.html" + link.Url;
        var markdownHtml = Markdown.ToHtml(document, _pipeline);
        var encodedBase = WebUtility.HtmlEncode(baseHref ?? string.Empty);
        var baseElement = string.IsNullOrEmpty(encodedBase) ? string.Empty : $"<base href=\"{encodedBase}\">";
        var width = settings.ContentWidth == 0 ? "none" : settings.ContentWidth + "px";
        var html = $$"""
            <!doctype html>
            <html lang="pt-BR" id="reader-top">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src https: data:; style-src 'unsafe-inline'; script-src 'none'; base-uri https://files.mdreader; form-action 'none'; frame-src 'none'">
                {{baseElement}}
                <style>{{ReaderAssets.Style}}
                :root { --font-size: {{settings.FontSize}}px; --content-width: {{width}}; }
                .back-to-top { display: block; margin-top: 32px; }
                </style>
            </head>
            <body class="{{(settings.DarkMode ? "dark" : "light")}}">
                <main id="reader-content">{{markdownHtml}}</main>
                <main><a class="back-to-top" href="https://reader.mdreader/index.html#reader-top">↑ Topo</a></main>
            </body>
            </html>
            """;
        return new RenderedMarkdownDocument(html, headings);
    }

    private static string InlineText(ContainerInline? container)
    {
        if (container is null) return string.Empty;
        var text = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal: text.Append(literal.Content); break;
                case CodeInline code: text.Append(code.Content); break;
                case LineBreakInline: text.Append(' '); break;
                case ContainerInline nested: text.Append(InlineText(nested)); break;
            }
        }
        return text.ToString();
    }
}
