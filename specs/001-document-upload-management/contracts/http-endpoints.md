# Contract: HTTP Endpoints

**Branch**: `001-document-upload-management` | **Date**: 2026-04-07  
**Informed by**: [research.md](../research.md)

Blazor Server components communicate with users over SignalR and cannot directly return HTTP responses. The following Razor Page endpoints bridge the gap for file delivery (download and browser preview), which requires a standard HTTP response with appropriate headers.

All endpoints require an authenticated session (cookie auth). Unauthorized requests return `403 Forbidden`.

---

## `GET /documents/download/{documentId}`

Serves the raw file content for download.

### Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| `documentId` | Route | `int` | Yes | Identifier of the document to download |

### Response

| Scenario | Status | Body / Headers |
|----------|--------|----------------|
| Authorized, file found | `200 OK` | File bytes; `Content-Type: {document.FileType}`; `Content-Disposition: attachment; filename="{document.Title}"` |
| Document not found | `404 Not Found` | Empty body |
| User lacks access | `403 Forbidden` | Empty body |
| Scanner/storage error | `500 Internal Server Error` | Empty body; error logged |

**Authorization**: Calls `IDocumentService.CanUserAccessDocumentAsync(documentId, currentUserId)`. Returns `403` immediately if false.

**Activity log**: Records `DocumentActivityType.Downloaded` for every `200` response.

---

## `GET /documents/preview/{documentId}`

Serves the file for inline browser rendering (PDF viewer, image display). Identical to the download endpoint except for the `Content-Disposition` header.

### Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| `documentId` | Route | `int` | Yes | Identifier of the document to preview |

### Response

| Scenario | Status | Body / Headers |
|----------|--------|----------------|
| Authorized, previewable file found | `200 OK` | File bytes; `Content-Type: {document.FileType}`; `Content-Disposition: inline` |
| File type not previewable (non-PDF, non-image) | `302 Found` | Redirect to `/documents/download/{documentId}` |
| Document not found | `404 Not Found` | Empty body |
| User lacks access | `403 Forbidden` | Empty body |

**Previewable MIME types** (inline rendering): `application/pdf`, `image/jpeg`, `image/png`.  
All other MIME types redirect to the download endpoint.

**Activity log**: Records `DocumentActivityType.Previewed` for every `200` response.

---

## Implementation Note

Both endpoints are implemented as a single Razor Page (`Pages/DocumentDownload.cshtml`) with two separate `OnGet` handler methods differentiated by a `preview` query parameter, or as two separate `OnGet` methods named `OnGetDownloadAsync` and `OnGetPreviewAsync`.

**Recommended approach** — single page, `preview` boolean parameter:

```
GET /documents/download/42          → download
GET /documents/download/42?preview  → inline preview (if supported)
```

This simplifies the link generation in Blazor components:

```razor
<a href="/documents/download/@doc.DocumentId">Download</a>
<a href="/documents/download/@doc.DocumentId?preview" target="_blank">Preview</a>
```

---

## Security Checklist for Both Endpoints

- [x] `[Authorize]` attribute on Razor Page class (or `RequireAuthorization()` in minimal API)
- [x] `documentId` extracted from route, never from query string body
- [x] `IDocumentService.CanUserAccessDocumentAsync` called before any file I/O
- [x] File path resolved only from the DB record (`Document.FilePath`) — never from user input
- [x] `Content-Security-Policy` header set to restrict inline script execution for previewed documents
- [x] `X-Content-Type-Options: nosniff` header set to prevent MIME sniffing
