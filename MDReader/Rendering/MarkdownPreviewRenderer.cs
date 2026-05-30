using System.Net;
using Markdig;

namespace MDReader.Rendering;

public sealed class MarkdownPreviewRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public void Render(WebBrowser target, string markdown, string filePath)
    {
        target.DocumentText = BuildHtmlDocument(markdown, filePath);
    }

    internal string BuildHtmlDocument(string markdown, string filePath)
    {
        var markdownHtml = Markdown.ToHtml(markdown, _pipeline);
        var baseHref = GetBaseHref(filePath);

        return $$"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
                <meta charset="utf-8">
                <meta http-equiv="X-UA-Compatible" content="IE=edge">
                <base href="{{baseHref}}">
                <style>
                    :root {
                        color-scheme: light;
                    }

                    body {
                        margin: 0;
                        padding: 24px 28px 40px;
                        color: #202327;
                        background: #ffffff;
                        font-family: "Segoe UI", Arial, sans-serif;
                        font-size: 16px;
                        line-height: 1.58;
                    }

                    h1,
                    h2,
                    h3,
                    h4,
                    h5,
                    h6 {
                        color: #143a5d;
                        line-height: 1.25;
                        margin: 1.2em 0 0.55em;
                    }

                    h1 {
                        font-size: 2em;
                        border-bottom: 1px solid #d7dee8;
                        padding-bottom: 0.25em;
                    }

                    h2 {
                        font-size: 1.55em;
                        border-bottom: 1px solid #e5eaf0;
                        padding-bottom: 0.2em;
                    }

                    h3 {
                        font-size: 1.3em;
                    }

                    p,
                    ul,
                    ol,
                    blockquote,
                    pre,
                    table {
                        margin-top: 0;
                        margin-bottom: 1em;
                    }

                    ul,
                    ol {
                        padding-left: 1.7em;
                    }

                    li {
                        margin: 0.25em 0;
                    }

                    blockquote {
                        border-left: 4px solid #9ab3cc;
                        color: #53606c;
                        padding: 0.1em 1em;
                        background: #f5f8fb;
                    }

                    code {
                        color: #18202a;
                        background: #eef2f6;
                        border-radius: 4px;
                        padding: 0.1em 0.35em;
                        font-family: Consolas, "Courier New", monospace;
                        font-size: 0.94em;
                    }

                    pre {
                        background: #f1f4f8;
                        border: 1px solid #d9e1ea;
                        border-radius: 6px;
                        overflow-x: auto;
                        padding: 14px 16px;
                    }

                    pre code {
                        display: block;
                        background: transparent;
                        padding: 0;
                        white-space: pre;
                    }

                    a {
                        color: #0f5c9c;
                        text-decoration: none;
                    }

                    a:hover {
                        text-decoration: underline;
                    }

                    table {
                        border-collapse: collapse;
                        width: 100%;
                    }

                    th,
                    td {
                        border: 1px solid #d4dde7;
                        padding: 8px 10px;
                        vertical-align: top;
                    }

                    th {
                        background: #edf3f9;
                        color: #143a5d;
                        font-weight: 700;
                    }

                    tr:nth-child(even) td {
                        background: #fafcfe;
                    }

                    hr {
                        border: 0;
                        border-top: 1px solid #d7dee8;
                        margin: 1.5em 0;
                    }

                    img {
                        max-width: 100%;
                        height: auto;
                    }

                    input[type="checkbox"] {
                        margin-right: 0.45em;
                    }
                </style>
            </head>
            <body>
                {{markdownHtml}}
            </body>
            </html>
            """;
    }

    private static string GetBaseHref(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        var directoryUri = new Uri(directory + Path.DirectorySeparatorChar);
        return WebUtility.HtmlEncode(directoryUri.AbsoluteUri);
    }
}
