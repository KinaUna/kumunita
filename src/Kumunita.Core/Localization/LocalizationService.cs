using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
/// (<see cref="TranslationResource"/> / <see cref="LocalizedPage"/>) for a removed
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
    public async Task<LocalizedPage?> GetPageAsync(string slug, string languageCode)
    {
        await using var session = _store.QuerySession();
        var ct = System.Threading.CancellationToken.None;

        return await session
            .Query<LocalizedPage>()
            .Where(p => p.Slug == slug && p.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
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

        // Static pages: the `en` universe (M·9) vs. the language's present set.
        var allPageRows = await session
            .Query<LocalizedPage>()
            .Where(p => p.LanguageCode == "en" || p.LanguageCode == languageCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var enSlugs = allPageRows
            .Where(p => p.LanguageCode == "en")
            .Select(p => p.Slug)
            .Distinct()
            .ToHashSet();

        var presentPageSlugs = allPageRows
            .Where(p => p.LanguageCode == languageCode)
            .Select(p => p.Slug)
            .ToHashSet();

        var missingPageSlugs = enSlugs.Except(presentPageSlugs).OrderBy(s => s).ToList();

        return new LanguageCompleteness(
            languageCode,
            presentStringKeys.OrderBy(k => k).ToList(),
            missingStringKeys,
            presentPageSlugs.OrderBy(s => s).ToList(),
            missingPageSlugs);
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

        // M·7: content rows (TranslationResource / LocalizedPage) are **retained**
        // — only the catalog row is deleted. Re-adding the language restores them.
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

    /// <inheritdoc />
    public async Task UpsertPageAsync(string slug, string languageCode, string title, string body, string actorId)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Pair idiom: upsert by (Slug, LanguageCode) — the unique index enforces
        // one row per pair (M·4: data, not config; live on the next request).
        var existing = await session
            .Query<LocalizedPage>()
            .Where(p => p.Slug == slug && p.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            session.Store(new LocalizedPage
            {
                Id = Guid.NewGuid().ToString("N"),
                Slug = slug,
                LanguageCode = languageCode,
                Title = title,
                Body = body,
                Updated = now
            });
        }
        else
        {
            existing.Title = title;
            existing.Body = body;
            existing.Updated = now; // server time (design doc §Pinned contract §3)
            session.Store(existing);
        }

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.save",
            TargetKind = "localized_page",
            TargetId = slug,
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
