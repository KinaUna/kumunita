using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Marten;
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
    /// <param name="ct">Cancellation.</param>
    Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default);

    /// <summary>
    /// The <see cref="ImportAsync(string, Stream, CancellationToken)"/>
    /// overload with an explicit <see cref="PortabilityImportMode"/> (ADR
    /// 0193). The Web surface resolves this (the
    /// <c>AdminPortabilityController</c> import action reads the form's
    /// <c>keepExisting</c> checkbox into the mode) and the tests target
    /// this overload verbatim. <see cref="PortabilityImportMode.CleanSlate"/> (the default —
    /// the D1 restore semantic): a clean/fresh instance is assumed; if any
    /// archived principal already exists in the target identity store the
    /// import is refused with a per-account <c>principal.exists:{email}</c>
    /// failure (a closed, renderable refusal — not an unhandled 500).
    /// <see cref="PortabilityImportMode.KeepExisting"/>: merge mode —
    /// existing principals and content documents are kept intact; only new
    /// (non-existent) ones are added; existing content is never overwritten.
    /// </summary>
    /// <param name="actorId">The GlobalAdmin actor's <c>subjectId</c> (the audit row's actor).</param>
    /// <param name="archive">The uploaded <c>*.kumunita</c> archive stream.</param>
    /// <param name="mode">The import mode (ADR 0193).</param>
    /// <param name="ct">Cancellation.</param>
    Task<PortabilityImportResult> ImportAsync(
        string actorId, Stream archive, PortabilityImportMode mode, CancellationToken ct = default);
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
    public Task<PortabilityImportResult> ImportAsync(
        string actorId, Stream archive, CancellationToken ct = default)
        => ImportAsync(actorId, archive, PortabilityImportMode.CleanSlate, ct);

    /// <inheritdoc />
    public async Task<PortabilityImportResult> ImportAsync(
        string actorId, Stream archive, PortabilityImportMode mode, CancellationToken ct = default)
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

        // The ADR 0193 mode gate (before any write):
        // ── CleanSlate (the default — the D1 restore semantic): the import
        //    REPLACES the instance's state. First the instance's version of
        //    the archive's surface (the identity principals + the content
        //    docs + the media bytes) is wiped, then the archive is applied
        //    onto the now-fresh store. This is what makes an import over an
        //    instance that already has accounts/content succeed (the user's
        //    original 500 — a "principal already exists" collision — is now
        //    resolved by the wipe). The operator's pre-import backup is the
        //    rollback path (the M11 D1 semantic, unchanged).
        // ── KeepExisting: merge mode — NO wipe. Existing principals and
        //    content docs are kept intact; only new (non-existent) ones are
        //    added. The collision is expected and not a failure.
        if (mode == PortabilityImportMode.CleanSlate)
            await WipeForCleanSlateImportAsync(data, ct).ConfigureAwait(false);

        // ADR 0275 — the KeepExisting principal merge map (archive principal
        // SubjectId → existing User.Id): the rows an archived principal merged
        // into an existing account. PortabilityApplyDocuments uses it to skip
        // the archive's doubled Profile (the directory is not doubled) and to
        // re-point the archive's content's principal references at the existing
        // account's id. CleanSlate → empty map → no skip, no re-point (exactly
        // the D1 restore semantic).
        var principalMergeMap = await PortabilityApplyIdentity.ApplyAsync(
            userManager, roleManager, principals, mode, ct).ConfigureAwait(false);

        // The config block (the LocaleSettings + the LanguageCatalog — the
        // §config field set). Parsed here, BEFORE the docs apply, so the ADR
        // 0274 KeepExisting tag merge can consult the archive's default
        // language (the merge key: a tag is merged when its slug AND its
        // display name in the default language are equal). The same POCO is
        // re-applied in step 4 (the §apply order is preserved — the config
        // WRITE still happens last, after the *Translation rows).
        var config = KumunitaArchive.FromJson<PortabilityConfig>(data.Config) ?? new PortabilityConfig();

        // Step 2 — store the domain documents in the registry's
        // import order (parents before children — the D7 order). In
        // KeepExisting mode, rows whose id already exists in the target
        // store are skipped (the existing content is kept intact); ADR 0274 —
        // the tags are merged (the archive's copy is dropped, its references
        // re-pointed at the existing tag, its translations folded in) when the
        // slug + default-language name are equal.
        await PortabilityApplyDocuments.ApplyAsync(
            documentStore, data, mode, config.Locale.DefaultLanguageCode, principalMergeMap, ct).ConfigureAwait(false);

        // Step 3 — copy the media bytes into the volume at the
        // {Id[0..2]}/{Id} content-addressed layout (C-M11·3 — the
        // same layout as the local volume; the dedup-by-content-hash
        // is preserved). Idempotent in both modes (the C-MED·4 dedup:
        // identical bytes → same id → no second volume file).
        await PortabilityApplyMedia.ApplyAsync(mediaFileStore, data, ct).ConfigureAwait(false);

        // Step 4 — the config apply (the LocaleSettings + the
        // LanguageCatalog — the §config field set; the U02 ConfigExport
        // mirror, verbatim). The last apply step (the §apply order —
        // the LanguageCatalog / LocaleSettings re-materialize after the
        // *Translation rows that reference them are stored). In
        // KeepExisting mode, rows that already exist are kept intact.
        await PortabilityApplyConfig.ApplyAsync(documentStore, config, mode, ct).ConfigureAwait(false);

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

    /// <summary>
    /// The CleanSlate wipe (ADR 0193) — runs <em>before</em> the apply, so
    /// the archive is applied onto a fresh instance (the D1 restore
    /// semantic: the instance's version of the archive's surface is
    /// replaced, not merged). Wipes, in the pinned order:
    /// <ol>
    /// <li>The <b>Identity principals</b> (the EF Core <c>identity</c>
    /// schema — every <c>AspNetUsers</c> row, which cascades the
    /// <c>AspNetUserRoles</c> / <c>AspNetUserClaims</c> /
    /// <c>AspNetUserTokens</c> / <c>AspNetUserLogins</c> rows), so the
    /// archive's principals re-create cleanly (no "already taken"
    /// collision — the user's original 500).</li>
    /// <li>The <b>Marten content docs</b> — every document type in the
    /// §inventory's 44 closed set (the same <see cref="PortabilityDocTypes"/>
    /// registry the apply loop drives off, via
    /// <see cref="PortabilityApplyDocuments.DeleteAllAsync{T}"/>).</li>
    /// <li>The <b>media bytes</b> on the volume + the <c>MediaObject</c>
    /// catalog rows (already covered by the docs wipe, but the <em>bytes</em>
    /// are a separate surface — the <see cref="IMediaFileStore"/> delete).</li>
    /// </ol>
    /// The operational docs (<c>AccessAudit</c> / <c>OutboxEmail</c> /
    /// <c>EmailDeadLetter</c> / <c>IdentityToken</c>) are deliberately
    /// <b>not</b> wiped — they are the instance's own operational state
    /// (the §inventory "Excluded" set), not resident content the archive
    /// carries. The audit trail of the wipe itself (the
    /// <c>portability.import</c> row) is emitted <em>after</em> the apply,
    /// so it survives.
    /// <para>
    /// <b>KeepExisting mode does not call this</b> — the existing state is
    /// the point (merge, not replace).
    /// </para>
    /// </summary>
    private async Task WipeForCleanSlateImportAsync(
        KumunitaArchiveData data, CancellationToken ct)
    {
        // 1. The Identity principals (the EF Core identity schema). The
        //    user-dependent tables are wiped <em>first, in the dependent-
        //    order</em> (AspNetUserRoles / Claims / Logins / Tokens, then
        //    AspNetUsers) — this is deliberate and not left to cascade:
        //    <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync"/>
        //    does not reliably cascade the dependent rows, and a surviving
        //    AspNetUserRoles row for a re-created subjectId would collide
        //    with the apply's <c>AddToRoleAsync</c> (a duplicate
        //    (userId, roleId) — the exact class of 500 the ADR 0193 lane
        //    exists to remove). The AspNetRoles / AspNetRoleClaims rows are
        //    the standing definitions (Member / Moderator / GlobalAdmin /
        //    Translator) — left in place; the re-creator's
        //    roleManager.FindByNameAsync / CreateAsync ensures existence
        //    and the archive's roles re-attach onto the fresh principals.
        var users = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(appDbContext.Users, ct).ConfigureAwait(false);
        if (users.Count > 0)
        {
            var userIds = users.Select(u => u.Id).ToList();
            // The four user-dependent tables (the stock Identity schema,
            // all keyed by the principal id). Each is a flat delete of the
            // rows for the to-be-deleted principals — one ExecuteDelete per
            // table, no cascade reliance.
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ExecuteDeleteAsync(appDbContext.UserRoles
                    .Where(r => userIds.Contains(r.UserId)), ct).ConfigureAwait(false);
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ExecuteDeleteAsync(appDbContext.UserClaims
                    .Where(c => userIds.Contains(c.UserId)), ct).ConfigureAwait(false);
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ExecuteDeleteAsync(appDbContext.UserLogins
                    .Where(l => userIds.Contains(l.UserId)), ct).ConfigureAwait(false);
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ExecuteDeleteAsync(appDbContext.UserTokens
                    .Where(t => userIds.Contains(t.UserId)), ct).ConfigureAwait(false);
            // The principals themselves.
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ExecuteDeleteAsync(appDbContext.Users
                    .Where(u => userIds.Contains(u.Id)), ct).ConfigureAwait(false);
            // ExecuteDeleteAsync bypasses the change tracker — any User /
            // dependent entities already attached to this context's tracker
            // (e.g. a principal read earlier in the same scoped request) stay
            // tracked, and the apply's re-CreateAsync of the same subjectId
            // would then throw an EF identity conflict ("another instance with
            // the same key value … is already being tracked"). A clean-slate
            // import wipes every principal, so dropping the whole tracker is
            // the correct fresh-slate semantic and keeps the re-create clean
            // regardless of what was previously tracked in this context.
            appDbContext.ChangeTracker.Clear();
        }

        // 2. The media bytes (the volume) — for every MediaObject listed in
        //    the archive's media manifest, the byte file. DeleteIfExists is
        //    idempotent (a missing file is a no-op).
        if (data.Manifest?.MediaManifest is not null)
        {
            foreach (var m in data.Manifest.MediaManifest)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(m.Id))
                    continue;
                await mediaFileStore.DeleteIfExistsAsync(m.Id, ct).ConfigureAwait(false);
            }
        }

        // 3. The Marten content docs — every document type in the 44 closed
        //    §inventory set (the same registry the apply loop drives off,
        //    via the generic DeleteAllAsync<T> helper). This wipes the
        //    MediaObject catalog rows, the Profile rows (keyed by the
        //    now-deleted principal's subjectId), the config rows
        //    (LocaleSettings / LanguageCatalog), and the full content graph.
        foreach (var entry in PortabilityDocTypes.InOrder())
        {
            ct.ThrowIfCancellationRequested();
            var docType = PortabilityExportDocuments.NameToType[entry.Type];
            var deleteAll = typeof(PortabilityApplyDocuments)
                .GetMethod(nameof(PortabilityApplyDocuments.DeleteAllAsync),
                    System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(docType);
            await (Task)deleteAll.Invoke(null, [documentStore, (object)ct])!;
        }
    }
}
