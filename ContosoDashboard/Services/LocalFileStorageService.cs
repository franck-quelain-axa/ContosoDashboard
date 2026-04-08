using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
  private readonly string _basePath;
  private readonly ILogger<LocalFileStorageService> _logger;

  public LocalFileStorageService(
      IConfiguration configuration,
      IWebHostEnvironment environment,
      ILogger<LocalFileStorageService> logger)
  {
    var configuredPath = configuration["FileStorage:BasePath"] ?? "AppData/uploads";

    _basePath = Path.IsPathRooted(configuredPath)
        ? configuredPath
        : Path.Combine(environment.ContentRootPath, configuredPath);

    _logger = logger;

    // Ensure base directory exists at startup
    Directory.CreateDirectory(_basePath);
  }

  public async Task<string> UploadAsync(Stream fileContent, string relativePath)
  {
    var absolutePath = GetAbsolutePath(relativePath);

    // Create intermediate directories as needed
    var directory = Path.GetDirectoryName(absolutePath);
    if (directory != null)
      Directory.CreateDirectory(directory);

    await using var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
    await fileContent.CopyToAsync(fileStream);

    _logger.LogInformation("File saved to {RelativePath}", relativePath);
    return relativePath;
  }

  public Task<Stream?> DownloadAsync(string relativePath)
  {
    var absolutePath = GetAbsolutePath(relativePath);

    if (!File.Exists(absolutePath))
    {
      _logger.LogWarning("File not found at {RelativePath}", relativePath);
      return Task.FromResult<Stream?>(null);
    }

    Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    return Task.FromResult<Stream?>(stream);
  }

  public Task DeleteAsync(string relativePath)
  {
    var absolutePath = GetAbsolutePath(relativePath);

    if (File.Exists(absolutePath))
    {
      File.Delete(absolutePath);
      _logger.LogInformation("File deleted at {RelativePath}", relativePath);
    }

    return Task.CompletedTask;
  }

  public Task<string> GetUrlAsync(string relativePath, TimeSpan expiration)
  {
    // Local implementation returns absolute path.
    // Azure implementation would return a SAS URL.
    return Task.FromResult(GetAbsolutePath(relativePath));
  }

  private string GetAbsolutePath(string relativePath)
  {
    // Normalize separators and prevent path traversal
    var normalized = relativePath.Replace('\\', '/').TrimStart('/');
    return Path.GetFullPath(Path.Combine(_basePath, normalized));
  }
}
