namespace MDReader.Core;

public static class ReaderAssets
{
    public static string Style { get; } = Read("reader.css");
    public static string Script { get; } = Read("reader.js");

    private static string Read(string name)
    {
        using var stream = typeof(ReaderAssets).Assembly.GetManifestResourceStream("MDReader.Core.Assets." + name)
            ?? throw new InvalidOperationException("Recurso de leitura não encontrado: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
