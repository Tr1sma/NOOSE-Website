namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Storage for the central file library, outside wwwroot; same type/size rules as source attachments.</summary>
public interface ILibraryStorageService
{
    long MaxBytes { get; }

    bool IsAllowedType(string contentType);

    /// <summary>Saves the content and returns the server-assigned file name.</summary>
    Task<string> SaveAsync(Stream content, string originalName, CancellationToken cancellationToken = default);

    Stream OpenRead(string fileNameSaved);

    void Delete(string fileNameSaved);
}
