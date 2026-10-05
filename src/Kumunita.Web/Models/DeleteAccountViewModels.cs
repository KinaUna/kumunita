using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The resident's self-serve <b>delete account</b> form (ADR 0142) — the
/// password-confirm + checkbox pair the <c>POST /account/delete</c> lane
/// binds. The password is verified server-side against the account before
/// the write (the self-serve lane confirms it is really this resident
/// deleting their own account — not a crafted request); it is not persisted.
/// The subject is always the signed-in principal minted server-side (never a
/// path param).
/// <para>
/// The <see cref="Confirmed"/> checkbox is the ADR 0142 "dangerous action"
/// guard — the resident must explicitly acknowledge that their data will be
/// pseudonymized (their audit trail remains, their identity is replaced by a
/// tombstone) and their account removed. The client-side confirm() dialog
/// (the repo's existing <c>data-confirm</c> idiom) is a *second* layer; this
/// checkbox is the *first* and is enforced server-side.
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
    /// View-only flag (not bound from the form): when the signed-in resident
    /// is <b>not</b> a <c>GlobalAdmin</c>, the view renders a notice that the
    /// self-serve lane is unavailable for them (the ADR 0142 D5 gate — the
    /// self-deletion seam is only reachable by a GlobalAdmin) and that they
    /// should contact an administrator instead. The ADR 0138
    /// <see cref="ChangePasswordLockedViewModel"/> "surface replaced by a
    /// notice" shape, generalized: the form is still shown (so a
    /// non-GlobalAdmin's crafted POST is refused with the same message), but
    /// the page makes the gate visible rather than letting the resident
    /// discover it only on submit.
    /// </summary>
    public bool SelfDeletionRefused { get; set; }
}
