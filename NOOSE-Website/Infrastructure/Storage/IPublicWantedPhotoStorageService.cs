namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Storage for the photo copies of public wanted notices; separate base path, so the anonymous delivery endpoint can never reach an internal file.</summary>
public interface IPublicWantedPhotoStorageService
{
    long MaxBytes { get; }

    bool IsAllowedType(string contentType);

    /// <summary>Saves the content and returns the server-assigned file name.</summary>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading; caller disposes the stream.</summary>
    Stream OpenRead(string fileNameSaved);

    void Delete(string fileNameSaved);
}
