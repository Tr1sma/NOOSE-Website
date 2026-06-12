using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc cref="IBibliothekStorageService" />
public class BibliothekStorageService : IBibliothekStorageService
{
    private readonly FileUploadOptions _options;
    private readonly string _basisPfad;

    public BibliothekStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _basisPfad = Path.IsPathRooted(_options.BibliothekPfad)
            ? _options.BibliothekPfad
            : Path.Combine(env.ContentRootPath, _options.BibliothekPfad);
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
        => File.OpenRead(DateiPfadHelfer.SichererPfad(_basisPfad, dateinameGespeichert));

    public void Loeschen(string dateinameGespeichert)
    {
        var pfad = DateiPfadHelfer.SichererPfad(_basisPfad, dateinameGespeichert);
        if (File.Exists(pfad))
        {
            File.Delete(pfad);
        }
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
