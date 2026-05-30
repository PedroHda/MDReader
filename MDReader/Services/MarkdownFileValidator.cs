namespace MDReader.Services;

public sealed class MarkdownFileValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown"
    };

    public string ValidatePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new MarkdownFileValidationException("Selecione um arquivo Markdown para abrir.");
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new MarkdownFileValidationException("O caminho do arquivo é inválido.");
        }

        if (!File.Exists(fullPath))
        {
            throw new MarkdownFileValidationException("O arquivo selecionado não existe.");
        }

        var extension = Path.GetExtension(fullPath);

        if (!AllowedExtensions.Contains(extension))
        {
            throw new MarkdownFileValidationException("Extensão não permitida. Selecione um arquivo .md ou .markdown.");
        }

        return fullPath;
    }
}
