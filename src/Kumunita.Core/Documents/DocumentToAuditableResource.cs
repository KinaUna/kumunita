using Kumunita.Core.Authorization;

namespace Kumunita.Core.Documents;

/// <summary>
/// Adapter (M21, ADR 0122 D2): presents a <see cref="Document"/> to the frozen
/// <see cref="IAuthorizationService"/> as an <see cref="IAuditableResource"/>.
/// Mapping (C-M21·3): <c>Id</c> = <see cref="Document.Id"/>; <c>Name</c> =
/// <see cref="Document.Title"/> (the audit row's human-facing label); <c>OwnerId</c>
/// = <see cref="Document.OwnerId"/> (the owner branch of the decision algorithm);
/// <c>Audience</c> = <see cref="Document.Audience"/> (projected verbatim — the
/// adapter never mutates it); <c>ComponentId</c> = <c>null</c> (a document is
/// not component-scoped); <c>TargetKind</c> = <c>"document"</c> (C-M21·3 — the
/// aggregate-feed row's <see cref="AccessAudit.TargetKind"/> discriminator; the
/// exact string is pinned by GATE-5).
/// <para>
/// The adapter does not *own* the <see cref="Document"/>: a single instance is
/// safe to pass into either <see cref="IAuthorizationService"/> overload
/// (<c>CanAsync</c> detail, <c>CanSeeAsync</c> feed) — each call is a value-level
/// projection, not a shared-mutable-state hazard (the same shape as
/// <see cref="Posts.PostToAuditableResource"/>). <c>sealed</c> keeps the surface
/// closed.
/// </para>
/// </summary>
public sealed class DocumentToAuditableResource : IAuditableResource
{
    public DocumentToAuditableResource(Document document) => Document = document;

    public Document Document { get; }

    public string Id => Document.Id;
    public string Name => Document.Title;
    public string? OwnerId => Document.OwnerId;
    public Audience Audience => Document.Audience;
    public string? ComponentId => null;
    public string TargetKind => "document";
}
