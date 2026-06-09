using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc cref="IFileStorageService" />
public class FileStorageService : IFileStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basisPfad;

    public FileStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basisPfad = Path.IsPathRooted(_options.PersonenPfad)
            ? _options.PersonenPfad
            : Path.Combine(env.ContentRootPath, _options.PersonenPfad);
    }

    public long MaxBytes => _options.MaxBytes;

    public bool IstErlaubterTyp(string contentType)
        => _options.ErlaubteContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public async Task<string> SpeichernAsync(Stream inhalt, string contentType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_basisPfad);
        var dateiname = $"{Guid.NewGuid():N}{EndungFuer(contentType)}";
        var ziel = Path.Combine(_basisPfad, dateiname);

        await using var fs = File.Create(ziel);
        await inhalt.CopyToAsync(fs, cancellationToken);
        return dateiname;
    }

    public Stream OeffnenLesen(string dateinameGespeichert)
        => File.OpenRead(SichererPfad(dateinameGespeichert));

    public void Loeschen(string dateinameGespeichert)
    {
        var pfad = SichererPfad(dateinameGespeichert);
        if (File.Exists(pfad))
        {
            File.Delete(pfad);
        }
    }

    /// <summary>Lässt nur blanke Dateinamen zu und kombiniert sie sicher mit dem Basispfad.</summary>
    private string SichererPfad(string dateiname)
    {
        if (string.IsNullOrWhiteSpace(dateiname)
            || dateiname.Contains('/') || dateiname.Contains('\\') || dateiname.Contains("..")
            || Path.GetFileName(dateiname) != dateiname)
        {
            throw new InvalidOperationException($"Ungültiger Dateiname: '{dateiname}'.");
        }
        return Path.Combine(_basisPfad, dateiname);
    }

    private static string EndungFuer(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin",
    };
}
