namespace Kumunita.Core.Usage;

/// <summary>
/// The D2 surface-key normalization (ADR 0114). Pure: no store, no
/// clock — a BCL-only string-map. <see cref="Map"/> takes the
/// <see cref="UsageEvent.RouteTemplate"/> (e.g. <c>GET /posts/{id}</c>),
/// extracts the <b>top-level segment</b> (the first non-empty path
/// segment after the <c>METHOD </c> prefix, lower-cased), and returns the
/// pinned surface key (§surface-key in the design doc). An unknown
/// segment (or a bare <c>/</c>) maps to <c>"other"</c> (one bucket,
/// never a crash).
/// </summary>
public static class SurfaceKey
{
    private static readonly (string Segment, string Key)[] Pinned =
    {
        ("posts", "posts"), ("events", "events"), ("groups", "groups"),
        ("admin", "admin"), ("messages", "messages"), ("todos", "todos"),
        ("boards", "boards"), ("projects", "projects"), ("search", "search"),
        ("about", "about"), ("terms", "terms"), ("help", "help"),
        ("privacy", "privacy"), ("conduct", "conduct"), ("language", "language"),
        ("settings", "settings"), ("account", "account"), ("my", "my"),
        ("pages", "pages"), ("community", "community"),
        ("attachments", "attachments"), ("content-image", "content-image"),
        ("notifications", "notifications"), ("calendar", "calendar"),
        ("whats-new", "whats-new"), ("health", "health")
    };

    public static string Map(string? routeTemplate)
    {
        // Strip the "METHOD " prefix (e.g. "GET /posts/{id}" → "/posts/{id}").
        var path = routeTemplate ?? string.Empty;
        var space = path.IndexOf(' ');
        if (space >= 0) path = path[(space + 1)..];
        // Strip the leading '/' and take the first non-empty segment.
        var seg = path.TrimStart('/');
        var slash = seg.IndexOf('/');
        if (slash > 0) seg = seg[..slash];
        seg = seg.ToLowerInvariant();
        foreach (var (s, k) in Pinned) if (seg == s) return k;
        return "other";
    }
}
