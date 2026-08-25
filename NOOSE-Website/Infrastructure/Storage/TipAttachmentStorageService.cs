using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc />
public class TipAttachmentStorageService : ITipAttachmentStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basePath;

    public TipAttachmentStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basePath = Path.IsPathRooted(_options.TipsPath)
            ? _options.TipsPath
            : Path.Combine(env.ContentRootPath, _options.TipsPath);
    }

    public long MaxBytes => _options.MaxBytes;

    /// <summary>Images only: what a witness sends is a photo, and an office document is an attack surface.</summary>
    public bool IsAllowedType(string contentType)
        => _options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_basePath);
        // server-assigned name: the citizen's own file name is kept on the row for display only
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
