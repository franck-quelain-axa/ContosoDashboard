# Research: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-04-07  
**Purpose**: Resolve technical unknowns before Phase 1 design. All NEEDS CLARIFICATION items from the Technical Context are addressed here.

---

## R-001: Blazor Server `IBrowserFile` Stream Handling

**Question**: What is the correct pattern for reading an uploaded file's stream in a Blazor Server component to avoid `ObjectDisposedException` and `InvalidOperationException` on second reads?

**Decision**: Copy the `IBrowserFile` stream into a `MemoryStream` immediately and synchronously per upload, then clear the `IBrowserFile` reference before any `await` that could trigger a render cycle.

**Rationale**:
- `IBrowserFile.OpenReadStream()` opens a one-shot stream backed by the browser IPC channel. After the first read or after the Blazor circuit processes a render, the underlying stream may be disposed.
- Copying to `MemoryStream` before any `await` that could yield to Blazor's render engine ensures the data is captured safely.
- The `@key` attribute on `<InputFile>` forces the component to recreate the DOM element after a successful upload, preventing the stale reference from being reused.
- `StateHasChanged()` after clearing the reference tells Blazor the selection is gone.

**Pattern**:
```csharp
var fileName  = SelectedFile.Name;          // capture before stream open
var fileSize  = SelectedFile.Size;
var mimeType  = SelectedFile.ContentType;

using var memStream = new MemoryStream();
using (var fileStream = SelectedFile.OpenReadStream(maxAllowedSize: 25 * 1024 * 1024))
{
    await fileStream.CopyToAsync(memStream);
}
memStream.Position = 0;

SelectedFile = null;      // clear reference before next render
StateHasChanged();
```

**Alternatives considered**:
- Passing `IBrowserFile` to the service directly — rejected; the stream is disposed by the time the service processes it if any `await` occurs between the component and service.
- Using `IFormFile` via a classic HTTP form — rejected; Blazor Server's interactive component model is already in use and mixing Razor Pages forms for the upload would break the SPA UX.

---

## R-002: Serving Files Outside `wwwroot` from Blazor Server

**Question**: Files must be stored outside `wwwroot` for security. How are they served with authorization checks in this Blazor Server application?

**Decision**: Use a Razor Page endpoint (`DocumentDownload.cshtml` + code-behind) that reads the file from the local filesystem, verifies the requesting user's authorization via `DocumentService`, then returns a `FileContentResult` with the appropriate `Content-Disposition` header.

**Rationale**:
- Blazor Server components cannot return HTTP responses (they operate over SignalR). A Razor Page endpoint bridges the gap: it participates in the ASP.NET Core pipeline, can use `[Authorize]`, and can call the same DI-registered `DocumentService` used by Blazor components.
- A Razor Page is already the pattern used for `Login.cshtml` and `Logout.cshtml` in this project, so no new infrastructure is introduced.
- The endpoint URL is `/documents/download/{documentId}`. An `inline` disposition header enables browser-native PDF/image preview; `attachment` triggers download.

**Pattern (code-behind)**:
```csharp
public async Task<IActionResult> OnGetAsync(int documentId, bool preview = false)
{
    var userId = GetCurrentUserId();
    var (stream, doc) = await _documentService.GetFileStreamAsync(documentId, userId);
    if (stream is null) return Forbid();

    var disposition = preview ? "inline" : "attachment";
    Response.Headers["Content-Disposition"] = $"{disposition}; filename=\"{doc.Title}\"";
    return File(stream, doc.FileType);
}
```

**Alternatives considered**:
- Minimal API endpoint (`app.MapGet(...)`) — viable, but the project does not currently use minimal APIs and Razor Pages are already established.
- Static file middleware with custom `IFileProvider` — rejected; requires exposing paths and cannot easily enforce per-document authorization.

---

## R-003: Team Lead "Team" Determination

**Question**: The spec says Team Leads can view documents uploaded by "members of their direct team(s)." The data model has no explicit Team entity. How is a Team Lead's team determined?

**Decision**: A Team Lead's "team" is the set of `Users` who are `ProjectMember`s in the same `Project`(s) where the Team Lead's own `ProjectMember.Role == "TeamLead"`.

**Rationale**:
- The existing `ProjectMember` table already records a `Role` string field with values like `"TeamLead"` and `"Developer"`.
- No new entity is needed. The query is:  
  `SELECT pm.UserId FROM ProjectMembers pm  WHERE pm.ProjectId IN (SELECT pm2.ProjectId FROM ProjectMembers pm2 WHERE pm2.UserId = @teamLeadId AND pm2.Role = 'TeamLead')`
- This naturally handles multiple projects: a Team Lead who leads two projects can see teammates from both.

**Alternatives considered**:
- Using the `Department` field on `User` to define teams — rejected; the seed data shows all engineering users in the same department, which would give over-broad access.
- Adding a separate `TeamMember` join table — rejected; adds a new entity with no benefit beyond what `ProjectMember` already provides.

---

## R-004: Virus/Malware Scanning in a Training (Offline) Environment

**Question**: FR-030 requires scanning before storage. The constitution prohibits external service dependencies. How is this reconciled for training?

**Decision**: Introduce an `IFileScanner` interface with a `MockFileScanner` implementation that accepts all files (returns `ScanResult.Clean` for every input). The real scanner implementation is left as a documented stub for production.

**Rationale**:
- The interface contract is established (students learn the abstraction pattern). Swapping in a real implementation (e.g., Windows Defender ATP, ClamAV HTTP client, or a cloud-based scanning API) requires only a new implementation class and a DI configuration change.
- `MockFileScanner` is registered only when `ASPNETCORE_ENVIRONMENT == "Development"` or `"Training"`. A comment in `Program.cs` marks the swap point.
- This satisfies both FR-030 (the system does call the scanner and gates on its result) and the constitution (no real external network call in training).

**Interface**:
```csharp
public interface IFileScanner
{
    Task<ScanResult> ScanAsync(Stream fileContent, string fileName);
}

public enum ScanResult { Clean, Infected, ScannerUnavailable }
```

**Alternatives considered**:
- Removing FR-030 from training scope — rejected; omitting the security gate entirely would teach a bad pattern and violate the spec.
- Running a local ClamAV daemon — rejected; requires daemon installation on student workstations, breaking the "zero-setup" training goal.

---

## R-005: EF Core Migration Strategy

**Question**: The existing database must be extended with three new entity tables. What migration approach avoids breaking the seeded data?

**Decision**: Add a single EF Core migration (`AddDocumentManagement`) that creates the three new tables (`Documents`, `DocumentShares`, `ActivityLogs`) and adds the two new `NotificationType` enum values. No existing tables are altered.

**Rationale**:
- All three new entities have no FK dependency on the existing entities that would require altering existing table schemas (they reference existing PKs as FKs, but the existing tables are not modified).
- Adding enum values to `NotificationType` is non-breaking because EF Core stores enum values as integers; new values append to the end.
- Migration command: `dotnet ef migrations add AddDocumentManagement`
- Apply: `dotnet ef database update`

**Note on clean state**: If a previous (failed) upload attempt left orphaned rows (empty `FilePath`), the stakeholder doc recommends dropping and recreating the LocalDB instance rather than writing a repair migration.

**Alternatives considered**:
- Multiple smaller migrations — rejected; all three new tables are introduced together in the same feature branch, so a single migration is simpler and easier to roll back.

---

## R-006: File Path Storage and Security

**Question**: How should file paths be structured and stored to prevent path traversal attacks, collisions, and to enable future cloud migration?

**Decision**: Use the pattern `{userId}/{projectId-or-"personal"}/{guid}.{extension}` for all stored paths. Store this relative path in the `Document.FilePath` column. The absolute base directory (`AppData/uploads/`) is resolved at runtime from configuration, never stored in the DB.

**Rationale**:
- GUID-based filenames remove any user-controlled content from the path, eliminating path traversal risk.
- The relative path pattern works identically as an Azure Blob Storage blob name, making the cloud migration a DI swap with no DB schema change.
- File extension is retained (from a whitelist, not from the user-supplied filename) to preserve MIME-type detection on download.
- User-supplied original filename is stored only in `Document.Title` (a metadata field), never in the filesystem path.

**Upload sequence** (prevents orphaned DB records):
1. Validate file (size, extension whitelist)
2. Authorize user
3. Run virus scan (via `IFileScanner`)
4. Generate unique path
5. Save file to disk (via `IFileStorageService.UploadAsync`)
6. Save `Document` record to DB
7. Send notifications

If step 5 fails, no DB record exists. If step 6 fails, a file exists on disk with no DB record (orphan), which is harmless and can be cleaned up by a separate maintenance routine.
