using Android.Content;
using Android.Provider;
using MDReader.Core;
using AndroidUri = Android.Net.Uri;

namespace MDReader.Mobile.Services;

public sealed record OpenedMarkdownDocument(string FileName, string Content, string Html, IReadOnlyList<HeadingInfo> Headings);

public sealed class AndroidMarkdownFileReader(ContentResolver resolver)
{
    private readonly MarkdownHtmlRenderer _renderer = new();

    public async Task<OpenedMarkdownDocument> ReadAsync(AndroidUri uri, CancellationToken cancellationToken, ReaderSettings? settings = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileName = GetDisplayName(uri);

        if (!MarkdownContentReader.HasMarkdownExtension(fileName))
        {
            throw new InvalidDataException("Extensão não permitida. Selecione um arquivo .md ou .markdown.");
        }

        using var stream = resolver.OpenInputStream(uri)
            ?? throw new FileNotFoundException("O arquivo selecionado não foi encontrado.");
        var content = await MarkdownContentReader.ReadAsync(stream, fileName, cancellationToken).ConfigureAwait(false);
        var rendered = _renderer.RenderDocument(content, "https://files.mdreader/", settings);
        cancellationToken.ThrowIfCancellationRequested();
        return new OpenedMarkdownDocument(fileName, content, rendered.Html, rendered.Headings);
    }

    private string GetDisplayName(AndroidUri uri)
    {
        using var cursor = resolver.Query(uri, new[] { IOpenableColumns.DisplayName }, null, null, null);

        if (cursor?.MoveToFirst() == true)
        {
            var column = cursor.GetColumnIndex(IOpenableColumns.DisplayName);
            if (column >= 0 && !cursor.IsNull(column))
            {
                return cursor.GetString(column) ?? string.Empty;
            }
        }

        return uri.LastPathSegment ?? string.Empty;
    }
}
