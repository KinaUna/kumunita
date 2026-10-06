namespace Kumunita.Core.Query;

/// <summary>
/// The one shared <b>items-per-page</b> resolution + clamp (the resident's
/// per-page preference, <see cref="UserInfo.Profile.PageSize"/>, resolved to a
/// concrete page size every paged surface uses). One constant set + one pure
/// helper so no feed controller duplicates the default or the clamp.
/// <para>
/// <see cref="Default"/> (10) is the platform default: a resident who has not
/// set a preference (<c>Profile.PageSize == null</c>) — or any paged read that
/// has no actor context — pages at 10. <see cref="Min"/> (5) /
/// <see cref="Max"/> (100) bound the settings control and the clamp: a stored
/// value outside the range (a pre-existing row written by an older build, or a
/// direct API write) is clamped to the nearest bound rather than rejected —
/// the ADR 0019 "resolve a bad value to the floor" posture, not a fail-closed
/// throw (a page-size preference is never a security gate).
/// </para>
/// </summary>
public static class PageSizer
{
    /// <summary>The platform default items-per-page (the user preference is
    /// additive on top of this — <c>null</c> ⇒ <see cref="Default"/>).</summary>
    public const int Default = 10;

    /// <summary>The settings-control minimum (and the clamp floor).</summary>
    public const int Min = 5;

    /// <summary>The settings-control maximum (and the clamp ceiling).</summary>
    public const int Max = 100;

    /// <summary>
    /// Resolve a resident's <see cref="UserInfo.Profile.PageSize"/> preference
    /// (or <c>null</c> = unset) to the concrete page size every paged seam
    /// should use: <c>null</c> ⇒ <see cref="Default"/>; a value below
    /// <see cref="Min"/> ⇒ <see cref="Min"/>; a value above
    /// <see cref="Max"/> ⇒ <see cref="Max"/>; otherwise the value as-is.
    /// Pure: no I/O, no actor — the caller resolves the preference and passes
    /// the value; this only normalizes it.
    /// </summary>
    public static int Resolve(int? preferred)
        => preferred is < Min ? Min : preferred is > Max ? Max : preferred ?? Default;

    /// <summary>
    /// Resolve a paged seam's <see cref="Resolve"/> override for a service that
    /// carries its own historical page-size constant (the M7 per-service
    /// <c>PageSize = 30</c> precedent): a caller-supplied value
    /// (<paramref name="override"/> &gt; 0, e.g. the resident's resolved
    /// <see cref="Resolve"/> preference) is used — clamped to
    /// <see cref="Min"/>…<see cref="Max"/>; a <c>null</c> / non-positive
    /// <paramref name="override"/> falls back to the service's own
    /// <paramref name="fallback"/> constant (the pre-override behavior, so a
    /// paged read with no actor context — and the pinned seam tests that call
    /// the seam directly — keep their historical page size).
    /// </summary>
    public static int ResolveOverride(int? @override, int fallback)
        => @override is > 0 ? Math.Clamp(@override.Value, Min, Max) : fallback;
}
