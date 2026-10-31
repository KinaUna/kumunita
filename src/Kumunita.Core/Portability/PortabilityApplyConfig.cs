using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Portability;

/// <summary>
/// The §apply step 4 — the **config apply** (D2 / §config): the
/// <c>config.json</c> POCO's <c>community</c> / <c>locale</c> /
/// <c>languages[]</c> blocks re-materialized on the instance. Mirrors U02's
/// <see cref="ConfigExport.ExportAsync"/> field set **verbatim** (the
/// design doc §config "the U02 ConfigExport mirror, verbatim"): the
/// <c>LocaleSettings</c> singleton (the seven instance-level locale fields)
/// + every <c>LanguageCatalog</c> row + the <c>CommunityOptions</c>
/// (name + support email).
/// <para>
/// The <c>LocaleSettings</c> + <c>LanguageCatalog</c> state travels <em>here</em>
/// (via <c>config.json</c>), <b>not</b> as <c>docs/</c> rows (drift guard
/// entry 3) — this is the single home of the instance identity +
/// localizations. The config apply is the **last** apply step (the
/// §validate "apply phase" order: principals → docs → media → config), so
/// the <c>LanguageCatalog</c> / <c>LocaleSettings</c> re-materialize after
/// the <c>*Translation</c> rows that reference them are stored.
/// </para>
/// <para>
/// The <c>community</c> block (<c>CommunityOptions.Name</c> /
/// <c>SupportEmail</c>) is **not applied** here — it is a host-config
/// value (the <c>Community__*</c> environment variables, ADR 0002 /
/// OPS.md), not instance state in a store; the archive carries it as the
/// self-description + the <c>manifest.json</c> <c>community_name</c>. The
/// applied instance state is the <c>locale</c> + <c>languages</c> blocks
/// (the store-backed <c>LocaleSettings</c> / <c>LanguageCatalog</c> docs).
/// </para>
/// </summary>
public static class PortabilityApplyConfig
{
    /// <summary>
    /// Applies the config snapshot (the §config field set: the
    /// <c>LocaleSettings</c> singleton + the <c>LanguageCatalog</c> rows).
    /// The <c>community</c> block is the host-config self-description
    /// (the <c>CommunityOptions</c>), carried in the archive as the
    /// self-description — not a store write.
    /// </summary>
    /// <param name="documentStore">The frozen Marten seam (the
    /// <c>LocaleSettings</c> + <c>LanguageCatalog</c> target — the
    /// <see cref="ConfigExport"/> source, mirrored verbatim).</param>
    /// <param name="config">The <c>config.json</c> POCO — the
    /// <c>community</c> / <c>locale</c> / <c>languages[]</c> / <c>escalation</c>
    /// blocks (the U02 <see cref="ConfigExport"/> mirror, verbatim).</param>
    /// <param name="mode">The import mode (ADR 0193).
    /// <see cref="PortabilityImportMode.CleanSlate"/> — the D1 restore
    /// semantic: the <c>LocaleSettings</c> singleton and every
    /// <c>LanguageCatalog</c> row are re-materialized from the archive
    /// (a fresh instance has no rows; a re-import overwrites them).
    /// <see cref="PortabilityImportMode.KeepExisting"/> — merge mode: a
    /// row that already exists in the target store is <b>kept intact</b>
    /// (the existing config is never overwritten); only new
    /// <c>LanguageCatalog</c> rows are added.</param>
    /// <param name="actorId">The import's actor (the admin who ran the
    /// import) — stamped as <c>UpdatedBy</c> on the
    /// <see cref="EscalationOutboundConfig"/> row the apply (re)stores
    /// (ADR 0276 — the Web save lane's <c>config.UpdatedBy = actor</c>
    /// shape, mirrored for the portability surface; the actor is the
    /// restore's author, not the source instance's admin).</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        Marten.IDocumentStore documentStore,
        PortabilityConfig config,
        PortabilityImportMode mode,
        string actorId,
        CancellationToken ct)
    {
        // The full session (the IDocumentStore.OpenSession seam, the same
        // as PortabilityApplyDocuments.ApplyAsync — the IQuerySession
        // does not expose Store / SaveChangesAsync).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());

        // The LocaleSettings singleton (one row per instance, Id =
        // "singleton") — the seven instance-level locale fields, verbatim
        // from the U02 ConfigExport mirror (the §config field set:
        // default language + timezone + date format + the four boolean
        // switches: sign-up open, notify admins on signup, announcement
        // comments, messaging).
        //
        // CleanSlate (the D1 restore semantic): the archive's state IS the
        // instance's state — the singleton row is (re)materialized from the
        // archive, overwriting whatever the seeder set on first boot. The
        // FirstBootSeeder's row (UTC / FloorFormat / the seeder's boolean
        // floors) is the *fresh-instance* default, not the *restore* value:
        // a CleanSlate import's whole point is that the archive's instance
        // becomes the target's instance, and that includes its platform
        // default time zone (ADR 0019) and its sign-up posture (ADR 0050) —
        // the two fields the user reported missing after a restore (the
        // seeder's UTC / IsSignupOpen = false had been sticking because the
        // apply's old "write only when null" shape saw the seeder's row and
        // skipped the write).
        //
        // KeepExisting (ADR 0193): the existing row is kept intact — the
        // instance's current config (the operator's own time zone / sign-up
        // posture) is never overwritten by the imported community's; only
        // the imported community's *content* is added. (The same keep-first
        // discipline ADR 0274 / ADR 0275 apply to the tag / principal
        // merges.)
        if (mode == PortabilityImportMode.CleanSlate)
        {
            var singleton = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
                ?? new LocaleSettings { Id = LocaleSettings.SingletonId };
            singleton.DefaultLanguageCode = config.Locale.DefaultLanguageCode ?? singleton.DefaultLanguageCode;
            singleton.DefaultTimezone = config.Locale.DefaultTimezone ?? singleton.DefaultTimezone;
            singleton.DefaultDateFormat = config.Locale.DefaultDateFormat ?? singleton.DefaultDateFormat;
            singleton.IsSignupOpen = config.Locale.IsSignupOpen;
            singleton.NotifyAdminsOnSignup = config.Locale.NotifyAdminsOnSignup;
            singleton.AnnouncementCommentsEnabled = config.Locale.AnnouncementCommentsEnabled;
            singleton.MessagingEnabled = config.Locale.MessagingEnabled;
            session.Store(singleton);
        }
        // KeepExisting — the existing LocaleSettings row is kept intact
        // (the instance's own config is never overwritten by the import).

        // Every LanguageCatalog row (the per-instance language catalog —
        // the §config languages[] field set, the U02 ConfigExport mirror,
        // verbatim). Keyed by the BCP-47 code (the catalog's identity,
        // ADR 0005 B).
        // CleanSlate (the D1 restore semantic): the load-then-Store
        // idempotent shape — a fresh instance has no row (created), a
        // re-import overwrites it with the archive's state.
        // KeepExisting (ADR 0193): an existing row is kept intact — only
        // a new language code is added (the "only added if it doesn't
        // exist already" semantic).
        foreach (var language in config.Languages)
        {
            if (string.IsNullOrEmpty(language.Id))
                continue;

            if (mode == PortabilityImportMode.KeepExisting)
            {
                // KeepExisting — the existing row is kept intact.
                if (await session.LoadAsync<LanguageCatalog>(language.Id, ct) is not null)
                    continue;
                session.Store(new LanguageCatalog
                {
                    Id = language.Id,
                    NativeName = language.NativeName,
                    Enabled = language.Enabled,
                    SortOrder = language.SortOrder,
                });
                continue;
            }

            // CleanSlate — the load-then-Store idempotent shape (the
            // original D1 restore behaviour, unchanged).
            var row = await session.LoadAsync<LanguageCatalog>(language.Id, ct)
                ?? new LanguageCatalog { Id = language.Id };
            row.NativeName = language.NativeName;
            row.Enabled = language.Enabled;
            row.SortOrder = language.SortOrder;
            session.Store(row);
        }

        // ADR 0276 — the /admin/escalation outbound config (the
        // EscalationOutboundConfig singleton's non-secret fields: the
        // receiving endpoint + the enabled switch). The Token (the
        // plaintext the origin admin was handed, the OUTGOING secret) is
        // structurally absent from the config's escalation block (the
        // C-M11·2 import boundary applied to the escalation surface) —
        // the restore carries the forwarding endpoint + posture, and the
        // admin re-enters the token after a CleanSlate import (the same
        // "re-enter your credential" shape as the password reset, the
        // PortabilityApplyIdentity boundary). The apply never writes the
        // token: a fresh restore's empty token is the admin's to set, and
        // a target that already holds a token keeps it (the apply's
        // keep-first discipline, the ADR 0274 / ADR 0275 shape).
        //
        // CleanSlate (the D1 restore semantic): the archive's escalation
        // config IS the instance's config — the singleton row is
        // (re)materialized from the archive's endpoint + enabled (the
        // token is blank, the admin re-enters it on /admin/escalation).
        // A null escalation block (the source never set one up) leaves the
        // target's row untouched — a fresh instance's forwarder env-var
        // fallback still works, and a target that had a config keeps it.
        //
        // KeepExisting (ADR 0193): the existing row is kept intact — the
        // target's own escalation config (endpoint + token + posture) is
        // never overwritten by the imported community's; only the imported
        // community's *content* is added. (The same keep-first discipline
        // ADR 0274 / ADR 0275 apply to the tag / principal merges.)
        if (mode == PortabilityImportMode.CleanSlate && config.Escalation is not null)
        {
            var escalationRow = await session.LoadAsync<EscalationOutboundConfig>(
                ConfigExport.EscalationOutboundConfigSingletonId, ct)
                ?? new EscalationOutboundConfig { Id = ConfigExport.EscalationOutboundConfigSingletonId };
            escalationRow.Endpoint = config.Escalation.Endpoint ?? string.Empty;
            escalationRow.Enabled = config.Escalation.Enabled;
            // The Token is NOT written (structurally absent from the
            // config's escalation block — the C-M11·2 boundary). A fresh
            // restore's row has the POCO's empty-string token default; a
            // re-import over a target that already holds a token leaves it
            // intact (the load above returned the existing row; we only
            // touch Endpoint + Enabled + the audit stamps).
            escalationRow.Updated = DateTimeOffset.UtcNow;
            escalationRow.UpdatedBy = actorId;
            session.Store(escalationRow);
        }

        // One commit (the C-M11·4 "one commit" pin — the config is the
        // last apply step; a mid-apply failure is the documented restore
        // path, never a silently-accepted half-import).
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
