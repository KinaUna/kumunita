using Kumunita.Core.Query;
using Xunit;

namespace Kumunita.Core.Tests.Query;

/// <summary>
/// M26 U3 (design §2.5 pins 1–6) — the 6 pure <see cref="SortKeys.Parse"/>
/// pins. **No Postgres, no DI**: the parser is a pure function of
/// (raw <c>?sort=</c>, raw <c>?dir=</c>, the surface's closed allowlist,
/// the surface's default key/dir) — C-SORT·1/3 (closed allowlist, HTTP-free
/// Core). An unknown/blank key or an invalid/absent direction never throws;
/// it resolves to the surface default (F4/F5).
/// </summary>
public class SortSpecTests
{
    // A representative surface (the community post feed, design §2.2 row 1):
    // allowlist {created, modified, title}, surface default (created, desc).
    private static readonly IReadOnlySet<string> PostFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };
    private const string DefaultKey = "created";
    private const bool DefaultDir = true;

    [Fact]
    public void Parse_AllowedKey_Applies()
    {
        // An allowlisted key (case-insensitive — the raw ?sort= value may be
        // "Created") resolves to the **lowercase** key with the requested
        // direction.
        var spec = SortKeys.Parse("Created", "asc", PostFeedAllowed, DefaultKey, DefaultDir);

        Assert.Equal("created", spec.Key);
        Assert.False(spec.Descending);
    }

    [Fact]
    public void Parse_UnknownKey_Defaults()
    {
        // F4: a key outside the closed allowlist → the surface's default
        // key, never an error, never a raw-string passthrough.
        var spec = SortKeys.Parse("hacked", "desc", PostFeedAllowed, DefaultKey, DefaultDir);

        Assert.Equal(DefaultKey, spec.Key);
    }

    [Fact]
    public void Parse_InvalidDir_Defaults()
    {
        // F5: an allowed key but an invalid direction ("sideways") → the
        // key's default direction.
        var spec = SortKeys.Parse("created", "sideways", PostFeedAllowed, DefaultKey, DefaultDir);

        Assert.Equal(DefaultKey, spec.Key);
        Assert.Equal(DefaultDir, spec.Descending);
    }

    [Fact]
    public void Parse_NullKey_Defaults()
    {
        // F4/F5: an absent key + an absent direction → the surface default
        // exactly.
        var spec = SortKeys.Parse(null, null, PostFeedAllowed, DefaultKey, DefaultDir);

        Assert.Equal(DefaultKey, spec.Key);
        Assert.Equal(DefaultDir, spec.Descending);
    }

    [Fact]
    public void Parse_DirAsc_DescendingFalse()
    {
        var spec = SortKeys.Parse("created", "asc", PostFeedAllowed, DefaultKey, DefaultDir);

        Assert.False(spec.Descending);
    }

    [Fact]
    public void Parse_DirDesc_DescendingTrue()
    {
        var spec = SortKeys.Parse("created", "desc", PostFeedAllowed, DefaultKey, DefaultDir);

        Assert.True(spec.Descending);
    }
}
