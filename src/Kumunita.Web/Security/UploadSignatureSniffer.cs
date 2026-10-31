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
    // Recognised leading signatures (design doc §2.2 U20). Static byte[] —
    // ReadOnlySpan<byte> is a ref struct and cannot be a field.
    private static readonly byte[] Png  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif  = [0x47, 0x49, 0x46, 0x38];   // "GIF8"
    private static readonly byte[] Riff = [0x52, 0x49, 0x46, 0x46];   // "RIFF"
    private static readonly byte[] Webp = [0x57, 0x45, 0x42, 0x50];   // "WEBP"
    private static readonly byte[] Pdf  = [0x25, 0x50, 0x44, 0x46];   // "%PDF"
    private static readonly byte[] Zip  = [0x50, 0x4B, 0x03, 0x04];   // "PK\x03\x04"

    // A strict UTF-8 decoder: GetChars throws on invalid bytes (the default
    // shared UTF8Encoding would substitute U+FFFD instead), so "text" is
    // distinguishable from an arbitrary binary blob.
    private static readonly System.Text.UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reports whether the first <paramref name="head"/> bytes (the caller
    /// reads the leading 16, or fewer if the payload is shorter) are consistent
    /// with the declared <paramref name="declaredContentType"/>.
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
    /// <returns>
    /// <c>true</c> only when the declared type has a known signature **and**
    /// the head bytes match it (or, for the plain-text family, are valid UTF-8
    /// with no NUL byte). <c>false</c> for a null/empty declared type, an empty
    /// head, a declared type with no known signature, or a mismatch.
    /// </returns>
    public static bool Matches(string declaredContentType, ReadOnlySpan<byte> head)
    {
        if (string.IsNullOrWhiteSpace(declaredContentType) || head.IsEmpty)
            return false;

        var baseType = BaseMime(declaredContentType);

        return baseType switch
        {
            "image/png"        => StartsWith(head, Png),
            "image/jpeg"       => StartsWith(head, Jpeg),
            "image/gif"        => StartsWith(head, Gif),
            "image/webp"       => MatchesWebP(head),
            "application/pdf"  => StartsWith(head, Pdf),
            "text/plain" or
            "text/markdown" or
            "text/csv"         => IsPlainText(head),
            "application/zip" or
            "application/x-zip-compressed" or
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" or
            "application/vnd.openxmlformats-officedocument.presentationml.presentation"
                                => StartsWith(head, Zip),
            // Any other declared type — outside the allowlist, or allowed but
            // with no known leading signature (legacy binary office, audio,
            // video) — is fail-closed.
            _ => false
        };
    }

    // "RIFF" at 0–3 and "WEBP" at 8–11 (bytes 4–7 are the RIFF chunk size).
    private static bool MatchesWebP(ReadOnlySpan<byte> head) =>
        head.Length >= 12
        && head.Slice(0, 4).SequenceEqual(Riff)
        && head.Slice(8, 4).SequenceEqual(Webp);

    private static bool StartsWith(ReadOnlySpan<byte> head, byte[] magic) =>
        head.Length >= magic.Length && head.Slice(0, magic.Length).SequenceEqual(magic);

    // Plain-text family: valid UTF-8 (strict decoder) and no NUL byte — a NUL
    // is the practical binary marker and is not valid in a text document.
    private static bool IsPlainText(ReadOnlySpan<byte> head)
    {
        if (head.Contains((byte)0x00))
            return false;
        try
        {
            StrictUtf8.GetString(head.ToArray());
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    // The client-supplied MIME with any ';' -delimited parameters stripped and
    // surrounding whitespace trimmed, lowercased — mirrors MediaOptions.BaseMime.
    private static string BaseMime(string contentType)
    {
        var semi = contentType.IndexOf(';');
        var baseType = semi >= 0 ? contentType[..semi] : contentType;
        return baseType.Trim().ToLowerInvariant();
    }
}
