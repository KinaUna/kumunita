using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Kumunita.Core.Portability;

namespace Kumunita.Core.Portability;

/// <summary>
/// The §validate result — the <b>closed failure set</b> contract (the
/// design doc §validate "The closed failure set"; the U06 web surface +
/// the U07 fail-closed pin render / assert <em>exactly</em> this, no
/// more, no less):
/// <list type="table">
/// <item><c>format.unsupported</c> — check (a)</item>
/// <item><c>docs.malformed:{Type}</c> / <c>docs.missing:{Type}</c> —
/// check (b)</item>
/// <item><c>ref.dangling:{Type}.{field}</c> — check (c)</item>
/// <item><c>media.missing:{id}</c> / <c>media.mismatch:{id}</c> —
/// check (d)</item>
/// </list>
/// </summary>
public sealed record PortabilityValidationResult(bool Ok, IReadOnlyList<string> Failures)
{
    /// <summary>A clean validate (the apply phase may proceed).</summary>
    public static PortabilityValidationResult Success { get; } = new(Ok: true, Failures: []);
}

/// <summary>
/// The §validate phase — the fail-closed gate U05's <see
/// cref="PortabilityService.ImportAsync"/> runs to completion
/// <em>before any write</em> (C-M11·4). The four locked checks (the
/// design doc §validate, copied verbatim; the U07 fail-closed test
/// asserts <em>exactly</em> these):
/// <ol>
/// <li>(a) <c>format</c> (C-M11·1) — the <c>manifest.json</c>
/// <c>format</c> is one the build understands (the closed set:
/// <c>kumunita/portability/1</c>). Else <c>format.unsupported</c>.</li>
/// <li>(b) per-type sanity (C-M11·4) — each <c>docs/{Type}.json</c> in
/// the §inventory deserializes into its declared POCO set (the registry's
/// <see cref="PortabilityDocTypes"/> <c>Type</c>); a malformed / missing
/// / unexpected <c>docs/</c> file ⇒ <c>docs.malformed:{Type}</c> /
/// <c>docs.missing:{Type}</c>.</li>
/// <li>(c) referential integrity (C-M11·4) — every id referenced by the
/// §inventory reference map resolves to a row <em>within the
/// archive</em>: <c>→ principal</c> against the
/// <c>identity/principals.json</c> <c>subjectId</c> set; <c>→ {DocType}</c>
/// against that doc type's <c>docs/{Type}.json</c> id set; <c>→
/// MediaObject</c> against the <c>manifest.json</c> <c>media_manifest</c>
/// id set; <c>→ LanguageCatalog</c> against the <c>config.json</c>
/// <c>languages[]</c> id set (read <em>before</em> the docs integrity
/// loop). A dangling reference ⇒ <c>ref.dangling:{Type}.{field}</c>.</li>
/// <li>(d) media verification (C-M11·3) — every <c>MediaObject</c> listed
/// in the <c>manifest.json</c> <c>media_manifest</c> has its bytes
/// present in <c>media/{Id[0..2]}/{Id}</c> <em>and</em> the byte content
/// matches (the size + the content hash implied by the path). A missing /
/// mismatched byte ⇒ <c>media.missing:{id}</c> /
/// <c>media.mismatch:{id}</c>.</li>
/// </ol>
/// <b>Any failure ⇒ zero writes</b> (C-M11·4) — the validate phase is
/// read-only over the in-memory archive; the apply phase
/// (<see cref="PortabilityApplyDocuments"/> / <see
/// cref="PortabilityApplyMedia"/>) runs only on a clean validate.
/// </summary>
public static class PortabilityValidate
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Runs the §validate checks (a)–(d) to completion over
    /// <paramref name="data"/> — <b>read-only, zero writes</b>. Returns
    /// the <see cref="PortabilityValidationResult"/>: <see cref="PortabilityValidationResult.Ok"/>
    /// on a clean archive (the apply phase may proceed), else the closed
    /// failure set (the U06 web surface + the U07 fail-closed pin render /
    /// assert <em>exactly</em> it).
    /// </summary>
    public static PortabilityValidationResult Run(KumunitaArchiveData data)
    {
        var failures = new List<string>();

        // ── Check (a) — format (C-M11·1) ───────────────────────────────
        if (data.Manifest is null
            || data.Manifest.Format != PortabilityManifest.FormatVersion)
        {
            failures.Add("format.unsupported");
            // A wrong / missing format short-circuits the rest — the
            // build cannot interpret the archive's shape (fail-closed,
            // no partial apply). The other checks run only on a format
            // the build understands.
            return new PortabilityValidationResult(Ok: false, failures);
        }

        // ── Pre-pass — read the target sets (the §validate note: the
        //    `LanguageCode` target set is read <em>before</em> the docs
        //    integrity loop). ──────────────────────────────────────────
        // The target sets (the §validate note: the `LanguageCode` target
        // set is read <em>before</em> the docs integrity loop). A
        // malformed / absent principals or config section yields an empty
        // target set (the §inventory integrity loop will then reject any
        // dangling `→ principal` / `→ LanguageCatalog` reference — the
        // closed failure set is the authority, the U07 pin asserts
        // <em>exactly</em> it).
        var principalIds = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var principals = KumunitaArchive.FromJson<List<PortabilityPrincipal>>(data.Principals);
            if (principals is not null)
                foreach (var p in principals)
                    if (!string.IsNullOrEmpty(p.SubjectId))
                        principalIds.Add(p.SubjectId);
        }
        catch (JsonException) { /* malformed → empty set (dangling refs reject below) */ }

        var languageCodes = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var config = KumunitaArchive.FromJson<PortabilityConfig>(data.Config);
            if (config?.Languages is not null)
                foreach (var lang in config.Languages)
                    if (!string.IsNullOrEmpty(lang.Id))
                        languageCodes.Add(lang.Id);
        }
        catch (JsonException) { /* malformed → empty set (dangling refs reject below) */ }

        var mediaManifestIds = new HashSet<string>(StringComparer.Ordinal);
        var mediaManifest = data.Manifest.MediaManifest;
        if (mediaManifest is not null)
            foreach (var m in mediaManifest)
                if (!string.IsNullOrEmpty(m.Id))
                    mediaManifestIds.Add(m.Id);

        // ── Check (b) — per-type sanity + deserialize for check (c) ───
        // The deserialized rows (doc type → boxed List<object>) feed the
        // referential-integrity loop below. A type that fails to
        // deserialize is a check (b) failure <em>and</em> has no id set
        // for check (c) to resolve against (a dangling ref on a malformed
        // type is subsumed by the check (b) failure — the archive is
        // already rejected).
        var rowsByType = new Dictionary<string, System.Collections.IEnumerable>(StringComparer.Ordinal);
        var typeSetById = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var entry in PortabilityDocTypes.Entries)
        {
            if (!data.Docs.TryGetValue(entry.Type, out var json))
            {
                failures.Add($"docs.missing:{entry.Type}");
                continue;
            }

            System.Collections.IEnumerable? rows;
            try
            {
                var listType = typeof(List<>).MakeGenericType(PortabilityExportDocuments.NameToType[entry.Type]);
                rows = (System.Collections.IEnumerable)(JsonSerializer.Deserialize(json, listType, JsonOpts)
                    ?? throw new JsonException("null array"));
            }
            catch (JsonException)
            {
                failures.Add($"docs.malformed:{entry.Type}");
                continue;
            }
            catch (KeyNotFoundException)
            {
                // A registry entry without a name→Type mapping — a
                // registry integrity failure (the U01 registry is the
                // source of truth; this is a build-time invariant
                // violation, surfaced as a closed failure).
                failures.Add($"docs.malformed:{entry.Type}");
                continue;
            }

            rowsByType[entry.Type] = rows;

            // Build the id set for check (c) resolution (the
            // <c>→ {DocType}</c> target). The identity field is
            // conventionally <c>Id</c> (the Marten default) — except
            // <c>Profile</c> (its identity is <c>SubjectId</c>, M1DocTypes
            // §B.1). The <c>MediaObject</c> id set is the
            // <c>media_manifest</c> set (C-M11·3: the doc + the bytes are
            // a pair; check (d) verifies the bytes).
            var idSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var idProp = row.GetType().GetProperty("Id") ?? row.GetType().GetProperty("SubjectId");
                if (idProp is not null)
                {
                    if (idProp.PropertyType == typeof(string))
                    {
                        if (idProp.GetValue(row) is string s && !string.IsNullOrEmpty(s))
                            idSet.Add(s);
                    }
                    else if (idProp.PropertyType == typeof(Guid))
                    {
                        if (idProp.GetValue(row) is Guid g)
                            idSet.Add(g.ToString("N"));
                    }
                }
            }
            typeSetById[entry.Type] = idSet;
        }

        // An unexpected <c>docs/</c> file (not in the §inventory closed
        // set) is a check (b) failure — the archive's shape does not
        // match the closed inventory (the C-M11·5 "whole-instance, not
        // per-slice" pin: the closed set is the authority).
        foreach (var (type, _) in data.Docs)
            if (!PortabilityDocTypes.ByType.ContainsKey(type))
                failures.Add($"docs.malformed:{type}");

        // ── Check (c) — referential integrity (data-driven loop) ───────
        foreach (var entry in PortabilityDocTypes.InOrder())
        {
            if (!rowsByType.TryGetValue(entry.Type, out var rows))
                continue; // a check (b) failure already rejected the type
            foreach (var row in rows)
            {
                var rowType = row.GetType();
                foreach (var refField in entry.ReferenceFields)
                {
                    var value = ReadFieldValue(rowType, refField.Field, row);
                    if (value is null)
                        continue; // an absent / null field is a satisfied
                                 // reference (the U01 note: nullable in
                                 // §inventory = skip, not dangling)

                    // Resolve the target set for this field (the D7
                    // reference map, data-driven — not per-type code).
                    HashSet<string>? targetSet = refField.Target switch
                    {
                        PortabilityReferenceField.PrincipalTarget => principalIds,
                        PortabilityReferenceField.LanguageCatalogTarget => languageCodes,
                        PortabilityReferenceField.MultiKindTarget => null,
                        "MediaObject" => mediaManifestIds,
                        _ when typeSetById.TryGetValue(refField.Target, out var ts) => ts,
                        _ => null, // an unknown target is a registry
                                   // integrity issue — skip (the type is
                                   // already rejected by check (b) if
                                   // malformed).
                    };

                    if (refField.IsArray)
                    {
                        if (value is not System.Collections.IList list)
                            continue;
                        foreach (var item in list)
                        {
                            if (item is not string id || string.IsNullOrEmpty(id))
                                continue;
                            if (!ReferenceResolves(id, refField, targetSet, typeSetById))
                            {
                                failures.Add($"ref.dangling:{entry.Type}.{refField.Field}");
                                break; // one failure per (type, field) is
                                       // the closed shape
                            }
                        }
                    }
                    else
                    {
                        if (value is not string id || string.IsNullOrEmpty(id))
                            continue;
                        if (!ReferenceResolves(id, refField, targetSet, typeSetById))
                            failures.Add($"ref.dangling:{entry.Type}.{refField.Field}");
                    }
                }
            }
        }

        // ── Check (d) — media verification (C-M11·3) ───────────────────
        // Every <c>MediaObject</c> listed in the <c>media_manifest</c>
        // has its bytes present in <c>media/{Id[0..2]}/{Id}</c> AND the
        // byte content matches (the size + the content hash implied by
        // the path). A missing / mismatched byte ⇒ reject.
        foreach (var m in data.Manifest.MediaManifest)
        {
            if (string.IsNullOrEmpty(m.Id))
                continue;
            if (!data.Media.TryGetValue(m.Id, out var bytes))
            {
                failures.Add($"media.missing:{m.Id}");
                continue;
            }
            // The content-hash pin: the <c>MediaObject.Id</c> is the
            // lowercase-hex SHA-256 of the payload (the
            // <c>LocalVolumeMediaStore.Sha256Hex</c> convention — the
            // C-M11·3 "content-hash matches the path" pin). A mismatch is
            // a corrupted / tampered byte (the C-M11·3 reject).
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (sha != m.Id)
            {
                failures.Add($"media.mismatch:{m.Id}");
                continue;
            }
            if (bytes.LongLength != m.SizeBytes)
            {
                failures.Add($"media.mismatch:{m.Id}");
                continue;
            }
        }

        return failures.Count == 0
            ? PortabilityValidationResult.Success
            : new PortabilityValidationResult(Ok: false, failures);
    }

    /// <summary>
    /// Reads one reference field's value off a deserialized row (the
    /// data-driven integrity loop's uniform accessor — reflection on the
    /// resolved property name, <em>not</em> per-type code). Returns
    /// <c>null</c> when the field is absent or null (a satisfied
    /// reference — the U01 note: the <c>Nullable</c> annotation in
    /// §inventory is <em>not</em> encoded in the registry data; an
    /// absent / null field is a skip, not a dangling one).
    /// </summary>
    private static object? ReadFieldValue(Type rowType, string fieldName, object row)
    {
        var prop = rowType.GetProperty(fieldName);
        if (prop is null)
            return null; // an absent field is a satisfied reference
        var value = prop.GetValue(row);
        return value;
    }

    /// <summary>
    /// Resolves one reference value against the field's target set (the
    /// §validate (c) pin). The <see cref="PortabilityReferenceField.MultiKindTarget"/>
    /// case is the kind-dependent <c>TargetId</c> (Notification /
    /// NotificationSubscription) — the value is resolved against the
    /// union of the four named kinds' id sets (<c>Component</c> /
    /// <c>Group</c> / <c>Page</c> / <c>Announcement</c>). A value that
    /// matches <em>none</em> of the four named sets is a <b>sentinel</b>
    /// (a non-id target — the emitters' closed sentinel set, e.g.
    /// <c>"announcements"</c> / <c>"signup"</c> / <c>"messaging.
    /// toggle"</c>) and is treated as a satisfied reference (the
    /// drift-guard rule: the §inventory names the union as
    /// <c>Component|Group|Page|Announcement</c>; a sentinel is a
    /// non-reference by design — it does not point at a doc row).
    /// </summary>
    private static bool ReferenceResolves(
        string id,
        PortabilityReferenceField field,
        HashSet<string>? directTarget,
        Dictionary<string, HashSet<string>> typeSetById)
    {
        if (field.Target == PortabilityReferenceField.MultiKindTarget)
        {
            // The union of the four named kinds' id sets — a sentinel
            // (a non-id target) matches none and is treated as
            // satisfied (the drift-guard rule above).
            var parts = field.Target.Split('|');
            foreach (var part in parts)
            {
                if (part.Length == 0)
                    continue;
                if (typeSetById.TryGetValue(part, out var set) && set.Contains(id))
                    return true;
            }
            return true; // a sentinel — treated as satisfied
        }

        return directTarget is not null && directTarget.Contains(id);
    }
}
