using System.Security.Claims;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ContosoDashboard.Pages;

[Authorize]
public class DocumentDownloadModel : PageModel
{
  private static readonly HashSet<string> PreviewableMimeTypes =
  [
      "application/pdf",
        "image/jpeg",
        "image/png",
    ];

  private readonly IDocumentService _documentService;

  public DocumentDownloadModel(IDocumentService documentService)
  {
    _documentService = documentService;
  }

  public async Task<IActionResult> OnGetAsync(int documentId, bool preview = false)
  {
    // Resolve authenticated user
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
    if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
      return Forbid();

    // Access check — returns 403 before touching storage
    var hasAccess = await _documentService.CanUserAccessDocumentAsync(documentId, userId);
    if (!hasAccess)
      return Forbid();

    var (stream, document) = await _documentService.GetFileStreamAsync(documentId, userId);

    if (stream == null || document == null)
      return NotFound();

    var mimeType = string.IsNullOrWhiteSpace(document.FileType)
        ? "application/octet-stream"
        : document.FileType;

    var fileName = document.OriginalFileName ?? document.Title;

    if (preview && PreviewableMimeTypes.Contains(mimeType))
    {
      // Inline — lets the browser render PDFs, images in the tab
      // FileStreamResult with EnableRangeProcessing supports partial-content requests (PDF scroll)
      return new FileStreamResult(stream, mimeType) { EnableRangeProcessing = true };
    }

    if (preview && !PreviewableMimeTypes.Contains(mimeType))
    {
      // Non-previewable type — fall back to download
      stream.Dispose();
      return Redirect($"/documents/download/{documentId}");
    }

    // Standard download — attachment disposition
    return File(stream, mimeType, fileName);
  }
}
