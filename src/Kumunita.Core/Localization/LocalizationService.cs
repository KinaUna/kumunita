using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Pages;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Localization;

/// <summary>
/// The admin-management seam's implementation (M·4, M·6, M·7).
/// <para>
/// <b>HTTP-free</b> (M·8): the Web <c>LanguagesController</c> (U5) is a thin
/// GlobalAdmin-gated surface over this — the actor's identity arrives as a plain
/// <see cref="string"/> (the subject id), never a cookie or a claim.
/// </para>
/// <para>
/// <b>Session shape (M·6, the UserInfoService admin-action idiom —
/// ARCHITECTURE.md §5):</b> every mutating call runs in a single document session
/// and ends in one <c>SaveChangesAsync</c>, so the domain write and the
/// accompanying <see cref="Authorization.AccessAudit"/> row commit atomically —
/// a failed save rolls back both. Reads touch the live rows directly (M·4: no
/// projection, no cache — a change is live on the very next call).
/// </para>
/// <para>
/// <b>M·7 (fail-closed):</b> <see cref="RemoveLanguageAsync"/> on the current
/// <see cref="LocaleSettings.DefaultLanguageCode"/> throws
/// <see cref="InvalidOperationException"/> <b>before</b> any write — no
/// <c>AccessAudit</c> row is committed for the blocked attempt. Content rows
/// (<see cref="TranslationResource"/>) for a removed
/// language are **retained** so re-adding restores them.
/// </para>
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly IDocumentStore _store;

    public LocalizationService(IDocumentStore store)
    {
        _store = store;
    }

    // ── Read paths (M·4: live rows, no projection, no cache) ──────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<LanguageCatalog>> ListLanguagesAsync()
    {
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        return await session
            .Query<LanguageCatalog>()
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultLanguageCodeAsync()
    {
        // ADR 0018 read seam: the instance default (LocaleSettings.DefaultLanguageCode)
        // with the `en` floor — a missing singleton or a blank code both yield `en`.
        // A read (no audit row), like ListLanguagesAsync.
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultLanguageCode))
            return settings.DefaultLanguageCode;

        return "en";
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultTimezoneAsync()
    {
        // ADR 0019 read seam: the instance default (LocaleSettings.DefaultTimezone)
        // with the `UTC` floor — a missing singleton or a blank id both yield `UTC`.
        // A read (no audit row), like GetDefaultLanguageCodeAsync.
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultTimezone))
            return settings.DefaultTimezone;

        return "UTC";
    }

    /// <inheritdoc />
    public async Task<TranslationResource?> GetTranslationAsync(string key, string languageCode)
    {
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        return await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetTranslationsForAsync(string languageCode)
    {
        // M·4 read path: one QuerySession, one query, no fallback (the editor
        // shows raw rows — the provider's M·2 fallback is the resident's path).
        // No audit row: this is a read.
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        var rows = await session
            .Query<TranslationResource>()
            .Where(t => t.LanguageCode == languageCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var map = new Dictionary<string, string>(rows.Count, StringComparer.Ordinal);
        foreach (var row in rows)
            map[row.Key] = row.Text;
        return map;
    }

    /// <inheritdoc />
    public async Task<LanguageCompleteness> GetCompletenessAsync(string languageCode)
    {
        // M·9: the "known" universe of keys / slugs is the instance's seeded `en`
        // set — a key is *missing* for a language iff it has an `en` row but no
        // row in this language. One query session, three live-row reads.
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        // UI strings: the `en` universe (M·9) vs. the language's present set.
        var allStringRows = await session
            .Query<TranslationResource>()
            .Where(t => t.LanguageCode == "en" || t.LanguageCode == languageCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var enKeys = allStringRows
            .Where(t => t.LanguageCode == "en")
            .Select(t => t.Key)
            .Distinct()
            .ToHashSet();

        var presentStringKeys = allStringRows
            .Where(t => t.LanguageCode == languageCode)
            .Select(t => t.Key)
            .ToHashSet();

        var missingStringKeys = enKeys.Except(presentStringKeys).OrderBy(k => k).ToList();

        return new LanguageCompleteness(
            languageCode,
            presentStringKeys.OrderBy(k => k).ToList(),
            missingStringKeys);
    }

    // ── ADR 0044: the bundled baseline read + the create-if-missing seed ──

    /// <inheritdoc />
    public Task<BundledLanguageBaseline?> GetBundledBaselineAsync(string code)
    {
        // A pure read of the code's per-language sources — no database access.
        // Only the bundled baseline languages (de / fr / da) have one; en is the
        // source language (a row, not a baseline) and any other code is
        // admin-authored (no code baseline) — both return null. `da` ships
        // disabled in the catalog (the pre-seeded lane) but still carries a full
        // baseline, so adding it back after a remove re-seeds from this source.
        IReadOnlyDictionary<string, string> uiStrings;
        (string Slug, string Title, string Body)[] pages;
        switch (code)
        {
            case "de":
                uiStrings = KnownTranslationKeys.DeValues;
                pages = FirstBootSeeder.DeDefaultPages();
                break;
            case "fr":
                uiStrings = KnownTranslationKeys.FrValues;
                pages = FirstBootSeeder.FrDefaultPages();
                break;
            case "da":
                uiStrings = KnownTranslationKeys.DaValues;
                pages = FirstBootSeeder.DaDefaultPages();
                break;
            default:
                return Task.FromResult<BundledLanguageBaseline?>(null);
        }

        var pageBaselines = pages
            .Select(p => (p.Slug, p.Title, p.Body))
            .ToList();
        return Task.FromResult<BundledLanguageBaseline?>(
            new BundledLanguageBaseline(code, uiStrings, pageBaselines));
    }

    /// <summary>
    /// ADR 0044 — write a bundled baseline into the instance, **create-
    /// if-missing** (an existing row is skipped, never refreshed — the same
    /// ownership invariant the first-boot seeder holds, ADR 0042 D1). Runs in
    /// the **caller's** in-flight session, so it commits in the caller's single
    /// <c>SaveChangesAsync</c>. No audit row of its own: it is part of
    /// <see cref="AddLanguageAsync"/>'s single audited action.
    /// </summary>
    private static async Task SeedBaselineAsync(
        IDocumentSession session,
        BundledLanguageBaseline baseline,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // 1. UI strings — one TranslationResource row per key, create-if-missing.
        foreach (var (key, text) in baseline.UiStrings)
        {
            var existing = await session
                .Query<TranslationResource>()
                .Where(t => t.Key == key && t.LanguageCode == baseline.LanguageCode)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                session.Store(new TranslationResource
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Key = key,
                    LanguageCode = baseline.LanguageCode,
                    Text = text
                });
            }
            // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
        }

        // 2. System pages — one PageTranslation row per slug, create-if-missing.
        // The page is looked up by slug (the en doc the baseline text belongs
        // to); a missing page is skipped (the en seed is the single source —
        // in practice always present, since en is the source language).
        foreach (var (slug, title, body) in baseline.PageBaselines)
        {
            var page = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.Kind == PageKind.System && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (page is null)
                continue;   // defensive — the en page is seeded first; skip rather than throw.

            var existing = await session
                .Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == baseline.LanguageCode)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                session.Store(new PageTranslation
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    PageId = page.Id,
                    LanguageCode = baseline.LanguageCode,
                    Title = title,
                    Body = body,
                    AuthorId = string.Empty,   // platform content — no resident author
                    Created = now,
                });
            }
            // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
        }
    }

    // ── Catalog mutations (M·6: one session + one SaveChangesAsync + one audit row) ──

    /// <inheritdoc />
    public async Task AddLanguageAsync(string code, string nativeName, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Determine the next sort order (max + 1) so the new language appears
        // last in the selector.
        var existing = await session
            .Query<LanguageCatalog>()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var nextSort = existing.Count == 0 ? 0 : existing.Max(c => c.SortOrder) + 1;

        session.Store(new LanguageCatalog
        {
            Id = code,
            NativeName = nativeName,
            Enabled = true,
            SortOrder = nextSort
        });

        // ADR 0044: seed the bundled baseline (create-if-missing) in the same
        // session, so the catalog row and its baseline data commit together. A
        // non-bundled code (null baseline) gets only the catalog row.
        var baseline = await GetBundledBaselineAsync(code).ConfigureAwait(false);
        if (baseline is not null)
            await SeedBaselineAsync(session, baseline, now, ct).ConfigureAwait(false);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "language.add",
            TargetKind = "language",
            TargetId = code,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetLanguageEnabledAsync(string code, bool enabled, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        var catalog = await session
            .Query<LanguageCatalog>()
            .Where(c => c.Id == code)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (catalog is null)
            throw new InvalidOperationException($"Language not found: {code}");

        catalog.Enabled = enabled;
        session.Store(catalog);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = enabled ? "language.enable" : "language.disable",
            TargetKind = "language",
            TargetId = code,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReorderLanguagesAsync(IReadOnlyList<string> codesInOrder, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Assign new SortOrder values (0-based index) to each code in the given order.
        for (var i = 0; i < codesInOrder.Count; i++)
        {
            var code = codesInOrder[i];
            var catalog = await session
                .Query<LanguageCatalog>()
                .Where(c => c.Id == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (catalog is null)
                throw new InvalidOperationException($"Language not found: {code}");

            catalog.SortOrder = i;
            session.Store(catalog);
        }

        // M·6: exactly one audit row for the reorder action (TargetId = first code).
        var firstCode = codesInOrder.Count > 0 ? codesInOrder[0] : string.Empty;
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "language.reorder",
            TargetKind = "language",
            TargetId = firstCode,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveLanguageAsync(string code, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // M·7: fail-closed — the current default cannot be removed. Check in the
        // same transaction, before any write. Throws → no audit row committed.
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is not null && settings.DefaultLanguageCode == code)
            throw new InvalidOperationException(
                $"Cannot remove language {code}: it is the current instance default (M·7).");

        var catalog = await session
            .Query<LanguageCatalog>()
            .Where(c => c.Id == code)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (catalog is null)
            throw new InvalidOperationException($"Language not found: {code}");

        // M·7: a **custom** code's content rows are **retained** — re-adding the
        // language restores them. ADR 0044: a **bundled** code's rows are the
        // exception — they are *deleted* (the reset), because re-adding re-seeds
        // them from the code baseline. An admin's edit of a bundled baseline is
        // intentionally discardable this way (see ADR 0044).
        var isBundled = GetBundledBaselineAsync(code).GetAwaiter().GetResult() is not null;
        if (isBundled)
        {
            // Delete every TranslationResource row for this code (UI strings).
            var uiRows = await session
                .Query<TranslationResource>()
                .Where(t => t.LanguageCode == code)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var row in uiRows)
                session.Delete<TranslationResource>(row.Id);

            // Delete every PageTranslation row attached to a page in this code.
            var pageTranslationRows = await session
                .Query<PageTranslation>()
                .Where(t => t.LanguageCode == code)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            foreach (var row in pageTranslationRows)
                session.Delete<PageTranslation>(row.Id);
        }

        session.Delete<LanguageCatalog>(catalog.Id);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "language.remove",
            TargetKind = "language",
            TargetId = code,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetDefaultLanguageAsync(string code, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Verify the language exists and is enabled.
        var catalog = await session
            .Query<LanguageCatalog>()
            .Where(c => c.Id == code)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (catalog is null)
            throw new InvalidOperationException($"Language not found: {code}");

        if (!catalog.Enabled)
            throw new InvalidOperationException($"Cannot set default to disabled language: {code}");

        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new LocaleSettings { DefaultLanguageCode = code };
        }
        else
        {
            settings.DefaultLanguageCode = code;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "language.set-default",
            TargetKind = "language",
            TargetId = code,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> GetDefaultDateFormatAsync()
    {
        // ADR 0020 read seam: the instance default (LocaleSettings.DefaultDateFormat)
        // with the floor (DateFormat.FloorFormat) — a missing singleton or a blank
        // stored value both yield the floor. A read (no audit row), like
        // GetDefaultTimezoneAsync.
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultDateFormat))
            return settings.DefaultDateFormat;

        return DateFormat.FloorFormat;
    }

    /// <inheritdoc />
    public async Task SetDefaultDateFormatAsync(string formatString, string actorId)
    {
        // ADR 0020 — the instance-default write lane (mirrors
        // SetDefaultTimezoneAsync's audited shape; the M·6 single audit row).
        //
        // Fail-closed (the M·7 / RemoveLanguageAsync pin): validate the .NET
        // format string *before* any write (DateFormat.IsValid — a blank or a
        // string .NET cannot apply throws here). **No audit row** is committed
        // for the blocked attempt.
        if (!DateFormat.IsValid(formatString))
            throw new InvalidOperationException($"Unknown date format: {formatString}");

        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Load-or-create the singleton (the SetDefaultTimezoneAsync shape) and
        // set the default; the resident override (Profile.DateFormat) is
        // untouched — this is the *platform default* only.
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new LocaleSettings { DefaultDateFormat = formatString };
        }
        else
        {
            settings.DefaultDateFormat = formatString;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "dateformat.set-default",
            TargetKind = "dateformat",
            TargetId = formatString,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetDefaultTimezoneAsync(string timezoneId, string actorId)
    {
        // ADR 0019 — the instance-default write lane (mirrors
        // SetDefaultLanguageAsync's audited shape; the M·6 single audit row).
        //
        // Fail-closed (the M·7 / RemoveLanguageAsync pin): validate the IANA id
        // against System.TimeZoneInfo *before* any write. A blank or unknown id
        // throws here — **no audit row** is committed for the blocked attempt.
        if (string.IsNullOrWhiteSpace(timezoneId) ||
            !System.TimeZoneInfo.TryFindSystemTimeZoneById(timezoneId, out _))
            throw new InvalidOperationException($"Unknown time zone: {timezoneId}");

        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Load-or-create the singleton (the SetDefaultLanguageAsync shape) and
        // set the default; the resident override (Profile.TimeZone) is untouched
        // — this is the *platform default* only.
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new LocaleSettings { DefaultTimezone = timezoneId };
        }
        else
        {
            settings.DefaultTimezone = timezoneId;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "timezone.set-default",
            TargetKind = "timezone",
            TargetId = timezoneId,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Translation / page upserts (M·6: one session + one audit row) ──

    /// <inheritdoc />
    public async Task UpsertTranslationAsync(string key, string languageCode, string text, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Pair idiom: upsert by (Key, LanguageCode) — the unique index enforces
        // one row per pair (M·4: data, not config; live on the next request).
        var existing = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = languageCode,
                Text = text
            });
        }
        else
        {
            existing.Text = text;
            session.Store(existing);
        }

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "translation.save",
            TargetKind = "translation",
            TargetId = key,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
