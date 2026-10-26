using System.Text.Json;

namespace MDReader.Core;

public sealed class ReaderStateStore
{
    private readonly string _path;
    public ReaderState State { get; }
    public string? LastError { get; private set; }
    public IEnumerable<RecentDocument> Recent => State.Documents.OrderByDescending(d => d.Favorite).ThenByDescending(d => d.LastOpened);

    public ReaderStateStore(string path)
    {
        _path = path;
        State = Load();
        State.Settings ??= new ReaderSettings();
        State.Settings.Normalize();
        State.Documents ??= new List<RecentDocument>();
        State.Documents = State.Documents.Where(d => d is not null && !string.IsNullOrWhiteSpace(d.Location))
            .DistinctBy(d => d.Location, StringComparer.Ordinal).Take(100).ToList();
        foreach (var document in State.Documents)
        {
            document.PreviewPosition = ClampPosition(document.PreviewPosition);
            document.RawPosition = ClampPosition(document.RawPosition);
        }
    }

    private ReaderState Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                if (new FileInfo(_path).Length > 1024 * 1024) throw new InvalidDataException("Histórico muito grande.");
                return JsonSerializer.Deserialize<ReaderState>(File.ReadAllText(_path)) ?? new ReaderState();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastError = "Não foi possível carregar as preferências. Os documentos não foram alterados.";
        }
        return new ReaderState();
    }

    public RecentDocument Open(string location, string fileName)
    {
        var document = Find(location);
        if (document is null)
        {
            document = new RecentDocument { Location = location };
            State.Documents.Add(document);
        }
        document.FileName = fileName;
        document.LastOpened = DateTimeOffset.UtcNow;
        State.LastDocument = location;
        State.Documents = Recent.Take(100).ToList();
        Save();
        return document;
    }

    public RecentDocument? Find(string? location) => State.Documents.FirstOrDefault(d => d.Location == location);

    public bool Save()
    {
        try
        {
            State.Settings.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, _path, overwrite: true);
            LastError = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastError = "Não foi possível salvar as preferências. Os documentos não foram alterados.";
            return false;
        }
    }

    public static double ClampPosition(double position) => double.IsFinite(position) ? Math.Clamp(position, 0, 1) : 0;
}
