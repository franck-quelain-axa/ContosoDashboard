# Contract: Service Interfaces

**Branch**: `001-document-upload-management` | **Date**: 2026-04-07  
**Informed by**: [research.md](../research.md), [data-model.md](../data-model.md)

These interfaces define the contracts between the Blazor presentation layer (pages/components) and the business logic / infrastructure layers. All implementations are registered in DI (`Program.cs`).

---

## `IFileStorageService`

Abstracts filesystem I/O. `LocalFileStorageService` is the training implementation. A future `AzureBlobStorageService` swaps in without any other code changes.

```csharp
// Services/IFileStorageService.cs
public interface IFileStorageService
{
    /// <summary>
    /// Persists a file stream and returns its relative storage path.
    /// The path follows the pattern: {userId}/{projectId-or-"personal"}/{guid}.{ext}
    /// </summary>
    Task<string> UploadAsync(Stream fileContent, string relativePath);

    /// <summary>
    /// Opens a read stream for a previously stored file.
    /// Returns null if the file does not exist at the given path.
    /// </summary>
    Task<Stream?> DownloadAsync(string relativePath);

    /// <summary>
    /// Permanently deletes the file at the given path.
    /// No-op if the file does not exist (idempotent).
    /// </summary>
    Task DeleteAsync(string relativePath);

    /// <summary>
    /// Returns a URL or local path suitable for serving the file.
    /// For LocalFileStorageService this is the absolute filesystem path.
    /// For AzureBlobStorageService this will be a SAS URL.
    /// </summary>
    Task<string> GetUrlAsync(string relativePath, TimeSpan expiration);
}
```

**LocalFileStorageService behaviour**:
- Base directory resolved from `IConfiguration["FileStorage:BasePath"]` (default: `AppData/uploads` relative to `ContentRootPath`)
- Directory created on application start if absent
- `UploadAsync`: writes stream to `{basePath}/{relativePath}`, creating intermediate directories as needed
- `DownloadAsync`: opens `FileStream` in `FileMode.Open, FileAccess.Read`
- `DeleteAsync`: calls `File.Delete` if file exists
- `GetUrlAsync`: returns absolute path (unused in training; required by interface for future migration)

---

## `IFileScanner`

Abstracts virus/malware scanning. `MockFileScanner` is the training implementation.

```csharp
// Services/IFileScanner.cs
public interface IFileScanner
{
    /// <summary>
    /// Scans the provided stream for malware.
    /// The stream position is reset to 0 before scanning and left at 0 after.
    /// </summary>
    Task<ScanResult> ScanAsync(Stream fileContent, string fileName);
}

public enum ScanResult
{
    Clean,
    Infected,
    ScannerUnavailable  // scanner service itself could not be reached
}
```

**MockFileScanner behaviour**:
- Always returns `ScanResult.Clean`
- Logs a debug message: `"[MockFileScanner] Scan bypassed in training environment for: {fileName}"`

**Caller contract** (enforced in `DocumentService`):
- `Infected` → reject upload, throw `DocumentUploadException("File failed security scan.")`
- `ScannerUnavailable` → reject upload, throw `DocumentUploadException("Document scanning service unavailable. Try again shortly.")` (FR-030 — scanner unavailability blocks upload)

---

## `IDocumentService`

The primary business logic interface. Orchestrates upload workflow, authorization, search, and notifications.

```csharp
// Services/IDocumentService.cs (partial — key methods)
public interface IDocumentService
{
    // ── Upload ──────────────────────────────────────────────────
    /// <summary>
    /// Validates, scans, stores, and records a new document.
    /// Throws DocumentUploadException for validation/scan failures.
    /// </summary>
    Task<Document> UploadDocumentAsync(UploadDocumentRequest request, int requestingUserId);

    // ── Browse / Retrieve ────────────────────────────────────────
    Task<List<Document>> GetMyDocumentsAsync(int userId, DocumentFilter? filter = null);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<(Stream? FileStream, Document? Document)> GetFileStreamAsync(int documentId, int requestingUserId);

    // ── Search ───────────────────────────────────────────────────
    Task<List<Document>> SearchDocumentsAsync(string query, int requestingUserId);

    // ── Metadata edit ────────────────────────────────────────────
    Task<Document> UpdateMetadataAsync(int documentId, UpdateDocumentMetadataRequest request, int requestingUserId);
    Task<Document> ReplaceFileAsync(int documentId, ReplaceFileRequest request, int requestingUserId);

    // ── Delete ───────────────────────────────────────────────────
    Task DeleteDocumentAsync(int documentId, int requestingUserId);

    // ── Share ────────────────────────────────────────────────────
    Task ShareDocumentAsync(int documentId, ShareDocumentRequest request, int requestingUserId);

    // ── Task attachment ──────────────────────────────────────────
    Task AttachToTaskAsync(int documentId, int taskId, int requestingUserId);

    // ── Dashboard ────────────────────────────────────────────────
    Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5);

    // ── Admin / Reporting ────────────────────────────────────────
    Task<List<ActivityLog>> GetActivityLogsAsync(int requestingUserId, ActivityLogFilter? filter = null);
    Task TransferOwnershipAsync(int deactivatedUserId);

    // ── Authorization helper ─────────────────────────────────────
    Task<bool> CanUserAccessDocumentAsync(int documentId, int userId);
}
```

### Supporting Request / Filter DTOs

```csharp
public record UploadDocumentRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags,
    int? ProjectId,
    int? TaskId,
    Stream FileContent,
    string OriginalFileName,
    string MimeType,
    long FileSize
);

public record UpdateDocumentMetadataRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags
);

public record ReplaceFileRequest(
    Stream FileContent,
    string OriginalFileName,
    string MimeType,
    long FileSize
);

public record ShareDocumentRequest(
    ShareRecipientType RecipientType,
    int RecipientId   // UserId or ProjectId depending on RecipientType
);

public record DocumentFilter(
    string? Category = null,
    int? ProjectId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SortBy = "UploadedDate",     // "Title" | "UploadedDate" | "Category" | "FileSize"
    bool SortDescending = true
);

public record ActivityLogFilter(
    int? DocumentId = null,
    int? ActorUserId = null,
    DocumentActivityType? ActivityType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);
```

### `DocumentUploadException`

Signals a user-facing upload failure (validation, scan, auth). The calling Blazor component catches this and displays the message directly to the user.

```csharp
public class DocumentUploadException : Exception
{
    public DocumentUploadException(string message) : base(message) { }
}
```

---

## Accepted File Types (Whitelist)

Enforced in `DocumentService` before the scanner is called.

| Extension | MIME Type |
|-----------|-----------|
| `.pdf` | `application/pdf` |
| `.doc` | `application/msword` |
| `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| `.xls` | `application/vnd.ms-excel` |
| `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| `.ppt` | `application/vnd.ms-powerpoint` |
| `.pptx` | `application/vnd.openxmlformats-officedocument.presentationml.presentation` |
| `.txt` | `text/plain` |
| `.jpg` / `.jpeg` | `image/jpeg` |
| `.png` | `image/png` |

Validation checks **both** extension and MIME type. Mismatch is treated as a failed validation.
