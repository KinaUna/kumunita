using System.Text.RegularExpressions;
using Kumunita.Core.InstanceStatus;
using Kumunita.Web.Helpers;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The Web-surface tests for IMPROVE-UX **O03** (D8, the E10 "the operator
/// learns about trouble only from logs / email" fix) — the
/// GlobalAdmin-gated dashboard notice that renders when any of the
/// operator's instance-status rows is red and links to
/// <c>/admin/status</c>.
/// <para>
/// O03 is the **last visible piece of D8** that lands in this round before
/// the docs units: O01 delivered the Core read seam
/// (<see cref="IInstanceStatusService"/> → the pure
/// <see cref="InstanceStatus"/> POCO + the pure
/// <see cref="ConfigWarningsPolicy"/>), O02 rendered that POCO as the
/// GlobalAdmin-gated <c>/admin/status</c> page, and O03 makes a bad read
/// *visible without navigating there* (the ADR 0176
/// <c>.kmb-notices</c> strip idiom, the guest-notice audience-scoping
/// precedent + the <see cref="InstanceStatusNotice"/> pure predicate).
/// </para>
/// <para>
/// <b>Two invariants are load-bearing (the O03 prompt card):</b>
/// (a) the notice reads O01's POCO — one
/// <see cref="IInstanceStatusService.GetStatusAsync"/> call, the
/// "is any row red?" decision is computed from the POCO's booleans
/// (the same ones O02's view renders) — and
/// (b) the read runs ONLY when the actor is a GlobalAdmin (the
/// <c>KumunitaPrincipal.IsGlobalAdmin</c> claim-only short-circuit —
/// a Member / Moderator / ordinary resident / anonymous visitor never
/// pays for the read; the "the reads never run for the other
/// standings" discipline, the guest-notice precedent in
/// <c>_LayoutNotices.cshtml</c>).
/// </para>
/// <para>
/// <b>Test model (the house idiom):</b> the Web.Tests suite has no
/// <c>RazorPage</c> / <c>RenderViewToString</c> harness, so the notice's
/// behaviour is pinned the same way as the
/// <see cref="AdminOnboardingBannerTests"/> structural-pin precedent:
/// (1) the <see cref="InstanceStatusNotice.AnyRedRow"/> pure predicate is
/// exercised directly over the <see cref="InstanceStatus"/> POCO
/// (parameterized: each single red fact trips it, a fully-healthy POCO
/// does not), and (2) the <c>_LayoutNotices.cshtml</c> source is pinned
/// structurally for the GlobalAdmin gate + the cheap short-circuit +
/// the <c>/admin/status</c> link + the absence of any warning-reason
/// text inline (the reasons live on the O02 page, not the notice).
/// The behavioural guarantee (the notice actually rendering at runtime)
/// is the Playwright e2e surface, not xUnit — the
/// <see cref="AdminOnboardingBannerTests"/> precedent.
/// </para>
/// </summary>
public sealed class O03AdminStatusNoticeTests
{
    // ── 1 — the predicate: each single red fact trips it (parameterized) ─
    // The O03 "is any row red?" decision is computed from the O01 POCO's
    // booleans — the same facts O02's view renders (DB / Mail / MailAlarm /
    // Storage / Warnings). The predicate mirrors the O02 view's green /
    // amber / red logic exactly, so the page and the notice can never
    // disagree (the "single source of truth" invariant, O03 prompt card).

    [Theory(DisplayName = "O03 predicate — {Fact} trips the red-row predicate")]
    [InlineData("database-unreachable")]
    [InlineData("mail-unavailable")]
    [InlineData("mail-unreachable")]
    [InlineData("mailalarm-firing")]
    [InlineData("storage-unavailable")]
    [InlineData("one-warning")]
    public void Predicate_SingleRedFact_Trips(string fact)
    {
        // One red fact per case; every other field is healthy. The
        // O02 view renders each of these facts as a red / warning
        // badge — the predicate must trip on each one (the "each
        // single red fact trips it" DoD).
        InstanceStatus poco = new()
        {
            AsOf = Now,
            Version = "1.4.2",
            Uptime = TimeSpan.FromMinutes(1),
            DatabaseReachable = true,
            Mail = HealthyMail(),
            MailAlarm = HealthyAlarm(),
            Storage = HealthyStorage(),
            Warnings = new ConfigWarning[0],
        };
        if (fact == "database-unreachable")
            poco = poco with { DatabaseReachable = false };
        else if (fact == "mail-unavailable")
            poco = poco with { Mail = MailStatus.Unavailable };
        else if (fact == "mail-unreachable")
            poco = poco with { Mail = new MailStatus(Available: true, Reachable: false, Reason: "timeout") };
        else if (fact == "mailalarm-firing")
            poco = poco with { MailAlarm = new MailAlarmStatus(Available: true, Bounces: 99, Complaints: 0, DeadLetters: 0, Firing: true) };
        else if (fact == "storage-unavailable")
            poco = poco with { Storage = StorageStatus.Unavailable };
        else if (fact == "one-warning")
            poco = poco with
            {
                Warnings = new[]
                {
                    new ConfigWarning(ConfigWarning.Keys.SeedAdminStillSet, ConfigWarning.Reasons.SeedAdminStillSet),
                },
            };
        else
            throw new InvalidOperationException($"Unexpected test fact: {fact}");

        Assert.True(InstanceStatusNotice.AnyRedRow(poco),
            $"{fact} is a red fact (the O02 view renders it as a red / warning badge) — the predicate must trip.");
    }

    [Fact(DisplayName = "O03 predicate — a fully-healthy POCO does NOT trip it (the notice is absent)")]
    public void Predicate_FullyHealthy_DoesNotTrip()
    {
        // The fully-healthy POCO: every section available + reachable,
        // the alarm not firing, and no configuration warnings. The O02
        // view renders every row green / "not firing" / "no warnings" —
        // the operator's dashboard is clean, the notice does not render.
        var poco = new InstanceStatus
        {
            AsOf = Now,
            Version = "1.4.2",
            Uptime = TimeSpan.FromMinutes(1),
            DatabaseReachable = true,
            Mail = HealthyMail(),
            MailAlarm = HealthyAlarm(),
            Storage = HealthyStorage(),
            Warnings = new ConfigWarning[0],
        };

        Assert.False(InstanceStatusNotice.AnyRedRow(poco),
            "A fully-healthy POCO trips the predicate — the notice would render on a clean instance (the O03 'healthy → no notice' DoD).");
    }

    [Fact(DisplayName = "O03 predicate — a non-firing alarm with DeadLetters > 0 does NOT trip it (the prompt's // TODO ❓ default-to-no)")]
    public void Predicate_NonFiringAlarmWithDeadLetters_DoesNotTrip()
    {
        // The O03 prompt's // TODO ❓: "whether a non-firing alarm with
        // DeadLetters > 0 is also red (O02 renders it as a count, not a
        // red row) — default to *no*, and note it." The O02 view renders
        // the dead-letter count as a plain number (the fs-4 cell, a
        // text-danger class only when the count is > 0 — see
        // Views/AdminStatus/Index.cshtml, the "dead letters" card) — it
        // is context for the firing state, not a tripped gate in itself.
        // The operator can still see the count on the /admin/status page;
        // the notice is for the tripped-gate / unreachable-section facts.
        var poco = new InstanceStatus
        {
            AsOf = Now,
            Version = "1.4.2",
            Uptime = TimeSpan.FromMinutes(1),
            DatabaseReachable = true,
            Mail = HealthyMail(),
            MailAlarm = new MailAlarmStatus(Available: true, Bounces: 3, Complaints: 1, DeadLetters: 7, Firing: false), // the count, not the gate
            Storage = HealthyStorage(),
            Warnings = new ConfigWarning[0],
        };

        Assert.False(InstanceStatusNotice.AnyRedRow(poco),
            "A non-firing alarm with DeadLetters > 0 trips the predicate — the O03 spec's // TODO ❓ says default to *no* (the O02 view renders it as a count, not a red row).");
    }

    // ── 2 — the notice is GlobalAdmin-gated (the _AdminOnboardingBanner structural-pin precedent) ─
    // The notice branch in _LayoutNotices.cshtml reads
    // KumunitaPrincipal.IsGlobalAdmin(User) (the Kumunita.Role claim read,
    // the thin-token rule ADR 0001-B) ANDed with
    // User.Identity?.IsAuthenticated == true (the signed-in gate — the
    // guest-notice precedent's "User.Identity?.IsAuthenticated == true"
    // shape). The IInstanceStatusService read is INSIDE the @if
    // (isGlobalAdmin) branch (the cheap short-circuit — the "the reads
    // never run for the other standings" discipline, the guest-notice
    // precedent in _LayoutNotices.cshtml).

    [Fact(DisplayName = "O03 notice — the branch is GlobalAdmin-gated (claim-only, signed-in) — structural pin on the source")]
    public void Notice_GlobalAdminGate_Present_In_Source()
    {
        var src = LoadLayoutNoticesSource();

        // (a) The signed-in gate (the guest-notice / _AdminOnboardingBanner
        // "User.Identity?.IsAuthenticated == true" shape — unauthenticated
        // never sees the notice).
        Assert.Contains("User.Identity?.IsAuthenticated == true", src);

        // (b) The GlobalAdmin gate (the thin-token rule ADR 0001-B — the
        // Kumunita.Role claim read, never a claim re-derived here):
        // KumunitaPrincipal.IsGlobalAdmin is the standard role read the
        // _AdminOnboardingBanner / _PinnedAnnouncement / AdminStatusController
        // all use for the GlobalAdmin gate.
        Assert.Contains("KumunitaPrincipal.IsGlobalAdmin(User)", src);

        // (c) The two are ANDed (the GlobalAdmin gate + the signed-in gate
        // are on the same @if — the cheap short-circuit: a Member /
        // Moderator / ordinary resident / anonymous visitor all fail the
        // gate and never reach the read).
        Assert.Contains("User.Identity?.IsAuthenticated == true && KumunitaPrincipal.IsGlobalAdmin(User)", src);
    }

    [Fact(DisplayName = "O03 notice — the IInstanceStatusService read is INSIDE the GlobalAdmin branch (the cheap short-circuit)")]
    public void Notice_SeamRead_IsInsideGlobalAdminBranch()
    {
        var src = LoadLayoutNoticesSource();

        // The O01 seam read (the "one seam call" invariant — the SAME
        // IInstanceStatusService.GetStatusAsync the AdminStatusController.Index
        // action calls, so the notice + the page resolve to the same value
        // — the M30·5 "the SAME seam" discipline).
        Assert.Contains("InstanceStatus.GetStatusAsync", src);

        // The read is gated by the GlobalAdmin branch (the @if
        // (User.Identity?.IsAuthenticated == true &&
        // KumunitaPrincipal.IsGlobalAdmin(User)) — the cheap
        // short-circuit: the read runs only when the actor is a
        // signed-in GlobalAdmin, never for a Member / Moderator /
        // ordinary resident / anonymous visitor — the guest-notice
        // precedent's "the reads never run for the other standings"
        // discipline).
        var gateIndex = src.IndexOf(
            "User.Identity?.IsAuthenticated == true && KumunitaPrincipal.IsGlobalAdmin(User)",
            StringComparison.Ordinal);
        Assert.True(gateIndex >= 0, "the GlobalAdmin gate is present in the source");
        var readIndex = src.IndexOf("InstanceStatus.GetStatusAsync", gateIndex, StringComparison.Ordinal);
        Assert.True(readIndex >= 0, "the IInstanceStatusService read is present in the source (after the gate)");
        // The read is BEFORE the next @await Html.PartialAsync (the
        // _OnboardingBanner — the next strip child), confirming the read
        // is in the O03 branch and not after it.
        var nextPartialIndex = src.IndexOf(
            "Html.PartialAsync(\"_OnboardingBanner\")", readIndex, StringComparison.Ordinal);
        Assert.True(nextPartialIndex > readIndex,
            "the IInstanceStatusService read is in the O03 branch (before the next strip child's @await) — the cheap short-circuit is structurally in place");
    }

    // ── 3 — red row → notice, healthy → no notice (the O03 "Done when") ──
    // The notice markup links to /admin/status and is present only when
    // the predicate is true. The predicate's unit tests (section 1) cover
    // the "red row → true / healthy → false" behaviour; the source pin
    // here covers the "the notice links to /admin/status" + "the notice
    // is gated on the predicate" shape.

    [Fact(DisplayName = "O03 notice — links to /admin/status and is gated on the predicate (structural pin)")]
    public void Notice_LinksToAdminStatus_And_GatedOnPredicate()
    {
        var src = LoadLayoutNoticesSource();

        // (a) The link to /admin/status (the O02 page — the "the page it
        // links to" invariant; the notice is the E10 fix: the operator
        // learns about trouble on the dashboard, not from logs).
        Assert.Contains("href=\"/admin/status\"", src);

        // (b) The notice is gated on the predicate (the
        // InstanceStatusNotice.AnyRedRow call — the "the red-row predicate
        // (Web layer)" invariant; the page + the notice resolve to the
        // same value via the SAME POCO + the SAME predicate).
        Assert.Contains("InstanceStatusNotice.AnyRedRow", src);

        // (c) The kw-l keys for the copy (the "the copy is a kw-l key"
        // invariant — never an inline string, the kw-l TagHelper
        // auto-escapes; the en/de/fr/da four-language parity is the C02
        // guard's job, pinned separately).
        Assert.Contains("key=\"admin.status_notice.text\"", src);
        Assert.Contains("key=\"admin.status_notice.action\"", src);

        // (d) The ADR 0176 strip idiom (data-kmb-notice — the notice-strip
        // module picks it up, the guest-notice / _PinnedAnnouncement /
        // _AdminOnboardingBanner precedent; the dismissible pattern is
        // data-dismiss-key + btn-close + data-bs-dismiss="alert" — the
        // _PinnedAnnouncement / _AdminOnboardingBanner dismissal
        // idiom).
        Assert.Contains("data-kmb-notice=\"admin-status\"", src);
        Assert.Contains("data-dismiss-key=\"kumunita-admin-status-notice-dismissed\"", src);
        Assert.Contains("data-bs-dismiss=\"alert\"", src);
        Assert.Contains("class=\"btn-close\"", src);
    }

    // ── 4 — no secret value (the "no secret values" DoD) ──────────────────
    // The notice carries only the kw-l key + the /admin/status link. The
    // warning REASONS live on the /admin/status page (the O02 view's
    // admin.status.warning.* kw-l set), not in the notice — the notice is
    // one line, the link is the affordance. A hard-coded warning reason
    // string in the notice would (a) leak a secret-free-but-instance-
    // specific operator-facing string into the global strip (visible to
    // any signed-in user's DOM if the gate regressed), and (b) drift from
    // the O02 page's rendering (the "single source of truth" invariant
    // would be violated).

    [Fact(DisplayName = "O03 notice — carries no warning-reason text inline (the reasons live on the /admin/status page)")]
    public void Notice_NoWarningReasonText_Inlined()
    {
        var src = LoadLayoutNoticesSource();

        // The four ConfigWarning.Reasons constants (the O02 page's
        // operator-facing fallback text, the ConfigWarning.Reasons
        // static-class fields) must NOT appear in the notice's markup.
        // The notice is the E10 "one line + link" shape — the reasons
        // are on the O02 page (admin.status.warning.* kw-l set), not
        // here.
        Assert.DoesNotContain(ConfigWarning.Reasons.SeedAdminStillSet, src);
        Assert.DoesNotContain(ConfigWarning.Reasons.SampleDataInProduction, src);
        Assert.DoesNotContain(ConfigWarning.Reasons.HealthTokenUnset, src);
        Assert.DoesNotContain(ConfigWarning.Reasons.DataProtectionNotPersisted, src);

        // The notice's kw-l key is the SHORT admin.status_notice.* set
        // (the "This instance has configuration or health warnings." one
        // liner), NOT the long admin.status.warning.* set (the per-warning
        // reasons). The notice's text key is admin.status_notice.text;
        // the reasons' keys are admin.status.warning.<key>.
        Assert.Contains("admin.status_notice.text", src);
        Assert.DoesNotContain("admin.status.warning.seedadmin_still_set", src);
        Assert.DoesNotContain("admin.status.warning.sampledata_in_production", src);
        Assert.DoesNotContain("admin.status.warning.health_token_unset", src);
        Assert.DoesNotContain("admin.status.warning.dataprotection_not_persisted", src);
    }

    // ─── Shared helpers ───────────────────────────────────────────────────

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static MailStatus HealthyMail() =>
        new(Available: true, Reachable: true, Reason: null);

    private static MailAlarmStatus HealthyAlarm() =>
        new(Available: true, Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false);

    private static StorageStatus HealthyStorage() =>
        new(Available: true, TotalUsedBytes: 1000, FreeVolumeBytes: 999_000, AsOf: Now);

    /// <summary>
    /// Load the <c>_LayoutNotices.cshtml</c> strip partial source, with the
    /// Razor comment blocks stripped (author documentation, not rendered
    /// markup — the <see cref="AdminOnboardingBannerTests"/> /
    /// <see cref="NavMoreFoldTests"/> house idiom).
    /// </summary>
    private static string LoadLayoutNoticesSource()
    {
        var viewsDir = ResolveViewsDir();
        var path = Path.Combine(
            viewsDir.FullName, "Shared", "_LayoutNotices.cshtml");
        Assert.True(File.Exists(path),
            $"_LayoutNotices.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not
        // rendered markup (the _AdminOnboardingBannerTests idiom).
        return Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            RegexOptions.Singleline);
    }

    /// <summary>
    /// Locates the repo's <c>src/Kumunita.Web/Views</c> folder by walking up
    /// from the test assembly's output directory to the repo root (the dir
    /// that holds <c>Kumunita.slnx</c>) — the
    /// <see cref="AdminOnboardingBannerTests"/> house idiom, correct
    /// regardless of the output depth (Debug/Release, xunit.v3, etc.).
    /// </summary>
    private static DirectoryInfo ResolveViewsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");

        var views = new DirectoryInfo(Path.Combine(dir.FullName!, "src", "Kumunita.Web", "Views"));
        Assert.True(views.Exists, $"Views directory not found at {views.FullName}.");
        return views;
    }
}
