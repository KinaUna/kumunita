using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C04 <see cref="EventStandingPolicy"/> truth table — the M4 (Events)
/// standing + translation-variant rules, lifted from the integration tests'
/// cases (<c>EventServiceTests</c> #11–#17 + the ADR 0059 display-pin cases)
/// and asserted against the policy directly. A **pure** unit — no
/// <c>PostgresFixture</>, no <see cref="Marten.IDocumentStore"/>: just the
/// <see cref="Event"/> POCO + the <see cref="Roles"/> constants. Behaviour is
/// byte-identical to the pre-C04 <c>EventService</c> statics; this file proves
/// the relocation kept every input/allow/deny/throw combination intact.
/// </summary>
public class EventStandingPolicyTests
{
    private static readonly IReadOnlySet<string> MemberRoles = new HashSet<string> { Roles.Member };
    private static readonly IReadOnlySet<string> GlobalAdminRoles = new HashSet<string> { Roles.GlobalAdmin };
    private static readonly IReadOnlySet<string> TranslatorRoles = new HashSet<string> { Roles.Translator };
    private static readonly IReadOnlySet<string> EmptyRoles = new HashSet<string>();

    private static void ThrowsUnauthorized(Action action)
    {
        Assert.Throws<UnauthorizedAccessException>(action);
    }

    // ══ CheckCreate (ADR 0054 §3.4 — any signed-in resident) ══════════════

    [Fact]
    public void CheckCreate_SignedInResident_Allows()
    {
        var @event = new Event { Id = "s1-ev", AuthorId = "u-s1" };
        EventStandingPolicy.CheckCreate("u-s1", MemberRoles, @event);            // no exception
        EventStandingPolicy.CheckCreate("u-s1", new HashSet<string> { Roles.Member, Roles.GlobalAdmin }, @event);
    }

    [Fact]
    public void CheckCreate_NoActor_Denies()
    {
        var @event = new Event { Id = "s2-ev", AuthorId = "u-s2" };
        ThrowsUnauthorized(() => EventStandingPolicy.CheckCreate("", MemberRoles, @event));
        ThrowsUnauthorized(() => EventStandingPolicy.CheckCreate(null!, MemberRoles, @event));
    }

    [Fact]
    public void CheckCreate_NullEvent_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => EventStandingPolicy.CheckCreate("u-s3", MemberRoles, null));
    }

    // ══ CheckEdit (ADR 0054 §3.4 — author ∪ GlobalAdmin) ══════════════════

    [Fact]
    public void CheckEdit_Author_Allows()
    {
        var @event = new Event { Id = "s4-ev", AuthorId = "u-s4-author" };
        EventStandingPolicy.CheckEdit("u-s4-author", MemberRoles, @event);        // no exception
    }

    [Fact]
    public void CheckEdit_GlobalAdmin_Allows()
    {
        var @event = new Event { Id = "s5-ev", AuthorId = "u-s5-author" };
        EventStandingPolicy.CheckEdit("u-s5-admin", GlobalAdminRoles, @event);    // Admin override
    }

    [Fact]
    public void CheckEdit_Stranger_Denies()
    {
        var @event = new Event { Id = "s6-ev", AuthorId = "u-s6-author" };
        ThrowsUnauthorized(() => EventStandingPolicy.CheckEdit("u-s6-stranger", MemberRoles, @event));
    }

    [Fact]
    public void CheckEdit_NoActor_Denies()
    {
        var @event = new Event { Id = "s8-ev", AuthorId = "u-s8-author" };
        ThrowsUnauthorized(() => EventStandingPolicy.CheckEdit("", GlobalAdminRoles, @event));
    }

    [Fact]
    public void CheckEdit_NullEvent_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => EventStandingPolicy.CheckEdit("u-s7-admin", GlobalAdminRoles, null));
    }

    // ══ CanAddTranslation (ADR 0059 display pin) ══════════════════════════

    [Fact]
    public void CanAddTranslation_MatchesStandingMatrix()
    {
        const string author = "ev-a059-author";

        // Author → Owner.
        Assert.True(EventStandingPolicy.CanAddTranslation(author, author, EmptyRoles));
        // ADR 0021 Translator → Admin.
        Assert.True(EventStandingPolicy.CanAddTranslation(author, "ev-a059-translator", TranslatorRoles));
        // GlobalAdmin → Admin.
        Assert.True(EventStandingPolicy.CanAddTranslation(author, "ev-a059-admin", GlobalAdminRoles));
        // Plain Member of the author's component (no Translator / GlobalAdmin) → deny.
        Assert.False(EventStandingPolicy.CanAddTranslation(author, "ev-a059-member", MemberRoles));
        // A stranger with no roles → deny.
        Assert.False(EventStandingPolicy.CanAddTranslation(author, "ev-a059-stranger", EmptyRoles));
    }

    [Fact]
    public void ResolveTranslationStanding_DerivesAccessVia()
    {
        const string author = "ev-res-author";
        Assert.Equal(Authorization.AccessVia.Owner,
            EventStandingPolicy.ResolveTranslationStanding(author, author, EmptyRoles));
        Assert.Equal(Authorization.AccessVia.Admin,
            EventStandingPolicy.ResolveTranslationStanding(author, "ev-res-translator", TranslatorRoles));
        Assert.Equal(Authorization.AccessVia.Admin,
            EventStandingPolicy.ResolveTranslationStanding(author, "ev-res-admin", GlobalAdminRoles));
        Assert.Null(
            EventStandingPolicy.ResolveTranslationStanding(author, "ev-res-member", MemberRoles));
    }
}
