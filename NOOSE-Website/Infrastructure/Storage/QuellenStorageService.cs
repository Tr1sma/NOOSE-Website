using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc cref="IQuellenStorageService" />
public class QuellenStorageService : IQuellenStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basisPfad;

    public QuellenStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basisPfad = Path.IsPathRooted(_options.QuellenPfad)
            ? _options.QuellenPfad
            : Path.Combine(env.ContentRootPath, _options.QuellenPfad);
    }

    public long MaxBytes => _options.QuellenMaxBytes;

    public bool IstErlaubterTyp(string contentType)
        => _options.ErlaubteQuellenContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public async Task<string> SpeichernAsync(Stream inhalt, string originalName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_basisPfad);
        var dateiname = $"{Guid.NewGuid():N}{SichereEndung(originalName)}";
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

    /// <summary>Übernimmt nur eine einfache, gefahrlose Endung aus dem Originalnamen (sonst „.bin").</summary>
    private static string SichereEndung(string originalName)
    {
        var endung = Path.GetExtension(originalName);
        if (string.IsNullOrEmpty(endung) || endung.Length > 12
            || endung.Skip(1).Any(c => !char.IsLetterOrDigit(c)))
        {
            return ".bin";
        }
        return endung.ToLowerInvariant();
    }
}
