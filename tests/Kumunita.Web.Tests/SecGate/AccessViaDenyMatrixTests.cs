using System.Collections.Generic;
using Kumunita.Core.Authorization;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS04 (IMPROVE-SEC) — the <c>AccessVia</c> deny-matrix gate. Turns the
/// U08 closed-loop audit table (the deny-path test per <see cref="AccessVia"/>,
/// <c>docs/archive/lanes/improve/improve-audit.md</c>) from a doc promise into
/// a build-time pin: every <see cref="AccessVia"/> member is mapped to the
/// xunit test that pins its deny behaviour, and that test is asserted to
/// exist in the tree. If the next change renames a test or adds an
/// <see cref="AccessVia"/> member without a pin, this gate fails.
/// </summary>
public class AccessViaDenyMatrixTests
{
    /// <summary>
    /// The U08 table, one row per <see cref="AccessVia"/>: the denied-path
    /// test (a) for that standing. The <c>Owner</c> row records the U08
    /// "allow-branch" note: a non-owner's denial is carried by the other
    /// branches' deny tests (e.g. the <c>Audience</c> membership-change deny),
    /// so the pin is that deny test.
    /// </summary>
    public static IReadOnlyDictionary<AccessVia, string> DenyMatrix { get; } = new Dictionary<AccessVia, string>
    {
        // U08: allow-branch; a non-owner's denial is the other branches' denies.
        [AccessVia.Owner] = "C4_MembershipChange_IsLiveOnTheNextDecision",
        // U08: the Deny row landed; also the vacuous-truth guard (E13 pin).
        [AccessVia.Audience] = "EvaluateAudience_EmptyAudience_AllMode_Denies",
        // U08: Deny row Via = Delegation, asserted in that test (E13 pin).
        [AccessVia.Delegation] = "C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded",
        // U08: the off-by-default C5 deny row.
        [AccessVia.Moderator] = "C5_ModeratorAccess_OffByDefault_ModeratorCannotSee",
        // U08: Deny row Via = Report, Outcome = Deny, asserted in that test.
        [AccessVia.Report] = "CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated",
        // U08: the non-elevated deny rows.
        [AccessVia.BreakGlass] = "BreakGlass_NotConsumed_DoesNotElevate",
        // U08: the (Admin, Deny) Forbid pins (Web.Tests).
        [AccessVia.Admin] = "AddTranslation_Denied_Forbid_NoWrite",
        // U08: aggregate Deny row Via = Group, asserted.
        [AccessVia.Group] = "G2_NonMemberFeedEmptyWithDenyRow",
        // U08: Via = Guardian write-lane deny row.
        [AccessVia.Guardian] = "F5_NonGuardian_Set_Is_Denied",
        // U08: the deny rows carry Via = Community.
        [AccessVia.Community] = "A0036_CommunityFlagButNotMember_Denies",
        // U08: the deny rows carry Via = Resident.
        [AccessVia.Resident] = "A0041_AllResidentsFlag_AnonymousActor_Denies",
        // ADR 0129 (M31·9 / ESC lane): the escalated-inbound accept lane —
        // its deny-side pin is the invalid-token error path (no row, no
        // escalation) plus the Via = Escalation audit-row assertion.
        [AccessVia.Escalation] = "ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow",
        // ADR 0154 (M31·5): the unsigned-visitor error-signal lane records
        // Via = Anonymous; the deny-side pin is the anonymous-create audit
        // row test (SubjectId blank, one AccessAudit row).
        [AccessVia.Anonymous] = "M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow",
    };

    [Fact(DisplayName = "Every AccessVia member has a named deny test (no gaps)")]
    public void EveryAccessVia_HasANamedDenyTest()
    {
        var all = (AccessVia[])Enum.GetValues(typeof(AccessVia));
        var uncovered = all.Where(v => !DenyMatrix.ContainsKey(v)).ToList();
        Assert.True(
            uncovered.Count == 0,
            $"AccessVia members without a pinned deny test: {string.Join(", ", uncovered.Select(v => v.ToString()))}");
    }

    [Fact(DisplayName = "Every named deny test in the matrix exists in the test tree")]
    public void DenyMatrix_NamedTestsExist()
    {
        var missing = DenyMatrix.Values
            .Where(name => !SecGateSource.TestMethodExists(name))
            .ToList();
        Assert.True(
            missing.Count == 0,
            $"Named deny tests that no longer exist in tests/**: {string.Join(", ", missing)}");
    }

    [Fact(DisplayName = "The E13 invariant tests (empty-audience deny, delegation scope deny) are pinned")]
    public void EmptyAudienceDenies_AndDelegationScope_ArePinned()
    {
        // E13's stale "named invariant tests" open item, now a gate.
        Assert.True(
            SecGateSource.TestMethodExists("EvaluateAudience_EmptyAudience_AllMode_Denies"),
            "EvaluateAudience_EmptyAudience_AllMode_Denies is missing from tests/**.");
        Assert.True(
            SecGateSource.TestMethodExists("C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded"),
            "C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded is missing from tests/**.");
    }
}
