namespace Kumunita.Core.Media;

/// <summary>
/// A content-addressed media object (ADR 0011; C-MED·4). <see cref="Id"/> is the
/// lowercase-hex SHA-256 of the payload — the dedup key and the document
/// identity. The bytes live on the local volume at
/// <c>{root}/{Id[0..2]}/{Id}</c> (C-MED·3/7); this document is the surviving
/// catalog reference that a <c>pg_dump</c> restore always carries.
/// </summary>
public sealed class MediaObject
{
    public string Id { get; set; } = "";                    // = lowercase-hex SHA-256 of payload
    public string? Filename { get; set; }                   // original name, metadata ONLY — C-MED·3: never a path component
    public string ContentType { get; set; } = "";           // e.g. "image/png" (validated at the upload boundary, C-MED·5)
    public long SizeBytes { get; set; }
    public DateTimeOffset Created { get; set; }
    public string? CreatedById { get; set; }                // subjectId of the actor who first stored this unique file
}
