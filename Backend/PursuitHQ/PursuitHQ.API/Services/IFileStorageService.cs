namespace PursuitHQ.API.Services
{
    /// <summary>
    /// Where uploaded files physically live.
    ///
    /// Everything goes through this interface so the storage location can change
    /// (local disk now, cloud object storage in production) without touching a
    /// single entity, controller, or migration.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Saves a file and returns the internal storage key. The key is
        /// GUID-based and is never shown to the client.
        /// </summary>
        Task<string> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);

        /// <summary>Opens a stored file for reading.</summary>
        Task<Stream> OpenAsync(string storedPath, CancellationToken ct = default);

        /// <summary>Deletes a stored file. Does not throw if it is already gone.</summary>
        Task DeleteAsync(string storedPath, CancellationToken ct = default);
    }
}
