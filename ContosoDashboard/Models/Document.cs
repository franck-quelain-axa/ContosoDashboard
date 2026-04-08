using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class Document
{
  [Key]
  public int DocumentId { get; set; }

  [Required, MaxLength(255)]
  public string Title { get; set; } = string.Empty;

  [MaxLength(2000)]
  public string? Description { get; set; }

  [Required, MaxLength(100)]
  public string Category { get; set; } = string.Empty;
  // Allowed values: see DocumentCategories.All

  [MaxLength(1000)]
  public string? Tags { get; set; }

  [Required]
  public int UploadedByUserId { get; set; }

  public int? ProjectId { get; set; }

  public int? TaskId { get; set; }

  [Required, MaxLength(500)]
  public string FilePath { get; set; } = string.Empty;
  // Relative path pattern: {userId}/{projectId-or-"personal"}/{guid}.{ext}
  // Always GUID-based — never derived from user-supplied filename

  [Required, MaxLength(255)]
  public string OriginalFileName { get; set; } = string.Empty;

  [Required, MaxLength(255)]
  public string FileType { get; set; } = string.Empty;
  // MIME type — 255 chars to fit long Office MIME types

  [Required]
  public long FileSize { get; set; }

  public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

  // Navigation properties
  [ForeignKey("UploadedByUserId")]
  public virtual User UploadedBy { get; set; } = null!;

  [ForeignKey("ProjectId")]
  public virtual Project? Project { get; set; }

  [ForeignKey("TaskId")]
  public virtual TaskItem? Task { get; set; }

  public virtual ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
  public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}

public static class DocumentCategories
{
  public const string ProjectDocuments = "Project Documents";
  public const string TeamResources = "Team Resources";
  public const string PersonalFiles = "Personal Files";
  public const string Reports = "Reports";
  public const string Presentations = "Presentations";
  public const string Other = "Other";

  public static readonly string[] All =
  [
      ProjectDocuments, TeamResources, PersonalFiles, Reports, Presentations, Other
  ];
}
