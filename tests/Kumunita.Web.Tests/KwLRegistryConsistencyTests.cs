using System.Text.RegularExpressions;
using Kumunita.Core.Localization;

namespace Kumunita.Web.Tests;

/// <summary>
/// The view↔registry seam (ADR 0015 D1 / M·3): every <c>kw-l key="…"</c> the
/// in-scope Razor views emit must be a key in the curated
/// <see cref="KnownTranslationKeys"/> registry.
///
/// <para>
/// <b>Why this test exists (2026-09-17, the PG-lane regression):</b> the
/// pages views (<c>Views/Page/*.cshtml</c>) wrapped seven strings in
/// <c>kw-l</c> keys — <c>pages.title</c>, <c>pages.new_button</c>,
/// <c>pages.none</c>, <c>pages.back</c>, <c>pages.by</c>, <c>pages.delete</c>,
/// <c>pages.untitled</c> — that were <b>never registered</b>. The
/// <see cref="LocalizeTagHelper"/> emits via <c>SetContent</c> (auto-escaped,
/// and an unregistered key falls back to the <b>raw key</b>), so a resident
/// saw the literal string <c>"pages.new_button"</c> where a label should have
/// been. The existing <see cref="MLUI_FacesTests"/> are <b>registry-driven</b>
/// — they verify every <i>registered</i> key resolves and the editor lists the
/// closed registry — but none of them cross-check that every <i>used</i> key
/// is registered. That is the blind spot this closes.
/// </para>
///
/// <para>
/// <b>Static, no Testcontainers:</b> this is a pure text scan of the shipped
/// <c>.cshtml</c> files against the in-memory registry — the same
/// "the completeness universe equals exactly what the views emit through
/// kw-l" invariant the registry doc states, enforced from the view side.
/// It needs no Postgres, no host, no view resolution (deliberately the one
/// thing the <c>PageControllerTests</c> harness does <b>not</b> exercise).
/// </para>
/// </summary>
public sealed class KwLRegistryConsistencyTests
{
    // <kw-l ... key="foo" ...> — key captured; the attribute may not be first
    // (the in-scope views always put it first, but the scan need not rely on it).
    private static readonly Regex KeyAttr =
        new(@"kw-l\b[^>]*\bkey=""([^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// Locates the repo's <c>src/Kumunita.Web/Views</c> folder by walking up
    /// from the test assembly's output directory to the repo root (the dir that
    /// holds <c>Kumunita.slnx</c>), so the test is correct regardless of the
    /// output depth (Debug/Release, xunit.v3, etc.).
    /// </summary>
    private static DirectoryInfo ResolveViewsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");

        var views = new DirectoryInfo(Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views"));
        Assert.True(views.Exists, $"Views directory not found at {views.FullName}.");
        return views;
    }

    [Fact(DisplayName = "Every kw-l key used in a Razor view is registered in KnownTranslationKeys")]
    public void Every_KwL_Key_In_A_View_Is_Registered()
    {
        var views = ResolveViewsDir();

        // Collect the (file, key) pairs across every .cshtml under Views/ so a
        // failure reports exactly where the unregistered key was used — not just
        // that one is missing.
        //
        // A key whose value **contains** a '@' is a Razor expression — either
        // wholly dynamic (e.g. key="@link.Key" on the RepositoryInfo.Links
        // foreach on home / about / footer) or a *prefix + dynamic suffix*
        // (e.g. key="notifications.kind.@n.Kind" on the M6 Notifications inbox
        // badge — the static "notifications.kind." prefix is a U07-shipped key
        // set, and the @n.Kind suffix resolves to one of the nine code-owned
        // kind constants at runtime). Those are not checkable by a text scan;
        // they are pinned separately (RepositoryInfoTests.
        // Repository_Link_Keys_Are_Registered for the whole-dynamic case; the
        // Notifications kind set is the closed NotificationKinds.Known
        // vocabulary U07 registered, and the view renders the en floor if a
        // kind were ever unregistered, so a typo degrades gracefully).
        var usages = new List<(string File, string Key)>();
        foreach (var file in views.EnumerateFiles("*.cshtml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(views.FullName, file.FullName);
            foreach (Match m in KeyAttr.Matches(File.ReadAllText(file.FullName)))
            {
                var key = m.Groups[1].Value;
                if (key.Contains('@'))
                {
                    continue;   // dynamic key (a C# expression, whole or embedded), not a static literal
                }
                usages.Add((relative, key));
            }
        }

        Assert.True(usages.Count > 0,
            "Expected to find at least one kw-l key in the Razor views; did the scan path break?");

        var unregistered = usages
            .Where(u => !KnownTranslationKeys.EnValues.ContainsKey(u.Key))
            .ToList();

        // A human-readable, actionable failure: file → key, so the fix is "add
        // this key to KnownTranslationKeys" and the location is obvious.
        Assert.True(unregistered.Count == 0,
            "kw-l key(s) used in Razor views are not in the KnownTranslationKeys " +
            "registry — a resident would see the raw key instead of the label:\n" +
            string.Join("\n", unregistered
                .Select(u => $"  {u.File}: {u.Key}")) +
            "\n\nRegister each in src/Kumunita.Core/Localization/KnownTranslationKeys.cs " +
            "with its en source text (plain text only — the kw-l TagHelper " +
            "auto-escapes via SetContent).");
    }
}
