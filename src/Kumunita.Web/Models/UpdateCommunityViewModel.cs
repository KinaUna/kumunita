using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin</c> "edit a community" form (the M3+ surface). Patch
/// semantics at the Core lane: <see cref="Kumunita.Core.UserInfo.IUserInfoService.
/// UpdateCommunityAsync"/> treats null arguments as <i>keep-as-is</i>, so
/// every optional field here is nullable and the controller leaves a null
/// alone (rather than erasing it). <see cref="Name"/> is the one required
/// field — renaming to whitespace is a validation error, not a silent
/// clear.
/// </summary>
public sealed class UpdateCommunityViewModel
{
    [Required]
    public string ComponentId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Sort order")]
    public int? SortOrder { get; set; }
}
