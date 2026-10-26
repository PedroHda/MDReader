using Android.Content;
using Android.Provider;
using MDReader.Core;
using AndroidUri = Android.Net.Uri;

namespace MDReader.Mobile.Services;

public sealed record FolderItem(string Id, string Name, bool IsDirectory, AndroidUri Uri);

public sealed class DocumentFolderService(ContentResolver resolver)
{
    public List<FolderItem> List(AndroidUri treeUri, string folderId)
    {
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, folderId)!;
        using var cursor = resolver.Query(children, new[] { DocumentsContract.Document.ColumnDocumentId,
            DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType }, null, null, null);
        var entries = new List<FolderItem>();
        while (cursor?.MoveToNext() == true)
        {
            var id = cursor.GetString(0)!;
            var name = cursor.GetString(1) ?? "Sem nome";
            var directory = cursor.GetString(2) == DocumentsContract.Document.MimeTypeDir;
            entries.Add(new FolderItem(id, name, directory, DocumentsContract.BuildDocumentUriUsingTree(treeUri, id)!));
        }
        return entries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public Stream? OpenImage(AndroidUri treeUri, string folderId, string path)
    {
        var segments = LocalResourcePath.ImageSegments(path);
        if (segments is null) return null;
        var current = folderId;
        for (var index = 0; index < segments.Length; index++)
        {
            var item = List(treeUri, current).FirstOrDefault(e => e.Name == segments[index]);
            if (item is null) return null;
            if (index == segments.Length - 1) return item.IsDirectory ? null : resolver.OpenInputStream(item.Uri);
            if (!item.IsDirectory) return null;
            current = item.Id;
        }
        return null;
    }
}
