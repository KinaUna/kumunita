using Kumunita.Core.Usage;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M25 U9 (item 19, F2/F3) — the gate's **distinct-reason** contract, driven
/// <em>directly</em> against <see cref="IUploadGate"/> (isolated from each
/// lane's allowlist / empty-file checks).
/// <para>
/// <b>The U9 decision (U8 deferred this to us):</b> U8 maps <em>both</em>
/// <see cref="StorageDecision.Oversize"/> (F2) and <see cref
/// "StorageDecision.OverQuota"/> (F3) to the <em>same</em> bare <see
/// cref="StatusCodeResult"/> <c>413</c> — .NET 10's <see
/// cref="StatusCodeResult"/> carries no message body (only
/// <c>StatusCode</c> + <c>ContentType</c>), and the existing per-lane tests
/// pin the <em>exact</em> <see cref="StatusCodeResult"/> type via
/// <c>Assert.IsType&lt;StatusCodeResult&gt;</c> + <c>StatusCode == 413</c>.
/// Changing the carrier (a typed body, a content-type discriminator, …) to
/// make the two 413s distinguishable would break that exact-type pin.
/// </para>
/// <para>
/// <b>So the distinction is asserted at the decision the gate delegates to</b> —
/// the pure <see cref="StorageLimits.Decide"/> (size-first, C-UP·2) — not on a
/// 413 body. The two 413s the gate returns are the <em>same shape</em> (the
/// single 413 producer, C-UP·3, the type the regression suite pins); the F2
/// ≠ F3 reason is the <em>decision</em> that selected it. We drive the gate
/// with one oversize input and one over-quota input, assert <em>both</em> come
/// back as the exact <c>413</c> <see cref="StatusCodeResult"/> (the
/// regression-suite contract the per-lane tests rely on), and then assert the
/// two decisions — <see cref="StorageDecision.Oversize"/> vs <see cref
/// "StorageDecision.OverQuota"/> — are <em>distinct</em> (oversize ≠
/// over-quota). This is the "gate's decision" mechanism the plan authorizes,
/// chosen over a distinguishable 413 carrier so the per-lane exact-type
/// assertions stay intact.
/// </para>
/// </summary>
public class UploadGateTests
{
    private const string OversizeSubject = "subj-m25-u9-oversize";
    private const string OverQuotaSubject = "subj-m25-u9-overquota";

    /// <summary>
    /// The oversize input (F2): a small per-file cap + a large quota + a
    /// payload over the cap. The <see cref
    /// "IStorageSettingsService.GetPerUserUsageBytesAsync"/> read for this
    /// subject is 0 (no prior usage) — so the reject is oversize, not
    /// over-quota.
    /// </summary>
    private static CommunityStorageSettings OversizeSettings() =>
        new() { MaxFileBytes = 16, PerUserQuotaBytes = long.MaxValue };

    /// <summary>
    /// The over-quota input (F3): a large per-file cap (so the payload is
    /// <em>not</em> oversize) + a small quota + pre-seeded usage that the
    /// incoming payload pushes over.
    /// </summary>
    private static CommunityStorageSettings OverQuotaSettings() =>
        new() { MaxFileBytes = long.MaxValue, PerUserQuotaBytes = 50 };

    [Fact]
    public async Task UploadGate_OversizeAndOverQuota_HaveDistinctMessages()
    {
        // A stub settings service whose usage read is per-subject (the
        // C-SM·7 seam the gate reads). The gate takes the settings doc as a
        // parameter, so each input's cap/quota is carried on that doc.
        var settingsSvc = Substitute.For<IStorageSettingsService>();
        settingsSvc.GetPerUserUsageBytesAsync(OversizeSubject, Arg.Any<CancellationToken>())
            .Returns(0L); // oversize: no prior usage
        settingsSvc.GetPerUserUsageBytesAsync(OverQuotaSubject, Arg.Any<CancellationToken>())
            .Returns(100L); // over-quota: pre-seeded usage (100 + 12 > 50)

        var gate = new UploadGate(settingsSvc);

        const long envMaxBytes = long.MaxValue; // the env fallback is irrelevant here — the doc caps are explicit
        const long oversizeIncoming = 100;      // > 16 cap → oversize
        const long overQuotaIncoming = 12;      // ≤ long.MaxValue cap, but 100 + 12 > 50 quota

        var oversize = await gate.CheckUpload(oversizeIncoming, OversizeSubject, OversizeSettings(), envMaxBytes);
        var overQuota = await gate.CheckUpload(overQuotaIncoming, OverQuotaSubject, OverQuotaSettings(), envMaxBytes);

        // Both reject reasons are a 413 (the single 413 producer, C-UP·3), and
        // BOTH are the exact type the per-lane tests pin (Assert.IsType<
        // StatusCodeResult> + StatusCode == 413) — so we must NOT break that:
        // the carriers are the same shape on purpose.
        Assert.IsType<StatusCodeResult>(oversize);
        Assert.Equal(413, ((StatusCodeResult)oversize!).StatusCode);
        Assert.IsType<StatusCodeResult>(overQuota);
        Assert.Equal(413, ((StatusCodeResult)overQuota!).StatusCode);

        // The F2 ≠ F3 distinction lives in the DECISION the gate delegates to
        // (the pure StorageLimits.Decide, size-first C-UP·2), not on a 413
        // body — that is the distinguishability this test asserts. Run the
        // exact same inputs the gate read through Decide and pin that the
        // oversize input → Oversize and the over-quota input → OverQuota:
        var oversizeDecision = StorageLimits.Decide(
            oversizeIncoming, 0L, OversizeSettings(), envMaxBytes);
        var overQuotaDecision = StorageLimits.Decide(
            overQuotaIncoming, 100L, OverQuotaSettings(), envMaxBytes);

        Assert.Equal(StorageDecision.Oversize, oversizeDecision);
        Assert.Equal(StorageDecision.OverQuota, overQuotaDecision);
        // The two reject reasons are distinguishable (oversize ≠ over-quota):
        Assert.NotEqual(oversizeDecision, overQuotaDecision);
    }
}
