using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kumunita.Core.Portability;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C10 (IMPROVE-CODE) — the **first tests ever naming
/// <see cref="PortabilityValidate"/>.Run** (E12's "parts work, seams don't"
/// finding: <c>PortabilityValidate</c> had no test naming it — the M11
/// round-trip D9c pin exercises it only indirectly, through
/// <see cref="PortabilityService"/>.ImportAsync).
/// <para>
/// <see cref="PortabilityValidate"/>.Run is a <b>static, read-only, in-memory</b>
/// gate over a <see cref="KumunitaArchiveData"/> — no DB, no Testcontainers —
/// so these are pure unit tests of the <b>closed failure set</b> contract
/// (the design doc §validate "The closed failure set" + the U07 fail-closed pin
/// that asserts <em>exactly</em> it): check (a) <c>format</c>, check (b)
/// <c>docs</c> sanity, check (c) <c>ref.dangling</c>, check (d)
/// <c>media</c> byte verification. A clean archive passes (the apply phase may
/// proceed); each failure kind is asserted by name.
/// </para>
/// </summary>
public sealed class PortabilityValidateTests
{
    /// <summary>
    /// A structurally <b>clean</b> archive: the right <c>format</c>, all 44
    /// §inventory <c>docs/{Type}.json</c> arrays present (each an empty
    /// <c>[]</c> — every reference field is absent ⇒ satisfied, so no
    /// <c>ref.dangling</c>), an empty principals set, and an empty media
    /// manifest (so check (d) has no bytes to verify). This is the shape the
    /// clean-validate path (the apply phase may proceed) requires — the
    /// closed set is the authority (the C-M11·5 "whole-instance, not
    /// per-slice" pin).
    /// </summary>
    private static KumunitaArchiveData CleanArchive()
    {
        var data = new KumunitaArchiveData
        {
            Manifest = new PortabilityManifest { Format = PortabilityManifest.FormatVersion },
            Principals = KumunitaArchive.ToJson(new List<PortabilityPrincipal>()),
            Config = KumunitaArchive.ToJson(new PortabilityConfig()),
        };
        foreach (var entry in PortabilityDocTypes.Entries)
            data.Docs[entry.Type] = Encoding.UTF8.GetBytes("[]");
        return data;
    }

    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Run_CleanArchive_Passes()
    {
        var result = PortabilityValidate.Run(CleanArchive());

        Assert.True(result.Ok);
        Assert.Empty(result.Failures);
        // The closed success shape is the Success singleton's (Ok, []).
        Assert.True(PortabilityValidationResult.Success.Ok);
    }

    [Fact]
    public void Run_WrongFormat_IsFormatUnsupportedAndShortCircuits()
    {
        var data = CleanArchive();
        data.Manifest!.Format = "kumunita/portability/0"; // a format the build does not understand

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        // Check (a) short-circuits the rest — the closed failure set is
        // EXACTLY this one entry (the U07 pin asserts exactly it).
        Assert.Equal(["format.unsupported"], result.Failures);
    }

    [Fact]
    public void Run_MissingDoc_IsDocsMissing()
    {
        var data = CleanArchive();
        data.Docs.Remove("Post"); // a §inventory type with no docs/Post.json

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        Assert.Contains("docs.missing:Post", result.Failures);
    }

    [Fact]
    public void Run_MalformedDoc_IsDocsMalformed()
    {
        var data = CleanArchive();
        data.Docs["Post"] = Json("{\"not\":\"an-array\"}"); // an object, not the declared array POCO set

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        Assert.Contains("docs.malformed:Post", result.Failures);
    }

    [Fact]
    public void Run_UnexpectedDocType_IsDocsMalformed()
    {
        var data = CleanArchive();
        data.Docs["NotARealType"] = Json("[]"); // a docs/ file outside the closed §inventory set

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        Assert.Contains("docs.malformed:NotARealType", result.Failures);
    }

    [Fact]
    public void Run_DanglingReference_IsRefDangling()
    {
        var data = CleanArchive();
        // A Post whose ComponentId points at a Component that is not in the
        // archive's docs/Component.json id set (the Component set is empty
        // here). Every other Post reference field is absent (satisfied), so
        // the closed failure set is EXACTLY this one ref.dangling entry.
        data.Docs["Post"] = Json("[{\"Id\":\"p1\",\"ComponentId\":\"missing-component\"}]");
        data.Docs["Component"] = Json("[]");

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        Assert.Equal(["ref.dangling:Post.ComponentId"], result.Failures);
    }

    [Fact]
    public void Run_MissingMediaByte_IsMediaMissing()
    {
        var data = CleanArchive();
        // A media_manifest entry with no bytes in the archive's media/ map.
        var contentId = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })).ToLowerInvariant();
        data.Manifest!.MediaManifest.Add(new PortabilityMediaEntry
        {
            Id = contentId,
            SizeBytes = 3,
            ContentType = "image/png",
        });
        // data.Media is intentionally NOT populated with contentId.

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        Assert.Contains($"media.missing:{contentId}", result.Failures);
    }

    [Fact]
    public void Run_MismatchedMediaByte_IsMediaMismatch()
    {
        var data = CleanArchive();
        // A media_manifest entry whose listed id is the SHA-256 of some
        // payload, but the stored bytes hash to a DIFFERENT id (a corrupted /
        // tampered byte — the C-M11·3 reject).
        var listedId = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })).ToLowerInvariant();
        var storedBytes = new byte[] { 9, 9, 9 }; // SHA-256 != listedId
        data.Manifest!.MediaManifest.Add(new PortabilityMediaEntry
        {
            Id = listedId,
            SizeBytes = storedBytes.Length,
            ContentType = "image/png",
        });
        data.Media[listedId] = storedBytes;

        var result = PortabilityValidate.Run(data);

        Assert.False(result.Ok);
        Assert.Contains($"media.mismatch:{listedId}", result.Failures);
    }

    [Fact]
    public void Run_CleanArchiveWithConsistentMedia_Passes()
    {
        var data = CleanArchive();
        // A media_manifest entry whose id IS the SHA-256 of the stored bytes
        // (the C-M11·3 "content-hash matches the path" pin) — check (d) passes.
        var bytes = new byte[] { 7, 8, 9, 10 };
        var contentId = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        data.Manifest!.MediaManifest.Add(new PortabilityMediaEntry
        {
            Id = contentId,
            SizeBytes = bytes.Length,
            ContentType = "image/png",
        });
        data.Media[contentId] = bytes;

        var result = PortabilityValidate.Run(data);

        Assert.True(result.Ok);
        Assert.Empty(result.Failures);
    }
}
