using Kumunita.Core.Usage;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Security;

/// <summary>
/// The M25 Web-only upload gate (C-UP·3 / C-UP·7) — the **single** place a
/// <c>413</c> <see cref="ActionResult"/> is produced. Every upload lane
/// (avatar / content-image / attachment / document upload, **and** the document
/// edit-lane re-upload) adopts it **before** <c>IMediaStore.PutAsync</c>
/// (guards-before-write, C-UP·2): on a reject, **no byte is written** (F9).
/// <para>
/// It maps the **pure** Core decision (<see cref="StorageDecision"/>) to a
/// single <c>413</c> <see cref="StatusCodeResult"/> for both reject reasons —
/// <see cref="StorageDecision.Oversize"/> (F2) and <see cref
/// "StorageDecision.OverQuota"/> (F3) — and <see cref
/// "StorageDecision.Allowed"/> (F1) → <c>null</c> (proceed). The decision
/// itself is the pure Core <see cref="StorageLimits.Decide"/> (size-first,
/// C-UP·2 ordering); **Core stays HTTP-free** — it never sees an
/// <see cref="IActionResult"/> (C-UP·3 / ADR 0006-D). Only this gate (Web)
/// produces the 413.
/// </para>
/// <para>
/// <b>Distinct F2/F3 message (deferred to U9):</b> design §2.1 asks for a
/// *distinct* oversize vs. over-quota message, but .NET 10's
/// <see cref="StatusCodeResult"/> carries no message body and the existing
/// per-lane tests pin the **exact** <see cref="StatusCodeResult"/> type. U8
/// delivers the single 413 producer with that type; the F2 ≠ F3 message
/// distinction is a U9 design decision (see the handoff drift-guard note).
/// </para>
/// <para>
/// <b>Async reconciliation (design §2.1):</b> §2.1 pins a sync
/// <c>ActionResult? CheckUpload(...)</c>, but the gate must read the subject's
/// usage via the **async** <see cref="IStorageSettingsService
/// .GetPerUserUsageBytesAsync"/> seam (the C-SM·7 <c>Σ SizeBytes WHERE
/// CreatedById</c> read, re-exposed by U4), so <c>CheckUpload</c> awaits it —
/// the signature is <c>Task&lt;ActionResult?&gt;</c>. The decision stays the
/// pure Core <see cref="StorageLimits.Decide"/>; only the usage read is async.
/// </para>
/// </summary>
public interface IUploadGate
{
    /// <summary>
    /// Reads the subject's usage (the C-SM·7 seam), runs the pure
    /// <see cref="StorageLimits.Decide"/> (size-first, C-UP·2), and maps:
    /// <see cref="StorageDecision.Oversize"/> (F2) → a <c>413</c>
    /// <see cref="StatusCodeResult"/>; <see cref="StorageDecision
    /// .OverQuota"/> (F3) → a <c>413</c> <see cref="StatusCodeResult"/>;
    /// <see cref="StorageDecision.Allowed"/> (F1) → <c>null</c> (proceed). The
    /// only place a <c>413</c> is produced (C-UP·3). The F2 vs. F3 distinction
    /// is in the <em>decision</em> (and the Core-side metric), not in a 413
    /// message body — the distinct-message design question is U9's (see the
    /// handoff drift-guard note).
    /// </summary>
    /// <param name="incomingBytes">The uploaded payload size (<c>file.Length</c>).</param>
    /// <param name="subjectId">The signed-in principal's subject — minted
    /// server-side, never a route param (self-scoped, C-UP·7).</param>
    /// <param name="settings">The community settings doc (get-or-created by the
    /// caller — the admin override + the per-user quota, C-UP·1).</param>
    /// <param name="envMaxBytes">The env <c>Media__MaxBytes</c> fallback the
    /// pure <see cref="StorageLimits.Decide"/> reads when
    /// <see cref="CommunityStorageSettings.MaxFileBytes"/> is <c>null</c>
    /// (C-UP·1/5).</param>
    Task<ActionResult?> CheckUpload(long incomingBytes, string subjectId,
        CommunityStorageSettings settings, long envMaxBytes);
}

/// <summary>
/// The Web-only <see cref="IUploadGate"/> impl (M25 U8, C-UP·3). The only
/// 413-producing call-site in the tree — the four upload lanes + the document
/// edit-lane re-upload all route their size/over-quota decision through
/// <see cref="CheckUpload"/>, so a rejected upload writes no byte (C-UP·2, F9).
/// </summary>
public sealed class UploadGate(IStorageSettingsService storageSettings) : IUploadGate
{
    private readonly IStorageSettingsService _storageSettings =
        storageSettings ?? throw new ArgumentNullException(nameof(storageSettings));

    /// <inheritdoc/>
    public async Task<ActionResult?> CheckUpload(long incomingBytes, string subjectId,
        CommunityStorageSettings settings, long envMaxBytes)
    {
        // The C-SM·7 seam (Σ SizeBytes WHERE CreatedById, re-exposed by U4) — the
        // subject's current usage. Read **here** (the gate), not in the lane: the
        // gate is the single reader of the decision's inputs (no double read).
        var usage = await _storageSettings.GetPerUserUsageBytesAsync(
            subjectId, CancellationToken.None);

        // The **pure** Core decision (size-first, C-UP·2 ordering) — Core stays
        // HTTP-free (C-UP·3); only the mapping below is Web.
        var decision = StorageLimits.Decide(incomingBytes, usage, settings, envMaxBytes);

        return decision switch
        {
            // F2 (oversize) and F3 (over-quota) are BOTH a 413 — the only reject
            // shape M25 produces (C-UP·7). The design §2.1 calls for a *distinct*
            // message on each, but .NET 10's <see cref="StatusCodeResult"/> carries
            // no message body (only StatusCode + ContentType), and the existing
            // per-lane tests pin the EXACT type (Assert.IsType&lt;StatusCodeResult&gt;).
            // The F2 ≠ F3 message distinction is therefore a **U9 design decision**
            // (see the handoff §2.6 drift-guard note) — not a U8 concern. U8
            // delivers the single 413 producer (C-UP·3) with the type the
            // regression suite already asserts.
            StorageDecision.Oversize  => Reject413(),   // F2
            StorageDecision.OverQuota => Reject413(),   // F3
            _                         => null,          // F1 — proceed
        };
    }

    /// <summary>
    /// The one 413 the gate produces (C-UP·3): a bare <see
    /// cref="StatusCodeResult"/> — the **exact** type the existing per-lane
    /// tests assert via <c>Assert.IsType&lt;StatusCodeResult&gt;</c>. .NET 10's
    /// <see cref="StatusCodeResult"/> has no message body; the distinct
    /// oversize/over-quota message (F2 ≠ F3, design §2.1) is a U9 design
    /// decision (handoff drift-guard note), not a U8 concern.
    /// </summary>
    private static ActionResult Reject413() =>
        new StatusCodeResult(StatusCodes.Status413RequestEntityTooLarge);
}
