namespace Kumunita.Web.Models;

public sealed class VerifyViewModel
{
    /// <summary>Non-null when the link could not be verified (invalid/expired/consumed).</summary>
    public string? Error { get; set; }
}
