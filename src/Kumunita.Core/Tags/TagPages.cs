using Kumunita.Core.Pages;
using Kumunita.Core.Posts;

namespace Kumunita.Core.Tags;

/// <summary>
/// One page of the by-tag **post** feed (ADR 0090 D6, M7 U01 — the design
/// doc §7.6 lock). <see cref="HasMore"/> (D1) is the sole paging signal —
/// <c>true</c> iff the page's candidate set filled the page
/// (<c>pageCount == PageSize</c>, <c>PageSize = 30</c> — the D4 shape); the
/// Web computes <c>HasNextPage</c> from it alone (there is no <c>Total</c>
/// field — D2 never applies: a tag page's candidates are the actor-readable
/// rows, not a pre-decision candidate count). **No**
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row (C-TG·8 — the
/// tag lane is a plain read; the content's own <c>Read</c> decision rows are
/// the content's, D7).
/// </summary>
public sealed record TagPostPage(
    IReadOnlyList<Post> Items,
    bool HasMore);

/// <summary>
/// One page of the by-tag **blog-page** feed (ADR 0090 D6, M7 U01 — the
/// design doc §7.6 lock). The <see cref="TagPostPage"/> shape on
/// <see cref="Page"/> — the two element types make a single
/// <c>object</c>-element record wrong (the U00 two-record lock). Same
/// <see cref="HasMore"/> / no-<c>Total</c> / no-audit-row pins (C-TG·8).
/// </summary>
public sealed record TagPagePage(
    IReadOnlyList<Page> Items,
    bool HasMore);
