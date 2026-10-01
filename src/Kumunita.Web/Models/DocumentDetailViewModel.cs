namespace Kumunita.Web.Models;

/// <summary>
/// M21 (ADR 0122, U03) — the document detail view model. <see
/// cref="Document"/> is the already-authorized document (U02's
/// <c>DocumentDetailResult.Document</c>); the controller 404s (D7) when it is
/// null — the missing and the Deny cases collapse to the **same** response
/// (feed and detail agree, C-M21·5). <see cref="CanDownload"/> is true iff the
/// actor is allowed (redundant with the controller's own decision here, but the
/// view (U04) uses it to render the download link only when visible).
/// <see cref="DownloadUrl"/> is the stable route the download link points at.
/// </summary>
public sealed record DocumentDetailViewModel(
    Kumunita.Core.Documents.Document Document,
    bool CanDownload,
    string DownloadUrl);
