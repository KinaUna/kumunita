using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;

namespace Kumunita.Core.Portability;

/// <summary>
/// The resident seam the Web layer + the tests target (M27 D7,
/// C-M27·7). The locked public surface (the design doc
/// <c>docs/design/m27-user-scoped-portability-design.md</c> §2.1, copied
/// verbatim by U01's shell; U03–U06 fill the bodies) — the resident
/// scope is a lane **on** the M11 operator portability surface (the
/// same <c>Kumunita.Core.Portability</c> context), not a new bounded
/// context.
/// <para>
/// **Resident lane, not an authorization lane (C-M27·7):** no
/// <c>AccessAction</c> / <c>AccessVia</c> / <c>IAuthorizationService</c>
/// branch / <c>Audience</c> — the <c>[Authorize]</c> verified-resident
/// gate (the Web <c>UserPortabilityController</c>) is the only decision,
/// and each **write** emits exactly one <c>AccessAudit</c> row
/// (<c>TargetKind "portability"</c>, <c>Via = Owner</c>, verb
/// <c>export</c> / <c>import</c> / <c>import.resolve</c>) — the
/// <em>service</em> emits it, the controller adds none (the ADR 0105
/// <c>messaging.toggle</c> shape).
/// </para>
/// </summary>
public interface IUserPortabilityService
{
    /// <summary>
    /// U03 (export) — one complete, valid <c>*.kumunita</c> archive at the
    /// resident scope (the D2 format: the same M11 archive + the
    /// <c>Scope = "resident"</c> manifest marker). Emits exactly one
    /// <c>portability.export</c> <c>AccessAudit</c> row (<c>Via = Owner</c>).
    /// </summary>
    /// <param name="residentSubjectId">The resident's own <c>subjectId</c> (the archive's scope anchor + the audit row's actor).</param>
    Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default);

    /// <summary>
    /// U04 (classify) — validate-then-classify, **no writes** (C-M27·5):
    /// the <c>clean</c>/<c>duplicate</c>/<c>conflict</c> classification
    /// (the §2.4 contract) + the per-entity reference-availability report
    /// runs to completion before any write. A <c>conflict</c> entity never
    /// auto-applies — it is surfaced for the resident's decision.
    /// </summary>
    /// <param name="residentSubjectId">The resident's own <c>subjectId</c> (the archive's scope anchor).</param>
    /// <param name="archive">The uploaded <c>*.kumunita</c> archive stream.</param>
    Task<UserPortabilityImportPlan> ClassifyAsync(
        string residentSubjectId, Stream archive, CancellationToken ct = default);

    /// <summary>
    /// U06 (resolve + apply) — apply **only** the resident-resolved
    /// entities, fail-closed (C-M27·4): a rejected / aborted resolve
    /// writes <b>nothing</b>; a <c>conflict</c> entity the resident did
    /// not resolve is <b>not applied</b> (no auto-merge). Emits exactly
    /// one <c>portability.import.resolve</c> <c>AccessAudit</c> row
    /// (<c>Via = Owner</c>).
    /// </summary>
    /// <param name="residentSubjectId">The resident's own <c>subjectId</c> (the audit row's actor).</param>
    /// <param name="plan">The U04 classification output (the <c>clean</c>/<c>duplicate</c>/<c>conflict</c> set).</param>
    /// <param name="resolutions">The resident's per-entity decisions (the §2.5 apply contract).</param>
    Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        CancellationToken ct = default);
}

/// <summary>
/// The classify output (U04; the closed shapes, the design doc §2.1
/// verbatim) — the per-entity <c>clean</c>/<c>duplicate</c>/<c>conflict</c>
/// classification (the §2.4 contract) + the per-entity
/// reference-availability report (the <see cref="UserPortabilityAbsentReference"/>
/// list). A classify failure (a malformed / out-of-scope / unsupported
/// archive) returns <see cref="Ok"/> = <c>false</c> + the closed
/// <see cref="Failures"/> set and writes nothing.
/// </summary>
public sealed record UserPortabilityImportPlan(
    bool Ok,
    IReadOnlyList<UserPortabilityEntityClassification> Entities,
    IReadOnlyList<string> Failures);

/// <summary>
/// One in-scope entity's classification (the design doc §2.1 verbatim):
/// the <see cref="Status"/> (the closed
/// <see cref="UserPortabilityEntityStatus"/> set) + the
/// <see cref="AbsentReferences"/> report (<c>conflict</c> only) + the
/// <see cref="DuplicateId"/> (<c>duplicate</c> only — the existing
/// entity's id in the target).
/// </summary>
public sealed record UserPortabilityEntityClassification(
    string Kind,              // e.g. "Post", "Event", "Message"
    string EntityId,          // the id in the archive
    UserPortabilityEntityStatus Status,
    IReadOnlyList<UserPortabilityAbsentReference> AbsentReferences,
    string? DuplicateId);     // for Duplicate: the existing entity's id in the target

/// <summary>
/// The closed classification status (the design doc §2.1 verbatim).
/// </summary>
public enum UserPortabilityEntityStatus
{
    Clean,
    Duplicate,
    Conflict
}

/// <summary>
/// One absent reference in a <c>conflict</c> entity's report (the
/// design doc §2.1 verbatim) — the data the U07/U08 resolve-review UI
/// renders as the "add elsewhere → pick a target" picker.
/// </summary>
public sealed record UserPortabilityAbsentReference(
    string Kind,              // e.g. "Group", "Tag", "Component", "Page"
    string Field,             // e.g. "GroupId", "TagIds", "ComponentId"
    string Value);            // the id in the archive that is absent in the target

/// <summary>
/// The resident's per-entity decision (U05; the closed shapes, the
/// design doc §2.1 verbatim) — a <see cref="UserPortabilityResolutionKind"/>
/// (the closed <see cref="UserPortabilityResolutionKind"/> set) + the
/// <see cref="PickedTargetId"/> / <see cref="AbsentRefKind"/> /
/// <see cref="AbsentRefField"/> re-point (<c>AddElsewhere</c> only).
/// </summary>
public sealed record UserPortabilityEntityResolution(
    string Kind,              // the entity kind (must match a classification entry)
    string EntityId,          // the entity id (must match a classification entry)
    UserPortabilityResolutionKind Resolution,
    string? PickedTargetId,   // for AddElsewhere: the target the resident chose
    string? AbsentRefKind,    // for AddElsewhere: the absent-reference kind being re-pointed
    string? AbsentRefField);  // for AddElsewhere: the absent-reference field being re-pointed

/// <summary>
/// The closed per-entity decision kinds (the design doc §2.1 verbatim):
/// <see cref="AddElsewhere"/> (apply with the absent reference re-pointed
/// to <c>PickedTargetId</c>) or <see cref="Discard"/> (no write).
/// </summary>
public enum UserPortabilityResolutionKind
{
    AddElsewhere,
    Discard
}

/// <summary>
/// The resolve output (U06; the closed shapes, the design doc §2.1
/// verbatim) — the apply outcome (the §2.5 contract):
/// <see cref="Ok"/> + <see cref="AppliedCount"/> /
/// <see cref="DiscardedCount"/> + the closed <see cref="Failures"/> set
/// (a fail-closed rejection, e.g. a <c>PickedTargetId</c> the resident
/// has no standing over, leaves <see cref="Ok"/> = <c>false</c> + the
/// failure set and writes nothing).
/// </summary>
public sealed record UserPortabilityImportResult(
    bool Ok,
    int AppliedCount,
    int DiscardedCount,
    IReadOnlyList<string> Failures);

/// <summary>
/// The <see cref="IUserPortabilityService"/> shell (M27 U01) — the ctor
/// over the frozen seams (<see cref="Marten.IDocumentStore"/> for the
/// domain docs, <see cref="IUserInfoService"/> for the resident standing
/// reads, <see cref="IMediaStore"/> for the content-addressed bytes, the
/// Identity <see cref="UserManager{User}"/> for the resident's own
/// account) + the static registry / archive machinery (the
/// <see cref="PortabilityDocTypes"/> + <see cref="KumunitaArchive"/>
/// resolved from the frozen seams inside each method, as M11's
/// <see cref="PortabilityService"/> does).
/// <para>
/// <b>U01 ships signatures only</b> — all three members throw
/// <see cref="NotImplementedException"/>; U03 fills
/// <see cref="IUserPortabilityService.ExportAsync"/>, U04 fills
/// <see cref="IUserPortabilityService.ClassifyAsync"/>, U06 fills
/// <see cref="IUserPortabilityService.ResolveAsync"/> (the same "seam +
/// shell now, logic lands in the later units" posture as the M11
/// <c>PortabilityService</c> U01 pin). The U09–U11 tests + the U07/U08
/// Web surface target these members verbatim.
/// </para>
/// </summary>
public sealed class UserPortabilityService(
    Marten.IDocumentStore documentStore,
    IUserInfoService userInfoService,
    IMediaStore mediaStore,
    Microsoft.AspNetCore.Identity.UserManager<User> userManager) : IUserPortabilityService
{
    /// <inheritdoc />
    public Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default)
        => throw new NotImplementedException("M27 U03 — the resident-scoped export (the M11 archive + the D2 resident-scope marker).");

    /// <inheritdoc />
    public Task<UserPortabilityImportPlan> ClassifyAsync(string residentSubjectId, Stream archive, CancellationToken ct = default)
        => throw new NotImplementedException("M27 U04 — the clean/duplicate/conflict classification (no writes, C-M27·5).");

    /// <inheritdoc />
    public Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        CancellationToken ct = default)
        => throw new NotImplementedException("M27 U06 — the fail-closed per-entity resolve apply (C-M27·4).");
}
