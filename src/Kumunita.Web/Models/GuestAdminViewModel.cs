using Kumunita.Core.Identity;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin/guests</c> form model (M19 U04 — the D6 admin surface). The
/// thin control-plane shape: the bounded window (D3, <see cref="ValidFrom"/> /
/// <see cref="ValidUntil"/>), the closed surface set as three checkboxes (D4 —
/// one per <see cref="GuestSurface"/> value, unchecked = absent, the empty
/// floor C-M19·4), and the guest's <see cref="SubjectId"/> (the document id —
/// one allowance per guest account). <see cref="HasStanding"/> /
/// <see cref="HasSurfaces"/> are the seeded-state flags the view renders;
/// <see cref="Seed"/> populates the form from a read of the existing standing
/// (the C-M19·4 empty floor: <c>null</c> seeds the empty set, not an error).
/// Matches the <see cref="AdminSignupController.SignupAdminViewModel"/> shape
/// (the ADR 0050 precedent).
/// </summary>
public sealed class GuestAdminViewModel
{
    /// <summary>The guest's subject id (the <see cref="GuestAccess"/> document
    /// id — one allowance per guest account).</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// The bounded window start (D3, "when"), inclusive. Bound to the form's
    /// <c>validFrom</c> input; the standing is live from this instant.
    /// </summary>
    public DateTimeOffset ValidFrom { get; set; }

    /// <summary>
    /// The bounded window end (D3, "when"), exclusive (<c>now &lt;
    /// ValidUntil</c>). Bound to the form's <c>validUntil</c> input. The save
    /// validates <c>ValidUntil &gt; ValidFrom</c> (an empty / inverted window
    /// is rejected at the boundary — C-M19·3 forbids an unbounded standing).
    /// </summary>
    public DateTimeOffset ValidUntil { get; set; }

    /// <summary>The <see cref="GuestSurface.Announcements"/> checkbox (D4).</summary>
    public bool Announcements { get; set; }

    /// <summary>The <see cref="GuestSurface.Events"/> checkbox (D4).</summary>
    public bool Events { get; set; }

    /// <summary>The <see cref="GuestSurface.Directory"/> checkbox (D4).</summary>
    public bool Directory { get; set; }

    /// <summary>
    /// Whether a settled standing was seeded for this <see cref="SubjectId"/>
    /// (the view's empty-state flag: <c>false</c> renders <c>admin.guests_empty</c>).
    /// </summary>
    public bool HasStanding { get; set; }

    /// <summary>
    /// True when at least one surface checkbox is checked (the view's
    /// "no surfaces selected" hint: <c>false</c> with a live window is a valid,
    /// least-privileged shell — C-M19·4 — not an error).
    /// </summary>
    public bool HasSurfaces => Announcements || Events || Directory;

    /// <summary>
    /// Populate the form from an existing <see cref="GuestAccess"/> standing
    /// (the <c>GET /admin/guests</c> seed). A <c>null</c> / absent standing seeds
    /// the empty floor (C-M19·4) — the window defaults to the current instant,
    /// no surface checked, <see cref="HasStanding"/> false (the view renders the
    /// <c>admin.guests_empty</c> state). The checkboxes decompose the
    /// <see cref="GuestAccess.AllowedSurfaces"/> <c>[Flags]</c> value back to the
    /// three booleans the form binds (D4).
    /// </summary>
    public void Seed(GuestAccess? existing)
    {
        if (existing is null)
        {
            // The C-M19·4 empty floor — a guest with no settled standing.
            HasStanding = false;
            ValidFrom = DateTimeOffset.UtcNow;
            ValidUntil = DateTimeOffset.UtcNow;
            Announcements = false;
            Events = false;
            Directory = false;
            return;
        }

        HasStanding = true;
        ValidFrom = existing.ValidFrom;
        ValidUntil = existing.ValidUntil;
        Announcements = (existing.AllowedSurfaces & GuestSurface.Announcements) != 0;
        Events = (existing.AllowedSurfaces & GuestSurface.Events) != 0;
        Directory = (existing.AllowedSurfaces & GuestSurface.Directory) != 0;
    }
}
