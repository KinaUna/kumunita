// ── /community/manage/{id} view model (ADR 0012) ───────────────────────────────
//
// The moderator's community-membership surface: the community's mandatory
// state (the on/off switch), who's a member (the removable list), and who's
// still join-able (the add-picker — non-blocked, non-member profiles).
namespace Kumunita.Web.Models;

public class ManageCommunityViewModel
{
    public string ComponentId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public bool Mandatory { get; init; }
    public bool Enabled { get; init; }

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

    public sealed record MemberRow(string SubjectId, string? DisplayName);
}
