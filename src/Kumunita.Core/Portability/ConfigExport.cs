using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Marten;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Portability;

/// <summary>
/// The U02 config snapshot (D2 / §config) — the <c>config.json</c> payload.
/// Reads the <see cref="CommunityOptions"/> (name + support email), the
/// <see cref="LocaleSettings"/> singleton, and every
/// <see cref="LanguageCatalog"/> row, and projects them into the locked
/// §config POCO set (<see cref="PortabilityConfig"/> + the sub-POCOs).
/// <para>
/// The <c>LocaleSettings</c> + <c>LanguageCatalog</c> state travels
/// <em>here</em> (via <c>config.json</c>), <b>not</b> as <c>docs/</c> rows
/// (drift guard entry 3) — this is the single home of the instance identity
/// + localizations. U06's <c>ApplyConfigAsync</c> mirrors the field set
/// verbatim.
/// </para>
/// </summary>
public static class ConfigExport
{
    /// <summary>
    /// Exports the config snapshot (the §config field set: community +
    /// locale + languages).
    /// </summary>
    /// <param name="documentStore">The frozen Marten seam (the <see cref="LocaleSettings"/> + <see cref="LanguageCatalog"/> source).</param>
    /// <param name="communityOptions">The <see cref="CommunityOptions"/> (name + support email).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The <c>config.json</c> payload bytes (a <see cref="PortabilityConfig"/>).</returns>
    public static async Task<byte[]> ExportAsync(
        IDocumentStore documentStore,
        IOptions<CommunityOptions> communityOptions,
        CancellationToken ct = default)
    {
        var community = communityOptions.Value;

        await using var session = documentStore.QuerySession();

        // The LocaleSettings singleton (one row per instance, Id = "singleton").
        var locale = (await session.Query<LocaleSettings>()
            .Where(l => l.Id == LocaleSettings.SingletonId)
            .ToListAsync())
            .FirstOrDefault()
            ?? new LocaleSettings();

        // Every LanguageCatalog row (the enabled set + the disabled).
        var languages = (await session.Query<LanguageCatalog>().ToListAsync())
            .OrderBy(l => l.SortOrder)
            .Select(l => new PortabilityConfigLanguage
            {
                Id = l.Id,
                NativeName = l.NativeName,
                Enabled = l.Enabled,
                SortOrder = l.SortOrder,
            })
            .ToList();

        // ADR 0276 — the /admin/escalation outbound config (the
        // EscalationOutboundConfig singleton's non-secret fields: the
        // receiving endpoint + the enabled switch). The Token (the
        // plaintext the origin admin was handed, the OUTGOING secret) is
        // structurally absent from the PortabilityConfigEscalation POCO
        // (the C-M11·2 import boundary applied to the escalation surface) —
        // the restore carries the forwarding endpoint + posture, and the
        // admin re-enters the token after a CleanSlate import (the same
        // "re-enter your credential" shape as the password reset). A
        // missing row (a fresh instance that never set up escalation) is
        // exported as null — the JSON field is omitted, and the apply
        // leaves the target's row untouched (the forwarder's env-var
        // fallback still works on both sides).
        PortabilityConfigEscalation? escalation = null;
        var outbound = await session.LoadAsync<EscalationOutboundConfig>(
            EscalationOutboundConfigSingletonId, ct);
        if (outbound is not null)
        {
            escalation = new PortabilityConfigEscalation
            {
                Endpoint = outbound.Endpoint,
                Enabled = outbound.Enabled,
            };
        }

        var config = new PortabilityConfig
        {
            Community = new PortabilityConfigCommunity
            {
                Name = community.Name,
                SupportEmail = community.SupportEmail,
            },
            Locale = new PortabilityConfigLocale
            {
                DefaultLanguageCode = locale.DefaultLanguageCode,
                DefaultTimezone = locale.DefaultTimezone,
                DefaultDateFormat = locale.DefaultDateFormat,
                IsSignupOpen = locale.IsSignupOpen,
                NotifyAdminsOnSignup = locale.NotifyAdminsOnSignup,
                AnnouncementCommentsEnabled = locale.AnnouncementCommentsEnabled,
                MessagingEnabled = locale.MessagingEnabled,
            },
            Languages = languages,
            Escalation = escalation,
        };

        return KumunitaArchive.ToJson(config);
    }

    /// <summary>
    /// ADR 0276 — the fixed singleton id for the <see cref="EscalationOutboundConfig"/>
    /// row (the Web layer's <c>EscalationConfigController</c> + <c>EscalationForwarder</c>
    /// both read/write under this id — the "outbound-config" conventional id
    /// the doc comment names; defined once here so the portability surface
    /// does not re-invent it).
    /// </summary>
    public const string EscalationOutboundConfigSingletonId = "outbound-config";
}
