# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-04-07 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/001-document-upload-management/spec.md`

## Summary

Add a document upload and management capability to the ContosoDashboard Blazor Server application, allowing employees to upload files (PDF, Office, images, text; ≤25 MB), organize them by category and project, search and browse them, share them with colleagues, and attach them to tasks. Files are stored on the local filesystem outside `wwwroot` via an `IFileStorageService` abstraction (enabling future swap to Azure Blob Storage). All document metadata, sharing relationships, and activity events are stored in the existing SQL Server (LocalDB) database using EF Core. Virus scanning is simulated by a mock `IFileScanner` (always returns clean) to satisfy the offline/no-external-services training constraint.

## Technical Context

**Language/Version**: C# 13 / .NET 10  
**Primary Dependencies**: Blazor Server, EF Core 10 (SQL Server/LocalDB), Microsoft.AspNetCore.Authentication (cookie-based mock auth), `System.IO` for local file operations  
**Storage**: SQL Server LocalDB (document metadata, shares, activity log); local filesystem at `AppData/uploads/` (file content, outside `wwwroot`)  
**Testing**: Manual integration testing (existing project has no automated test harness)  
**Target Platform**: Blazor Server hosted on Windows/Linux (training workstation)  
**Project Type**: Web application (Blazor Server)  
**Performance Goals**: Upload ≤30 s for 25 MB; document list page ≤2 s for 500 docs; search ≤2 s; preview load ≤3 s  
**Constraints**: Fully offline — no external service calls; mock authentication (cookie-based, custom `AuthenticationStateProvider`); files must be served through an authorized endpoint (not `wwwroot`); GUID-based filenames to prevent path traversal  
**Scale/Scope**: Training environment; <100 concurrent users; up to 500 documents per user for performance target

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Security-First** | ✅ Pass | Files stored outside `wwwroot`; GUID paths; authorization checked on every download/preview endpoint; service layer enforces IDOR protection. |
| **II. Training-Oriented** | ✅ Pass | Local filesystem storage; no cloud dependencies; mock scanner satisfies FR-030 in training context. |
| **III. User-Centric** | ✅ Pass | Feature directly addresses stated employee pain-points (document scatter, insecure sharing). |
| **IV. Observability** | ✅ Pass | `ActivityLog` entity records all document operations with actor and timestamp. |
| **V. Simplicity** | ✅ Pass | Interface-based abstractions use DI patterns already present; no new frameworks introduced. |
| **No external service deps** | ⚠️ Justified violation | FR-030 requires virus scanning. Production would use a real scanner SDK. For training, `MockFileScanner` satisfies the interface with no external calls. See Complexity Tracking. |
| **WCAG 2.1** | ✅ Pass (design intent) | Upload UI and document list must use accessible labels, ARIA attributes, and keyboard navigation consistent with existing pages. |

### Post-Design Re-check (Phase 1)

| Item | Result |
|------|--------|
| `IFileStorageService` / `IFileScanner` interfaces keep business logic free of I/O details | ✅ |
| No new project added to solution (single `.csproj`) | ✅ |
| No Repository pattern added (direct `DbContext` access via service layer, consistent with codebase) | ✅ |
| Virus scanner abstraction justified — see Complexity Tracking | ✅ |

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   ├── service-interfaces.md   ← Phase 1 output
│   └── http-endpoints.md       ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs              # new — Document entity + DocumentCategory enum
│   ├── DocumentShare.cs         # new — DocumentShare entity + ShareRecipientType enum
│   └── ActivityLog.cs           # new — ActivityLog entity + DocumentActivityType enum
│   (Notification.cs)            # modify — add DocumentShared, DocumentAddedToProject to NotificationType
│
├── Data/
│   └── ApplicationDbContext.cs  # modify — add DbSets, EF config, indexes for new entities
│
├── Services/
│   ├── IFileStorageService.cs   # new — abstraction for file I/O
│   ├── LocalFileStorageService.cs  # new — System.IO implementation
│   ├── IFileScanner.cs          # new — abstraction for virus scanning
│   ├── MockFileScanner.cs       # new — training stub (always returns clean)
│   ├── DocumentService.cs       # new — IDocumentService + implementation (orchestrates upload workflow, authorization, notifications)
│   └── DashboardService.cs      # modify — add recent documents to DashboardSummary
│
├── Pages/
│   ├── Documents.razor          # new — My Documents list + upload modal (P1/P2)
│   ├── DocumentDownload.cshtml  # new — Razor Page for authorized file serving (download + preview)
│   └── DocumentDownload.cshtml.cs  # new — code-behind for file endpoint
│   (ProjectDetails.razor)       # modify — add project documents tab
│   (Tasks.razor / task detail)  # modify — add document attachment panel
│   (Index.razor)                # modify — add Recent Documents widget
│
├── Shared/
│   └── RecentDocumentsWidget.razor  # new — extracted widget for dashboard
│
└── Migrations/                  # EF migration (generated via dotnet ef)
```

**Structure Decision**: Single-project Blazor Server app — mirrors the existing `ContosoDashboard/` layout. All new code follows the established Models / Services / Pages pattern. The one new layer (`Services/IFileStorageService.cs`, `Services/IFileScanner.cs`) uses the same DI registration style already applied to `INotificationService`, `IProjectService`, etc.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| `IFileScanner` / `MockFileScanner` abstraction | FR-030 requires scanning before storage. Constitution prohibits external service calls in training. An interface keeps the contract honest while the stub satisfies the offline constraint. | Removing scanning entirely would leave FR-030 unmet and set a bad security pattern for training. Hard-coding "always clean" without an interface would make production swap impossible. |
