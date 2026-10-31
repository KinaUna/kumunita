using System.Text.Json;
using Kumunita.Core.Localization;
using Marten;

namespace Kumunita.Core.Portability;

/// <summary>
/// The §apply step 2 — store the domain documents in the registry's
/// <b>import order</b> (parents before children, the D7 order) as
/// <b>one commit</b> (a single <c>SaveChangesAsync</c> — the C-M11·4
/// "one commit" pin). The loop is uniform (the
/// <see cref="PortabilityExportDocuments.NameToType"/> table lookup +
/// <c>List&lt;T&gt;</c> deserialization + <c>session.Store(row)</c> —
/// <em>not</em> per-type code).
/// <para>
/// The input is the <see cref="KumunitaArchiveData.Docs"/> map (the
/// <c>docs/{Type}.json</c> array bytes, the archive's single source of
/// truth). A clean validate guarantees the deserialization succeeds
/// (check (b)) — this unit's apply is fail-open on a pre-validated
/// archive (a mid-apply failure is the documented restore path,
/// C-M11·4, never a silently-accepted half-import).
/// </para>
/// <para>
/// The non-generic <c>session.Store(object)</c> call is the same seam
/// U04's <see cref="PortabilityService.ExportAsync"/> uses for the
/// <c>AccessAudit</c> row — no reflection needed.
/// </para>
/// </summary>
public static class PortabilityApplyDocuments
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Stores every content doc in the closed inventory in the
    /// <see cref="PortabilityDocTypes.InOrder()"/> import order (parents
    /// before children — the D7 order) as one commit (one
    /// <c>SaveChangesAsync</c>). The loop is uniform (the
    /// <see cref="PortabilityExportDocuments.NameToType"/> table lookup
    /// + reflection-dispatched <c>List&lt;T&gt;</c> deserialization +
    /// <c>ISession.Store&lt;T&gt;</c> — <em>not</em> per-type code).
    /// </summary>
    /// <param name="documentStore">The frozen Marten seam.</param>
    /// <param name="data">The <see cref="KumunitaArchiveData"/> — the
    /// <see cref="KumunitaArchiveData.Docs"/> map (doc type name →
    /// <c>docs/{Type}.json</c> array bytes) is the single source of
    /// truth for the apply.</param>
    /// <param name="mode">The import mode (ADR 0193).
    /// <see cref="PortabilityImportMode.CleanSlate"/> — the D1 restore
    /// semantic: every archived row is stored (the fresh instance has no
    /// conflicting rows — a principal collision was refused before this
    /// unit ran, so the content graph's author references are safe).
    /// <see cref="PortabilityImportMode.KeepExisting"/> — merge mode: a
    /// row whose id already exists in the target store is <b>skipped</b>
    /// (the existing content is kept intact — never overwritten); only
    /// new rows are added.</param>
    /// <param name="defaultLanguageCode">The target instance's default
    /// <c>LanguageCode</c> (the archive's <c>config.json</c> <c>locale</c>
    /// block, <c>null</c> if the archive carried none) — the ADR 0274 tag
    /// merge key: a <see cref="Kumunita.Core.Tags.Tag"/> row is merged into
    /// an existing tag only when its <c>Slug</c> AND its display name in
    /// this language are equal (different languages, different names →
    /// two tags, as designed by ADR 0044 D3). Only consulted for the
    /// <c>Tag</c> doc type in <see cref="PortabilityImportMode.KeepExisting"/>;
    /// ignored elsewhere (and the merge degrades gracefully to a name-only
    /// comparison when <c>null</c>).</param>
    /// <param name="principalMergeMap">ADR 0275 — the KeepExisting <b>principal
    /// merge map</b> (archive principal <c>SubjectId</c> → the existing
    /// account's <c>User.Id</c>), produced by
    /// <see cref="PortabilityApplyIdentity"/>: when present, an archived
    /// <see cref="Kumunita.Core.UserInfo.Profile"/> row whose <c>SubjectId</c>
    /// is merged into an existing account is NOT stored (the existing profile
    /// is kept intact — the ADR 0193 discipline re-applied to the directory),
    /// and every content doc's <c>Target == "principal"</c> reference is
    /// re-pointed at the existing account's id. <c>null</c> outside
    /// <see cref="PortabilityImportMode.KeepExisting"/> (or when no principal
    /// merged) — then nothing is re-pointed and every row is stored as-is,
    /// exactly as before.</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        Marten.IDocumentStore documentStore,
        KumunitaArchiveData data,
        PortabilityImportMode mode,
        string? defaultLanguageCode,
        Dictionary<string, string>? principalMergeMap,
        CancellationToken ct)
    {
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());

        // ADR 0274 — the KeepExisting <b>tag merge</b> (computed ONCE before
        // the loop — it is a function of the archive's <see
        // cref="Kumunita.Core.Tags.Tag"/> rows + the target's existing tags,
        // not of the doc type being stored): the archive's <see
        // cref="Kumunita.Core.Tags.Tag"/> rows (order 4, before every doc that
        // references one) that match an existing tag on <b>slug +
        // default-language display name</b> (the ADR 0044 D3 business key +
        // the user's requirement) are NOT stored (the existing tag is kept
        // intact — the ADR 0193 "keep intact, never overwritten" discipline,
        // re-applied); instead the archive's content's tag references are
        // re-pointed at the existing tag (so the archive's content lands on
        // the target's tag — not a second one), and the archive's
        // <see cref="Kumunita.Core.Tags.TagTranslation"/> rows for it are
        // folded in as translations of the existing tag (one row per
        // language — a language the existing tag already has is left
        // untouched, never overwritten; a new one is added). A tag that
        // matches neither (different slug, or different default-language
        // name) is stored as its own new tag (two names, two tags — the
        // ADR 0044 D3 shape). This closes the duplicate-tag failure a
        // KeepExisting import produced over a live instance (the
        // natural-identity skip below cannot see the <see
        // cref="Kumunita.Core.Tags.Tag"/> / <see
        // cref="Kumunita.Core.Tags.TagTranslation"/> pair: <see
        // cref="Kumunita.Core.Tags.Tag"/> has no unique index — C-TG·4, the
        // write-lane create-or-reuse is its identity guard — and the
        // <see cref="Kumunita.Core.Tags.TagTranslation"/>'s (TagId,
        // LanguageCode) unique index is keyed on the surrogate TagId, so it
        // never collides — it silently doubles).
        Dictionary<string, string>? tagMergeMap = null;
        Dictionary<string, string>? tagTranslationIdRemap = null;
        if (mode == PortabilityImportMode.KeepExisting)
        {
            tagMergeMap = await BuildTagMergeMapAsync(documentStore, data, defaultLanguageCode, ct).ConfigureAwait(false);
            if (tagMergeMap.Count > 0)
            {
                RemapReferencesInArchive(data, "Tag", tagMergeMap, "Tag", "TagTranslation");
                tagTranslationIdRemap = BuildTagTranslationIdRemap(data, tagMergeMap);
            }

            // ADR 0275 — the KeepExisting <b>principal merge</b> (computed by
            // <see cref="PortabilityApplyIdentity"/> before this unit ran — a
            // principal is matched to an existing account on its email, and
            // the archive's <see cref="Kumunita.Core.UserInfo.Profile"/> row
            // for that person is the directory entry that would double): the
            // archived <see cref="Kumunita.Core.UserInfo.Profile"/> rows for
            // merged principals are NOT stored (the existing profile is kept
            // intact — the ADR 0193 discipline re-applied to the directory),
            // and every content doc's <c>Target == "principal"</c> reference
            // is re-pointed at the existing account's id (so the archive's
            // content lands on the target's account — not a dangling foreign
            // subject id). A principal that was NOT merged (stored fresh under
            // its own id) is left as-is; a reference to an id the archive does
            // not carry (an untouched target-local account) is left as-is. The
            // <see cref="Kumunita.Core.UserInfo.Profile"/> rows themselves are
            // excluded from the re-point (their <c>SubjectId</c> is their own
            // identity, not a reference to another principal).
            if (principalMergeMap is not null && principalMergeMap.Count > 0)
                RemapReferencesInArchive(data, "principal", principalMergeMap, "Profile");
        }

        foreach (var entry in PortabilityDocTypes.InOrder())
        {
            ct.ThrowIfCancellationRequested();

            if (!data.Docs.TryGetValue(entry.Type, out var json))
                continue; // a clean validate guaranteed presence (check
                          // (b)); a missing type is a caller error — skip
                          // (the archive was pre-validated; the apply is
                          // fail-open on the pre-validated shape).

            var docType = PortabilityExportDocuments.NameToType[entry.Type];
            var listType = typeof(List<>).MakeGenericType(docType);
            var rows = (System.Collections.IList)JsonSerializer.Deserialize(json, listType, JsonOpts)!;

            // KeepExisting (ADR 0193) — the existing-row skip, keyed on the
            // type's <b>natural identity</b>, NOT just the surrogate id.
            // A document's unique business-key index (e.g. TranslationResource
            // (Key, LanguageCode), GroupMembership (GroupId, UserId), the
            // *Translation (ParentId, LanguageCode) rows) is the real
            // identity, and it is the DB-enforced uniqueness. The archive's
            // row and the target's row for the same logical entity carry
            // <b>different</b> surrogate ids (two independent instances), so an
            // id-only skip misses them and the Store collides on the unique
            // index (23505). The skip key is therefore the union of the
            // surrogate id + every unique index's member set, derived from the
            // Marten mapping (the single source of truth for the index) so the
            // skip key always matches the actual constraint.
            HashSet<string>? existingSkipKeys = null;
            List<string[]>? keySets = null;
            if (mode == PortabilityImportMode.KeepExisting)
            {
                keySets = GetIdentityKeySets(documentStore, docType);
                existingSkipKeys = new HashSet<string>(StringComparer.Ordinal);
                var queryAll = typeof(PortabilityApplyDocuments)
                    .GetMethod(nameof(QueryAllAsync),
                        System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(docType);
                var existing = (System.Collections.IEnumerable)
                    (await (Task<object>)queryAll.Invoke(null, [documentStore, (object)ct])!)!;
                foreach (var exRow in existing)
                    AddSkipKeys(exRow, docType.Name, keySets, existingSkipKeys);
            }

            foreach (var row in rows)
            {
                if (row is null)
                    continue;

                // ADR 0275 — a profile whose principal merged into an existing
                // account is NOT stored (the existing profile is kept intact —
                // the ADR 0193 discipline re-applied to the directory — so the
                // person is not doubled on /directory). The archive's content's
                // principal references were re-pointed at the existing account's
                // id above. Profile has no unique index, so the natural-identity
                // skip below cannot see it — this is the skip that closes the
                // duplicate-directory failure (the SubjectId is the merge key —
                // the registry's "principal" target, ADR 0275).
                if (principalMergeMap is not null
                    && row.GetType().Name == "Profile"
                    && row.GetType().GetProperty("SubjectId")?.GetValue(row) is string profSubId
                    && principalMergeMap.ContainsKey(profSubId))
                    continue;

                // ADR 0274 — a tag merged into an existing one is NOT stored
                // (its existing counterpart is kept intact — never
                // overwritten); the archive's content references were
                // re-pointed at the existing tag above, and the archive's
                // TagTranslation rows for it fold in as translations of the
                // existing tag (their TagId is remapped below, so the
                // natural-identity skip keeps the existing tag's (TagId, Lang)
                // rows and adds the new ones).
                if (tagMergeMap is not null
                    && row.GetType().Name == "Tag"
                    && row.GetType().GetProperty("Id")?.GetValue(row) is string rowId
                    && tagMergeMap.ContainsKey(rowId))
                    continue;

                // ADR 0274 — a TagTranslation row whose parent tag was merged
                // is a translation of the EXISTING tag: re-point its TagId so
                // the archive's translations land on the target's tag — the
                // existing tag's (TagId, Lang) rows stay intact (the
                // natural-identity skip below), a new language is added.
                if (tagTranslationIdRemap is not null
                    && row.GetType().Name == "TagTranslation"
                    && row.GetType().GetProperty("TagId")?.GetValue(row) is string ttTagId
                    && tagTranslationIdRemap.TryGetValue(ttTagId, out var ttExistingId))
                    row.GetType().GetProperty("TagId")!.SetValue(row, ttExistingId);

                // KeepExisting (ADR 0193) — skip the row the target already
                // holds (the existing content is kept intact, never
                // overwritten). The check is on the natural identity
                // (surrogate id + business-key member set), not just the
                // surrogate id.
                if (existingSkipKeys is not null && keySets is not null
                    && IsSkipped(row, docType.Name, keySets, existingSkipKeys))
                    continue;

                session.Store(row); // the non-generic Store(object) —
                                    // the same seam U04's ExportAsync
                                    // uses for the AccessAudit row (no
                                    // reflection needed).
            }
        }

        // One commit (the C-M11·4 "one commit" pin) — every doc type's
        // rows are staged in a single session; one SaveChangesAsync
        // commits them atomically (a mid-apply failure is the
        // documented restore path, never a silently-accepted
        // half-import).
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── The ADR 0274 KeepExisting tag merge ──────────────────────────────────
    // A <see cref="Kumunita.Core.Tags.Tag"/> has no unique index (C-TG·4 — the
    // write lane's create-or-reuse is its identity guard), so ADR 0193's
    // natural-identity skip (derived from the Marten unique indexes) cannot
    // see the <c>Tag</c> / <c>TagTranslation</c> pair: two instances' archives
    // of the same tag are stored as two rows (duplicate on the /tags page) and
    // their <c>TagTranslation</c> rows land on two parent ids (the
    // <c>(TagId, LanguageCode)</c> unique index is keyed on the surrogate
    // <c>TagId</c>, so it never collides — it silently doubles). The merge key
    // is the ADR 0044 D3 business key the write lane itself uses — the
    // <c>Slug</c> — plus the user's requirement: the display name in the
    // instance's default language must be the same (different languages, or a
    // re-worded default-language name → two tags, as designed). The merge is
    // <b>kept-first</b> (the existing tag row is never touched — the ADR 0193
    // "keep intact, never overwritten" discipline, re-applied): the archive's
    // row is dropped, its references re-pointed (so the archive's content
    // lands on the existing tag), and its translations folded in (one row per
    // language; a language the existing tag already has is left untouched).

    /// <summary>
    /// Builds the ADR 0274 tag merge map (archive <c>Tag</c> id → the
    /// existing tag's id) for the rows of the archive that match an existing
    /// tag on <b>slug + default-language display name</b> (case-insensitive —
    /// the two instances may spell the same name in different cases). An empty
    /// map (no matches, or no archive <c>Tag</c> rows) is the no-op shape: the
    /// call site falls through to the untouched store path, which is correct
    /// either way.
    /// </summary>
    private static async Task<Dictionary<string, string>> BuildTagMergeMapAsync(
        Marten.IDocumentStore store, KumunitaArchiveData data, string? defaultLanguageCode, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var archiveTags = LoadArchiveTags(data);
        if (archiveTags.Count == 0)
            return map; // no archive Tag rows — nothing to merge (defensive;
                        // the apply loop's TryGetValue guard already implies
                        // presence when this runs).

        await using var session = store.QuerySession();
        var existing = await session.Query<Kumunita.Core.Tags.Tag>().ToListAsync(ct);
        if (existing.Count == 0)
            return map; // the target has no tags — nothing to merge into.

        // The existing tag's display name in the default language (the ADR
        // 0005 D5 preference order the tag read lane uses — the /tags page's
        // displayed name): the TagTranslation row for that language, falling
        // back to the tag's base Name.
        var existingTranslations = await session.Query<Kumunita.Core.Tags.TagTranslation>()
            .ToListAsync(ct);
        var translationByTag = existingTranslations
            .GroupBy(t => t.TagId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // The archive's TagTranslation rows grouped by their parent tag (the
        // archive's per-language display names — the merge key compares the
        // archive tag's name in the default language against the existing
        // tag's name in the same language, the ADR 0005 D5 preference order).
        var archiveTranslations = await Task.FromResult(
            LoadArchiveTagTranslations(data).GroupBy(t => t.TagId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal));

        foreach (var tag in archiveTags)
        {
            if (tag is null || string.IsNullOrEmpty(tag.Id) || map.ContainsKey(tag.Id))
                continue;

            // The ADR 0044 D3 business key, normalized the way the write lane
            // derives it (Trim + ToLowerInvariant — the C-TG·4 create-or-reuse
            // comparison).
            var slug = (tag.Slug ?? string.Empty).Trim().ToLowerInvariant();
            if (slug.Length == 0)
                continue; // a blank-slug row is not a merge candidate (the
                          // write lane rejects blanks — never a false merge).

            var archiveTagTranslations = archiveTranslations.TryGetValue(tag.Id, out var at)
                ? at
                : new List<Kumunita.Core.Tags.TagTranslation>();

            var match = existing.FirstOrDefault(t =>
                string.Equals((t.Slug ?? string.Empty).Trim().ToLowerInvariant(), slug, StringComparison.Ordinal)
                && DefaultLanguageNameEquals(
                    ResolveTagName(t, defaultLanguageCode,
                        translationByTag.TryGetValue(t.Id, out var et)
                            ? et
                            : new List<Kumunita.Core.Tags.TagTranslation>()),
                    ResolveTagName(tag, defaultLanguageCode, archiveTagTranslations)));

            if (match is not null)
                map[tag.Id] = match.Id;
        }

        return map;
    }

    /// <summary>
    /// A tag's display name in <paramref name="defaultLanguageCode"/> (the
    /// ADR 0005 D5 preference order the tag read lane uses — the /tags page's
    /// displayed name): the <see cref="Kumunita.Core.Tags.TagTranslation"/>
    /// row for that language, falling back to the tag's base
    /// <see cref="Kumunita.Core.Tags.Tag.Name"/>. <c>null</c> language → the
    /// base <c>Name</c>.
    /// </summary>
    private static string ResolveTagName(
        Kumunita.Core.Tags.Tag tag, string? defaultLanguageCode,
        System.Collections.Generic.IReadOnlyList<Kumunita.Core.Tags.TagTranslation> translations)
    {
        if (defaultLanguageCode is not null)
        {
            var translated = translations.FirstOrDefault(t => t.LanguageCode == defaultLanguageCode)?.Name;
            if (!string.IsNullOrEmpty(translated))
                return translated!;
        }
        return tag.Name ?? string.Empty;
    }

    /// <summary>
    /// Whether two tags are the "same tag" for the ADR 0274 merge (the slug
    /// is already compared by the caller): their display name in the
    /// instance's default language is equal (case-insensitive — the two
    /// instances may spell the same name in different cases).
    /// </summary>
    private static bool DefaultLanguageNameEquals(string existingName, string archiveName)
    {
        return string.Equals(existingName ?? string.Empty, archiveName ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The archive's <see cref="Kumunita.Core.Tags.Tag"/> rows (the
    /// <c>docs/Tag.json</c> array bytes) — a defensive null-safe read (a clean
    /// validate guaranteed the shape; a malformed row is skipped, never a
    /// false merge).
    /// </summary>
    private static System.Collections.Generic.List<Kumunita.Core.Tags.Tag> LoadArchiveTags(KumunitaArchiveData data)
    {
        if (data.Docs.TryGetValue("Tag", out var json))
        {
            var list = JsonSerializer.Deserialize<System.Collections.Generic.List<Kumunita.Core.Tags.Tag>>(json, JsonOpts);
            if (list is not null)
                return list;
        }
        return new System.Collections.Generic.List<Kumunita.Core.Tags.Tag>();
    }


    /// <summary>
    /// Re-points every archived content doc's reference to <paramref name="target"/>
    /// (the registry's <c>Target == target</c> fields — the <c>Tag</c> family
    /// for ADR 0274, the <c>principal</c> family for ADR 0275) to the existing
    /// entity's id, data-driven (the §inventory "not per-type code" pin,
    /// re-applied), so the archive's content lands on the target's existing
    /// entity — not a second one. A reference to an entity that was NOT merged
    /// (stored fresh under its own id) is left as-is; a reference to an id the
    /// archive does not carry (an untouched target-local row) is left as-is.
    /// <paramref name="skipTypes"/> are doc types whose own rows must NOT be
    /// re-pointed (the merged entity's own rows — e.g. <c>Tag</c> /
    /// <c>TagTranslation</c>). No-op when the map is empty or a doc type has no
    /// references to <paramref name="target"/> (the registry's closed field set
    /// drives it — no per-type code).
    /// </summary>
    private static void RemapReferencesInArchive(
        KumunitaArchiveData data, string target, Dictionary<string, string> mergeMap,
        params string[] skipTypes)
    {
        var skip = new HashSet<string>(skipTypes, StringComparer.Ordinal);
        foreach (var entry in PortabilityDocTypes.Entries)
        {
            if (skip.Contains(entry.Type))
                continue; // the merged entity's own rows — dropped (its existing
                          // counterpart is kept) / folded in, not re-pointed.

            var fields = entry.ReferenceFields
                .Where(f => f.Target == target && string.IsNullOrEmpty(f.Field) == false)
                .Select(f => f.Field)
                .ToHashSet(StringComparer.Ordinal);
            if (fields.Count == 0)
                continue;

            if (!data.Docs.TryGetValue(entry.Type, out var json))
                continue; // a missing type is a caller error (the apply loop's
                          // guard) — nothing to remap.

            var listType = typeof(List<>).MakeGenericType(
                PortabilityExportDocuments.NameToType[entry.Type]);
            var rows = (System.Collections.IList?)JsonSerializer.Deserialize(json, listType, JsonOpts);
            if (rows is null || rows.Count == 0)
                continue;

            var changed = false;
            foreach (var row in rows)
            {
                if (row is null)
                    continue;
                var type = row.GetType();
                foreach (var field in fields)
                {
                    var prop = type.GetProperty(field);
                    if (prop is null || !prop.CanRead || !prop.CanWrite)
                        continue; // a field the registry names that the POCO
                                  // does not hold (a drift — leave it; the
                                  // U05 integrity gate is the real guard).

                    var value = prop.GetValue(row);
                    if (value is string s && mergeMap.TryGetValue(s, out var mergedId))
                    {
                        prop.SetValue(row, mergedId);
                        changed = true;
                    }
                    else if (value is System.Collections.IEnumerable list)
                    {
                        var newList = new List<string>();
                        var listChanged = false;
                        foreach (var item in list)
                        {
                            if (item is string s2 && mergeMap.TryGetValue(s2, out var mergedId2))
                            {
                                newList.Add(mergedId2);
                                listChanged = true;
                            }
                            else
                            {
                                newList.Add(item?.ToString() ?? string.Empty);
                            }
                        }
                        if (listChanged)
                        {
                            prop.SetValue(row, newList);
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
                data.Docs[entry.Type] = JsonSerializer.SerializeToUtf8Bytes(rows, listType, JsonOpts);
        }
    }

    /// <summary>
    /// Builds the ADR 0274 <see cref="Kumunita.Core.Tags.TagTranslation"/>
    /// remap (archive <see cref="Kumunita.Core.Tags.TagTranslation.TagId"/>
    /// → the existing tag's id) from the <c>tagMergeMap</c>: for each archive
    /// <see cref="Kumunita.Core.Tags.TagTranslation"/> row whose parent tag
    /// was merged, the row's <see
    /// cref="Kumunita.Core.Tags.TagTranslation.TagId"/> is re-pointed at the
    /// existing tag (so the archive's translation lands on the target's tag —
    /// the existing tag's (TagId, LanguageCode) rows stay intact via the
    /// natural-identity skip, a new language is added). An empty map is the
    /// no-op shape (no merged tags, or no archive <see
    /// cref="Kumunita.Core.Tags.TagTranslation"/> rows).
    /// </summary>
    private static Dictionary<string, string> BuildTagTranslationIdRemap(KumunitaArchiveData data, Dictionary<string, string> tagMergeMap)
    {
        var remap = new Dictionary<string, string>(StringComparer.Ordinal);
        var archiveTranslations = LoadArchiveTagTranslations(data);
        if (archiveTranslations.Count == 0)
            return remap;

        foreach (var tt in archiveTranslations)
        {
            if (tt is null || string.IsNullOrEmpty(tt.TagId))
                continue;
            if (tagMergeMap.TryGetValue(tt.TagId, out var existingTagId))
                remap[tt.TagId] = existingTagId;
        }

        return remap;
    }

    /// <summary>
    /// The archive's <see cref="Kumunita.Core.Tags.TagTranslation"/> rows (the
    /// <c>docs/TagTranslation.json</c> array bytes) — a defensive null-safe
    /// read (a clean validate guaranteed the shape; a malformed row is
    /// skipped, never a false remap).
    /// </summary>
    private static System.Collections.Generic.List<Kumunita.Core.Tags.TagTranslation> LoadArchiveTagTranslations(KumunitaArchiveData data)
    {
        if (data.Docs.TryGetValue("TagTranslation", out var json))
        {
            var list = JsonSerializer.Deserialize<System.Collections.Generic.List<Kumunita.Core.Tags.TagTranslation>>(json, JsonOpts);
            if (list is not null)
                return list;
        }
        return new System.Collections.Generic.List<Kumunita.Core.Tags.TagTranslation>();
    }

    // ── The KeepExisting natural-identity skip helpers (ADR 0193) ────────────
    // A row's natural identity is its surrogate id PLUS the member set of
    // every unique index on its table (the DB-enforced business key). The
    // member names are read from the Marten mapping so the skip key is always
    // in sync with the actual unique constraint; a business-key index whose
    // columns are not POCO members (a stored / duplicated column) is skipped
    // for the skip key and the surrogate id remains the guard (no worse than
    // the pre-fix behaviour).

    /// <summary>
    /// The type's identity key sets: the surrogate id first (<c>Id</c>, or
    /// <c>SubjectId</c> for the <c>Profile</c> shape), then one entry per
    /// unique index whose columns are all POCO members. Each entry is the
    /// closed member-name tuple that constitutes that identity.
    /// </summary>
    private static List<string[]> GetIdentityKeySets(
        Marten.IDocumentStore store, Type docType)
    {
        var sets = new List<string[]>();
        var idProp = docType.GetProperty("Id") ?? docType.GetProperty("SubjectId");
        if (idProp is not null)
            sets.Add(new[] { idProp.Name });

        var idType = store.Options.FindOrResolveDocumentType(docType);
        if (idType is null)
            return sets; // an unknown type has no business-key indexes —
                         // the surrogate-id key set above is the guard.
        var indexes = idType.Indexes;
        if (indexes is null)
            return sets;
        foreach (var index in indexes)
        {
            if (index is null || !index.IsUnique)
                continue;
            var columns = index.Columns;
            if (columns is null)
                continue;
            var members = new List<string>();
            var allMembers = true;
            foreach (var column in columns)
            {
                var member = ExtractMember(column);
                if (member is null) { allMembers = false; break; }
                members.Add(member);
            }
            if (allMembers && members.Count > 0)
                sets.Add(members.ToArray());
        }
        return sets;
    }

    /// <summary>
    /// Adds every computable identity signature of <paramref name="row"/>
    /// to <paramref name="sink"/>. A signature that cannot be built (a null
    /// member — nulls are distinct in a unique index, so never a duplicate;
    /// or a non-member column) is omitted, never a false positive.
    /// </summary>
    private static void AddSkipKeys(
        object row, string typeName, List<string[]> keySets, HashSet<string> sink)
    {
        for (var i = 0; i < keySets.Count; i++)
        {
            var signature = IdentitySignature(row.GetType(), keySets[i], row, typeName + "#" + i);
            if (signature is not null)
                sink.Add(signature);
        }
    }

    /// <summary>
    /// <c>true</c> iff <paramref name="row"/> shares any identity with an
    /// already-existing row (its surrogate id or a business-key member set
    /// matches) — i.e. the target already holds this logical entity.
    /// </summary>
    private static bool IsSkipped(
        object row, string typeName, List<string[]> keySets, HashSet<string> existing)
    {
        for (var i = 0; i < keySets.Count; i++)
        {
            var signature = IdentitySignature(row.GetType(), keySets[i], row, typeName + "#" + i);
            if (signature is not null && existing.Contains(signature))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A canonical signature of <paramref name="row"/> over one key set
    /// (the type name + key-set ordinal + the member values). Returns
    /// <c>null</c> when any member is null (nulls are distinct in a unique
    /// index — not a duplicate) or when a member cannot be resolved.
    /// </summary>
    private static string? IdentitySignature(
        Type type, string[] members, object row, string prefix)
    {
        var sb = new System.Text.StringBuilder(prefix);
        sb.Append('\u0001');
        foreach (var member in members)
        {
            var value = type.GetProperty(member)?.GetValue(row);
            if (value is null)
                return null;
            sb.Append(value.ToString()).Append('\u0001');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts the POCO member name from a Marten index column. A POCO-member
    /// index column is rendered as <c>(data --&gt; 'MemberName')</c>; any other
    /// shape (a stored / duplicated column, an expression) is not reflectable
    /// here and returns <c>null</c>.
    /// </summary>
    private static string? ExtractMember(string? columnSql)
    {
        if (string.IsNullOrEmpty(columnSql))
            return null;
        const string prefix = "(data ->> '";
        if (columnSql.StartsWith(prefix, StringComparison.Ordinal)
            && columnSql.EndsWith("')", StringComparison.Ordinal))
            return columnSql.Substring(prefix.Length, columnSql.Length - prefix.Length - 2);
        return null;
    }

    /// <summary>
    /// The generic helper the KeepExisting keep-skip + the CleanSlate
    /// wipe (ADR 0193) drive off — loads <em>all</em> rows of a doc type
    /// from the target store. Invoked with <c>MakeGenericMethod(docType)</c>
    /// since the type is only known at runtime (the registry's data-driven
    /// loop).
    /// </summary>
    internal static async Task<object> QueryAllAsync<T>(
        Marten.IDocumentStore documentStore, CancellationToken ct)
        where T : notnull
    {
        await using var session = documentStore.QuerySession();
        return await session.Query<T>().ToListAsync(ct);
    }

    /// <summary>
    /// The CleanSlate wipe helper (ADR 0193) — loads <em>all</em> rows of
    /// a doc type, deletes them, and commits. Invoked with
    /// <c>MakeGenericMethod(docType)</c> since the type is only known at
    /// runtime (the registry's data-driven loop).
    /// </summary>
    internal static async Task DeleteAllAsync<T>(
        Marten.IDocumentStore documentStore, CancellationToken ct)
        where T : notnull
    {
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var rows = await session.Query<T>().ToListAsync(ct);
        foreach (var row in rows)
            session.Delete(row);
        if (rows.Count > 0)
            await session.SaveChangesAsync(ct);
    }
}
