using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentShare
{
  [Key]
  public int DocumentShareId { get; set; }

  [Required]
  public int DocumentId { get; set; }

  [Required]
  public int SharedByUserId { get; set; }

  [Required]
  public ShareRecipientType RecipientType { get; set; }
  // User    → RecipientId is a UserId
  // Project → RecipientId is a ProjectId (all current members inherit access)

  [Required]
  public int RecipientId { get; set; }

  public DateTime SharedDate { get; set; } = DateTime.UtcNow;

  // Navigation properties
  [ForeignKey("DocumentId")]
  public virtual Document Document { get; set; } = null!;

  [ForeignKey("SharedByUserId")]
  public virtual User SharedBy { get; set; } = null!;
}

public enum ShareRecipientType
{
  User,
  Project
}
