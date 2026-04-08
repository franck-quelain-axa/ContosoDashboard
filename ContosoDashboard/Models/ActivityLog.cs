using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class ActivityLog
{
  [Key]
  public int ActivityLogId { get; set; }

  [Required]
  public int DocumentId { get; set; }

  [Required]
  public int ActorUserId { get; set; }

  [Required]
  public DocumentActivityType ActivityType { get; set; }

  public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

  [MaxLength(500)]
  public string? Details { get; set; }

  // Navigation properties
  [ForeignKey("DocumentId")]
  public virtual Document Document { get; set; } = null!;

  [ForeignKey("ActorUserId")]
  public virtual User Actor { get; set; } = null!;
}

public enum DocumentActivityType
{
  Uploaded,
  Downloaded,
  Previewed,
  Deleted,
  MetadataEdited,
  FileReplaced,
  Shared,
  AttachedToTask
}
