using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public class DocumentService : IDocumentService
{
  private readonly ApplicationDbContext _context;
  private readonly IFileStorageService _fileStorage;
  private readonly IFileScanner _fileScanner;
  private readonly INotificationService _notificationService;
  private readonly ILogger<DocumentService> _logger;

  // Accepted file types: extension → MIME type
  private static readonly Dictionary<string, string> AllowedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf",  "application/pdf" },
        { ".doc",  "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls",  "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt",  "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".txt",  "text/plain" },
        { ".jpg",  "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png",  "image/png" },
    };

  private const long MaxFileSizeBytes = 25L * 1024 * 1024; // 25 MB

  public DocumentService(
      ApplicationDbContext context,
      IFileStorageService fileStorage,
      IFileScanner fileScanner,
      INotificationService notificationService,
      ILogger<DocumentService> logger)
  {
    _context = context;
    _fileStorage = fileStorage;
    _fileScanner = fileScanner;
    _notificationService = notificationService;
    _logger = logger;
  }

  // ── Upload ────────────────────────────────────────────────────────────────

  public async Task<Document> UploadDocumentAsync(UploadDocumentRequest request, int requestingUserId)
  {
    // 1. Validate file extension
    var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
    if (!AllowedFileTypes.ContainsKey(extension))
      throw new DocumentUploadException(
          $"File type '{extension}' is not allowed. Accepted types: {string.Join(", ", AllowedFileTypes.Keys)}");

    // 2. Validate MIME type matches extension
    var expectedMime = AllowedFileTypes[extension];
    if (!string.IsNullOrEmpty(request.MimeType) &&
        !request.MimeType.StartsWith(expectedMime, StringComparison.OrdinalIgnoreCase))
      throw new DocumentUploadException("File content does not match the declared file type.");

    // 3. Validate file size
    if (request.FileSize <= 0 || request.FileSize > MaxFileSizeBytes)
      throw new DocumentUploadException(
          $"File size must be between 1 byte and 25 MB. Provided: {request.FileSize:N0} bytes.");

    // 4. Validate category
    if (!DocumentCategories.All.Contains(request.Category))
      throw new DocumentUploadException(
          $"Invalid category. Choose from: {string.Join(", ", DocumentCategories.All)}");

    // 5. Authorize: if ProjectId is specified, user must be a project member
    if (request.ProjectId.HasValue)
    {
      var isMember = await _context.ProjectMembers
          .AnyAsync(pm => pm.ProjectId == request.ProjectId.Value && pm.UserId == requestingUserId);
      var isProjectManager = await _context.Projects
          .AnyAsync(p => p.ProjectId == request.ProjectId.Value && p.ProjectManagerId == requestingUserId);
      var requestingUser = await _context.Users.FindAsync(requestingUserId);
      var isAdmin = requestingUser?.Role == UserRole.Administrator;

      if (!isMember && !isProjectManager && !isAdmin)
        throw new DocumentUploadException("You are not a member of the specified project.");
    }

    // 6. Virus scan
    var scanResult = await _fileScanner.ScanAsync(request.FileContent, request.OriginalFileName);
    switch (scanResult)
    {
      case ScanResult.Infected:
        throw new DocumentUploadException("File failed the security scan and cannot be uploaded.");
      case ScanResult.ScannerUnavailable:
        throw new DocumentUploadException("Document scanning service is unavailable. Please try again shortly.");
    }

    // 7. Generate unique GUID-based file path (R-006)
    var projectSegment = request.ProjectId.HasValue
        ? request.ProjectId.Value.ToString()
        : "personal";
    var uniqueFileName = $"{Guid.NewGuid()}{extension}";
    var relativePath = $"{requestingUserId}/{projectSegment}/{uniqueFileName}";

    // 8. Save file to disk
    request.FileContent.Position = 0;
    await _fileStorage.UploadAsync(request.FileContent, relativePath);

    // 9. Save metadata to database
    var document = new Document
    {
      Title = request.Title.Trim(),
      Description = request.Description?.Trim(),
      Category = request.Category,
      Tags = request.Tags?.Trim(),
      UploadedByUserId = requestingUserId,
      ProjectId = request.ProjectId,
      TaskId = request.TaskId,
      FilePath = relativePath,
      OriginalFileName = request.OriginalFileName,
      FileType = request.MimeType,
      FileSize = request.FileSize,
      UploadedDate = DateTime.UtcNow,
    };

    _context.Documents.Add(document);
    await _context.SaveChangesAsync();

    // 10. Activity log
    await LogActivityAsync(document.DocumentId, requestingUserId, DocumentActivityType.Uploaded);

    // 11. Notify project members (FR-026)
    if (request.ProjectId.HasValue)
    {
      await NotifyProjectMembersAsync(request.ProjectId.Value, requestingUserId, document);
    }

    _logger.LogInformation("Document {DocumentId} uploaded by user {UserId}", document.DocumentId, requestingUserId);
    return document;
  }

  // ── Authorization ─────────────────────────────────────────────────────────

  public async Task<bool> CanUserAccessDocumentAsync(int documentId, int userId)
  {
    var user = await _context.Users.FindAsync(userId);
    if (user == null) return false;

    // Administrators have full access
    if (user.Role == UserRole.Administrator) return true;

    var document = await _context.Documents.FindAsync(documentId);
    if (document == null) return false;

    // 1. Owner
    if (document.UploadedByUserId == userId) return true;

    // 2. Shared directly with this user
    var sharedDirectly = await _context.DocumentShares.AnyAsync(s =>
        s.DocumentId == documentId &&
        s.RecipientType == ShareRecipientType.User &&
        s.RecipientId == userId);
    if (sharedDirectly) return true;

    // Get user's project memberships once
    var userProjectIds = await _context.ProjectMembers
        .Where(pm => pm.UserId == userId)
        .Select(pm => pm.ProjectId)
        .ToListAsync();

    // 3. Shared with a project the user belongs to
    var sharedViaProject = await _context.DocumentShares.AnyAsync(s =>
        s.DocumentId == documentId &&
        s.RecipientType == ShareRecipientType.Project &&
        userProjectIds.Contains(s.RecipientId));
    if (sharedViaProject) return true;

    // 4. Document belongs to a project the user is a member of
    if (document.ProjectId.HasValue && userProjectIds.Contains(document.ProjectId.Value))
      return true;

    // 5. Team Lead — read access to documents uploaded by team members (R-003)
    if (user.Role == UserRole.TeamLead)
    {
      // Team = users who are project members in projects where this user is TeamLead
      var ledProjectIds = await _context.ProjectMembers
          .Where(pm => pm.UserId == userId && pm.Role == "TeamLead")
          .Select(pm => pm.ProjectId)
          .ToListAsync();

      if (ledProjectIds.Count > 0)
      {
        var teamMemberIds = await _context.ProjectMembers
            .Where(pm => ledProjectIds.Contains(pm.ProjectId) && pm.UserId != userId)
            .Select(pm => pm.UserId)
            .Distinct()
            .ToListAsync();

        if (teamMemberIds.Contains(document.UploadedByUserId))
          return true;
      }
    }

    return false;
  }

  // ── Browse ────────────────────────────────────────────────────────────────

  public async Task<List<Document>> GetMyDocumentsAsync(int userId, DocumentFilter? filter = null)
  {
    var query = _context.Documents
        .Include(d => d.UploadedBy)
        .Include(d => d.Project)
        .Where(d => d.UploadedByUserId == userId)
        .AsQueryable();

    if (filter != null)
    {
      if (!string.IsNullOrWhiteSpace(filter.Category))
        query = query.Where(d => d.Category == filter.Category);

      if (filter.ProjectId.HasValue)
        query = query.Where(d => d.ProjectId == filter.ProjectId.Value);

      if (filter.FromDate.HasValue)
        query = query.Where(d => d.UploadedDate >= filter.FromDate.Value);

      if (filter.ToDate.HasValue)
        query = query.Where(d => d.UploadedDate <= filter.ToDate.Value);

      query = (filter.SortBy?.ToLowerInvariant(), filter.SortDescending) switch
      {
        ("title", true) => query.OrderByDescending(d => d.Title),
        ("title", false) => query.OrderBy(d => d.Title),
        ("category", true) => query.OrderByDescending(d => d.Category),
        ("category", false) => query.OrderBy(d => d.Category),
        ("filesize", true) => query.OrderByDescending(d => d.FileSize),
        ("filesize", false) => query.OrderBy(d => d.FileSize),
        _ => filter.SortDescending
                                    ? query.OrderByDescending(d => d.UploadedDate)
                                    : query.OrderBy(d => d.UploadedDate),
      };
    }
    else
    {
      query = query.OrderByDescending(d => d.UploadedDate);
    }

    return await query.ToListAsync();
  }

  public async Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
  {
    var user = await _context.Users.FindAsync(requestingUserId);
    if (user == null) return [];

    // Verify access: must be project member, PM, or Administrator
    if (user.Role != UserRole.Administrator)
    {
      var hasAccess = await _context.ProjectMembers
          .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == requestingUserId);
      var isPM = await _context.Projects
          .AnyAsync(p => p.ProjectId == projectId && p.ProjectManagerId == requestingUserId);
      if (!hasAccess && !isPM) return [];
    }

    return await _context.Documents
        .Include(d => d.UploadedBy)
        .Where(d => d.ProjectId == projectId)
        .OrderByDescending(d => d.UploadedDate)
        .ToListAsync();
  }

  public async Task<(Stream? FileStream, Document? Document)> GetFileStreamAsync(int documentId, int requestingUserId)
  {
    if (!await CanUserAccessDocumentAsync(documentId, requestingUserId))
      return (null, null);

    var document = await _context.Documents.FindAsync(documentId);
    if (document == null) return (null, null);

    var stream = await _fileStorage.DownloadAsync(document.FilePath);
    if (stream == null) return (null, null);

    await LogActivityAsync(documentId, requestingUserId, DocumentActivityType.Downloaded);
    return (stream, document);
  }

  // ── Search ────────────────────────────────────────────────────────────────

  public async Task<List<Document>> SearchDocumentsAsync(string query, int requestingUserId)
  {
    var user = await _context.Users.FindAsync(requestingUserId);
    if (user == null || string.IsNullOrWhiteSpace(query)) return [];

    var term = query.Trim().ToLower();

    // Base search predicate
    var results = await _context.Documents
        .Include(d => d.UploadedBy)
        .Include(d => d.Project)
        .Where(d =>
            (d.Title.ToLower().Contains(term)) ||
            (d.Description != null && d.Description.ToLower().Contains(term)) ||
            (d.Tags != null && d.Tags.ToLower().Contains(term)) ||
            (d.UploadedBy.DisplayName.ToLower().Contains(term)) ||
            (d.Project != null && d.Project.Name.ToLower().Contains(term)))
        .ToListAsync();

    // Permission filter in memory (access check is complex due to sharing logic)
    var accessible = new List<Document>();
    foreach (var doc in results)
    {
      if (await CanUserAccessDocumentAsync(doc.DocumentId, requestingUserId))
        accessible.Add(doc);
    }

    // Order: title-exact matches first, then by date
    return accessible
        .OrderByDescending(d => d.Title.ToLower().Contains(term))
        .ThenByDescending(d => d.UploadedDate)
        .ToList();
  }

  // ── Metadata edit ─────────────────────────────────────────────────────────

  public async Task<Document> UpdateMetadataAsync(int documentId, UpdateDocumentMetadataRequest request, int requestingUserId)
  {
    var document = await _context.Documents.FindAsync(documentId)
        ?? throw new KeyNotFoundException($"Document {documentId} not found.");

    if (document.UploadedByUserId != requestingUserId)
      throw new UnauthorizedAccessException("Only the document owner can edit metadata.");

    if (!DocumentCategories.All.Contains(request.Category))
      throw new DocumentUploadException($"Invalid category '{request.Category}'.");

    document.Title = request.Title.Trim();
    document.Description = request.Description?.Trim();
    document.Category = request.Category;
    document.Tags = request.Tags?.Trim();

    await _context.SaveChangesAsync();
    await LogActivityAsync(documentId, requestingUserId, DocumentActivityType.MetadataEdited);

    return document;
  }

  public async Task<Document> ReplaceFileAsync(int documentId, ReplaceFileRequest request, int requestingUserId)
  {
    var document = await _context.Documents.FindAsync(documentId)
        ?? throw new KeyNotFoundException($"Document {documentId} not found.");

    if (document.UploadedByUserId != requestingUserId)
      throw new UnauthorizedAccessException("Only the document owner can replace the file.");

    var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
    if (!AllowedFileTypes.ContainsKey(extension))
      throw new DocumentUploadException($"File type '{extension}' is not allowed.");

    if (request.FileSize <= 0 || request.FileSize > MaxFileSizeBytes)
      throw new DocumentUploadException("File size must be between 1 byte and 25 MB.");

    var scanResult = await _fileScanner.ScanAsync(request.FileContent, request.OriginalFileName);
    if (scanResult == ScanResult.Infected)
      throw new DocumentUploadException("Replacement file failed the security scan.");
    if (scanResult == ScanResult.ScannerUnavailable)
      throw new DocumentUploadException("Document scanning service is unavailable. Try again shortly.");

    // Delete old file
    await _fileStorage.DeleteAsync(document.FilePath);

    // Generate new path
    var projectSegment = document.ProjectId.HasValue
        ? document.ProjectId.Value.ToString()
        : "personal";
    var newRelativePath = $"{requestingUserId}/{projectSegment}/{Guid.NewGuid()}{extension}";

    request.FileContent.Position = 0;
    await _fileStorage.UploadAsync(request.FileContent, newRelativePath);

    document.FilePath = newRelativePath;
    document.OriginalFileName = request.OriginalFileName;
    document.FileType = request.MimeType;
    document.FileSize = request.FileSize;

    await _context.SaveChangesAsync();
    await LogActivityAsync(documentId, requestingUserId, DocumentActivityType.FileReplaced);

    return document;
  }

  // ── Delete ────────────────────────────────────────────────────────────────

  public async Task DeleteDocumentAsync(int documentId, int requestingUserId)
  {
    var document = await _context.Documents
        .Include(d => d.Project)
        .FirstOrDefaultAsync(d => d.DocumentId == documentId)
        ?? throw new KeyNotFoundException($"Document {documentId} not found.");

    var user = await _context.Users.FindAsync(requestingUserId)
        ?? throw new UnauthorizedAccessException("User not found.");

    var isOwner = document.UploadedByUserId == requestingUserId;
    var isAdmin = user.Role == UserRole.Administrator;

    var isPM = false;
    if (!isOwner && !isAdmin && document.ProjectId.HasValue)
    {
      isPM = await _context.Projects
          .AnyAsync(p => p.ProjectId == document.ProjectId.Value && p.ProjectManagerId == requestingUserId);
    }

    if (!isOwner && !isAdmin && !isPM)
      throw new UnauthorizedAccessException("You do not have permission to delete this document.");

    await LogActivityAsync(documentId, requestingUserId, DocumentActivityType.Deleted);

    await _fileStorage.DeleteAsync(document.FilePath);
    _context.Documents.Remove(document);
    await _context.SaveChangesAsync();
  }

  // ── Share ─────────────────────────────────────────────────────────────────

  public async Task ShareDocumentAsync(int documentId, ShareDocumentRequest request, int requestingUserId)
  {
    var document = await _context.Documents.FindAsync(documentId)
        ?? throw new KeyNotFoundException($"Document {documentId} not found.");

    if (document.UploadedByUserId != requestingUserId)
      throw new UnauthorizedAccessException("Only the document owner can share this document.");

    // Prevent duplicate share (unique index will also guard this)
    var exists = await _context.DocumentShares.AnyAsync(s =>
        s.DocumentId == documentId &&
        s.RecipientType == request.RecipientType &&
        s.RecipientId == request.RecipientId);

    if (exists) return; // Already shared — silently ignore

    var share = new DocumentShare
    {
      DocumentId = documentId,
      SharedByUserId = requestingUserId,
      RecipientType = request.RecipientType,
      RecipientId = request.RecipientId,
      SharedDate = DateTime.UtcNow,
    };

    _context.DocumentShares.Add(share);
    await _context.SaveChangesAsync();
    await LogActivityAsync(documentId, requestingUserId, DocumentActivityType.Shared,
        $"{request.RecipientType}:{request.RecipientId}");

    // Notify recipients
    if (request.RecipientType == ShareRecipientType.User)
    {
      await _notificationService.CreateNotificationAsync(new Notification
      {
        UserId = request.RecipientId,
        Title = "Document Shared with You",
        Message = $"A document has been shared with you: \"{document.Title}\"",
        Type = NotificationType.DocumentShared,
        Priority = NotificationPriority.Informational,
      });
    }
    else // Project
    {
      var members = await _context.ProjectMembers
          .Where(pm => pm.ProjectId == request.RecipientId && pm.UserId != requestingUserId)
          .ToListAsync();

      foreach (var member in members)
      {
        await _notificationService.CreateNotificationAsync(new Notification
        {
          UserId = member.UserId,
          Title = "Document Shared with Your Team",
          Message = $"A document has been shared with your project team: \"{document.Title}\"",
          Type = NotificationType.DocumentShared,
          Priority = NotificationPriority.Informational,
        });
      }
    }
  }

  // ── Shared with me ────────────────────────────────────────────────────────

  public async Task<List<Document>> GetSharedWithMeAsync(int userId)
  {
    var userProjectIds = await _context.ProjectMembers
        .Where(pm => pm.UserId == userId)
        .Select(pm => pm.ProjectId)
        .ToListAsync();

    return await _context.Documents
        .Include(d => d.UploadedBy)
        .Include(d => d.Project)
        .Where(d => _context.DocumentShares.Any(s =>
            s.DocumentId == d.DocumentId &&
            (
                (s.RecipientType == ShareRecipientType.User && s.RecipientId == userId) ||
                (s.RecipientType == ShareRecipientType.Project && userProjectIds.Contains(s.RecipientId))
            )))
        .OrderByDescending(d => d.UploadedDate)
        .ToListAsync();
  }

  // ── Task attachment ───────────────────────────────────────────────────────

  public async Task AttachToTaskAsync(int documentId, int taskId, int requestingUserId)
  {
    var document = await _context.Documents.FindAsync(documentId)
        ?? throw new KeyNotFoundException($"Document {documentId} not found.");

    if (!await CanUserAccessDocumentAsync(documentId, requestingUserId))
      throw new UnauthorizedAccessException("You do not have permission to access this document.");

    var task = await _context.Tasks.FindAsync(taskId)
        ?? throw new KeyNotFoundException($"Task {taskId} not found.");

    document.TaskId = taskId;

    // Auto-associate with the task's project if not already set
    if (!document.ProjectId.HasValue && task.ProjectId.HasValue)
      document.ProjectId = task.ProjectId;

    await _context.SaveChangesAsync();
    await LogActivityAsync(documentId, requestingUserId, DocumentActivityType.AttachedToTask, $"TaskId:{taskId}");
  }

  // ── Dashboard ─────────────────────────────────────────────────────────────

  public async Task<List<Document>> GetRecentDocumentsAsync(int userId, int count = 5)
  {
    return await _context.Documents
        .Where(d => d.UploadedByUserId == userId)
        .OrderByDescending(d => d.UploadedDate)
        .Take(count)
        .ToListAsync();
  }

  // ── Admin / Reporting ─────────────────────────────────────────────────────

  public async Task<List<ActivityLog>> GetActivityLogsAsync(int requestingUserId, ActivityLogFilter? filter = null)
  {
    var user = await _context.Users.FindAsync(requestingUserId);
    if (user?.Role != UserRole.Administrator)
      throw new UnauthorizedAccessException("Only Administrators can view activity logs.");

    var query = _context.ActivityLogs
        .Include(a => a.Actor)
        .Include(a => a.Document)
        .AsQueryable();

    if (filter != null)
    {
      if (filter.DocumentId.HasValue)
        query = query.Where(a => a.DocumentId == filter.DocumentId.Value);
      if (filter.ActorUserId.HasValue)
        query = query.Where(a => a.ActorUserId == filter.ActorUserId.Value);
      if (filter.ActivityType.HasValue)
        query = query.Where(a => a.ActivityType == filter.ActivityType.Value);
      if (filter.FromDate.HasValue)
        query = query.Where(a => a.ActivityDate >= filter.FromDate.Value);
      if (filter.ToDate.HasValue)
        query = query.Where(a => a.ActivityDate <= filter.ToDate.Value);
    }

    return await query.OrderByDescending(a => a.ActivityDate).Take(500).ToListAsync();
  }

  public async Task TransferOwnershipAsync(int deactivatedUserId)
  {
    var adminUser = await _context.Users
        .Where(u => u.Role == UserRole.Administrator)
        .OrderBy(u => u.UserId)
        .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("No active Administrator account found to transfer ownership to.");

    var documents = await _context.Documents
        .Where(d => d.UploadedByUserId == deactivatedUserId)
        .ToListAsync();

    foreach (var doc in documents)
      doc.UploadedByUserId = adminUser.UserId;

    await _context.SaveChangesAsync();
    _logger.LogInformation(
        "Transferred {Count} documents from user {UserId} to administrator {AdminId}",
        documents.Count, deactivatedUserId, adminUser.UserId);
  }

  // ── Private helpers ───────────────────────────────────────────────────────

  private async Task LogActivityAsync(int documentId, int actorUserId, DocumentActivityType type, string? details = null)
  {
    try
    {
      _context.ActivityLogs.Add(new ActivityLog
      {
        DocumentId = documentId,
        ActorUserId = actorUserId,
        ActivityType = type,
        Details = details,
        ActivityDate = DateTime.UtcNow,
      });
      await _context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
      // Logging failure must not break the main operation
      _logger.LogError(ex, "Failed to record activity log for document {DocumentId}", documentId);
    }
  }

  private async Task NotifyProjectMembersAsync(int projectId, int uploaderId, Document document)
  {
    var members = await _context.ProjectMembers
        .Where(pm => pm.ProjectId == projectId && pm.UserId != uploaderId)
        .ToListAsync();

    foreach (var member in members)
    {
      try
      {
        await _notificationService.CreateNotificationAsync(new Notification
        {
          UserId = member.UserId,
          Title = "New Project Document",
          Message = $"A new document \"{document.Title}\" was added to your project.",
          Type = NotificationType.DocumentAddedToProject,
          Priority = NotificationPriority.Informational,
        });
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Failed to notify member {UserId} of new project document", member.UserId);
      }
    }
  }
}
