namespace Kumunita.Core.Identity;

/// <summary>
/// F9 (AUTH-15) — the instance's disposable-email blocklist. A committed, versioned,
/// owner-editable text file (<c>Identity/disposable-emails.txt</c>, embedded as an
/// <c>EmbeddedResource</c> in this assembly — the <c>sample-data.json</c> precedent,
/// <c>Kumunita.Core.csproj</c>), NOT a NuGet package.
/// <para>
/// Exact-match only on the full domain after the <c>@</c>, case-insensitive, no
/// wildcards, no subdomain matching, no Punycode conversion (a disposable domain is
/// ASCII by definition; an IDN entry is a false-positive risk). A domain NOT on the
/// list is allowed (deny-by-default blocklist — there is no allowlist).
/// </para>
/// <para>
/// A single static <c>lazy</c>-loaded <see cref="IReadOnlySet{T}"/> (lower-cased) built
/// once at first use. No DI registration — a pure static read.
/// </para>
/// </summary>
public static class DisallowedEmailDomains
{
    /// <summary>
    /// The embedded blocklist resource, built once at first use.
    /// </summary>
    private static readonly Lazy<IReadOnlySet<string>> Domains = new(BuildDomains, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// <c>true</c> when <paramref name="email"/>'s full domain after the <c>@</c> is in
    /// the instance's disposable-email blocklist. A malformed address (no <c>@</c>, or a
    /// trailing <c>@</c>) is <c>false</c> — the caller's duplicate/validity check handles
    /// that shape; this check must not throw (pin §2c).
    /// </summary>
    public static bool IsDisposable(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        int at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1)
            return false;

        string domain = email[(at + 1)..].Trim().ToLowerInvariant();
        if (domain.Length == 0)
            return false;

        return Domains.Value.Contains(domain);
    }

    private static IReadOnlySet<string> BuildDomains()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var stream = ResolveBlocklistStream()
            ?? throw new InvalidOperationException(
                "The disposable-emails.txt embedded resource is missing from the Kumunita.Core assembly.");
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var entry = line.Trim().ToLowerInvariant();
            if (entry.Length == 0)
                continue;
            set.Add(entry);
        }
        return set;
    }

    /// <summary>
    /// Resolves the embedded blocklist stream by the SDK-computed manifest name
    /// (<c>&lt;root-ns&gt;.Identity.disposable-emails.txt</c>), with a case-insensitive
    /// scan for any resource ending in <c>disposable-emails.txt</c> as the fallback, so
    /// the lookup is correct regardless of the exact manifest-name computation.
    /// </summary>
    private static System.IO.Stream? ResolveBlocklistStream()
    {
        var asm = typeof(DisallowedEmailDomains).Assembly;
        var exact = asm.GetManifestResourceStream("Kumunita.Core.Identity.disposable-emails.txt");
        if (exact is not null)
            return exact;

        const string suffix = ".disposable-emails.txt";
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : asm.GetManifestResourceStream(name);
    }
}
