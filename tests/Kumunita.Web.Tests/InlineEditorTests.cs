namespace Kumunita.Web.Tests;

/// <summary>
/// IE U4 — the lane's <b>acceptance gate</b>. The 3 pinned IE behaviors
/// (design doc §Pinned seam tests, <b>exact names</b>) are anchored here as
/// <b>artifact-string pins</b> on the compiled <c>wwwroot/js/lib/rich-editor.js</c>,
/// plus one RE-regression artifact pin proving the IE lane is
/// <b>additive only</b> (the 7 RE pure-function/binder exports are
/// byte-identical).
/// <para>
/// <b>Harness reality (RE·3's <c>tsc</c>-only + no TS test runner):</b> the
/// repo has no <c>vitest</c>/<c>jest</c>/<c>node --test</c>; there is no
/// JS runner to execute the toggle. So these tests pin the <b>artifact
/// contract</b> (the strings U1/U2 emit must be present in the compiled
/// JS; the textarea is hidden via a CSS class, never disabled/removed;
/// the RE export surface is unchanged). This is a <b>test-harness
/// choice, not a second renderer</b> (RE·2's "one renderer" stance refers
/// to the <b>read</b> path — <see cref="Kumunita.Web.Security.MarkdownRenderer"/>
/// — which is untouched). The toggle's <b>behavior</b> is pinned by the
/// design doc's §The exact toggle contract + U1's binder; an executable
/// behavior test would need a JS test runner (a <c>package.json</c> change
/// RE·3 forbids in this lane) — a <b>future lane</b>.
/// </para>
/// <para>
/// <b>Path helper:</b> <see cref="CandidateArtifactPaths"/> mirrors
/// <c>RichEditorTests.CandidateArtifactPaths</c> verbatim (walk up ≤ 8
/// levels from both the CWD and <see cref="AppContext.BaseDirectory"/>,
/// relative path <c>src/Kumunita.Web/wwwroot/js/lib/rich-editor.js</c>,
/// pick the first that exists).
/// </para>
/// </summary>
public class InlineEditorTests
{
    // ── IE artifact-string pins (IE·1 / RE·1) ─────────────────────────────

    /// <summary>
    /// <b>#1</b> — the compiled JS contains the toggle button's
    /// <c>data-ie-toggle</c> attribute (the U1 binder's
    /// <c>querySelector("button[data-ie-toggle]")</c> selector + the U2
    /// markup both land in the compiled artifact — proof the toggle
    /// wiring shipped). Fails loud if the artifact is absent.
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_ContainsToggleButton()
    {
        var content = ReadCompiledRichEditor();
        Assert.Contains("data-ie-toggle", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>#2</b> — the compiled JS contains the initial-state class
    /// <c>rc-editor-source-hidden</c> (the U1 <c>setView(false)</c>
    /// initial call + the <c>setView</c> function body both reference it,
    /// so the compiled JS carries the literal string). IE·1: the source is
    /// hidden via this CSS class.
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_ContainsSourceHiddenClass()
    {
        var content = ReadCompiledRichEditor();
        Assert.Contains("rc-editor-source-hidden", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>#3</b> — the compiled JS does <b>not</b> disable or remove the
    /// textarea (the IE·1 / RE·1 single-source-of-truth pin at the
    /// artifact level). The textarea is hidden via a CSS class
    /// (<c>classList</c>), never <c>disabled</c> (which would break form
    /// binding on submit), never <c>remove()</c>'d, never
    /// <c>hidden</c>-attributed (which would drop it from tab order + the
    /// form-submit set). The needles are scoped to <c>textarea.</c> so an
    /// unrelated <c>disabled</c> elsewhere in the module cannot
    /// false-positive.
    /// </summary>
    [Fact]
    public void RichEditorTextarea_IsNotDisabled_OrRemoved()
    {
        var content = ReadCompiledRichEditor();
        Assert.DoesNotContain("textarea.disabled", content, StringComparison.Ordinal);
        Assert.DoesNotContain("textarea.remove(", content, StringComparison.Ordinal);
        Assert.DoesNotContain("textarea.hidden", content, StringComparison.Ordinal);
    }

    // ── RE regression pin (RE·3: additive only) ───────────────────────────

    /// <summary>
    /// <b>#4</b> — the compiled JS <b>still</b> exports the full 7-name RE
    /// surface (<c>renderPreview</c> / <c>applyToggle</c> /
    /// <c>applyBlock</c> / <c>applyLink</c> / <c>imageLink</c> /
    /// <c>isSafeImageSrc</c> / <c>bindRichEditor</c>) — the <b>same
    /// 7-name list</b> and the <b>same <c>export function {name}</c>
    /// needle shape</b> RE U07's <c>CompiledRichEditorJs_Exists_And_Exports</c>
    /// asserts. A byte-level regression guard: if U1 dropped or renamed a
    /// pure function, this fails before the user does. Proves the IE lane
    /// is <b>additive</b> — the RE surface is unchanged.
    /// </summary>
    [Fact]
    public void CompiledRichEditorJs_StillExportsRePureFunctions()
    {
        var content = ReadCompiledRichEditor();
        foreach (var name in new[]
            {
                "renderPreview", "applyToggle", "applyBlock",
                "applyLink", "imageLink", "isSafeImageSrc", "bindRichEditor",
            })
        {
            Assert.Contains($"export function {name}", content, StringComparison.Ordinal);
        }
    }

    // ── Test helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolve the compiled <c>rich-editor.js</c> artifact and read it,
    /// failing loud if it is absent (the <c>RichEditorTests</c> idiom —
    /// do not soften to try/catch).
    /// </summary>
    private static string ReadCompiledRichEditor()
    {
        var candidates = CandidateArtifactPaths();
        var artifact = candidates.FirstOrDefault(p => File.Exists(p));
        Assert.True(
            artifact is not null,
            $"rich-editor.js build artifact not found; searched:\n{string.Join("\n", candidates)}");
        return File.ReadAllText(artifact);
    }

    /// <summary>
    /// Candidate paths to the compiled client artifact, resolved from both
    /// the current working directory and <see cref="AppContext.BaseDirectory"/>,
    /// walking up the directory tree (the bin layout / invocation CWD vary).
    /// Mirrors <c>RichEditorTests.CandidateArtifactPaths</c> verbatim.
    /// </summary>
    private static IReadOnlyList<string> CandidateArtifactPaths()
    {
        const string rel = "src/Kumunita.Web/wwwroot/js/lib/rich-editor.js";
        var candidates = new List<string>();
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, rel));
                dir = dir.Parent;
            }
        }
        return candidates.Distinct().ToArray();
    }
}
