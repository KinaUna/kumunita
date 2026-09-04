using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

public sealed class SetRoleViewModel
{
    [Required]
    public string TargetSubjectId { get; set; } = string.Empty;

    [Display(Name = "Assign role")]
    public string? Role { get; set; }

    [Display(Name = "Component scope (Moderator only)")]
    public string[] ComponentIds { get; set; } = [];
}
