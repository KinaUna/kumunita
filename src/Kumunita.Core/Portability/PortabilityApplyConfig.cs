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
    /// <c>community</c> / <c>locale</c> / <c>languages[]</c> blocks (the
    /// U02 <see cref="ConfigExport"/> mirror, verbatim).</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        Marten.IDocumentStore documentStore,
        PortabilityConfig config,
        CancellationToken ct)
    {
        // The full session (the IDocumentStore.OpenSession seam, the same
        // as PortabilityApplyDocuments.ApplyAsync — the IQuerySession
        // does not expose Store / SaveChangesAsync).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());

        // The LocaleSettings singleton (one row per instance, Id =
        // "singleton") — the seven instance-level locale fields, verbatim
        // from the U02 ConfigExport mirror (the §config field set). The
        // load-then-Store idempotent shape (the FirstBootSeeder idiom —
        // a fresh instance has no row, a re-import overwrites it with the
        // archive's state).
        var locale = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            ?? new LocaleSettings { Id = LocaleSettings.SingletonId };
        locale.DefaultLanguageCode = config.Locale.DefaultLanguageCode!;
        locale.DefaultTimezone = config.Locale.DefaultTimezone!;
        locale.DefaultDateFormat = config.Locale.DefaultDateFormat!;
        locale.IsSignupOpen = config.Locale.IsSignupOpen;
        locale.NotifyAdminsOnSignup = config.Locale.NotifyAdminsOnSignup;
        locale.AnnouncementCommentsEnabled = config.Locale.AnnouncementCommentsEnabled;
        locale.MessagingEnabled = config.Locale.MessagingEnabled;
        session.Store(locale);

        // Every LanguageCatalog row (the per-instance language catalog —
        // the §config languages[] field set, the U02 ConfigExport mirror,
        // verbatim). Keyed by the BCP-47 code (the catalog's identity,
        // ADR 0005 B); the load-then-Store idempotent shape (a fresh
        // instance has no row, a re-import overwrites it with the
        // archive's state).
        foreach (var language in config.Languages)
        {
            var row = await session.LoadAsync<LanguageCatalog>(language.Id, ct)
                ?? new LanguageCatalog { Id = language.Id };
            row.NativeName = language.NativeName;
            row.Enabled = language.Enabled;
            row.SortOrder = language.SortOrder;
            session.Store(row);
        }

        // One commit (the C-M11·4 "one commit" pin — the config is the
        // last apply step; a mid-apply failure is the documented restore
        // path, never a silently-accepted half-import).
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
