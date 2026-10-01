namespace Kumunita.Core.Documents;

/// <summary>
/// <see cref="DocumentService.GetAsync"/>'s result (M21, ADR 0122; design
/// doc §5, the C-M21·4 single-decision-row shape). <see cref="Document"/> is the
/// already-loaded <see cref="Document"/> document, null in **two** fail-closed
/// cases the Web layer maps to the **same** response (404 — D7: feed and detail
/// agree on the deny posture): the document does not exist (no decision ran, no
/// <c>AccessAudit</c> row) vs. the visit was Deny'd (the <c>CanAsync</c>
/// decision's row <i>was</i> written — invariant C-M21·4 — and the Web layer
/// renders 404, **not** 403). Exactly one decision call per visit (C6) — this
/// service adds **no** second call.
/// </summary>
public sealed record DocumentDetailResult(Document? Document);
