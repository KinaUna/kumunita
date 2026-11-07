namespace Kumunita.Core.Portability;

/// <summary>
/// The export scope (ADR 0299, IS27) — <see cref="ContentFree"/> is the
/// default: structure, public content, and the resident's own data, with
/// all class (b) private content stripped (the §2.6 table). <see cref="Full"/>
/// restores the complete M11 archive (all 44 types, no fields blanked,
/// no rows filtered) and requires an active <c>AdminOverride</c>
/// (IS28's gate, ADR 0299).
/// </summary>
public enum PortabilityExportScope
{
    /// <summary>
    /// The content-free export (the default; ADR 0299): the 44-type archive
    /// with the design §2.6 strip rules applied —
    /// <see cref="Privacy.DataClassKind.PrivateContent"/> types omitted
    /// (except the 7 audience-bearing types, whose restricted rows are
    /// filtered), <see cref="Privacy.DataClassKind.Pii"/> fields blanked,
    /// and the 8 <c>TODO ❓</c> types omitted.
    /// </summary>
    ContentFree,

    /// <summary>
    /// The full export (ADR 0299): the complete M11 archive (all 44 types,
    /// no fields blanked, no rows filtered). Requires an active
    /// <c>AdminOverride</c> (IS28's gate) and is audited
    /// <c>Via: BreakGlass</c>.
    /// </summary>
    Full,
}

/// <summary>
/// The §2.6 content-free filter (IS27) — the strip rules driven by
/// <see cref="Privacy.DataClassCatalog"/> (ADR 0298):
/// <list type="number">
///   <item><b>Rule 1</b> — <see cref="Privacy.DataClassKind.PrivateContent"/>
///         types <em>without</em> an <c>Audience</c> property are omitted
///         from the archive entirely (the 23 of the 30).</item>
///   <item><b>Rule 2</b> — <see cref="Privacy.DataClassKind.PrivateContent"/>
///         types <em>with</em> an <c>Audience</c> property keep the type
///         but drop rows whose <c>Audience</c> is non-null (restricted) —
///         public rows (<c>Audience = null</c>, the frozen
///         <c>Decide()</c> branch 5) stay (the 7 of the 30).</item>
///   <item><b>Rule 3</b> — <see cref="Privacy.DataClassKind.Pii"/> types
///         (<c>Profile</c>) keep the row but blank the PII fields
///         (<c>Email</c>, <c>Phone</c>, <c>Address</c>).</item>
/// </list>
/// The 8 <c>TODO ❓</c> types (<see cref="Privacy.DataClassKind.AuditModeration"/>
/// or <see cref="Privacy.DataClassKind.Operational"/> with
/// <see cref="Privacy.ExportScope.Never"/>) are omitted; the 5 always-present
/// types (<see cref="Privacy.DataClassKind.Platform"/>,
/// <see cref="Privacy.DataClassKind.Media"/>, and
/// <see cref="Privacy.DataClassKind.AuditModeration"/> with
/// <see cref="Privacy.ExportScope.InstanceOnly"/>) are kept.
/// The distinction between TODO-❓ and always-present
/// <see cref="Privacy.DataClassKind.AuditModeration"/> types is the
/// <see cref="Privacy.ExportScope"/>: <c>Never</c> = TODO-❓ (skip),
/// <c>InstanceOnly</c> = governance (keep).
/// </summary>
internal static class ContentFreeFilter
{
    /// <summary>
    /// The <c>Profile</c> PII fields blanked by Rule 3 (design §2.6).
    /// </summary>
    private static readonly string[] ProfilePiiFields = { "Email", "Phone", "Address" };

    /// <summary>
    /// Whether a type is omitted entirely under
    /// <see cref="PortabilityExportScope.ContentFree"/> (Rule 1, or a
    /// TODO-❓ type).
    /// </summary>
    public static bool ShouldSkipType(Type docType)
    {
        var policy = Privacy.DataClassCatalog.For(docType);
        if (policy is null) return false; // no policy → keep (defensive)

        return policy.Kind switch
        {
            // Rule 1: PrivateContent without an Audience property → skip.
            // Rule 2 types (PrivateContent + Audience) are NOT skipped —
            // their rows are filtered by ShouldFilterRows.
            Privacy.DataClassKind.PrivateContent => !HasAudienceProperty(docType),

            // TODO-❓ types (ExportScope.Never) are skipped;
            // governance types (ExportScope.InstanceOnly) are kept.
            Privacy.DataClassKind.AuditModeration
            or Privacy.DataClassKind.Operational
                => policy.Export == Privacy.ExportScope.Never,

            // Pii (Rule 3: blank, don't skip), Platform, Media, Secret → keep.
            _ => false,
        };
    }

    /// <summary>
    /// Whether a type's rows should be filtered (Rule 2): keep the type,
    /// drop rows whose <c>Audience</c> is non-null (restricted).
    /// </summary>
    public static bool ShouldFilterRows(Type docType)
    {
        var policy = Privacy.DataClassCatalog.For(docType);
        return policy is { Kind: Privacy.DataClassKind.PrivateContent }
            && HasAudienceProperty(docType);
    }

    /// <summary>
    /// Whether a type's PII fields should be blanked (Rule 3):
    /// <see cref="Kumunita.Core.UserInfo.Profile"/>.
    /// </summary>
    public static bool ShouldBlankPii(Type docType)
        => docType == typeof(Kumunita.Core.UserInfo.Profile);

    /// <summary>
    /// Blanks the §2.6 PII fields on a <c>Profile</c> row (Rule 3).
    /// </summary>
    public static void BlankProfilePii(object profile)
    {
        var t = profile.GetType();
        foreach (var field in ProfilePiiFields)
        {
            t.GetProperty(field)?.SetValue(profile, null);
        }
    }

    /// <summary>
    /// Rule 2 — keeps only the public rows (<c>Audience = null</c>, the
    /// frozen <c>Decide()</c> branch 5) from the materialized list and
    /// returns a new <c>List&lt;T&gt;</c> of the same type. Uniform
    /// (reflection on the resolved <see cref="Type"/> — not per-type code),
    /// mirroring the export loop's existing dispatch shape.
    /// </summary>
    public static System.Collections.IList KeepOnlyPublicRows(
        System.Collections.IList rows, Type docType)
    {
        var audienceProp = docType.GetProperty("Audience",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!;
        var resultType = typeof(List<>).MakeGenericType(docType);
        var result = (System.Collections.IList)System.Activator.CreateInstance(resultType)!;
        foreach (var row in (System.Collections.IEnumerable)rows)
        {
            if (IsPublicAudience(audienceProp.GetValue(row)))
                result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// Whether a row's <c>Audience</c> is "public" (design §2.6 "null/empty"):
    /// <c>null</c> = world-readable (the frozen <c>Decide()</c> branch 5);
    /// or an <c>isEmpty</c> Audience with no <c>Community</c>/<c>AllResidents</c>
    /// flags (the codebase's "no specific audience restriction" shape —
    /// <c>Audience.IsEmpty</c> = empty grants list). A non-empty grants list,
    /// or any Audience with the <c>Community</c>/<c>AllResidents</c> flag set,
    /// is restricted and filtered out.
    /// </summary>
    public static bool IsPublicAudience(object? audience)
    {
        if (audience is null) return true;
        if (audience is not Kumunita.Core.Authorization.Audience a) return false;
        return a.IsEmpty && !a.Community && !a.AllResidents;
    }

    private static bool HasAudienceProperty(Type docType)
        => docType.GetProperty("Audience",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) is not null;
}
