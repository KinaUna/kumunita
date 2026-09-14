// ── /community/manage/{id} view model (ADR 0012) ───────────────────────────────
//
// The community-membership management surface: the community's mandatory
// state (the toggle — offered to the GlobalAdmin only), who's a member (the
// removable list), and who's still join-able (the add-picker — non-blocked,
// non-member profiles).
namespace Kumunita.Web.Models;

public class ManageCommunityViewModel
{
    public string ComponentId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public bool Mandatory { get; init; }
    public bool Enabled { get; init; }

    /// <summary>
    /// The acting principal is a GlobalAdmin — only their manage page offers
    /// the mandatory/optional toggle (product decision per ADR 0012); a
    /// community moderator sees member management without it.
    /// </summary>
    public bool CanSetMandatory { get; init; }

    /// <summary>
    /// The acting principal holds the community-scoped claim (as opposed to
    /// being a GlobalAdmin) — lets the view say "you are the moderator of this
    /// community".
    /// </summary>
    public bool ActorHasScopeClaim { get; init; }

    /// <summary>Every member row (the removable list — includes the actor if
    /// they're a member).</summary>
    public IReadOnlyList<MemberRow> Members { get; init; } = [];

    /// <summary>Add-picker candidates: non-blocked profiles, not already a
    /// member, excluding the actor.</summary>
    public IReadOnlyList<MemberRow> Candidates { get; init; } = [];

    // ── ADR 0026 — community name/description translations ─────────────────
    // The same three properties the ADR 0022 post-detail surface carries
    // (PostDetailViewModel): the row set (a "a read, not a decision" surface —
    // visibility already inherited the community's enabled visibility), the
    // enabled catalog language set (with its HasTranslation flag) the chips /
    // "add a translation" candidate list render from, and the standing pin (a
    // display convenience, not a gate — the real deny is UserInfoService's
    // AddCommunityTranslationAsync standing check, which re-runs the same rule
    // server-side). Reuses the ADR 0022 LanguageOption record (the same
    // (code, native name, has translation) shape).
    public IReadOnlyList<Kumunita.Core.UserInfo.CommunityTranslation> CommunityTranslations { get; init; } = [];

    public IReadOnlyList<LanguageOption> Languages { get; init; } = [];

    public bool CanTranslate { get; init; }

    public sealed record MemberRow(string SubjectId, string? DisplayName);
}
