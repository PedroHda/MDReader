using System.Text;

namespace MDReader.Core;

public static class MarkdownContentReader
{
    public const int MaximumBytes = 5 * 1024 * 1024;

    public static bool HasMarkdownExtension(string? fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<string> ReadAsync(Stream source, string fileName, CancellationToken cancellationToken = default)
    {
        if (!HasMarkdownExtension(fileName))
        {
            throw new InvalidDataException("Extensão não permitida. Selecione um arquivo .md ou .markdown.");
        }

        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (content.Length + bytesRead > MaximumBytes)
            {
                throw new InvalidDataException("O arquivo é muito grande. No Android, o limite é de 5 MB.");
            }

            content.Write(buffer, 0, bytesRead);
        }

        cancellationToken.ThrowIfCancellationRequested();
        content.Position = 0;
        using var reader = new StreamReader(content, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync().ConfigureAwait(false);

        if (text.Contains('\0'))
        {
            throw new InvalidDataException("O arquivo parece conter dados binários. Selecione um documento Markdown de texto.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return text;
    }
}
