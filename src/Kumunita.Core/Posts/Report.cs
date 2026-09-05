namespace Kumunita.Core.Posts;

/// <summary>
/// A *dormant* report row (M3b workflow). The **table** is registered in M3
/// for forward compatibility (the Q1↔Q3 resolution: the table in M3, the flow
/// in M3b); M3's surface ships **no** workflow, **no** tests, and **no**
/// <see cref="Status"/> writes against it. <see cref="Status"/> is nullable
/// until M3b lands a write lane that sets it.
/// </summary>
public sealed class Report
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string? ComponentId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }   // null until M3b's write lane sets it
    public DateTimeOffset At { get; set; }
}
