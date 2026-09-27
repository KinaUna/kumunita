using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Portability;

/// <summary>
/// The import outcome — <see cref="Ok"/> (the apply succeeded) or a
/// <b>closed</b> failure set (the §validate contract: the U06 web surface +
/// the U07 fail-closed pin render / assert <em>exactly</em> this, no more,
/// no less — <c>format.unsupported</c> / <c>docs.malformed</c> /
/// <c>docs.missing</c> / <c>ref.dangling:{Type}.{field}</c> /
/// <c>media.missing:{id}</c> / <c>media.mismatch:{id}</c>).
/// </summary>
public sealed record PortabilityImportResult(bool Ok, IReadOnlyList<string> Failures)
{
    /// <summary>A clean import (the apply succeeded, one
    /// <c>portability.import</c> audit row emitted).</summary>
    public static PortabilityImportResult Success { get; } = new(Ok: true, Failures: []);
}

/// <summary>
/// The operator seam the Web layer + the tests target (M11 D8,
/// C-M11·6/7). The locked public surface (the design doc §Seams) — U01's
/// shell (the ctor + the two method signatures, the bodies
/// <see cref="NotImplementedException"/> until U02–U06 fill them); the
/// Web surface resolves this (the U04/U06 <c>AdminPortabilityController</c>
/// actions) and the U07 tests target these two members verbatim.
/// <para>
/// **Operator lane, not a content-decision lane (C-M11·7):** no
/// <c>AccessAction</c> / <c>AccessVia</c> / <c>IAuthorizationService</c>
/// branch / <c>Audience</c> — the GlobalAdmin role gate (the Web
/// <c>[Authorize]</c>) is the only decision, and each action emits exactly
/// one <c>AccessAudit</c> row (<c>TargetKind "portability"</c>,
/// <c>Via = Admin</c>, verb <c>export</c> / <c>import</c>) — the
/// <em>service</em> emits it, the controller adds none (the ADR 0105
/// <c>messaging.toggle</c> shape).
/// </para>
/// </summary>
public interface IPortabilityService
{
    /// <summary>
    /// U02–U03 (export) — one complete, valid <c>*.kumunita</c> archive
    /// (the §layout: the docs + the no-secret principals + the config,
    /// then the media bytes + the finalized manifest). Emits exactly one
    /// <c>portability.export</c> <c>AccessAudit</c> row (<c>Via = Admin</c>).
    /// </summary>
    /// <param name="actorId">The GlobalAdmin actor's <c>subjectId</c> (the audit row's actor).</param>
    Task<Stream> ExportAsync(string actorId, CancellationToken ct = default);

    /// <summary>
    /// U05–U06 (import) — validate-then-apply, fail-closed (C-M11·4): the
    /// validate phase runs to completion <em>before any write</em> (the
    /// §validate checks (a)–(d)); a validate failure returns the
    /// <see cref="PortabilityImportResult"/> closed failure set and writes
    /// <b>zero</b> rows. On success it applies in the locked order
    /// (principals → docs → media → config, §validate "apply phase") and
    /// emits exactly one <c>portability.import</c> <c>AccessAudit</c> row
    /// (<c>Via = Admin</c>).
    /// </summary>
    /// <param name="actorId">The GlobalAdmin actor's <c>subjectId</c> (the audit row's actor).</param>
    /// <param name="archive">The uploaded <c>*.kumunita</c> archive stream.</param>
    Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default);
}

/// <summary>
/// The <see cref="IPortabilityService"/> shell (M11 U01) — the ctor over
/// the frozen seams (<see cref="Marten.IDocumentStore"/> for the domain
/// docs, the Identity <see cref="UserManager{User}"/> for the principals
/// re-creation, <see cref="IMediaStore"/> / <see cref="IMediaFileStore"/>
/// for the bytes) + the registry / archive (the static
/// <see cref="PortabilityDocTypes"/> + <see cref="KumunitaArchive"/>).
/// <para>
/// <b>U01 ships signatures only</b> — both members throw
/// <see cref="NotImplementedException"/>; U02–U04 fill
/// <see cref="ExportAsync"/>, U05–U06 fill <see cref="ImportAsync"/> (the
/// same "seam + shell now, logic lands in the later units" posture as the
/// M4 <c>EventService</c> U01 pin). The U07 tests + the U04/U06 web
/// surface target these members verbatim.
/// </para>
/// </summary>
public sealed class PortabilityService(
    Marten.IDocumentStore documentStore,
    Identity.AppDbContext appDbContext,
    UserManager<Identity.User> userManager,
    IOptions<CommunityOptions> communityOptions,
    IMediaStore mediaStore,
    IMediaFileStore mediaFileStore) : IPortabilityService
{
    /// <inheritdoc />
    public async Task<Stream> ExportAsync(string actorId, CancellationToken ct = default)
    {
        // U02 (this unit) — the docs + the no-secret principals + the config
        // (the media bytes + the manifest finalize are U03's — the reserved
        // seam below).
        var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(documentStore, ct);
        var principals = await PrincipalsExport.ExportAsync(appDbContext, userManager, documentStore, ct);
        var config = await ConfigExport.ExportAsync(documentStore, communityOptions, ct);

        // U03 — the media bytes (the `media/` section) + the manifest finalize
        // (the `format` / `generated_at` / `community_name` / `doc_counts` /
        // `media_manifest` head). Until U03 lands, this is a minimal
        // placeholder (the media section empty, the manifest's doc counts
        // populated, the media manifest empty) so the archive is
        // structurally complete over the three U02 sections.
        var media = new Dictionary<string, byte[]>();
        var manifest = new PortabilityManifest
        {
            Format = PortabilityManifest.FormatVersion,
            GeneratedAt = DateTimeOffset.UtcNow,
            CommunityName = communityOptions.Value.Name,
            DocCounts = docCounts,
            MediaManifest = [],
        };

        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest, docs, media, principals, config, ct);
        stream.Position = 0;
        return stream;
    }

    /// <inheritdoc />
    public Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default) =>
        throw new NotImplementedException("M11 U01 shell — the import body lands in U05–U06 (the validate-then-apply, the identity re-creation with secrets reset, the config apply).");
}
