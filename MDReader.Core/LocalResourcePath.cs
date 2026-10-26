namespace MDReader.Core;

public static class LocalResourcePath
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".svg", ".avif"
    };

    public static string[]? ImageSegments(string relativePath)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(relativePath);
            if (decoded.Length > 2048 || decoded.IndexOfAny(new[] { '\\', '\0', ':' }) >= 0) return null;
            var segments = decoded.TrimStart('/').Split('/');
            if (segments.Length == 0 || segments.Any(s => string.IsNullOrWhiteSpace(s) || s is "." or "..")) return null;
            return ImageExtensions.Contains(Path.GetExtension(segments[^1])) ? segments : null;
        }
        catch (UriFormatException) { return null; }
    }

    public static string? ResolveImage(string directory, string relativePath)
    {
        var segments = ImageSegments(relativePath);
        if (segments is null) return null;
        var root = Path.GetFullPath(directory);
        if (!Path.EndsInDirectorySeparator(root)) root += Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        return path.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? path : null;
    }

    public static string MimeType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif",
        ".webp" => "image/webp", ".svg" => "image/svg+xml", ".avif" => "image/avif",
        ".bmp" => "image/bmp", _ => "image/x-icon"
    };
}
