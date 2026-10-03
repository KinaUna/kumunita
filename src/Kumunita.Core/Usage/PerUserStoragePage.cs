namespace Kumunita.Core.Usage;

/// <summary>
/// The paged per-user table (F3) — the M7 <see cref="HasMore"/> discipline
/// (C-SM·4). <see cref="TotalUsers"/> counts distinct rows **including** the
/// one "unknown" bucket row when present (C-SM·5).
/// </summary>
public sealed record PerUserStoragePage(
    IReadOnlyList<PerUserStorageRow> Items,
    int TotalUsers,
    int Page,
    bool HasMore);
