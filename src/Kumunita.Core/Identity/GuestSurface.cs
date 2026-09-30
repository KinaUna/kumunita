namespace Kumunita.Core.Identity;

/// <summary>
/// M19 (ADR 0120, D4) — the closed, enumerated set of surfaces a guest may
/// be allowed onto. The floor is the empty set (a guest with no surfaces
/// signs in to a shell with no content). Additive: a new surface is a new
/// enum value (the ADR 0030 composable-role / the AccessVia append
/// precedent), never a free-form string. A typo or a wrong value grants
/// nothing — the set is a closed union.
/// </summary>
[Flags]
public enum GuestSurface
{
    /// <summary>No surfaces (the floor; a guest with this set signs in to a shell).</summary>
    None = 0,
    /// <summary>See the community's public announcement feed (read).</summary>
    Announcements = 1,
    /// <summary>See public events (the M4 events read surface, read).</summary>
    Events = 2,
    /// <summary>See public directory basic info (name + verified badge, read).</summary>
    Directory = 4,
}
