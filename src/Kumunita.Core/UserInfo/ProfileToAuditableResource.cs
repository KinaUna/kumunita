using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// Adapter (M2, U4 — pinned in the design doc §2.2, Shape B): presents a
/// <see cref="Profile"/> to the frozen <see cref="IAuthorizationService"/> as an
/// <see cref="IAuditableResource"/>. Mapping (M2 design §C3 / ADR 0006-C3):
/// <para>
/// <c>Id</c> = <see cref="Profile.SubjectId"/> — the profile's document identity is the
/// subject id (the thin principal's subject); <c>Name</c> = <see cref="Profile.DisplayName"/>;
/// <c>OwnerId</c> = <see cref="Profile.SubjectId"/> — a profile's owner IS its subject (the owner
/// branch of the §4.4 algorithm); <c>Audience</c> = <see cref="Profile.Visibility"/> — the
/// *profile-visibility* audience; <see cref="Profile.ContactVisibility"/> is a *separate*,
/// caller-side decision (design §2.4) and is intentionally NOT projected onto this resource's
/// <see cref="Audience"/>; <c>ComponentId</c> = <c>null</c> — a profile is not component-scoped
/// (M2 has no component concept for the directory); <c>TargetKind</c> = <c>"directory"</c>
/// (matches the audit aggregate-row shape in <see cref="AccessAudit"/> and the
/// <c>"directory"</c> string pinned by ADR 0006-C3).
/// </para>
/// <para>
/// The adapter does not *own* the <see cref="Profile"/>: the same profile may be wrapped by
/// multiple adapters in the same request (once for the bulk <c>CanSeeAsync</c>, once per
/// <c>CanAsync</c> in the "view-as" preview, …) — each wrap is a value-level projection, not a
/// shared-mutable-state hazard. <c>sealed</c> keeps the surface closed (nothing downstream
/// should extend it; the ADR 0006-D single-decision-path is what matters, not subclassability).
/// </para>
/// </summary>
public sealed class ProfileToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="profile"/>.
    /// </summary>
    public ProfileToAuditableResource(Profile profile) => Profile = profile;

    /// <summary>The profile this adapter presents. The adapter does not own it.</summary>
    public Profile Profile { get; }

    /// <summary>Resource id = the profile's subject id (document identity).</summary>
    public string Id => Profile.SubjectId;

    /// <summary>Display name — the human-facing label the decision's audit row records.</summary>
    public string Name => Profile.DisplayName;

    /// <summary>Absolute owner = the subject (owner branch of the §4.4 algorithm).</summary>
    public string? OwnerId => Profile.SubjectId;

    /// <summary>
    /// The *profile-visibility* audience (M2 design §2.4: <c>ContactVisibility</c> is the
    /// caller's second, separate decision — not folded into this <see cref="Audience"/> here).
    /// </summary>
    public Audience? Audience => Profile.Visibility;

    /// <summary>Component scope — a directory profile is not component-scoped (M2 has none).</summary>
    public string? ComponentId => null;

    /// <summary>
    /// Resource target kind — <c>"directory"</c> (ADR 0006-C3's named value; the audit
    /// aggregate row's <see cref="AccessAudit.TargetKind"/> for this line of decisions).
    /// </summary>
    public string TargetKind => "directory";
}
