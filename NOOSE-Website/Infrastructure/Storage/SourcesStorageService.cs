using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc cref="IQuellenStorageService" />
public class SourcesStorageService : ISourcesStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basePath;

    public SourcesStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basePath = Path.IsPathRooted(_options.SourcesPath)
            ? _options.SourcesPath
            : Path.Combine(env.ContentRootPath, _options.SourcesPath);
    }

    public long MaxBytes => _options.SourcesMaxBytes;

    public bool IsAllowedType(string contentType)
        => _options.AllowedSourcesContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public async Task<string> SaveAsync(Stream content, string originalName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_basePath);
        var fileName = $"{Guid.NewGuid():N}{SafeExtension(originalName)}";
        var target = Path.Combine(_basePath, fileName);

        await using var fs = File.Create(target);
        await content.CopyToAsync(fs, cancellationToken);
        return fileName;
    }

    public Stream OpenRead(string fileNameSaved)
        => File.OpenRead(SafePath(fileNameSaved));

    public void Delete(string fileNameSaved)
    {
        var path = SafePath(fileNameSaved);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Lässt nur blanke Dateinamen zu und kombiniert sie sicher mit dem Basispfad.</summary>
    private string SafePath(string fileName) => FilePathHelper.SafePath(_basePath, fileName);

    /// <summary>Übernimmt nur eine einfache, gefahrlose Endung aus dem Originalnamen (sonst „.bin").</summary>
    private static string SafeExtension(string originalName)
    {
        var extension = Path.GetExtension(originalName);
        if (string.IsNullOrEmpty(extension) || extension.Length > 12
            || extension.Skip(1).Any(c => !char.IsLetterOrDigit(c)))
        {
            return ".bin";
        }
        return extension.ToLowerInvariant();
    }
}
