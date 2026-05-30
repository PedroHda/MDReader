namespace MDReader.Models;

public sealed class MarkdownDocument
{
    public MarkdownDocument(string filePath, string content)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Content = content;
    }

    public string FilePath { get; }

    public string FileName { get; }

    public string Content { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Content);
}
