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
    RoleManager<IdentityRole> roleManager,
    IOptions<CommunityOptions> communityOptions,
    IMediaStore mediaStore,
    IMediaFileStore mediaFileStore) : IPortabilityService
{
    /// <inheritdoc />
    public async Task<Stream> ExportAsync(string actorId, CancellationToken ct = default)
    {
        // U02 — the docs + the no-secret principals + the config.
        var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(documentStore, ct);
        var principals = await PrincipalsExport.ExportAsync(appDbContext, userManager, documentStore, ct);
        var config = await ConfigExport.ExportAsync(documentStore, communityOptions, ct);

        // U03 (this unit) — the media bytes (the `media/` section, the
        // `{Id[0..2]}/{Id}` content-addressed layout the C-M11·3 pin) + the
        // media manifest + the manifest finalize (the `format` /
        // `generated_at` / `community_name` / `doc_counts` / `media_manifest`
        // head). After this the export produces a complete, valid
        // `*.kumunita` archive (the U04 web surface just streams it).
        var (media, mediaManifest) = await MediaExport.ExportAsync(documentStore, mediaStore, ct);
        var manifest = ManifestFinalize.Build(
            communityOptions.Value.Name,
            docCounts,
            mediaManifest,
            DateTimeOffset.UtcNow);

        // U04 — the one `portability.export` AccessAudit row (C-M11·6, the
        // ADR 0105 `messaging.toggle` one-audit-row shape — the controller adds
        // none; `Action` is the verb, `TargetKind`/`TargetId` are
        // "portability"). Emitted after the archive is built (a failed build
        // throws before this point, so no audit row for a refused export).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Authorization.AccessAudit
        {
            Id = System.Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "portability.export",
            TargetKind = "portability",
            TargetId = "portability",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        var stream = new MemoryStream();
        await KumunitaArchive.WriteAsync(stream, manifest, docs, media, principals, config, ct);
        stream.Position = 0;
        return stream;
    }

    /// <inheritdoc />
    public async Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default)
    {
        // ── Read the archive (the §layout inverse) ─────────────────────
        var data = await KumunitaArchive.ReadAsync(archive, ct).ConfigureAwait(false);

        // ── §validate — the fail-closed gate (C-M11·4) ─────────────────
        // Runs to completion before ANY write (the C-M11·4 pin). A
        // failure returns the closed failure set and writes ZERO rows
        // (the U07 fail-closed test witnesses it: the fresh instance
        // has zero rows after a failed import).
        var validation = PortabilityValidate.Run(data);
        if (!validation.Ok)
            return new PortabilityImportResult(Ok: false, Failures: validation.Failures);

        // ── §apply — one commit, the pinned dependency order (C-M11·4) ─
        // The locked order (design doc §validate "apply phase"):
        //   1. Re-create the Identity principals (the
        //      ApplyIdentityAsync — secrets reset, C-M11·2 import
        //      boundary) + the role re-apply.
        //   2. Store the domain documents in the §inventory import
        //      order (the D7 order, parents before children).
        //   3. Copy the media bytes into the volume at the
        //      {Id[0..2]}/{Id} layout (C-M11·3).
        //   4. Apply the config (the ApplyConfigAsync — the
        //      LocaleSettings + LanguageCatalog, the §config field set).

        // Step 1 — the identity re-creation (from identity/principals.json,
        // keyed by the exported subjectId, secrets reset — a fresh
        // non-portable password + a fresh security stamp — the C-M11·2
        // import boundary) + the role re-apply. Runs BEFORE the docs apply
        // (the §apply order — the subjectId sign-in works before the
        // content graph is stored).
        var principals = KumunitaArchive.FromJson<List<PortabilityPrincipal>>(data.Principals)
            ?? new List<PortabilityPrincipal>();
        await PortabilityApplyIdentity.ApplyAsync(userManager, roleManager, principals, ct).ConfigureAwait(false);

        // Step 2 — store the domain documents in the registry's
        // import order (parents before children — the D7 order).
        await PortabilityApplyDocuments.ApplyAsync(documentStore, data, ct).ConfigureAwait(false);

        // Step 3 — copy the media bytes into the volume at the
        // {Id[0..2]}/{Id} content-addressed layout (C-M11·3 — the
        // same layout as the local volume; the dedup-by-content-hash
        // is preserved).
        await PortabilityApplyMedia.ApplyAsync(mediaFileStore, data, ct).ConfigureAwait(false);

        // Step 4 — the config apply (the LocaleSettings + the
        // LanguageCatalog — the §config field set; the U02 ConfigExport
        // mirror, verbatim). The last apply step (the §apply order —
        // the LanguageCatalog / LocaleSettings re-materialize after the
        // *Translation rows that reference them are stored).
        var config = KumunitaArchive.FromJson<PortabilityConfig>(data.Config) ?? new PortabilityConfig();
        await PortabilityApplyConfig.ApplyAsync(documentStore, config, ct).ConfigureAwait(false);

        // The one portability.import AccessAudit row (TargetKind
        // "portability", Via = Admin, verb import) — emitted by the
        // service (the controller adds none, the ADR 0105
        // messaging.toggle shape). After a clean apply (a validate
        // failure returned the closed failure set before this point, so
        // no audit row for a refused import — the same "no audit for a
        // refused action" posture as the export lane above).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Authorization.AccessAudit
        {
            Id = System.Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "portability.import",
            TargetKind = "portability",
            TargetId = "portability",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        return PortabilityImportResult.Success;
    }
}
