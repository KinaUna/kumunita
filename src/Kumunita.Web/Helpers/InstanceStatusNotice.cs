using Kumunita.Core.InstanceStatus;

namespace Kumunita.Web.Helpers;

/// <summary>
/// The red-row predicate for the IMPROVE-UX **O03** dashboard notice (the
/// GlobalAdmin-gated <c>.kmb-notices</c> strip branch that renders when any
/// of the operator's <see cref="InstanceStatus"/> rows is red and links to
/// <c>/admin/status</c> — ADR 0176 strip idiom, the E10 "the operator
/// learns about trouble only from logs / email" fix, D8 decision).
/// <para>
/// <b>Single source of truth:</b> this method computes the notice's trigger
/// directly from the O01 POCO's booleans — <see cref="InstanceStatus"/>
/// (DB / Mail / MailAlarm / Storage / Warnings) — the <b>same</b> facts the
/// O02 <c>/admin/status</c> page renders. The predicate mirrors that view's
/// green / amber / red logic exactly, so the page and the notice can never
/// disagree: a row O02 renders red or amber (the "operator must look at this
/// row" facts) is what trips the notice, and a fully-healthy POCO trips it
/// not.
/// </para>
/// <para>
/// <b>What counts as red</b> (each of these is rendered as a red fact or a
/// warning badge in <c>Views/AdminStatus/Index.cshtml</c>):
/// <list type="bullet">
/// <item><c>!DatabaseReachable</c> — the O02 view renders <c>text-danger</c>
/// "Unreachable" for the DB row.</item>
/// <item><c>!Mail.Available</c> — the O02 view renders a
/// <c>text-bg-warning</c> "unavailable" badge (the probe itself failed —
/// the section is unusable, the operator must look).</item>
/// <item><c>Mail.Available &amp;&amp; !Mail.Reachable</c> — the O02 view
/// renders a <c>text-bg-warning</c> "Relay degraded" badge (the relay
/// handshake failed — e-mail is not flowing, the operator must look).</item>
/// <item><c>MailAlarm.Firing</c> — the O02 view renders a
/// <c>text-bg-danger</c> "firing" badge (the T4 gate is tripped — the
/// operator must look).</item>
/// <item><c>!Storage.Available</c> — the O02 view renders a
/// <c>text-bg-warning</c> "unavailable" badge (the byte-store metrics are
/// unreadable — the operator must look).</item>
/// <item><c>Warnings.Count &gt; 0</c> — the O02 view renders each warning
/// as a <c>text-bg-warning</c> badge row (a misconfiguration the operator
/// must look at — the E11 "SeedAdmin still set" class of warning).</item>
/// </list>
/// </para>
/// <para>
/// <b>What does NOT count as red:</b> <c>MailAlarm.Available == false</c>
/// (the O02 view renders an "unavailable" fact for that section — the
/// <b>read itself</b> failed, which is already covered by
/// <c>!DatabaseReachable</c> in practice, and is rendered as a
/// <c>text-bg-warning</c> badge rather than a red row; the O03 spec's
/// red-row predicate intentionally does NOT include this — a degraded
/// section that the operator can still see a value for is not a "red row"
/// in the O02 view's own rendering), and <c>MailAlarm.DeadLetters &gt; 0</c>
/// with <c>Firing == false</c> (the O02 view renders the dead-letter count
/// as a plain number, <b>not</b> a red badge — the count is context for
/// the firing state, not a tripped gate in itself).
/// </para>
/// <para>
/// <b>Open question (the O03 prompt's <c>// TODO ❓</c>):</b> a non-firing
/// alarm with <c>DeadLetters &gt; 0</c> — the O02 view renders the
/// dead-letter count as a plain number (a <c>text-danger</c> class only
/// when the count is &gt; 0, the <c>fs-4</c> number cell — see
/// <c>Views/AdminStatus/Index.cshtml</c>, the "dead letters" card). The
/// O03 spec says: <i>"default to *no*, and note it"</i> — a non-firing
/// alarm is NOT a red row for the notice's purposes (this method returns
/// <c>false</c> for that fact alone). The operator can still see the count
/// on the <c>/admin/status</c> page; the notice is for the tripped-gate /
/// unreachable-section facts. If a future unit promotes the dead-letter
/// backlog to a red fact in the O02 view (e.g. a threshold badge), this
/// predicate should be extended to match in the same commit.
/// </para>
/// </summary>
public static class InstanceStatusNotice
{
    /// <summary>
    /// True iff any of the operator's <see cref="InstanceStatus"/> rows is
    /// red — the O03 dashboard-notice trigger (the "red row → notice"
    /// done-when, the O02 view's green / amber / red logic mirrored
    /// exactly). A fully-healthy POCO returns <c>false</c> (the notice
    /// does not render; the operator's dashboard is clean).
    /// </summary>
    /// <param name="status">The O01 POCO (the same read the O02
    /// <c>/admin/status</c> page renders — the single source of truth).</param>
    public static bool AnyRedRow(InstanceStatus status)
    {
        if (status is null)
            return false;

        // The six red facts (each of these is rendered as a red / warning
        // badge in Views/AdminStatus/Index.cshtml — the "operator must look
        // at this row" set):

        // (1) DB unreachable (the O02 view's text-danger "Unreachable").
        if (!status.DatabaseReachable)
            return true;

        // (2) Mail section degraded — the probe itself failed (!Available)
        // or the relay handshake failed (Available && !Reachable). Either
        // way e-mail is not flowing and the O02 view renders a warning
        // badge for the mail relay row.
        if (!status.Mail.Available || !status.Mail.Reachable)
            return true;

        // (3) The T4 gate is firing (the O02 view's text-bg-danger "firing"
        // badge — the operator must look at the mail-alarm state).
        if (status.MailAlarm.Firing)
            return true;

        // (4) Storage section degraded (the O02 view's text-bg-warning
        // "unavailable" badge — the byte-store metrics are unreadable).
        if (!status.Storage.Available)
            return true;

        // (5) Any configuration warning (the O02 view renders each as a
        // text-bg-warning badge row — the E11 "SeedAdmin still set" class
        // of misconfiguration the operator must fix).
        if (status.Warnings.Count > 0)
            return true;

        // No red row (the fully-healthy POCO — the notice does not render).
        return false;
    }
}
