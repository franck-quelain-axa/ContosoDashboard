namespace ContosoDashboard.Services;

public interface IFileScanner
{
  /// <summary>
  /// Scans the provided stream for malware.
  /// Stream position is reset to 0 before scanning and left at 0 after.
  /// </summary>
  Task<ScanResult> ScanAsync(Stream fileContent, string fileName);
}

public enum ScanResult
{
  Clean,
  Infected,
  ScannerUnavailable
}
