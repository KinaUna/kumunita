namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0075 — the lane's <b>acceptance gate</b>. The simple image editor
/// (crop + resize) is a <c>tsc</c>-only ES module in
/// <c>src/Kumunita.Web/client/lib/image-editor.ts</c>. Following the
/// <see cref="RichEditorTests"/> convention (the repo has no JS test runner
/// — RE·3's <c>tsc</c>-only stance), this file anchors the module's
/// <b>pure geometry</b> as a small <b>C# spec mirror</b> and adds one
/// <b>artifact pin</b> on the compiled <c>wwwroot/js/lib/image-editor.js</c>.
/// <para>
/// The mirror is the <b>executable spec</b> of the three pure functions
/// (<c>clampCropRect</c> / <c>centerSquareCrop</c> / <c>outputDims</c>); the
/// modal itself is a browser-only, lazy + guarded surface (it has no pure
/// surface to mirror — same shape as <c>bindRichEditor</c>, which is pinned
/// only by its <c>export function</c> artifact check). The mirror must stay
/// byte-faithful in <b>semantics</b> to the TS: same clamps, same minimum
/// 1px floor, same aspect-preserving resize, same JS <c>Math.round</c>
/// (round-half-away-from-zero, so C# uses <c>MidpointRounding.AwayFromZero</c>).
/// </para>
/// </summary>
public class ImageEditorTests
{
    // ── clampCropRect (the in-bounds / min-1px invariant) ─────────────────

    /// <summary>
    /// An in-bounds rect is returned unchanged (identity case).
    /// </summary>
    [Fact]
    public void ClampCropRect_InBounds_IsIdentity()
    {
        var r = ImageEditorSpec.ClampCropRect(10, 20, 50, 30, 200, 150);

        Assert.Equal(10, r.x);
        Assert.Equal(20, r.y);
        Assert.Equal(50, r.w);
        Assert.Equal(30, r.h);
    }

    /// <summary>
    /// A rect extending past the right/bottom edges is clamped to the source;
    /// the top-left stays where it was.
    /// </summary>
    [Fact]
    public void ClampCropRect_OverflowsRightBottom_IsClampedToSource()
    {
        var r = ImageEditorSpec.ClampCropRect(150, 120, 100, 100, 200, 150);

        Assert.Equal(150, r.x);
        Assert.Equal(120, r.y);
        Assert.Equal(50, r.w); // 200 - 150
        Assert.Equal(30, r.h); // 150 - 120
    }

    /// <summary>
    /// A rect starting before the top-left is clamped to the origin.
    /// </summary>
    [Fact]
    public void ClampCropRect_NegativeOrigin_SnapsToOrigin()
    {
        var r = ImageEditorSpec.ClampCropRect(-10, -5, 50, 40, 200, 150);

        Assert.Equal(0, r.x);
        Assert.Equal(0, r.y);
        Assert.Equal(50, r.w);
        Assert.Equal(40, r.h);
    }

    /// <summary>
    /// A zero-size rect is floored to the 1px minimum (a zero-size canvas is
    /// what <c>toBlob</c> would reject — this keeps the editor always able to
    /// produce a non-empty image).
    /// </summary>
    [Fact]
    public void ClampCropRect_ZeroSize_IsFlooredToOnePixel()
    {
        var r = ImageEditorSpec.ClampCropRect(0, 0, 0, 0, 200, 150);

        Assert.Equal(1, r.w);
        Assert.Equal(1, r.h);
    }

    /// <summary>
    /// A zero-size source is defended to a 1×1 crop (degenerate input can
    /// never yield a zero-rect out of the editor).
    /// </summary>
    [Fact]
    public void ClampCropRect_ZeroSource_YieldsOnePixel()
    {
        var r = ImageEditorSpec.ClampCropRect(0, 0, 50, 40, 0, 0);

        Assert.Equal(0, r.x);
        Assert.Equal(0, r.y);
        Assert.Equal(1, r.w);
        Assert.Equal(1, r.h);
    }

    // ── centerSquareCrop (the avatar default) ─────────────────────────────

    /// <summary>
    /// A wider image is cropped to a centered square of side = height.
    /// </summary>
    [Fact]
    public void CenterSquareCrop_Wider_IsCenteredSquareOfHeight()
    {
        var r = ImageEditorSpec.CenterSquareCrop(200, 100);

        Assert.Equal(50, r.x); // (200 - 100) / 2
        Assert.Equal(0, r.y);
        Assert.Equal(100, r.w);
        Assert.Equal(100, r.h);
    }

    /// <summary>
    /// A taller image is cropped to a centered square of side = width.
    /// </summary>
    [Fact]
    public void CenterSquareCrop_Taller_IsCenteredSquareOfWidth()
    {
        var r = ImageEditorSpec.CenterSquareCrop(100, 200);

        Assert.Equal(0, r.x);
        Assert.Equal(50, r.y);
        Assert.Equal(100, r.w);
        Assert.Equal(100, r.h);
    }

    /// <summary>
    /// A square image is returned unchanged (already the ideal avatar crop).
    /// </summary>
    [Fact]
    public void CenterSquareCrop_Square_IsIdentity()
    {
        var r = ImageEditorSpec.CenterSquareCrop(80, 80);

        Assert.Equal(0, r.x);
        Assert.Equal(0, r.y);
        Assert.Equal(80, r.w);
        Assert.Equal(80, r.h);
    }

    /// <summary>
    /// Odd dimensions: the square is as large as the smaller side, and the
    /// one-pixel leftover is dropped (integer shift), never split.
    /// </summary>
    [Fact]
    public void CenterSquareCrop_NonEvenOffset_DropsTheLeftover()
    {
        var r = ImageEditorSpec.CenterSquareCrop(102, 100);

        Assert.Equal(1, r.x); // (102 - 100) >> 1
        Assert.Equal(0, r.y);
        Assert.Equal(100, r.w);
        Assert.Equal(100, r.h);
    }

    // ── outputDims (aspect-preserving resize) ─────────────────────────────

    /// <summary>
    /// A landscape crop downscaled to the target width keeps its aspect.
    /// </summary>
    [Fact]
    public void OutputDims_Landscape_PreservesAspect()
    {
        var d = ImageEditorSpec.OutputDims(1000, 500, 800);

        Assert.Equal(800, d.w);
        Assert.Equal(400, d.h); // 500 * (800/1000)
    }

    /// <summary>
    /// A non-integer result is rounded (JS <c>Math.round</c>, half away from
    /// zero), so the height is never floored to a wrong value.
    /// </summary>
    [Fact]
    public void OutputDims_FractionalHeight_RoundsHalfAwayFromZero()
    {
        var d = ImageEditorSpec.OutputDims(1000, 501, 800);

        Assert.Equal(800, d.w);
        Assert.Equal(401, d.h); // 501 * 0.8 = 400.8 → 401
    }

    /// <summary>
    /// The target width is the authoritative width; the height follows.
    /// </summary>
    [Fact]
    public void OutputDims_Upscale_Allowed()
    {
        var d = ImageEditorSpec.OutputDims(100, 100, 500);

        Assert.Equal(500, d.w);
        Assert.Equal(500, d.h);
    }

    /// <summary>
    /// A zero source width cannot divide by zero — the scale falls back to 1,
    /// and the width is still floored to at least 1.
    /// </summary>
    [Fact]
    public void OutputDims_ZeroSourceWidth_DefendsDivision()
    {
        var d = ImageEditorSpec.OutputDims(0, 500, 800);

        Assert.Equal(800, d.w);
        Assert.Equal(500, d.h); // scale = 1 (sw == 0)
    }

    /// <summary>
    /// A sub-1 target width is floored to 1px (the editor never emits a
    /// zero-size image).
    /// </summary>
    [Fact]
    public void OutputDims_SmallTarget_IsFlooredToOnePixel()
    {
        var d = ImageEditorSpec.OutputDims(100, 100, 0);

        Assert.Equal(1, d.w);
        Assert.Equal(1, d.h);
    }

    // ── Artifact pin (the client artifact is built + exported) ────────────

    /// <summary>
    /// <b>Artifact pin</b> — the <c>tsc</c> build output
    /// <c>wwwroot/js/lib/image-editor.js</c> exists on disk and exports the
    /// pinned surface (<c>clampCropRect</c> / <c>centerSquareCrop</c> /
    /// <c>outputDims</c> / <c>openImageEditor</c> / <c>editAvatarBlob</c>).
    /// Fails loud if the artifact is absent (do not soften to a try/catch).
    /// </summary>
    [Fact]
    public void CompiledImageEditorJs_Exists_And_Exports()
    {
        var candidates = CandidateArtifactPaths();
        var artifact = candidates.FirstOrDefault(p => File.Exists(p));
        Assert.True(
            artifact is not null,
            $"image-editor.js build artifact not found; searched:\n{string.Join("\n", candidates)}");

        var content = File.ReadAllText(artifact);
        foreach (var name in new[]
            {
                "clampCropRect", "centerSquareCrop", "outputDims",
                "openImageEditor", "editAvatarBlob",
            })
        {
            Assert.Contains($"export function {name}", content, StringComparison.Ordinal);
        }
    }

    // ── Test helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Candidate paths to the compiled client artifact, resolved from both the
    /// current working directory and <see cref="AppContext.BaseDirectory"/>,
    /// walking up the directory tree (the bin layout / invocation CWD vary) —
    /// the <see cref="RichEditorTests"/> helper, re-pointed at this module.
    /// </summary>
    private static IReadOnlyList<string> CandidateArtifactPaths()
    {
        const string rel = "src/Kumunita.Web/wwwroot/js/lib/image-editor.js";
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

/// <summary>
/// A <b>small internal static C# spec mirror</b> of the pure geometry in
/// <c>src/Kumunita.Web/client/lib/image-editor.ts</c> (ADR 0075). It
/// <b>verbatim-encodes</b> the module's clamps (the in-bounds + 1px floor),
/// the centered-square avatar default, and the aspect-preserving resize —
/// the same JS <c>Math.round</c> semantics (C# <c>MidpointRounding.AwayFromZero</c>).
/// It is the <b>executable spec</b>, not a second product: the only shipped
/// editor is the TS module, and the only read path is the frozen
/// <c>MarkdownRenderer</c> (both unchanged by this lane).
/// </summary>
internal static class ImageEditorSpec
{
    public static (int x, int y, int w, int h) ClampCropRect(int x, int y, int w, int h, int srcW, int srcH)
    {
        // Mirror of the TS clampCropRect (the floor + clamp order matters).
        int W = Math.Max(1, srcW);
        int H = Math.Max(1, srcH);
        int cx = x;
        int cy = y;
        int cw = Math.Max(1, w);
        int ch = Math.Max(1, h);
        cx = Math.Min(Math.Max(cx, 0), W - 1);
        cy = Math.Min(Math.Max(cy, 0), H - 1);
        cw = Math.Min(cw, W - cx);
        ch = Math.Min(ch, H - cy);
        return (cx, cy, cw, ch);
    }

    public static (int x, int y, int w, int h) CenterSquareCrop(int srcW, int srcH)
    {
        // Mirror of the TS centerSquareCrop ((W-side) >> 1 = integer / 2).
        int W = Math.Max(1, srcW);
        int H = Math.Max(1, srcH);
        int side = Math.Min(W, H);
        int x = (W - side) / 2;
        int y = (H - side) / 2;
        return (x, y, side, side);
    }

    public static (int w, int h) OutputDims(int sw, int sh, int targetW)
    {
        // Mirror of the TS outputDims (JS Math.round = round-half-away-from-
        // zero for our non-negative pixel values).
        int w = Math.Max(1, targetW);
        double scale = sw > 0 ? (double)w / sw : 1.0;
        int h = (int)Math.Max(1, Math.Round(sh * scale, MidpointRounding.AwayFromZero));
        return (w, h);
    }
}
