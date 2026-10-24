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
    // F16 / UPL-02 — the quarantine flag. false = clean / never flagged (the
    // default, unchanged path). true = the scanner flagged this byte infected
    // at serve time; the serve lane refuses it (404, zero audit rows). Set at
    // SERVE time (the enforcement point), idempotent (a re-serve short-circuits
    // before re-scanning). A new column on the existing doc — the
    // MediaDocTypes.Configure registration (opts.Schema.For<MediaObject>()) is
    // unchanged; Marten applies it idempotently at boot (ADR 0004 §B).
    public bool Quarantined { get; set; }
}
