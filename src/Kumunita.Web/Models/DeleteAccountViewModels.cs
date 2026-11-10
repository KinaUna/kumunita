using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The resident's self-serve <b>delete account</b> form (ADR 0142 D7, now
/// ADR 0301 D1/D2 — the ADR 0301 grace period amends the surface: the
/// self-serve lane is open to **every** resident, and it *schedules* rather
/// than executes). The password-confirm + checkbox pair the
/// <c>POST /account/delete</c> lane binds. The password is verified
/// server-side against the account before the write (the self-serve lane
/// confirms it is really this resident scheduling their own deletion — not a
/// crafted request); it is not persisted. The subject is always the
/// signed-in principal minted server-side (never a path param).
/// <para>
/// The <see cref="Confirmed"/> checkbox is the ADR 0142 "dangerous action"
/// guard (carried over by ADR 0301 D1 — the lane is now schedule, not
/// execute, but the dangerous-action shape stands) — the resident must
/// explicitly acknowledge that their data will be pseudonymized (their audit
/// trail remains, their identity is replaced by a tombstone) and their
/// account removed. The client-side confirm() dialog (the repo's existing
/// <c>data-confirm</c> idiom) is a *second* layer; this checkbox is the
/// *first* and is enforced server-side.
/// </para>
/// <para>
/// The two view-only state flags (the ADR 0301 D2/D3 surface — the
/// ADR 0142 D5 <c>SelfDeletionRefused</c> gate is **removed** by ADR 0301
/// D1) are:
/// <list type="bullet">
/// <item><see cref="IsScheduled"/> / <see cref="ScheduledFor"/> — the
/// pending <c>DeletionRequest</c> state (the "deletion scheduled for
/// {date} — sign in to cancel" notice + the Cancel button, ADR 0301 D2).
/// The GET sets them when a <c>Pending</c> row exists; the POST re-renders
/// them on a crafted-POST-after-schedule (the fail-closed shape).</item>
/// <item><see cref="IsChildRefused"/> — the ADR 0301 D3 child rule (the
/// resident is a child under an active <c>GuardianLink</c>; the R11 seam
/// refused the schedule with <c>UnauthorizedAccessException</c>). The
/// distinct "contact a parent/guardian" notice — the ADR 0301 D3 "the same
/// shape as 0142 D5's old <c>SelfDeletionRefused</c>, with a different
/// reason".</item>
/// </list>
/// </para>
/// </summary>
public sealed class DeleteAccountViewModel
{
    /// <summary>
    /// The resident's current password (verified server-side against the
    /// account before the write). <see cref="DataType.Password"/> so the
    /// browser autofills the saved credential. Not persisted.
    /// </summary>
    [Required, DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The explicit acknowledgment checkbox (ADR 0142's dangerous-action
    /// guard). <c>true</c> = the resident has read the consequences and
    /// confirms the deletion. <c>false</c> (or unchecked) = the POST is
    /// refused (the controller checks <c>model.Confirmed</c> explicitly and
    /// adds a model error — a bare <c>[Required]</c> on a bool would accept
    /// <c>false</c>, since the bound value is non-null).
    /// </summary>
    [Display(Name = "I understand my account will be permanently deleted")]
    public bool Confirmed { get; set; }

    /// <summary>
    /// View-only flag (not bound from the form): <c>true</c> when the
    /// signed-in resident has a pending <c>DeletionRequest</c> row (the
    /// ADR 0301 D2 scheduled state). The view renders the "deletion
    /// scheduled for {date} — sign in to cancel" notice + the Cancel button
    /// (the ADR 0301 D2 "no extra button, no extra form" — the Cancel button
    /// is the explicit lane the R13 prompt calls for; the ADR defines the
    /// rule, this surface renders it). The GET sets this when a
    /// <c>Pending</c> row exists (the <c>Cancelled</c> / <c>Executed</c>
    /// rows are terminal — a new schedule is a fresh act, so they render
    /// the form, not the scheduled state).
    /// </summary>
    public bool IsScheduled { get; set; }

    /// <summary>
    /// The scheduled execution instant, formatted in the resident's effective
    /// time zone (the ADR 0019 <c>EffectiveTimezoneResolver</c> seam — their
    /// zone when the resolver is available, the <c>UTC</c> floor in the test
    /// harness shape). <c>null</c> when <see cref="IsScheduled"/> is
    /// <c>false</c> (the form renders, not the scheduled state). The format
    /// is the current culture's long date (the ADR 0020 platform-default floor
    /// — the resident's effective language drives the UI strings, the date
    /// glyph set is the platform's).
    /// </summary>
    public string? ScheduledFor { get; set; }

    /// <summary>
    /// View-only flag (not bound from the form): <c>true</c> when the
    /// signed-in resident is a child under an active <c>GuardianLink</c> and
    /// the R11 seam refused the schedule (the ADR 0301 D3 child rule — the
    /// <c>UnauthorizedAccessException</c> catch in the POST /account/delete
    /// action). The view renders the distinct "contact a parent/guardian"
    /// notice (the ADR 0301 D3 "the same shape as 0142 D5's old
    /// <c>SelfDeletionRefused</c>, with a different reason"). The ADR 0142
    /// D5 GlobalAdmin gate that used to set <c>SelfDeletionRefused</c> is
    /// removed by ADR 0301 D1; this flag is the *new* distinct state.
    /// </summary>
    public bool IsChildRefused { get; set; }
}
