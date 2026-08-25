namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Storage for citizen tip attachments; own base path, so the delivery endpoint can never reach an internal file.</summary>
public interface ITipAttachmentStorageService
{
    long MaxBytes { get; }

    bool IsAllowedType(string contentType);

    /// <summary>Saves the content and returns the server-assigned file name.</summary>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading; caller disposes the stream.</summary>
    Stream OpenRead(string fileNameSaved);

    void Delete(string fileNameSaved);
}
