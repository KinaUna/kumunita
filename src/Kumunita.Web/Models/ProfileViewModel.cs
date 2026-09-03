using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

public sealed class ProfileViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    public bool Verified { get; set; }
}
