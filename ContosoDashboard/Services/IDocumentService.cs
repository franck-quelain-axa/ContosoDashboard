using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

// ── Request / Filter DTOs ────────────────────────────────────────────────────

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
    int RecipientId
);

public record DocumentFilter(
    string? Category = null,
    int? ProjectId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SortBy = "UploadedDate",
    bool SortDescending = true
);

public record ActivityLogFilter(
    int? DocumentId = null,
    int? ActorUserId = null,
    DocumentActivityType? ActivityType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

// ── Exception ────────────────────────────────────────────────────────────────

public class DocumentUploadException : Exception
{
  public DocumentUploadException(string message) : base(message) { }
}

// ── Service interface ─────────────────────────────────────────────────────────

public interface IDocumentService
{
  // Upload
  Task<Document> UploadDocumentAsync(UploadDocumentRequest request, int requestingUserId);

  // Browse / Retrieve
  Task<List<Document>> GetMyDocumentsAsync(int userId, DocumentFilter? filter = null);
  Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
  Task<(Stream? FileStream, Document? Document)> GetFileStreamAsync(int documentId, int requestingUserId);

  // Search
  Task<List<Document>> SearchDocumentsAsync(string query, int requestingUserId);

  // Metadata edit
  Task<Document> UpdateMetadataAsync(int documentId, UpdateDocumentMetadataRequest request, int requestingUserId);
  Task<Document> ReplaceFileAsync(int documentId, ReplaceFileRequest request, int requestingUserId);

  // Delete
  Task DeleteDocumentAsync(int documentId, int requestingUserId);

  // Share
  Task ShareDocumentAsync(int documentId, ShareDocumentRequest request, int requestingUserId);

  // Task attachment
  Task AttachToTaskAsync(int documentId, int taskId, int requestingUserId);

  // Shared with me
  Task<List<Document>> GetSharedWithMeAsync(int userId);

  // Dashboard
  Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5);

  // Admin / Reporting
  Task<List<ActivityLog>> GetActivityLogsAsync(int requestingUserId, ActivityLogFilter? filter = null);
  Task TransferOwnershipAsync(int deactivatedUserId);

  // Authorization helper
  Task<bool> CanUserAccessDocumentAsync(int documentId, int userId);
}
