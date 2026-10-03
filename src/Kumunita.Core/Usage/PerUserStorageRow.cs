namespace Kumunita.Core.Usage;

/// <summary>
/// One per-user row (F5/F6). <see cref="CreatedById"/> == <c>null</c> /
/// <c>""</c> is the single **"unknown / not captured"** bucket (C-SM·5).
/// </summary>
public sealed record PerUserStorageRow(string? CreatedById, long Bytes, int FileCount);
