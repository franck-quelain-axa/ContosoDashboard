# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-upload-management/`  
**Prerequisites**: [plan.md](plan.md) ✅ | [spec.md](spec.md) ✅ | [research.md](research.md) ✅ | [data-model.md](data-model.md) ✅ | [contracts/](contracts/) ✅

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel (different files, no incomplete dependencies)
- **[Story]**: Which user story this task belongs to (US1–US9)
- Tests are **not** included — no automated test harness exists in the project

---

## Phase 1: Setup

**Purpose**: Configure the environment and storage settings required before any code is written.

- [ ] T001 Add `FileStorage:BasePath` key with value `"AppData/uploads"` to `ContosoDashboard/appsettings.Development.json`
- [ ] T002 Add `ContosoDashboard/AppData/` to `.gitignore` to prevent storing uploaded files in source control

**Checkpoint**: Dev environment ready — storage path configured, uploads directory excluded from git.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: All new models, database schema, service abstractions, and DI wiring that MUST be complete before any user story work begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T003 [P] Create `Document` entity with all properties, navigation properties, and `DocumentCategories` static class per data-model.md in `ContosoDashboard/Models/Document.cs`
- [ ] T004 [P] Create `DocumentShare` entity and `ShareRecipientType` enum per data-model.md in `ContosoDashboard/Models/DocumentShare.cs`
- [ ] T005 [P] Create `ActivityLog` entity and `DocumentActivityType` enum per data-model.md in `ContosoDashboard/Models/ActivityLog.cs`
- [ ] T006 Add `DocumentShared` and `DocumentAddedToProject` values to the `NotificationType` enum in `ContosoDashboard/Models/Notification.cs`
- [ ] T007 Add `Documents`, `DocumentShares`, `ActivityLogs` DbSets and configure all EF relationships, `OnDelete` behaviors, and performance indexes per data-model.md in `ContosoDashboard/Data/ApplicationDbContext.cs`
- [ ] T008 Generate and apply EF Core migration: run `dotnet ef migrations add AddDocumentManagement` then `dotnet ef database update` in `ContosoDashboard/`
- [ ] T009 [P] Create `IFileStorageService` interface with `UploadAsync`, `DownloadAsync`, `DeleteAsync`, and `GetUrlAsync` per contracts/service-interfaces.md in `ContosoDashboard/Services/IFileStorageService.cs`
- [ ] T010 [P] Create `IFileScanner` interface and `ScanResult` enum (`Clean`, `Infected`, `ScannerUnavailable`) per contracts/service-interfaces.md in `ContosoDashboard/Services/IFileScanner.cs`
- [ ] T011 [P] Create `IDocumentService` interface with all methods and supporting DTOs (`UploadDocumentRequest`, `UpdateDocumentMetadataRequest`, `ReplaceFileRequest`, `ShareDocumentRequest`, `DocumentFilter`, `ActivityLogFilter`) and `DocumentUploadException` per contracts/service-interfaces.md in `ContosoDashboard/Services/IDocumentService.cs`
- [ ] T012 [P] Implement `LocalFileStorageService` using `System.IO`, resolving base path from `IConfiguration["FileStorage:BasePath"]` relative to `IWebHostEnvironment.ContentRootPath`, creating directories as needed in `ContosoDashboard/Services/LocalFileStorageService.cs`
- [ ] T013 [P] Implement `MockFileScanner` that always returns `ScanResult.Clean` and emits a debug log message per research.md R-004 in `ContosoDashboard/Services/MockFileScanner.cs`
- [ ] T014 [P] Create `DocumentService` class skeleton: `public class DocumentService : IDocumentService` with constructor injecting `ApplicationDbContext`, `IFileStorageService`, `IFileScanner`, and `INotificationService` in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T015 Register `IFileStorageService → LocalFileStorageService`, `IFileScanner → MockFileScanner`, and `IDocumentService → DocumentService` as scoped services in `ContosoDashboard/Program.cs`
- [ ] T016 Add a **Documents** navigation link (visible to all authenticated users) in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: Foundation complete — all models, DB tables, interfaces, and DI registrations are in place. All user story phases can now begin.

---

## Phase 3: User Story 1 — Upload a Document (Priority: P1) 🎯 MVP

**Goal**: An authenticated user can upload a file with required metadata; the system validates, scans, stores, and records it.

**Independent Test**: Upload a PDF ≤ 25 MB with a title and category → document appears in the list with correct metadata, upload date, and uploader name. Attempt an `.exe` and a file > 25 MB → both are rejected with clear messages.

- [ ] T017 [P] [US1] Implement `DocumentService.UploadDocumentAsync`: validate extension + MIME (whitelist), validate file size (≤ 25 MB), call `IFileScanner.ScanAsync`, generate GUID-based `FilePath`, call `IFileStorageService.UploadAsync`, insert `Document` record, insert `ActivityLog` (Uploaded), per research.md R-006 upload sequence in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T018 [P] [US1] Implement `DocumentService.CanUserAccessDocumentAsync` (6-condition access check per data-model.md) and `DocumentService.GetMyDocumentsAsync` (returns caller's own documents) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T019 [P] [US1] Create `Documents.razor` page with `@page "/documents"`, `[Authorize]` attribute, `@inject IDocumentService`, and page-level `@code` section skeleton in `ContosoDashboard/Pages/Documents.razor`
- [ ] T020 [US1] Add upload modal to `Documents.razor`: `<InputFile>` with `@key` attribute, title input (required), category dropdown (`DocumentCategories.All`), description textarea, project selector, tags input, progress indicator bound to an `isUploading` bool, and success/error message display in `ContosoDashboard/Pages/Documents.razor`
- [ ] T021 [US1] Implement upload handler in `Documents.razor`: copy `IBrowserFile` to `MemoryStream` before any `await` (R-001 pattern), clear `IBrowserFile` reference, call `DocumentService.UploadDocumentAsync`, call `StateHasChanged()`, handle `DocumentUploadException` to display user-facing error in `ContosoDashboard/Pages/Documents.razor`
- [ ] T022 [US1] Add document list table to `Documents.razor` bound to `DocumentService.GetMyDocumentsAsync`, displaying columns: Title, Category, Upload Date, File Size (formatted), Associated Project in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 1 fully functional — upload works end-to-end with validation, scanning, storage, and list display.

---

## Phase 4: User Story 2 — Browse Personal Documents (Priority: P2)

**Goal**: User can sort and filter their document list by any column and by category, project, or date range.

**Independent Test**: Upload 3+ documents with different categories and dates → sort by each column → apply category and date-range filters → confirm list reorders/filters correctly.

- [ ] T023 [US2] Extend `DocumentService.GetMyDocumentsAsync` to accept `DocumentFilter` (category, projectId, fromDate, toDate, sortBy, sortDescending) and apply all filter/sort conditions as EF queryable operations in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T024 [US2] Add sortable column headers (Title, Upload Date, Category, File Size) to the document list table; clicking a header toggles sort direction and calls `LoadDocuments()` in `ContosoDashboard/Pages/Documents.razor`
- [ ] T025 [US2] Add filter controls above the document list: category dropdown, associated-project dropdown (populated from user's projects), from-date and to-date pickers; wire all controls to `DocumentFilter` parameters and refresh list on change in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 2 fully functional — list sorts and filters correctly across all supported axes.

---

## Phase 5: User Story 3 — Download and Preview Documents (Priority: P2)

**Goal**: Users can download documents they have access to, preview PDFs and images inline in the browser, and are denied access to documents they do not own or share.

**Independent Test**: Upload a PDF → Preview opens inline in a new tab without a download dialog. Download delivers the correct file. Attempt to access another user's document URL directly → 403 returned.

- [ ] T026 [US3] Implement `DocumentService.GetFileStreamAsync(documentId, requestingUserId)`: call `CanUserAccessDocumentAsync`, look up `Document`, call `IFileStorageService.DownloadAsync`, insert `ActivityLog` record; return `(null, null)` if unauthorized or missing in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T027 [US3] Create `DocumentDownload.cshtml.cs` Razor Page code-behind: inject `IDocumentService`, resolve current `UserId` from claims, call `GetFileStreamAsync`; return `FileStreamResult` with `attachment` disposition for download, `inline` disposition for preview, redirect to download for non-previewable types, `Forbid()` if null in `ContosoDashboard/Pages/DocumentDownload.cshtml.cs`
- [ ] T028 [US3] Create `DocumentDownload.cshtml` Razor Page template with `[Authorize]` attribute and an `OnGetAsync` handler accepting `int documentId` and `bool preview` route/query params in `ContosoDashboard/Pages/DocumentDownload.cshtml`
- [ ] T029 [US3] Add `X-Content-Type-Options: nosniff` and `Content-Security-Policy: default-src 'self'` response headers to the download/preview endpoint in `ContosoDashboard/Pages/DocumentDownload.cshtml.cs`
- [ ] T030 [US3] Add **Download** and **Preview** action buttons/links to each row in the document list table, linking to `/documents/download/{id}` and `/documents/download/{id}?preview` respectively in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 3 fully functional — download and preview work with authorization enforced.

---

## Phase 6: User Story 4 — View and Manage Project Documents (Priority: P3)

**Goal**: All project members see documents linked to their project; Project Managers can upload to and delete from the project; non-members are denied access.

**Independent Test**: PM uploads a project-linked document → team member logs in and sees it in the project view → PM deletes it after confirming → document is gone. Non-member cannot access the project documents URL.

- [ ] T031 [US4] Implement `DocumentService.GetProjectDocumentsAsync(projectId, requestingUserId)`: verify requesting user is a project member or Administrator (return empty if not), query documents where `ProjectId == projectId` in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T032 [US4] Implement `DocumentService.DeleteDocumentAsync(documentId, requestingUserId)`: allow if owner OR Project Manager of the document's project OR Administrator; call `IFileStorageService.DeleteAsync`, delete `Document` record, insert `ActivityLog` (Deleted); throw `UnauthorizedAccessException` otherwise in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T033 [P] [US4] Add a **Documents** tab/section panel to the project detail layout in `ContosoDashboard/Pages/ProjectDetails.razor`
- [ ] T034 [US4] Wire the project documents panel to `DocumentService.GetProjectDocumentsAsync`; show document list (title, uploader, date, size); show **Upload to Project** button visible only when current user is a Project Manager or Administrator in `ContosoDashboard/Pages/ProjectDetails.razor`
- [ ] T035 [US4] Add **Delete** button with a confirmation dialog to each row in the project documents panel; show button only to document owner, PM of the project, or Administrator; wire to `DocumentService.DeleteDocumentAsync` in `ContosoDashboard/Pages/ProjectDetails.razor`
- [ ] T036 [US4] Implement project-upload notification in `DocumentService.UploadDocumentAsync`: when `ProjectId` is set, call `INotificationService.CreateNotificationAsync` for each project member (except the uploader) with `NotificationType.DocumentAddedToProject` in `ContosoDashboard/Services/DocumentService.cs`

**Checkpoint**: User Story 4 fully functional — project documents visible to all members, manageable by PM, inaccessible to non-members.

---

## Phase 7: User Story 5 — Search for Documents (Priority: P3)

**Goal**: Users search across titles, descriptions, tags, uploader names, and project names and receive only results they are permitted to see, within 2 seconds.

**Independent Test**: Upload documents with distinct titles and tags → search by a title keyword → only matching documents owned by or shared with the current user appear in results.

- [ ] T037 [US5] Implement `DocumentService.SearchDocumentsAsync(query, requestingUserId)`: build an EF LINQ query filtering `Document` records where `Title`, `Description`, `Tags`, `UploadedBy.DisplayName`, or `Project.Name` contains the search term (case-insensitive); intersect with the permission predicate (owner OR shared OR project member OR admin OR team lead); return results ordered by relevance (title match first, then date desc) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T038 [P] [US5] Add a search text input and a results panel to `ContosoDashboard/Pages/Documents.razor`, triggered on input change with a 300 ms debounce
- [ ] T039 [US5] Wire search input to `DocumentService.SearchDocumentsAsync`, display results in the results panel with document title, category, uploader, and date; clear results when query is empty in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 5 fully functional — search returns permission-filtered results within 2 seconds.

---

## Phase 8: User Story 6 — Edit Document Metadata and Replace File (Priority: P4)

**Goal**: Document owners can update title, description, category, and tags and can replace the file content while preserving all metadata and associations.

**Independent Test**: Upload a document → edit its title and category → changes appear immediately in the list. Replace the file → download confirms the new file content while metadata is preserved.

- [ ] T040 [US6] Implement `DocumentService.UpdateMetadataAsync(documentId, request, requestingUserId)`: verify caller is document owner; update `Title`, `Description`, `Category`, `Tags`; insert `ActivityLog` (MetadataEdited); save changes in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T041 [US6] Implement `DocumentService.ReplaceFileAsync(documentId, request, requestingUserId)`: verify caller is owner; validate new file (extension, MIME, size); scan; call `IFileStorageService.DeleteAsync` for old path; generate new GUID path; call `IFileStorageService.UploadAsync`; update `Document.FilePath`, `FileSize`, `FileType`, `OriginalFileName`; insert `ActivityLog` (FileReplaced) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T042 [P] [US6] Add **Edit** action button to document list rows; add Edit modal with pre-filled title, description, category, and tags inputs in `ContosoDashboard/Pages/Documents.razor`
- [ ] T043 [US6] Wire Edit modal **Save** to `DocumentService.UpdateMetadataAsync` and refresh the document list on success in `ContosoDashboard/Pages/Documents.razor`
- [ ] T044 [US6] Add **Replace File** option in document row actions; add file picker that calls `DocumentService.ReplaceFileAsync` using the R-001 `MemoryStream` copy pattern; display success/error feedback in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 6 fully functional — metadata edits and file replacement work with owner-only authorization.

---

## Phase 9: User Story 7 — Share Documents (Priority: P4)

**Goal**: Document owners share with a named user or a project's membership; recipients get notified and see the document in a "Shared with Me" section.

**Independent Test**: Share a document with another user → log in as that user → document appears in "Shared with Me" → in-app notification was created.

- [ ] T045 [US7] Implement `DocumentService.ShareDocumentAsync(documentId, request, requestingUserId)`: verify caller is owner; check for duplicate share (unique index prevents DB insert, catch gracefully); insert `DocumentShare`; for `User` recipient call `INotificationService.CreateNotificationAsync` with `NotificationType.DocumentShared`; for `Project` recipient notify each current project member in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T046 [US7] Implement `DocumentService.GetSharedWithMeAsync(userId)`: return `Document` records reachable via `DocumentShare` where `(RecipientType==User && RecipientId==userId)` OR `(RecipientType==Project && RecipientId IN user's ProjectIds)` in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T047 [P] [US7] Add **Share** button and Share modal (recipient type toggle: User / Project; searchable picker) to document list rows; add **Shared with Me** tab section to the Documents page showing results from `GetSharedWithMeAsync` in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: User Story 7 fully functional — sharing grants access, notifications fire, and "Shared with Me" is populated.

---

## Phase 10: User Story 8 — Task and Dashboard Integration (Priority: P5)

**Goal**: Users attach documents to tasks from the task detail page; the dashboard shows a "Recent Documents" widget with the 5 most recently uploaded documents.

**Independent Test**: Attach a document to a task → document appears in the task panel linked to the task's project. Upload a document → open the dashboard → Recent Documents widget shows it at the top.

- [ ] T048 [US8] Implement `DocumentService.AttachToTaskAsync(documentId, taskId, requestingUserId)`: verify caller has access to both the document and the task; set `Document.TaskId` and (if not already set) `Document.ProjectId` from `TaskItem.ProjectId`; insert `ActivityLog` (AttachedToTask) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T049 [US8] Implement `DocumentService.GetRecentDocumentsAsync(userId, count = 5)`: return the `count` most recently uploaded documents owned by `userId` ordered by `UploadedDate` desc in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T050 [P] [US8] Create `RecentDocumentsWidget.razor` component: accepts `[Parameter] int UserId`; on initialization calls `IDocumentService.GetRecentDocumentsAsync`; renders a card list with document title, category, date, and a download link in `ContosoDashboard/Shared/RecentDocumentsWidget.razor`
- [ ] T051 [P] [US8] Add a document attachment panel to the task detail view: lists documents currently attached to the task (`Document.TaskId == taskId`), plus an **Attach Existing Document** picker calling `DocumentService.AttachToTaskAsync` in `ContosoDashboard/Pages/Tasks.razor`
- [ ] T052 [US8] Add `<RecentDocumentsWidget UserId="@currentUserId" />` to the dashboard home page and a "Document Count" summary card using `Documents.Count(d => d.UploadedByUserId == userId)` in `ContosoDashboard/Pages/Index.razor`
- [ ] T053 [US8] Update `DashboardSummary` record and `DashboardService.GetDashboardSummaryAsync` to include `TotalDocuments` count for the current user in `ContosoDashboard/Services/DashboardService.cs`

**Checkpoint**: User Story 8 fully functional — task attachment and dashboard widget both work.

---

## Phase 11: User Story 9 — Audit and Activity Reporting (Priority: P5)

**Goal**: Administrators can view a full activity log and a summary report (top file types, top uploaders, access patterns).

**Independent Test**: Perform uploads, downloads, deletes, and shares across multiple users → log in as Administrator → Activity Report page shows all events with actor names and timestamps; summary aggregations are correct.

- [ ] T054 [US9] Implement `DocumentService.GetActivityLogsAsync(requestingUserId, filter)`: verify caller has `UserRole.Administrator`; return filtered, paginated `ActivityLog` records with navigation properties included (`Actor`, `Document`) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T055 [P] [US9] Create `ActivityReport.razor` page with `@page "/documents/activity"`, `[Authorize(Roles = "Administrator")]`, page scaffold, and `@inject IDocumentService` in `ContosoDashboard/Pages/ActivityReport.razor`
- [ ] T056 [US9] Implement activity log table (event type, document title, actor, timestamp) and summary aggregation cards (top 5 file types by count, top 5 uploaders by count, download-to-upload ratio) in `ContosoDashboard/Pages/ActivityReport.razor`
- [ ] T057 [US9] Add **Activity Report** nav link visible only to users with `UserRole.Administrator` in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: User Story 9 fully functional — Administrators can view full audit logs and summary reports.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Accessibility, security hardening, missing edge-case handling, and final validation.

- [ ] T058 [P] Add `aria-label`, `aria-describedby`, and `role` attributes to upload modal buttons, progress indicator, and file input to meet WCAG 2.1 in `ContosoDashboard/Pages/Documents.razor`
- [ ] T059 [P] Add explicit `aria-label` and accessible text to all icon-only action buttons (Download, Preview, Edit, Delete, Share) in the document list in `ContosoDashboard/Pages/Documents.razor`
- [ ] T060 [P] Implement `DocumentService.TransferOwnershipAsync(deactivatedUserId)`: find first active Administrator (`UserRole.Administrator`), update all `Document.UploadedByUserId == deactivatedUserId` to the admin's `UserId`, save changes in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T061 [P] Verify all delete confirmation dialogs include explicit warning copy ("This action is permanent and cannot be undone") in `ContosoDashboard/Pages/Documents.razor` and `ContosoDashboard/Pages/ProjectDetails.razor`
- [ ] T062 Run the full manual verification checklist in `specs/001-document-upload-management/quickstart.md` against the running application and confirm all items pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **User Story Phases (3–11)**: All depend on Phase 2 completion; can then proceed in priority order or in parallel if team capacity allows
- **Polish (Phase 12)**: Depends on all intended user story phases being complete

### User Story Inter-Dependencies

| Story | Depends On | Notes |
|-------|-----------|-------|
| US1 (Upload) | Phase 2 only | No story dependencies — true MVP |
| US2 (Browse) | US1 | Requires documents to exist; extends `GetMyDocumentsAsync` |
| US3 (Download/Preview) | US1 | Requires documents to exist; adds Razor Page endpoint |
| US4 (Project Docs) | US1 | Extends upload with project notifications |
| US5 (Search) | US1 | Requires documents to exist; new query method |
| US6 (Edit/Replace) | US1 | Extends existing document CRUD |
| US7 (Share) | US1 | Adds sharing layer on top of existing documents |
| US8 (Integration) | US1 | Extends tasks page and dashboard |
| US9 (Audit) | Phase 2 (ActivityLog) | ActivityLog is written by all other stories; reporting reads it |

### Within Each User Story

- Service method(s) before UI wiring
- UI skeleton `[P]` tasks can run in parallel with service implementation (different files)
- Wire-up tasks come last (depend on both service and UI skeleton)

### Parallel Opportunities Per Phase

**Phase 2** (all can run at the same time after T001–T002):
```
T003, T004, T005  →  (simultaneously)
T006              →  after T003 (same file pattern check)
T007              →  after T003, T004, T005
T008              →  after T007
T009, T010, T011  →  (simultaneously)
T012, T013, T014  →  (simultaneously, after T009/T010/T011 respectively)
T015              →  after T012, T013, T014
T016              →  after T015
```

**User Stories with parallel service + UI tasks (different files — do these together)**:
- US1: T017 + T018 + T019 in parallel → then T020 → T021 → T022
- US3: T026 + T027 → T028 → T029 + T030 in parallel
- US4: T031 + T032 + T033 in parallel → T034 → T035 + T036
- US5: T037 + T038 in parallel → T039
- US6: T040 + T041 + T042 in parallel → T043 → T044
- US8: T048 + T049 + T050 + T051 in parallel → T052 → T053

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete **Phase 1** (Setup)
2. Complete **Phase 2** (Foundational — CRITICAL)
3. Complete **Phase 3** (User Story 1 — Upload)
4. **STOP and VALIDATE**: Upload a PDF, confirm it appears in the list with correct metadata
5. Demo or deploy this increment

### Incremental Delivery

Each phase can be validated and demonstrated independently:

| Increment | Phases | Value Delivered |
|-----------|--------|----------------|
| MVP | 1 + 2 + 3 | Employees can upload and see their documents |
| v0.2 | + 4 + 5 | Browse with sort/filter and download/preview |
| v0.3 | + 6 + 7 | Project documents and search |
| v0.4 | + 8 + 9 | Edit, replace, and share |
| v0.5 | + 10 + 11 | Task integration, dashboard widget, admin reporting |
| v1.0 | + 12 | Polish, accessibility, ownership transfer |

### Parallel Team Strategy

After Phase 2 is complete, multiple developers can work simultaneously:

```
Developer A: US1 (Phase 3) → US2 (Phase 4) → US3 (Phase 5)
Developer B: US4 (Phase 6) → US5 (Phase 7)
Developer C: US6 (Phase 8) → US7 (Phase 9)
Developer D: US8 (Phase 10) → US9 (Phase 11)
```

---

## Task Summary

| Phase | Story | Tasks | Count |
|-------|-------|-------|-------|
| 1 — Setup | — | T001–T002 | 2 |
| 2 — Foundational | — | T003–T016 | 14 |
| 3 — Upload | US1 (P1) | T017–T022 | 6 |
| 4 — Browse | US2 (P2) | T023–T025 | 3 |
| 5 — Download/Preview | US3 (P2) | T026–T030 | 5 |
| 6 — Project Docs | US4 (P3) | T031–T036 | 6 |
| 7 — Search | US5 (P3) | T037–T039 | 3 |
| 8 — Edit/Replace | US6 (P4) | T040–T044 | 5 |
| 9 — Share | US7 (P4) | T045–T047 | 3 |
| 10 — Task/Dashboard | US8 (P5) | T048–T053 | 6 |
| 11 — Audit/Reporting | US9 (P5) | T054–T057 | 4 |
| 12 — Polish | — | T058–T062 | 5 |
| **Total** | | **T001–T062** | **62** |
