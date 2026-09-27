using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kumunita.Core.Localization;

namespace Kumunita.Core.Portability;

/// <summary>
/// One <c>identity/principals.json</c> row — the no-secret identity graph
/// (M11 D3 / C-M11·2; the exact locked field set from
/// <c>docs/design/m11-portability-design.md</c> §principals). The eight
/// allowed fields — <c>subjectId</c> (the key U06 re-creates by) /
/// <c>username</c> / <c>email</c> / <c>normalizedEmail</c> /
/// <c>displayName</c> / <c>verified</c> / <c>blocked</c> / <c>roles</c>.
/// <para>
/// <b>The C-M11·2 boundary at the type level:</b> this POCO has <em>no</em>
/// <c>PasswordHash</c> / <c>SecurityStamp</c> / <c>AccessToken</c> /
/// <c>RefreshToken</c> / <c>RecoveryCode</c> field — credential material is
/// structurally incapable of traveling (the U07 no-secret field-shape pin).
/// </para>
/// </summary>
public sealed record PortabilityPrincipal
{
    /// <summary>The exported subjectId — the key U06 re-creates by (§principals).</summary>
    [JsonPropertyName("subjectId")]
    public string SubjectId { get; init; } = "";

    /// <summary>The Identity username.</summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>The Identity email.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>The Identity normalized email.</summary>
    [JsonPropertyName("normalizedEmail")]
    public string? NormalizedEmail { get; init; }

    /// <summary>The <c>Profile.DisplayName</c> — the display identity.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>The <c>Profile.Verified</c> flag (the sign-in gate).</summary>
    [JsonPropertyName("verified")]
    public bool Verified { get; init; }

    /// <summary>The <c>Profile.Blocked</c> flag (the suspension).</summary>
    [JsonPropertyName("blocked")]
    public bool Blocked { get; init; }

    /// <summary>The Identity role names — the standing (ADR 0030, composable).</summary>
    [JsonPropertyName("roles")]
    public List<string> Roles { get; init; } = new();
}

/// <summary>
/// The <c>config.json</c> POCO — the instance identity + localizations
/// (M11 D2 / §config; the exact locked field set: <c>community</c> /
/// <c>locale</c> / <c>languages[]</c>). The <c>LocaleSettings</c> +
/// <c>LanguageCatalog</c> state travels <em>here</em>, not as <c>docs/</c>
/// rows (drift guard entry 3). U02's <c>ConfigExport</c> copies the field
/// set verbatim; U06's <c>ApplyConfigAsync</c> mirrors it verbatim.
/// </summary>
public sealed record PortabilityConfig
{
    [JsonPropertyName("community")]
    public PortabilityConfigCommunity Community { get; init; } = new();

    [JsonPropertyName("locale")]
    public PortabilityConfigLocale Locale { get; init; } = new();

    /// <summary>
    /// Every <c>LanguageCatalog</c> row — the per-instance language catalog
    /// (the <c>LanguageCode</c> target set the §validate integrity loop
    /// resolves <c>*Translation</c> rows against).
    /// </summary>
    [JsonPropertyName("languages")]
    public List<PortabilityConfigLanguage> Languages { get; init; } = new();
}

/// <summary>The <c>config.json</c> <c>community</c> block (§config).</summary>
public sealed record PortabilityConfigCommunity
{
    /// <summary><c>CommunityOptions.Name</c> — the instance's display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary><c>CommunityOptions.SupportEmail</c> (nullable).</summary>
    [JsonPropertyName("support_email")]
    public string? SupportEmail { get; init; }
}

/// <summary>
/// The <c>config.json</c> <c>locale</c> block — the seven
/// <c>LocaleSettings</c> instance-level fields (§config).
/// </summary>
public sealed record PortabilityConfigLocale
{
    [JsonPropertyName("default_language_code")]
    public string? DefaultLanguageCode { get; init; }

    [JsonPropertyName("default_timezone")]
    public string? DefaultTimezone { get; init; }

    [JsonPropertyName("default_date_format")]
    public string? DefaultDateFormat { get; init; }

    [JsonPropertyName("is_signup_open")]
    public bool IsSignupOpen { get; init; }

    [JsonPropertyName("notify_admins_on_signup")]
    public bool NotifyAdminsOnSignup { get; init; }

    [JsonPropertyName("announcement_comments_enabled")]
    public bool AnnouncementCommentsEnabled { get; init; }

    [JsonPropertyName("messaging_enabled")]
    public bool MessagingEnabled { get; init; }
}

/// <summary>
/// One <c>config.json</c> <c>languages[]</c> row — a
/// <c>LanguageCatalog</c> row (§config: <c>id</c> / <c>native_name</c> /
/// <c>enabled</c> / <c>sort_order</c>).
/// </summary>
public sealed record PortabilityConfigLanguage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("native_name")]
    public string NativeName { get; init; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; init; }
}

/// <summary>
/// The deserialized archive — the <see cref="KumunitaArchive.ReadAsync"/>
/// output. The <see cref="Docs"/> map is doc-type name → the
/// <c>docs/{Type}.json</c> array bytes (the U05 per-type sanity check
/// deserializes each into its declared POCO set); the <see cref="Media"/>
/// map is content id → payload bytes (the U05 byte-verification leg,
/// C-M11·3).
/// </summary>
public sealed class KumunitaArchiveData
{
    public PortabilityManifest? Manifest { get; set; }

    /// <summary>Doc type name → the <c>docs/{Type}.json</c> array bytes.</summary>
    public Dictionary<string, byte[]> Docs { get; } = new(StringComparer.Ordinal);

    /// <summary>Content id → the <c>media/{Id[0..2]}/{Id}</c> payload bytes.</summary>
    public Dictionary<string, byte[]> Media { get; } = new(StringComparer.Ordinal);

    public byte[] Principals { get; set; } = Array.Empty<byte>();

    public byte[] Config { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// The <c>*.kumunita</c> archive (de)serializer (M11 D2) —
/// <see cref="WriteAsync"/> / <see cref="ReadAsync"/> over the exact locked
/// §layout, using BCL <c>System.IO.Compression.ZipArchive</c> (in the
/// .NET 10 framework — <b>no new package</b>, the lean-stack + tsc-only
/// discipline holds) + <c>System.Text.Json</c>:
/// <code>
/// manifest.json | docs/{Type}.json | media/{Id[0..2]}/{Id}
/// | identity/principals.json | config.json
/// </code>
/// The media entry-name convention mirrors
/// <c>LocalVolumeFileStore</c>'s <c>{root}/{Id[0..2]}/{Id}</c> path
/// derivation (ADR 0011 C-MED·3/4/7) — import is a byte-copy and the
/// dedup-by-content-hash is preserved (C-M11·3).
/// </summary>
public sealed class KumunitaArchive
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public const string ManifestPath = "manifest.json";
    public const string PrincipalsPath = "identity/principals.json";
    public const string ConfigPath = "config.json";
    public const string DocsPrefix = "docs/";
    public const string MediaPrefix = "media/";

    /// <summary>
    /// The archive entry name for a media payload — the same
    /// <c>{Id[0..2]}/{Id}</c> sub-layout <c>LocalVolumeFileStore</c>
    /// derives under its <c>RootPath</c> (ADR 0011 C-MED·3/4/7).
    /// </summary>
    public static string MediaEntryName(string contentId) =>
        $"{MediaPrefix}{contentId[..2]}/{contentId}";

    /// <summary>
    /// Derives the content id from a media archive entry name (the
    /// <see cref="MediaEntryName"/> inverse — the part after the
    /// <c>media/{dir}/</c> prefix).
    /// </summary>
    public static string MediaIdFromEntryName(string entryName)
    {
        var idx = entryName.IndexOf('/', entryName.IndexOf('/') + 1);
        return idx < 0 ? entryName : entryName[(idx + 1)..];
    }

    /// <summary>
    /// Serializes one archive section to JSON bytes (the
    /// <c>manifest.json</c> / <c>identity/principals.json</c> /
    /// <c>config.json</c> / <c>docs/{Type}.json</c> writers — the U02/U03
    /// loops call this over their POCO set / doc arrays).
    /// </summary>
    public static byte[] ToJson<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);

    /// <summary>Deserializes one archive section's JSON bytes.</summary>
    public static T? FromJson<T>(byte[] json) =>
        JsonSerializer.Deserialize<T>(json, JsonOpts);

    /// <summary>
    /// Writes the whole archive into <paramref name="stream"/> (the D2
    /// layout). <paramref name="docs"/> is doc-type name → the
    /// <c>docs/{Type}.json</c> array bytes (the U02 doc loop's output, one
    /// entry per §inventory type); <paramref name="media"/> is content id →
    /// the payload bytes (the U03 media loop's output, the
    /// <see cref="MediaEntryName"/> layout).
    /// </summary>
    public static async Task WriteAsync(
        Stream stream,
        PortabilityManifest manifest,
        IReadOnlyDictionary<string, byte[]> docs,
        IReadOnlyDictionary<string, byte[]> media,
        byte[] principals,
        byte[] config,
        CancellationToken ct = default)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        await AddEntryAsync(zip, ManifestPath, ToJson(manifest), ct);
        foreach (var (type, json) in docs)
            await AddEntryAsync(zip, DocsPrefix + type + ".json", json, ct);
        foreach (var (id, bytes) in media)
            await AddEntryAsync(zip, MediaEntryName(id), bytes, ct);
        await AddEntryAsync(zip, PrincipalsPath, principals, ct);
        await AddEntryAsync(zip, ConfigPath, config, ct);
    }

    private static async Task AddEntryAsync(
        ZipArchive zip, string entryName, byte[] bytes, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName);
        await using var sink = entry.Open();
        await sink.WriteAsync(bytes.AsMemory(), ct);
    }

    /// <summary>
    /// Reads + deserializes one archive from <paramref name="stream"/>
    /// (the §layout inverse). The <c>docs/</c> entries are kept as raw
    /// array bytes keyed by doc type name (the per-type POCO deserialization
    /// is the U05 validate check (b), the registry's <c>Type</c> drives it);
    /// the <c>media/</c> entries as raw payload bytes keyed by content id.
    /// Throws <see cref="InvalidDataException"/> when <c>manifest.json</c>
    /// is absent (a malformed archive is the U05 reject surface,
    /// C-M11·1/4).
    /// </summary>
    public static async Task<KumunitaArchiveData> ReadAsync(
        Stream stream, CancellationToken ct = default)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var data = new KumunitaArchiveData();
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (name == ManifestPath)
                data.Manifest = FromJson<PortabilityManifest>(await ReadEntryBytesAsync(entry, ct))
                    ?? throw new InvalidDataException("archive manifest.json is missing or malformed");
            else if (name == PrincipalsPath)
                data.Principals = await ReadEntryBytesAsync(entry, ct);
            else if (name == ConfigPath)
                data.Config = await ReadEntryBytesAsync(entry, ct);
            else if (name.StartsWith(DocsPrefix, StringComparison.Ordinal)
                     && name.EndsWith(".json", StringComparison.Ordinal))
                data.Docs[name[DocsPrefix.Length..^5]] = await ReadEntryBytesAsync(entry, ct);
            else if (name.StartsWith(MediaPrefix, StringComparison.Ordinal))
                data.Media[MediaIdFromEntryName(name)] = await ReadEntryBytesAsync(entry, ct);
        }
        if (data.Manifest is null)
            throw new InvalidDataException("archive manifest.json is missing");
        return data;
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        await using var source = entry.Open();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}