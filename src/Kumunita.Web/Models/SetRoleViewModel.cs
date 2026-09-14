using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

public sealed class SetRoleViewModel
{
    [Required]
    public string TargetSubjectId { get; set; } = string.Empty;

    /// <summary>
    /// The elevated roles to grant (ADR 0030 — independent, composable): any subset of
    /// <c>Moderator</c>, <c>Translator</c>, and <c>GlobalAdmin</c> (a resident may hold
    /// more than one, e.g. GlobalAdmin + Translator). An **empty** array means "no
    /// elevated role" (a plain Member — the implicit verified-resident standing). The
    /// <c>Member</c> string itself is never carried; it is the "nothing selected" state.
    /// </summary>
    [Display(Name = "Roles (independent — a resident may hold more than one)")]
    public string[] RoleNames { get; set; } = [];

    [Display(Name = "Component scope (Moderator only)")]
    public string[] ComponentIds { get; set; } = [];
}
