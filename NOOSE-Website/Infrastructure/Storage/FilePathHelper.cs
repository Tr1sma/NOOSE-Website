namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Shared path-traversal guard: allows only bare file names and combines them safely with the base path.</summary>
internal static class FilePathHelper
{
    public static string SafePath(string basePath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..")
            || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException($"Ungültiger Dateiname: '{fileName}'.");
        }
        return Path.Combine(basePath, fileName);
    }
}
