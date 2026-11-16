using System.Reflection;
using Kumunita.Core.Portability;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M27 U05 — the four resolve-model <b>shape pins</b> (the exact names pinned
/// in the unit plan's Deliverable 2 + the design doc §2.5 resolve apply
/// contract). These are <b>pure shape pins</b>: they assert the closed
/// record / enum shapes that U01's shell shipped (the design doc §2.1
/// verbatim) + the D4 no-auto-merge posture, over the POCO / enum surface
/// directly — <b>no Postgres boot</b> (the apply behavior — the
/// <see cref="UserPortabilityService.ResolveAsync"/> body — is still
/// <c>NotImplementedException</c> until U06, so there is no behavior to
/// exercise here).
/// <para>
/// The four pins:
/// <list type="bullet">
/// <item><b>closed set</b> — <c>ResolveModel_ClosedResolutionKindSet</c>:
///     <see cref="UserPortabilityResolutionKind"/> is exactly
///     <c>{AddElsewhere, Discard}</c> — two members, no default, no
///     fallback (the D4 no-auto-merge pin: the choice is the resident's,
///     never a computed default).</item>
/// <item><b>re-point shape</b> — <c>ResolveModel_AddElsewhere_RepointShape</c>:
///     the <see cref="UserPortabilityEntityResolution"/> closed POCO shape +
///     the <c>AddElsewhere</c> re-point fields key off U04's
///     <see cref="UserPortabilityAbsentReference"/> <c>{Kind, Field, Value}</c>
///     shape verbatim (<c>AbsentRefKind</c> ↔ <c>Kind</c>,
///     <c>AbsentRefField</c> ↔ <c>Field</c>) + the picked target id
///     (<c>PickedTargetId</c>).</item>
/// <item><b>discard no-write</b> — <c>ResolveModel_Discard_NoWritePin</c>:
///     the <c>Discard</c> resolution carries <b>no</b> re-point data (the
///     three <c>AddElsewhere</c>-only fields are null) — the shape of the
///     no-write choice (the apply is U06's pin, not this one's).</item>
/// <item><b>closed-failure set</b> — <c>ResolveModel_ImportResult_ClosedFailureSet</c>:
///     the <see cref="UserPortabilityImportResult"/> closed-failure shape —
///     the M11 <c>PortabilityImportResult(bool Ok, IReadOnlyList&lt;string&gt;
///     Failures)</c> shape emulated (the U05 entry read #2), extended with the
///     apply counts + the D4 no-auto-merge failure set.</item>
/// </list>
/// </para>
/// <para>
/// **No apply logic** (U06 owns <c>ResolveAsync</c>) and **no new
/// authorization surface** (C-M27·7 — these pins touch only the POCO / enum
/// shapes, no <c>AccessAction</c> / <c>AccessVia</c> /
/// <c>IAuthorizationService</c> branch).
/// </para>
/// </summary>
public sealed class UserPortabilityResolveModelTests
{
    /// <summary>
    /// The closed, writable positional properties of a sealed record, in a
    /// name → property-type map (the "closed shape" — the pin asserts the
    /// exact name set + the type of each, which is the record's public
    /// value shape; nullable-reference fields share the same runtime type as
    /// their non-nullable counterparts, so nullability is asserted by
    /// construction in the individual pins, not by the type map).
    /// </summary>
    private static Dictionary<string, Type> ClosedProps(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.Ordinal);

    /// <summary>
    /// The closed <see cref="UserPortabilityResolutionKind"/> set —
    /// <c>{AddElsewhere, Discard}</c>, exactly two members, no default / no
    /// fallback (the D4 no-auto-merge pin: a <c>conflict</c> entity is
    /// <em>chosen</em> by the resident, never auto-re-pointed to a computed
    /// default).
    /// </summary>
    [Fact]
    public void ResolveModel_ClosedResolutionKindSet()
    {
        var t = typeof(UserPortabilityResolutionKind);
        Assert.True(t.IsEnum);

        var names = new HashSet<string>(
            Enum.GetNames(t), StringComparer.Ordinal);
        Assert.Equal(
            new HashSet<string>(["AddElsewhere", "Discard"], StringComparer.Ordinal),
            names);

        // The closed-set pin is also a count pin — no third "default" member
        // (the D4 no-auto-merge: there is no implicit choice, so a resident
        // who does not resolve a conflict gets nothing applied, not a
        // computed re-point).
        Assert.Equal(2, Enum.GetValues(t).Length);
    }

    /// <summary>
    /// The <see cref="UserPortabilityEntityResolution"/> closed POCO shape +
    /// the <c>AddElsewhere</c> re-point shape: the decision is keyed on the
    /// entity kind + in-archive id (<c>Kind</c> + <c>EntityId</c>), the
    /// <see cref="UserPortabilityResolutionKind"/> discriminator, and the
    /// <c>AddElsewhere</c> re-point fields — the picked target id
    /// (<c>PickedTargetId</c>) + the absent-reference kind / field the
    /// re-point targets (<c>AbsentRefKind</c> / <c>AbsentRefField</c>). The
    /// re-point keys off U04's <see cref="UserPortabilityAbsentReference"/>
    /// <c>{Kind, Field, Value}</c> shape verbatim — <c>AbsentRefKind</c>
    /// carries the absent-ref's <c>Kind</c> and <c>AbsentRefField</c>
    /// carries the absent-ref's <c>Field</c> (the resolve-review UI renders
    /// that reason verbatim; the <c>Value</c> is the archive id that is
    /// absent, replaced by the resident's picked target).
    /// </summary>
    [Fact]
    public void ResolveModel_AddElsewhere_RepointShape()
    {
        // ── The closed POCO shape: exactly six positional fields, the locked
        //    types (the design doc §2.1 verbatim). ──
        var t = typeof(UserPortabilityEntityResolution);
        Assert.True(t.IsSealed);
        Assert.False(t.IsValueType);

        var props = ClosedProps(t);
        Assert.Equal(
            new HashSet<string>(
                ["Kind", "EntityId", "Resolution", "PickedTargetId",
                 "AbsentRefKind", "AbsentRefField"],
                StringComparer.Ordinal),
            props.Keys.ToHashSet(StringComparer.Ordinal));

        Assert.Equal(typeof(string), props["Kind"]);
        Assert.Equal(typeof(string), props["EntityId"]);
        Assert.Equal(typeof(UserPortabilityResolutionKind), props["Resolution"]);
        Assert.Equal(typeof(string), props["PickedTargetId"]);
        Assert.Equal(typeof(string), props["AbsentRefKind"]);
        Assert.Equal(typeof(string), props["AbsentRefField"]);

        // ── The re-point source shape is U04's conflict reason, verbatim —
        //    the UserPortabilityAbsentReference closed {Kind, Field, Value}
        //    (the design doc §2.1 / §2.4; the U07/U08 resolve-review renders
        //    it). AbsentRefKind / AbsentRefField mirror its Kind / Field. ──
        var absentT = typeof(UserPortabilityAbsentReference);
        var absent = ClosedProps(absentT);
        Assert.Equal(
            new HashSet<string>(["Kind", "Field", "Value"], StringComparer.Ordinal),
            absent.Keys.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(typeof(string), absent["Kind"]);
        Assert.Equal(typeof(string), absent["Field"]);
        Assert.Equal(typeof(string), absent["Value"]);

        // ── The AddElsewhere re-point accepts U04's conflict reason verbatim
        //    (the absent-ref Kind + Field) and carries the picked target id —
        //    the "add it to a group you are a member of" decision. ──
        var absentRef = new UserPortabilityAbsentReference(
            Kind: "Group", Field: "GroupId", Value: "g-absent-in-target");
        var res = new UserPortabilityEntityResolution(
            Kind: "Post",
            EntityId: "post-1",
            Resolution: UserPortabilityResolutionKind.AddElsewhere,
            PickedTargetId: "g-picked-by-resident",
            AbsentRefKind: absentRef.Kind,   // "Group"   — verbatim from U04's shape
            AbsentRefField: absentRef.Field); // "GroupId" — verbatim from U04's shape

        Assert.Equal(UserPortabilityResolutionKind.AddElsewhere, res.Resolution);
        Assert.Equal("post-1", res.EntityId);
        Assert.Equal("Post", res.Kind);
        Assert.Equal("g-picked-by-resident", res.PickedTargetId);
        Assert.Equal("Group", res.AbsentRefKind);
        Assert.Equal("GroupId", res.AbsentRefField);
    }

    /// <summary>
    /// The <c>Discard</c> no-write pin (the shape — the apply is U06's): a
    /// <see cref="UserPortabilityResolutionKind.Discard"/> resolution is a
    /// first-class closed member of the decision set that carries <b>no</b>
    /// re-point data — the three <c>AddElsewhere</c>-only fields
    /// (<c>PickedTargetId</c> / <c>AbsentRefKind</c> / <c>AbsentRefField</c>)
    /// are null. The no-write choice is a closed, explicit decision (the D4
    /// no-auto-merge posture — it is <em>chosen</em>, not defaulted), and the
    /// shape that U06's <c>ResolveAsync</c> will apply (no write, counted in
    /// <see cref="UserPortabilityImportResult.DiscardedCount"/>).
    /// </summary>
    [Fact]
    public void ResolveModel_Discard_NoWritePin()
    {
        // ── Discard is a closed member of the decision set (the no-write
        //    choice is explicit, not a fallback). ──
        var discard = Enum.Parse<UserPortabilityResolutionKind>("Discard");
        Assert.Equal(UserPortabilityResolutionKind.Discard, discard);

        // ── The shape: a Discard resolution is constructible and carries no
        //    re-point data (no target, no absent-ref) — the no-write choice
        //    (the apply behavior — "writes nothing" — is U06's pin, this one
        //    pins the shape). ──
        var res = new UserPortabilityEntityResolution(
            Kind: "Post",
            EntityId: "post-1",
            Resolution: UserPortabilityResolutionKind.Discard,
            PickedTargetId: null,
            AbsentRefKind: null,
            AbsentRefField: null);

        Assert.Equal(UserPortabilityResolutionKind.Discard, res.Resolution);
        Assert.Equal("post-1", res.EntityId);
        Assert.Null(res.PickedTargetId);
        Assert.Null(res.AbsentRefKind);
        Assert.Null(res.AbsentRefField);
    }

    /// <summary>
    /// The <see cref="UserPortabilityImportResult"/> closed-failure shape —
    /// the M11 <c>PortabilityImportResult(bool Ok, IReadOnlyList&lt;string&gt;
    /// Failures)</c> shape emulated (the U05 entry read #2, the closed
    /// failure-set), extended with the apply counts (<c>AppliedCount</c> /
    /// <c>DiscardedCount</c>). A fail-closed rejection (a
    /// <c>PickedTargetId</c> the resident has no standing over, a write error,
    /// a mid-apply failure — the D4 / §2.5 contract) is <c>Ok</c> =
    /// <c>false</c> + the closed <c>Failures</c> set and writes nothing.
    /// </summary>
    [Fact]
    public void ResolveModel_ImportResult_ClosedFailureSet()
    {
        // ── The closed POCO shape: exactly four positional fields, the locked
        //    types (the design doc §2.1 verbatim). ──
        var t = typeof(UserPortabilityImportResult);
        Assert.True(t.IsSealed);
        Assert.False(t.IsValueType);

        var props = ClosedProps(t);
        Assert.Equal(
            new HashSet<string>(
                ["Ok", "AppliedCount", "DiscardedCount", "Failures"],
                StringComparer.Ordinal),
            props.Keys.ToHashSet(StringComparer.Ordinal));

        Assert.Equal(typeof(bool), props["Ok"]);
        Assert.Equal(typeof(int), props["AppliedCount"]);
        Assert.Equal(typeof(int), props["DiscardedCount"]);
        Assert.Equal(typeof(IReadOnlyList<string>), props["Failures"]);

        // ── The closed-failure shape is a valid instance: a fail-closed
        //    rejection (no standing over the picked target) is Ok = false +
        //    the closed failure set, zero applied (the D4 / §2.5 fail-closed
        //    contract — the write is rolled back, nothing is written). ──
        var result = new UserPortabilityImportResult(
            Ok: false,
            AppliedCount: 0,
            DiscardedCount: 2,
            Failures: [
                "conflict 'post-1' not resolved — not applied (no auto-merge)",
                "'post-2' AddElsewhere target 'g-x' — resident has no standing",
            ]);

        Assert.False(result.Ok);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(2, result.DiscardedCount);
        Assert.Equal(2, result.Failures.Count);
        Assert.Contains("no auto-merge", result.Failures[0]);
    }
}
