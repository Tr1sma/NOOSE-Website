using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc />
public class LibraryStorageService : ILibraryStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basePath;

    public LibraryStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basePath = Path.IsPathRooted(_options.LibraryPath)
            ? _options.LibraryPath
            : Path.Combine(env.ContentRootPath, _options.LibraryPath);
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
        => File.OpenRead(FilePathHelper.SafePath(_basePath, fileNameSaved));

    public void Delete(string fileNameSaved)
    {
        var path = FilePathHelper.SafePath(_basePath, fileNameSaved);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Takes only a simple, harmless extension from the original name (else ".bin").</summary>
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
