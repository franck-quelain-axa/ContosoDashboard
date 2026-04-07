# Quickstart: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-04-07

This guide covers everything needed to build, run, and manually verify the document upload and management feature on a development workstation.

---

## Prerequisites

| Requirement | Version | Check |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| SQL Server LocalDB | 2022+ | `sqllocaldb info` |
| EF Core tools | Latest | `dotnet ef --version` |

Install EF tools if not present:
```powershell
dotnet tool install --global dotnet-ef
```

---

## 1. Apply the Database Migration

After implementing the new model files and `ApplicationDbContext` changes, generate and apply the migration:

```powershell
# From the repository root
cd ContosoDashboard

dotnet ef migrations add AddDocumentManagement
dotnet ef database update
```

**Expected output**: Three new tables created — `Documents`, `DocumentShares`, `ActivityLogs`.

### Clean-state reset (if needed)

If a previous failed attempt left orphaned rows or a broken migration state:

```powershell
dotnet ef database drop --force
dotnet ef database update
# OR reset LocalDB entirely:
sqllocaldb stop mssqllocaldb
sqllocaldb delete mssqllocaldb
# Database recreates automatically on next app start
```

---

## 2. Configure File Storage

Add the storage base path to `appsettings.Development.json` (create the key if absent):

```json
{
  "FileStorage": {
    "BasePath": "AppData/uploads"
  }
}
```

The directory is created automatically at application startup if it does not exist. It is placed outside `wwwroot` intentionally — files are not directly accessible via URL.

> **Do not commit the `AppData/uploads/` directory.** Add it to `.gitignore`:
> ```
> ContosoDashboard/AppData/
> ```

---

## 3. Register Services in `Program.cs`

Add the following registrations (after existing service registrations):

```csharp
// File storage — local implementation for training
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// Virus scanner — mock for training (swap for real implementation in production)
builder.Services.AddScoped<IFileScanner, MockFileScanner>();

// Document business logic
builder.Services.AddScoped<IDocumentService, DocumentService>();
```

---

## 4. Run the Application

```powershell
dotnet run --project ContosoDashboard
```

Navigate to `https://localhost:{port}` and log in as one of the seeded users:

| Email | Role |
|---|---|
| `admin@contoso.com` | Administrator |
| `camille.nicole@contoso.com` | Project Manager |
| `floris.kregel@contoso.com` | Team Lead |
| `ni.kang@contoso.com` | Employee |

---

## 5. Manual Verification Checklist

### P1 — Document Upload (User Story 1)

- [ ] Log in as `ni.kang@contoso.com` (Employee)
- [ ] Navigate to **Documents** in the nav menu
- [ ] Click **Upload Document**
- [ ] Select a PDF ≤ 25 MB, enter a title, choose a category → submit
- [ ] Confirm: document appears in the list with correct title, category, upload date, and file size
- [ ] Try uploading a `.exe` file → confirm rejection message is shown
- [ ] Try uploading a file > 25 MB → confirm size error message is shown
- [ ] Upload with an optional project association → confirm document appears in the project's documents

### P2 — Browse, Sort, Filter (User Story 2)

- [ ] Upload 3+ documents with different categories and dates
- [ ] Sort by **Title** → confirm alphabetical order
- [ ] Sort by **File Size** → confirm numeric order
- [ ] Filter by **Category** → confirm only matching documents shown
- [ ] Filter by **Date Range** → confirm only documents within range shown

### P2 — Download and Preview (User Story 3)

- [ ] Click **Download** on the PDF you uploaded → file downloads with correct content
- [ ] Click **Preview** on the PDF → opens inline in a new browser tab (no download dialog)
- [ ] Click **Preview** on a `.docx` → redirected to download instead
- [ ] Log in as a different user → attempting to access the first user's document URL directly returns 403

### P3 — Project Documents (User Story 4)

- [ ] Log in as `camille.nicole@contoso.com` (PM), navigate to project → upload a document
- [ ] Log in as `ni.kang@contoso.com` (team member) → navigate to same project → confirm document is visible
- [ ] As PM, delete the document → confirm permanent removal after confirmation dialog

### P3 — Search (User Story 5)

- [ ] Upload documents with distinct titles and tags
- [ ] Search by a title keyword → correct documents returned
- [ ] Search by a tag value → correct documents returned
- [ ] Confirm results appear quickly (< 2 s)
- [ ] Confirm only documents the logged-in user can access appear in results

### P4 — Edit Metadata (User Story 6)

- [ ] Click **Edit** on one of your documents, change the title and category → save
- [ ] Confirm updated values appear in the document list

### P4 — Share (User Story 7)

- [ ] As `ni.kang@contoso.com`, share a document with `floris.kregel@contoso.com`
- [ ] Log in as `floris.kregel@contoso.com` → open **Shared with Me** → confirm document appears
- [ ] Confirm in-app notification was created for `floris.kregel`

### P5 — Dashboard Integration (User Story 8)

- [ ] Upload a new document → navigate to **Dashboard**
- [ ] Confirm **Recent Documents** widget shows the latest upload at the top
- [ ] Attach a document to a task from the task detail page → confirm document appears in task view

### P5 — Admin Reporting (User Story 9)

- [ ] Log in as `admin@contoso.com`
- [ ] Navigate to the activity report page
- [ ] Confirm upload, download, and share events are all logged with actor names and timestamps

---

## 6. Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| `SqlException: duplicate key` on upload | Orphaned `Document` row with empty `FilePath` | Drop and recreate LocalDB (see Clean-state reset above) |
| `ObjectDisposedException` on upload | `IBrowserFile` stream read after yield | Ensure stream is copied to `MemoryStream` before any `await` (R-001 pattern) |
| `403 Forbidden` on download | Missing or incorrect auth claim | Verify Login page includes all required claims: `NameIdentifier`, `Name`, `Email`, `Role`, `Department` |
| Files not found after restart | `BasePath` resolves differently | Use a path relative to `ContentRootPath` in `LocalFileStorageService` constructor |
| Preview shows download dialog for PDF | Wrong `Content-Disposition` header | Ensure `inline` (not `attachment`) is set when `preview=true` |
