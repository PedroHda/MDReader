namespace MDReader.Services;

public abstract class MarkdownFileException : Exception
{
    protected MarkdownFileException(string message)
        : base(message)
    {
    }

    protected MarkdownFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class MarkdownFileValidationException : MarkdownFileException
{
    public MarkdownFileValidationException(string message)
        : base(message)
    {
    }
}

public sealed class MarkdownFileReadException : MarkdownFileException
{
    public MarkdownFileReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
