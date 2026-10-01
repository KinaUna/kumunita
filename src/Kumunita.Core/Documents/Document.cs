namespace Kumunita.Core.Documents;

/// <summary>
/// M21 (ADR 0122, D1) — an official document in the community's shared
/// repository (contracts, minutes, notices). The bytes are **not** stored here:
/// <see cref="MediaId"/> is the content-addressed id of the file on the frozen
/// ADR 0011 <c>IMediaStore</c> (D3 — the media lane is untouched, a new
/// consumer). <see cref="Audience"/> is the per-document access control,
/// written verbatim and projected as-is to the frozen
/// <see cref="Authorization.IAuthorizationService"/> Read path (C-M21·1, C-M21·3).
/// </summary>
public sealed class Document
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Authorization.Audience Audience { get; set; } = new();
    public string OwnerId { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
}
