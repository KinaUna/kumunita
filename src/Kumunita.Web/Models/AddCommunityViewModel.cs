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

    /// <summary>
    /// ADR 0012 — create the community <em>mandatory</em>: every verified
    /// resident is an implicit member (nobody may be removed or leave it).
    /// <see cref="Kumunita.Core.UserInfo.Component"/> defaults to <c>false</c>,
    /// so <c>true</c> is applied in a second audited lane right after creation
    /// (the controller composes <c>CreateCommunityAsync</c> + the
    /// <c>SetCommunityMandatoryAsync</c> admin lane) rather than changing the
    /// frozen Core <c>CreateAsync</c> shape.
    /// </summary>
    [Display(Name = "Mandatory")]
    public bool Mandatory { get; set; }
}
