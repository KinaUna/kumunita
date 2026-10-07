namespace Kumunita.Core.SiteContent;

/// <summary>
/// The read + write seams for the <c>SiteContent</c> singleton (ADR 0150).
/// The <c>GetAsync</c> read is the ADR 0050 <c>IsSignupOpenAsync</c>
/// best-effort shape (missing row / read failure degrades to the in-code
/// fallback — never throws, never returns null); the <c>SaveAsync</c> write
/// is the ADR 0050 <c>SetSignupOpenAsync</c> single audited write-lane
/// shape (one session, one <c>AccessAudit</c> row, strong consistency).
/// </summary>
public interface ISiteContentService
{
    /// <summary>
    /// Loads the <c>SiteContent</c> singleton. **Best-effort**: a missing
    /// store, a missing row (a fresh boot before the seeder ran), or a read
    /// failure degrades to the **in-code fallback** (a fresh
    /// <see cref="SiteContent"/> with every field at its shipped default —
    /// the byte-identical <c>kw-l</c> text + every section shown). The read
    /// is a **public landing surface** (SITE·1) — never an access decision,
    /// never a claim, never audited.
    /// </summary>
    Task<SiteContent> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the <c>SiteContent</c> singleton — the ADR 0050
    /// <c>SetSignupOpenAsync</c> single audited write-lane shape. Loads the
    /// existing row (a missing row is a **no-op** — the lane does not
    /// load-or-create; a fresh instance is already at the in-code fallback,
    /// and the seeder is the only writer that creates the row), applies the
    /// full 13-field set from <paramref name="content"/>, and saves in one
    /// session (invariant C3). Exactly **one** <c>AccessAudit</c> row per
    /// call (<c>Via = Admin</c>, action <c>site.save</c>, <c>TargetKind</c>
    /// "site" — the <c>signup.set-open</c> / <c>timezone.set-default</c> /
    /// <c>dateformat.set-default</c> singleton-toggle shape, SITE·2).
    /// **Strong consistency** (invariant C4): the new value is live on the
    /// very next <c>GetAsync</c> / render. The lane **upserts** the
    /// singleton — it never creates a second row (SITE·6).
    /// </summary>
    Task SaveAsync(SiteContent content, string actorBy, CancellationToken ct = default);
}
