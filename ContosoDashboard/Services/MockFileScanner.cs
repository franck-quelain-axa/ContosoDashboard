namespace ContosoDashboard.Services;

/// <summary>
/// Training-only virus scanner stub. Always returns Clean.
/// Swap for a real implementation (e.g. Windows Defender ATP, ClamAV) in production
/// by registering a different IFileScanner in Program.cs.
/// </summary>
public class MockFileScanner : IFileScanner
{
  private readonly ILogger<MockFileScanner> _logger;

  public MockFileScanner(ILogger<MockFileScanner> logger)
  {
    _logger = logger;
  }

  public Task<ScanResult> ScanAsync(Stream fileContent, string fileName)
  {
    _logger.LogDebug("[MockFileScanner] Scan bypassed in training environment for: {FileName}", fileName);

    // Reset stream position so the caller can read it after scanning
    if (fileContent.CanSeek)
      fileContent.Position = 0;

    return Task.FromResult(ScanResult.Clean);
  }
}
