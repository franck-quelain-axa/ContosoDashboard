namespace ContosoDashboard.Services;

public interface IFileStorageService
{
  /// <summary>
  /// Persists a file stream at the given relative path and returns that path.
  /// The path follows the pattern: {userId}/{projectId-or-"personal"}/{guid}.{ext}
  /// </summary>
  Task<string> UploadAsync(Stream fileContent, string relativePath);

  /// <summary>
  /// Opens a read stream for a previously stored file.
  /// Returns null if the file does not exist.
  /// </summary>
  Task<Stream?> DownloadAsync(string relativePath);

  /// <summary>
  /// Permanently deletes the file at the given path. No-op if the file does not exist.
  /// </summary>
  Task DeleteAsync(string relativePath);

  /// <summary>
  /// Returns a URL or local path suitable for serving the file.
  /// Local: absolute filesystem path. Azure: SAS URL.
  /// </summary>
  Task<string> GetUrlAsync(string relativePath, TimeSpan expiration);
}
