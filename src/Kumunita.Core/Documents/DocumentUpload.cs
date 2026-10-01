namespace Kumunita.Core.Documents;

/// <summary>
/// The upload draft for <see cref="DocumentService.UploadAsync"/> (M21, ADR 0122
/// D3/D5; design doc §5). <see cref="MediaId"/> / <see cref="Filename"/> /
/// <see cref="ContentType"/> / <see cref="SizeBytes"/> are populated
/// **server-side by the Web layer** (U03) from the frozen ADR 0011
/// <c>IMediaStore.PutAsync</c> result — the Core service never touches the bytes
/// (D3: the media lane is untouched, a new consumer). <see cref="Audience"/> is
/// written verbatim (C-M21·1); the Core lane is **standing-agnostic** (D5: the
/// upload right is gated at the Web boundary, not here).
/// </summary>
public sealed record DocumentUpload(
    string Title,
    string? Summary,
    string MediaId,
    string? Filename,
    string ContentType,
    long SizeBytes,
    Authorization.Audience Audience);
