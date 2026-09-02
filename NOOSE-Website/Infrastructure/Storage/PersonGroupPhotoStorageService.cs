using Microsoft.Extensions.Options;

namespace NOOSE_Website.Infrastructure.Storage;

/// <inheritdoc cref="IPersonGroupPhotoStorageService" />
public class PersonGroupPhotoStorageService(IWebHostEnvironment env, IOptions<FileUploadOptions> options)
    : RecordPhotoStorageService(env, options.Value, options.Value.GroupsPath), IPersonGroupPhotoStorageService;
