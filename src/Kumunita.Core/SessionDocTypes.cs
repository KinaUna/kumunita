using Kumunita.Core.Sessions;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The sessions lane's (the per-session list + revoke one device) Marten-native
/// document registration surface (ADR 0004 §B.1) — the parallel surface to
/// <see cref="CoreDocTypes"/> and <see cref="PostDocTypes"/> for the sessions
/// bounded context (<c>Kumunita.Core.Sessions</c>). <see cref="SessionDoc"/> is a
/// POCO with the conventional <c>string</c> <c>Id</c> identity (a GUID, the
/// session id), so no non-default convention (identity, business-key index) needs
/// pinning: Marten's defaults apply (the <see cref="PostDocTypes"/> precedent —
/// a one-line <c>Schema.For&lt;T&gt;()</c>, not a hand-rolled
/// <c>FeatureSchemaBase</c> subclass).
/// </summary>
public static class SessionDocTypes
{
    /// <summary>
    /// Registers the sessions bounded context's document. Idempotent: calling twice is
    /// safe — Marten's <c>Schema.For&lt;T&gt;()</c> returns the same document mapping
    /// each time, and the delta is applied idempotently by
    /// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> at boot (the dev-only
    /// <c>ApplyAllDatabaseChangesOnStartup</c> loop in <c>Program.cs</c> and the
    /// <c>SchemaBootstrap</c> versioned boot both pick the surface up automatically
    /// from the <see cref="StoreOptions"/> this is registered on —
    /// <c>SchemaBootstrap</c> itself calls no <c>*DocTypes</c> surface, the
    /// <c>Program.cs</c> registration is the only boot wiring, per the
    /// M37/PersonalNotes, M31/ErrorReport, M35/Kiosk, and every other bounded
    /// context's precedent in <c>Program.cs</c>'s <c>AddMarten</c> block).
    /// </summary>
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<SessionDoc>();
    }
}
