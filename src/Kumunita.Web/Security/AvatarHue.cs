namespace Kumunita.Web.Security;

/// <summary>
/// Picks one of six stable monogram colours for a resident, so the initial
/// fallback avatar (<c>.avatar-mono</c>) is recognisable per person instead of
/// every monogram being the same grey-green. Presentation only — not an
/// identity or authorization input.
/// <para>
/// Deliberately NOT <see cref="string.GetHashCode()"/>: that is randomized per
/// process in .NET, so a resident's colour would change on every restart.
/// </para>
/// </summary>
public static class AvatarHue
{
    public const int Count = 6;

    /// <summary>A stable index in <c>[0, Count)</c> for <paramref name="subjectId"/>.</summary>
    public static int For(string? subjectId)
    {
        if (string.IsNullOrEmpty(subjectId)) return 0;
        uint hash = 2166136261; // FNV-1a
        foreach (var ch in subjectId)
        {
            hash ^= ch;
            hash *= 16777619;
        }
        return (int)(hash % Count);
    }
}
