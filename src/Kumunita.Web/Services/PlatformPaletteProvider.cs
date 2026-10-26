using Kumunita.Core.SiteContent;
using Kumunita.Web.Security;

namespace Kumunita.Web.Services;

/// <summary>
/// The admin's platform-default palette (ADR 0175 D3), read by <c>_Layout</c> on
/// every request. <see cref="ISiteContentService"/> is transient and hits Postgres,
/// so this singleton caches the value in-process (one container per deployment —
/// ADR architecture) with a short expiry as a safety net, and
/// <see cref="Invalidate"/> is called by the admin save so a change is live on the
/// very next render (strong consistency, like the rest of SiteContent).
/// <para>Best-effort: any failure degrades to <see cref="PaletteCookie.Forest"/> —
/// the layout never throws because of this lookup.</para>
/// </summary>
public sealed class PlatformPaletteProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private string? _value;
    private DateTimeOffset _expires;

    public PlatformPaletteProvider(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<string> GetDefaultAsync(CancellationToken ct = default)
    {
        var cached = _value;
        if (cached is not null && DateTimeOffset.UtcNow < _expires) return cached;

        try
        {
            using var scope = _scopes.CreateScope();
            var site = scope.ServiceProvider.GetRequiredService<ISiteContentService>();
            var content = await site.GetAsync(ct).ConfigureAwait(false);
            _value = PaletteCookie.Normalize(content.DefaultPalette);
        }
        catch
        {
            _value = PaletteCookie.Forest; // floor: never break page rendering
        }
        _expires = DateTimeOffset.UtcNow + Ttl;
        return _value;
    }

    /// <summary>Drops the cached value (the admin just saved a new default).</summary>
    public void Invalidate() => _value = null;
}
