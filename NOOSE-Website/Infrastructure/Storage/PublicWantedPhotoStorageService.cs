using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc />
public class PublicWantedPhotoStorageService : IPublicWantedPhotoStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basePath;

    public PublicWantedPhotoStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basePath = Path.IsPathRooted(_options.WantedPath)
            ? _options.WantedPath
            : Path.Combine(env.ContentRootPath, _options.WantedPath);
    }

    public long MaxBytes => _options.MaxBytes;

    public bool IsAllowedType(string contentType)
        => _options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_basePath);
        var fileName = $"{Guid.NewGuid():N}{ExtensionFor(contentType)}";
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

    private string SafePath(string fileName) => FilePathHelper.SafePath(_basePath, fileName);

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin",
    };
}
