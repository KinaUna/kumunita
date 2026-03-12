namespace Kumunita.Shared.Kernel.Communities;

/// <summary>
/// Marks a Marten document as community-scoped (multi-tenant).
/// Documents implementing this interface are stored in the community's
/// dedicated schema and are never visible across tenant boundaries
/// except via explicit cross-tenant queries.
/// </summary>
public interface ITenantScoped
{
    /// <summary>
    /// The community slug this document belongs to.
    /// Set automatically by Marten's tenancy infrastructure —
    /// do not set manually in application code.
    /// </summary>
    string TenantId { get; }
}