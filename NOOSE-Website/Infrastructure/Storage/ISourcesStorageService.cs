namespace NOOSE_Website.Infrastructure.Storage;

/// <summary>Source-attachment storage outside wwwroot; server-assigned file names, read/delete accept bare names only (path-traversal guard).</summary>
public interface ISourcesStorageService
{
    long MaxBytes { get; }

    bool IsAllowedType(string contentType);

    /// <summary>Saves the content and returns the server-assigned file name; extension taken safely from the original name.</summary>
    Task<string> SaveAsync(Stream content, string originalName, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading; caller disposes the stream.</summary>
    Stream OpenRead(string fileNameSaved);

    void Delete(string fileNameSaved);
}
