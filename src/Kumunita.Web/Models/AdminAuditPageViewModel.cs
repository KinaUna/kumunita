namespace Kumunita.Web.Models;

public sealed class AdminAuditPageViewModel
{
    public sealed class Row
    {
        public string Id { get; init; } = string.Empty;
        public DateTimeOffset At { get; init; }
        public string ActorId { get; init; } = string.Empty;
        public string? EffectivePrincipal { get; init; }
        public string Action { get; init; } = string.Empty;
        public string TargetKind { get; init; } = string.Empty;
        public string? TargetId { get; init; }
        public int? VisibleCount { get; init; }
        public int? HiddenCount { get; init; }
        public string Via { get; init; } = string.Empty;
        public string Outcome { get; init; } = string.Empty;
    }

    public IReadOnlyList<Row> Rows { get; init; } = [];
    public string? Via { get; init; }
    public string? Outcome { get; init; }
    public int Page { get; init; }
}
