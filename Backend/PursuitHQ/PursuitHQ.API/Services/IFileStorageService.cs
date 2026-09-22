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

        /// <summary>
        /// Can this actually store anything? Returns null when yes, or a short
        /// explanation when no. Never throws.
        ///
        /// Called once at startup and logged, because the alternative is what
        /// happened here: storage was misconfigured, nothing said so, and the
        /// first anyone knew of it was a student picking a file and watching it
        /// fail. A credential problem is a deployment problem and should be
        /// visible at deployment time.
        /// </summary>
        Task<string?> CheckAsync(CancellationToken ct = default);
    }
}
