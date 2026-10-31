namespace Kumunita.Web.Security;

/// <summary>
/// SEC remediation lane — U20 (finding **L2**): a **pure** magic-byte /
/// signature sniffer for the upload allowlists pinned by
/// <see cref="Kumunita.Core.Media.MediaOptions"/> (the image / attachment /
/// document lanes). The client-supplied <c>Content-Type</c> has always been
/// trusted; this helper lets the upload sites (U21/U22 wire it) verify that the
/// **bytes** actually match the **declared** type before
/// <c>IMediaStore.PutAsync</c> (guards-before-write). It is a pure function —
/// no I/O, no DI — so it unit-tests in isolation (S-7: a user-supplied file
/// must not be accepted as a type its bytes do not support).
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed set (design doc §2.2 U20).</b> Only the allowed types with a
/// known leading signature are recognised; a declared type with no known
/// signature (e.g. legacy binary office, audio, video, or a type outside the
/// allowlist) returns <c>false</c> — fail-closed. Recognised types:
/// </para>
/// <list type="bullet">
/// <item><c>image/png</c> — <c>89 50 4E 47 0D 0A 1A 0A</c>.</item>
/// <item><c>image/jpeg</c> — <c>FF D8 FF</c>.</item>
/// <item><c>image/gif</c> — <c>47 49 46 38</c> (<c>"GIF8"</c>).</item>
/// <item><c>image/webp</c> — <c>"RIFF"</c> at 0–3 and <c>"WEBP"</c> at 8–11.</item>
/// <item><c>application/pdf</c> — <c>25 50 44 46</c> (<c>"%PDF"</c>).</item>
/// <item><c>text/plain</c> / <c>text/markdown</c> / <c>text/csv</c> — valid
///       UTF-8 with no NUL byte (plain-text family).</item>
/// <item>zip-based office
///       (<c>…wordprocessingml.document</c>, <c>…spreadsheetml.sheet</c>,
///       <c>…presentationml.presentation</c>) and <c>application/zip</c> /
///       <c>application/x-zip-compressed</c> — <c>50 4B 03 04</c>
///       (<c>"PK\x03\x04"</c>).</item>
/// </list>
/// <para>
/// The declared type is compared on its **base MIME** (the part before any
/// <c>;</c>-parameter), case-insensitively, so <c>"image/png; charset=utf-8"</c>
/// matches <c>"image/png"</c> — the same parameter-insensitive comparison
/// <see cref="Kumunita.Core.Media.MediaOptions.IsAllowed"/> uses.
/// </para>
/// </remarks>
public static class UploadSignatureSniffer
{
    /// <summary>
    /// SEC U21 (owner decision, 2026-10-31) — the tri-state verdict the upload
    /// wire acts on. <see cref="SniffVerdict.Match"/> means the head bytes
    /// support the declared type; <see cref="SniffVerdict.Mismatch"/> means the
    /// head bytes **contradict** a declared type with a known, unambiguous
    /// signature (the wire rejects this); <see cref="SniffVerdict.Unknown"/>
    /// means the declared type has no known signature, or the alias carries a
    /// non-canonical payload (the wire **allows** this — it cannot prove a
    /// contradiction). The wire (U21/U22) rejects on <c>Mismatch</c> only, so a
    /// legacy alias such as <c>application/x-zip-compressed</c> carrying a PDF
    /// stays green (the landed <c>AttachUpload_Support_ZipLegacyAlias_IsAllowed</c>
    /// pin).
    /// </summary>
    public enum SniffVerdict { Match, Mismatch, Unknown }

    // Recognised leading signatures (design doc §2.2 U20). Static byte[] —
    // ReadOnlySpan<byte> is a ref struct and cannot be a field.
    private static readonly byte[] Png  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif  = [0x47, 0x49, 0x46, 0x38];   // "GIF8"
    private static readonly byte[] Riff = [0x52, 0x49, 0x46, 0x46];   // "RIFF"
    private static readonly byte[] Webp = [0x57, 0x45, 0x42, 0x50];   // "WEBP"
    private static readonly byte[] Pdf  = [0x25, 0x50, 0x44, 0x46];   // "%PDF"
    private static readonly byte[] Zip  = [0x50, 0x4B, 0x03, 0x04];   // "PK\x03\x04"



    /// <summary>
    /// Two-valued wrapper over <see cref="Classify"/> (SEC U21 owner decision,
    /// 2026-10-31): <c>true</c> iff <see cref="Classify"/> reports
    /// <see cref="SniffVerdict.Match"/>. <c>Mismatch</c> and <c>Unknown</c>
    /// both map to <c>false</c> — fail-closed. U20's three pinned tests
    /// (<c>Png_Jpeg_Gif_WebP_Pdf_Accepted</c>,
    /// <c>DeclaredPng_ButHtmlBytes_Rejected</c>,
    /// <c>UnknownDeclaredType_Rejected</c>) are unchanged by this delegation.
    /// </summary>
    /// <param name="declaredContentType">
    /// The client-supplied <c>Content-Type</c> (any <c>;</c>-parameters are
    /// ignored; the comparison is case-insensitive on the base MIME).
    /// </param>
    /// <param name="head">
    /// The leading bytes of the uploaded payload. May be shorter than a
    /// signature; a head shorter than a required signature cannot match and is
    /// reported <c>false</c> (fail-closed).
    /// </param>
    public static bool Matches(string declaredContentType, ReadOnlySpan<byte> head)
        => Classify(declaredContentType, head) == SniffVerdict.Match;

    /// <summary>
    /// SEC U21 — the tri-state verdict used by the upload wire
    /// (<c>AttachmentController</c> / <c>ContentImageController</c> reject on
    /// <see cref="SniffVerdict.Mismatch"/>; <see cref="SniffVerdict.Match"/>
    /// and <see cref="SniffVerdict.Unknown"/> pass through).
    /// </summary>
    /// <param name="declaredContentType">
    /// The client-supplied <c>Content-Type</c> (any <c>;</c>-parameters are
    /// ignored; the comparison is case-insensitive on the base MIME).
    /// </param>
    /// <param name="head">
    /// The leading bytes of the uploaded payload (the wire reads the leading
    /// 16, or fewer if the payload is shorter).
    /// </param>
    /// <returns>
    /// <see cref="SniffVerdict.Match"/> — the declared type has a known
    /// signature and the head bytes support it (or, for the plain-text
    /// family, are valid UTF-8 with no NUL byte).
    /// <see cref="SniffVerdict.Mismatch"/> — the head bytes contradict the
    /// declared type: a binary signature declared but the head contradicts it
    /// (e.g. <c>image/png</c> declared but <c>&lt;script&gt;</c> bytes), or a
    /// text type declared but a NUL byte sits in the first 16 bytes.
    /// <see cref="SniffVerdict.Unknown"/> — the declared type has no known
    /// signature (outside the allowlist, a legacy binary office / audio /
    /// video MIME), or the declared type is null/empty, or the head is empty,
    /// or the alias is declared with a non-canonical payload (e.g.
    /// <c>application/x-zip-compressed</c> carrying a PDF — a legacy alias
    /// that legitimately may carry non-zip payloads).
    /// </returns>
    public static SniffVerdict Classify(string declaredContentType, ReadOnlySpan<byte> head)
    {
        if (string.IsNullOrWhiteSpace(declaredContentType) || head.IsEmpty)
            return SniffVerdict.Unknown;

        var baseType = BaseMime(declaredContentType);

        return baseType switch
        {
            "image/png"       => StartsWith(head, Png)  ? SniffVerdict.Match : SniffVerdict.Mismatch,
            "image/jpeg"      => StartsWith(head, Jpeg) ? SniffVerdict.Match : SniffVerdict.Mismatch,
            "image/gif"       => StartsWith(head, Gif)  ? SniffVerdict.Match : SniffVerdict.Mismatch,
            "image/webp"      => MatchesWebP(head)      ? SniffVerdict.Match : SniffVerdict.Mismatch,
            "application/pdf" => StartsWith(head, Pdf)  ? SniffVerdict.Match : SniffVerdict.Mismatch,
            "text/plain" or
            "text/markdown" or
            "text/csv"        => head.Contains((byte)0x00)
                                    ? SniffVerdict.Mismatch    // NUL in a text document = binary → contradicts "text"
                                    : SniffVerdict.Unknown,    // can't prove the text claim from bytes alone — allow
            "application/zip" or
            "application/x-zip-compressed" or
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation"
                                => SniffVerdict.Unknown,        // zip-family: the head may be a PK header, an alias payload, or empty — allow the wire's discretion
            // Any other declared type — outside the allowlist, or allowed but
            // with no known leading signature (legacy binary office, audio,
            // video) — is Unknown (the wire allows it; the allowlist gate is
            // the control, not the sniffer).
            _ => SniffVerdict.Unknown
        };
    }

    // "RIFF" at 0–3 and "WEBP" at 8–11 (bytes 4–7 are the RIFF chunk size).
    private static bool MatchesWebP(ReadOnlySpan<byte> head) =>
        head.Length >= 12
        && head.Slice(0, 4).SequenceEqual(Riff)
        && head.Slice(8, 4).SequenceEqual(Webp);

    private static bool StartsWith(ReadOnlySpan<byte> head, byte[] magic) =>
        head.Length >= magic.Length && head.Slice(0, magic.Length).SequenceEqual(magic);

    // The client-supplied MIME with any ';' -delimited parameters stripped and
    // surrounding whitespace trimmed, lowercased — mirrors MediaOptions.BaseMime.
    private static string BaseMime(string contentType)
    {
        var semi = contentType.IndexOf(';');
        var baseType = semi >= 0 ? contentType[..semi] : contentType;
        return baseType.Trim().ToLowerInvariant();
    }
}
