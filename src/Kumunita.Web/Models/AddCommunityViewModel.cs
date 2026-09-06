using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin</c> "add a community" form (the M3+ surface — a new
/// <see cref="Kumunita.Core.UserInfo.Component"/> row).
/// </summary>
public sealed class AddCommunityViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Sort order")]
    public int? SortOrder { get; set; }
}
